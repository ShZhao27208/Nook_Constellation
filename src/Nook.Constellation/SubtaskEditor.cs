using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace Nook.Constellation;
public sealed partial class TaskEditor
{
    private readonly TextBox pendingSubtask = Ui.Input();
    private readonly StackPanel subtaskRows = new();
    private readonly TextBlock subtaskProgress = Ui.Section("子任务");
    private UIElement BuildSubtasks()
    {
        var panel = new StackPanel(); panel.Children.Add(subtaskProgress);
        panel.Children.Add(subtaskRows);
        var input = pendingSubtask; input.ToolTip = "输入小任务，Enter 连续添加";
        System.Windows.Automation.AutomationProperties.SetName(input, "新增子任务标题");
        void Add() { if (string.IsNullOrWhiteSpace(input.Text)) return; if(task.Subtasks.Count >= 500) { error.Text = "最多支持500个子任务。"; return; } task.Subtasks.Add(new Subtask { Title = input.Text.Trim() }); input.Clear(); RefreshSubtasks(); input.Focus(); }
        input.KeyDown += (_,e) => { if(e.Key == Key.Enter) { e.Handled = true; Add(); } };
        var row = new DockPanel(); var button = Ui.Button("＋ 子任务", Add); DockPanel.SetDock(button, Dock.Right); row.Children.Add(button); row.Children.Add(input); panel.Children.Add(row);
        RefreshSubtasks(); return panel;
    }
    private void RefreshSubtasks()
    {
        subtaskRows.Children.Clear(); subtaskProgress.Text = "子任务 · " + task.Progress;
        foreach (var sub in task.Subtasks)
        {
            var group = new StackPanel { Margin = new Thickness(0,3,0,6) };
            var row = new DockPanel(); var done = new CheckBox { IsChecked = sub.Done, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,6,0) };
            done.Click += (_,_) => { sub.Done = done.IsChecked == true; subtaskProgress.Text = "子任务 · " + task.Progress; };
            row.Children.Add(done);
            var remove = Ui.Button("删除", () => { task.Subtasks.Remove(sub); RefreshSubtasks(); }); DockPanel.SetDock(remove, Dock.Right); row.Children.Add(remove);
            var titleBox = Ui.Input(sub.Title); titleBox.TextChanged += (_,_) => sub.Title = titleBox.Text; row.Children.Add(titleBox); group.Children.Add(row);
            var fields = new StackPanel();
            fields.Children.Add(Ui.FieldLabel("留空或不启用时，沿用父任务时间和评分"));
            fields.Children.Add(SubTime("开始", sub.Start, v => sub.Start = v)); fields.Children.Add(SubTime("截止", sub.Deadline, v => sub.Deadline = v));
            void SubScore(string label, int? value, Slider parent, Action<int?> set)
            {
                var header = new DockPanel();
                var inherit = new CheckBox { Content = "沿用父任务", IsChecked = !value.HasValue, VerticalAlignment = VerticalAlignment.Center };
                DockPanel.SetDock(inherit, Dock.Right); header.Children.Add(inherit); header.Children.Add(Ui.FieldLabel(label)); fields.Children.Add(header);
                var slider = new Slider { Minimum = 0, Maximum = 100, TickFrequency = 1, IsSnapToTickEnabled = true };
                var control = (FrameworkElement)Score(slider, value ?? (int)parent.Value); control.IsEnabled = value.HasValue; fields.Children.Add(control);
                slider.ValueChanged += (_, _) => { if (inherit.IsChecked != true) set((int)slider.Value); };
                inherit.Click += (_, _) => { control.IsEnabled = inherit.IsChecked != true; if (inherit.IsChecked == true) { set(null); slider.Value = parent.Value; } else set((int)slider.Value); };
                RoutedPropertyChangedEventHandler<double> sync = (_, _) => { if (inherit.IsChecked == true) slider.Value = parent.Value; };
                control.Loaded += (_, _) => { parent.ValueChanged += sync; if (inherit.IsChecked == true) slider.Value = parent.Value; };
                control.Unloaded += (_, _) => parent.ValueChanged -= sync;
            }
            SubScore("紧急性", sub.Urgency, urgency, n => sub.Urgency = n); SubScore("重要性", sub.Importance, importance, n => sub.Importance = n);
            fields.Children.Add(Ui.FieldLabel("备注")); var note = Ui.Input(sub.Notes); note.AcceptsReturn = true; note.TextWrapping = TextWrapping.Wrap; note.MinHeight = 48; note.TextChanged += (_,_) => sub.Notes = note.Text; fields.Children.Add(note);
            group.Children.Add(new Expander { Header = "时间、评分与备注", Content = fields, Foreground = Ui.Quiet }); subtaskRows.Children.Add(new Border { Child = group, BorderBrush = Ui.Rule, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 4, 0, 8) });
        }
    }
    private UIElement SubTime(string label, DateTime? value, Action<DateTime?> set)
    {
        var row = new WrapPanel(); var enabled = new CheckBox { Content = label, IsChecked = value.HasValue, Foreground = Ui.Quiet, VerticalAlignment = VerticalAlignment.Center };
        var initial = value ?? task.Start ?? task.PlanDate.Date.AddHours(9);
        var day = new DatePicker { SelectedDate = initial.Date, Width = 125 };
        var hour = Ui.Choice(Enumerable.Range(0,24).Select(n => n.ToString("00")), initial.Hour.ToString("00")); hour.Width = 50;
        var minute = Ui.Choice(Enumerable.Range(0,60).Select(n => n.ToString("00")), initial.Minute.ToString("00")); minute.Width = 50;
        void Update() { day.IsEnabled = hour.IsEnabled = minute.IsEnabled = enabled.IsChecked == true; set(enabled.IsChecked == true ? (day.SelectedDate ?? initial.Date).AddHours(int.Parse((string)hour.SelectedItem)).AddMinutes(int.Parse((string)minute.SelectedItem)) : null); }
        enabled.Click += (_,_) => Update(); day.SelectedDateChanged += (_,_) => Update(); hour.SelectionChanged += (_,_) => Update(); minute.SelectionChanged += (_,_) => Update();
        row.Children.Add(enabled); row.Children.Add(day); row.Children.Add(hour); row.Children.Add(minute); Update(); return row;
    }
}
