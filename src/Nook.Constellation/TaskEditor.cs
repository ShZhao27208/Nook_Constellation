using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
namespace Nook.Constellation;
public static class Ui
{
    public static readonly Brush Surface = new SolidColorBrush(Color.FromRgb(24, 31, 48));
    public static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(173, 187, 210));
    public static Window Dialog(Window owner, string title, double width = 560, double height = 650) => new()
    { Owner = owner, Title = "隅星集 · " + title, Width = Math.Min(width, SystemParameters.WorkArea.Width - 32), Height = Math.Min(height, SystemParameters.WorkArea.Height - 32), Background = Surface, Foreground = Brushes.White, WindowStartupLocation = WindowStartupLocation.CenterScreen, ShowInTaskbar = false };
    public static TextBlock Label(string text) => new() { Text = text, Foreground = Muted, Margin = new Thickness(0, 10, 0, 5), TextWrapping = TextWrapping.Wrap };
    public static Button Button(string text, Action action)
    { var b = new Button { Content = text, MinHeight = 30 }; b.Click += (_, _) => action(); return b; }
    public static TextBox Input(string text = "") => new() { Text = text, Padding = new Thickness(7), Margin = new Thickness(0, 2, 0, 5), Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(36, 46, 65)), BorderBrush = new SolidColorBrush(Color.FromRgb(80, 100, 125)) };
    public static ComboBox Choice(IEnumerable<string> values, string selected)
    { var box = new ComboBox { ItemsSource = values, SelectedItem = selected, MinHeight = 28, Margin = new Thickness(0, 3, 0, 6) }; return box; }
    public static void Error(Exception e) => MessageBox.Show(e.Message, "隅星集", MessageBoxButton.OK, MessageBoxImage.Warning);
}
public sealed class EditorDraft
{
    public TaskItem Task { get; set; } = new();
    public string Start { get; set; } = "";
    public string Deadline { get; set; } = "";
    public string Tags { get; set; } = "";
    public string Frequency { get; set; } = "不重复";
    public int Weekdays { get; set; } = 127;
    public int MonthDay { get; set; } = 1;
}
public sealed class TaskEditor : Window
{
    private readonly TaskStore store;
    private readonly TaskItem task;
    private readonly string draftKey;
    private readonly RichTextBox title = new();
    private readonly RichTextBox notes = new();
    private readonly Slider urgency = new() { Minimum = 0, Maximum = 100, TickFrequency = 1, IsSnapToTickEnabled = true };
    private readonly Slider importance = new() { Minimum = 0, Maximum = 100, TickFrequency = 1, IsSnapToTickEnabled = true };
    private readonly DatePicker date = new();
    private readonly TextBox start = Ui.Input();
    private readonly TextBox deadline = Ui.Input();
    private readonly TextBox tags = Ui.Input();
    private readonly ComboBox frequency = Ui.Choice(new[] { "不重复", "每天", "每周", "每月" }, "不重复");
    private readonly TextBox monthDay = Ui.Input();
    private readonly CheckBox[] weekdays = new CheckBox[7];
    private readonly TextBlock error = new() { Foreground = Brushes.Salmon, TextWrapping = TextWrapping.Wrap };
    private readonly DispatcherTimer draftTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool saved;
    private bool discard;
    public bool Changed { get; private set; }
    public TaskEditor(Window owner, TaskStore taskStore, TaskItem? existing)
    {
        store = taskStore; task = existing?.Copy() ?? new(); draftKey = existing?.Id ?? "new";
        System.Windows.Automation.AutomationProperties.SetName(title, "任务标题");
        System.Windows.Automation.AutomationProperties.SetName(notes, "任务备注");
        Owner = owner; Title = "隅星集 · " + (existing is null ? "新任务" : "编辑任务");
        Width = Math.Min(570, SystemParameters.WorkArea.Width - 32); Height = Math.Min(740, SystemParameters.WorkArea.Height - 40);
        Background = Ui.Surface; Foreground = Brushes.White; ShowInTaskbar = false; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var root = new DockPanel { Margin = new Thickness(20) };
        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(Ui.Button("放弃草稿", () => { discard = true; store.Remove("draft", draftKey); Close(); }));
        buttons.Children.Add(Ui.Button("保存", Save)); DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);
        var panel = new StackPanel(); root.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); Content = root;
        panel.Children.Add(Ui.Label("任务标题 · Enter 保存")); panel.Children.Add(RichEditor(title, task.TitleRtf, task.Title, 70));
        var detail = new StackPanel();
        detail.Children.Add(Ui.Label("紧急性（横轴）")); detail.Children.Add(Score(urgency, task.Urgency));
        detail.Children.Add(Ui.Label("重要性（纵轴）")); detail.Children.Add(Score(importance, task.Importance));
        detail.Children.Add(Ui.Label("计划日期")); date.SelectedDate = task.PlanDate; detail.Children.Add(date);
        detail.Children.Add(Ui.Label("开始时刻 · HH:mm，可留空")); start.Text = task.PlanTime?.ToString(@"hh\:mm") ?? ""; detail.Children.Add(start);
        detail.Children.Add(Ui.Label("截止时间 · yyyy-MM-dd HH:mm，可留空")); deadline.Text = task.Deadline?.ToString("yyyy-MM-dd HH:mm") ?? ""; detail.Children.Add(deadline);
        detail.Children.Add(Ui.Label("标签 · 用逗号分隔")); tags.Text = string.Join(", ", task.Tags); detail.Children.Add(tags);
        detail.Children.Add(Ui.Label("重复")); detail.Children.Add(frequency);
        var weekly = new WrapPanel(); var names = new[] { "日", "一", "二", "三", "四", "五", "六" };
        for (int i = 0; i < 7; i++) { weekdays[i] = new CheckBox { Content = "周" + names[i], Foreground = Brushes.White, Margin = new Thickness(4), IsChecked = true }; weekly.Children.Add(weekdays[i]); }
        detail.Children.Add(weekly); var monthlyLabel = Ui.Label("每月几号（1–31，无此日期取月末）"); detail.Children.Add(monthlyLabel); detail.Children.Add(monthDay); monthDay.Text = task.PlanDate.Day.ToString();
        void RepeatVisibility() { weekly.Visibility = (string?)frequency.SelectedItem == "每周" ? Visibility.Visible : Visibility.Collapsed; monthDay.Visibility = monthlyLabel.Visibility = (string?)frequency.SelectedItem == "每月" ? Visibility.Visible : Visibility.Collapsed; }
        frequency.SelectionChanged += (_, _) => RepeatVisibility(); RepeatVisibility();
        if (task.SeriesId is { } sid && store.Series.FirstOrDefault(s => s.Id == sid) is { } series)
        { frequency.SelectedItem = series.Frequency; monthDay.Text = series.MonthDay.ToString(); for (int i = 0; i < 7; i++) weekdays[i].IsChecked = (series.Weekdays & (1 << i)) != 0; }
        detail.Children.Add(Ui.Label("备注 · Ctrl+Enter 保存")); detail.Children.Add(RichEditor(notes, task.NotesRtf, task.Notes, 160));
        panel.Children.Add(new Expander { Header = "时间、标签、评分与备注", Content = detail, IsExpanded = existing is not null, Foreground = Brushes.White, Margin = new Thickness(0, 12, 0, 8) }); panel.Children.Add(error);
        if (store.Draft(draftKey) is { } json)
        {
            try
            {
                var draft = JsonSerializer.Deserialize<EditorDraft>(json)!;
                RichText.Load(title, draft.Task.TitleRtf, draft.Task.Title); RichText.Load(notes, draft.Task.NotesRtf, draft.Task.Notes);
                date.SelectedDate = draft.Task.PlanDate; urgency.Value = draft.Task.Urgency; importance.Value = draft.Task.Importance;
                start.Text = draft.Start; deadline.Text = draft.Deadline; tags.Text = draft.Tags; frequency.SelectedItem = draft.Frequency; monthDay.Text = draft.MonthDay.ToString();
                for (int i = 0; i < 7; i++) weekdays[i].IsChecked = (draft.Weekdays & (1 << i)) != 0;
                error.Text = "已恢复上次未保存的草稿。";
            }
            catch (JsonException) { error.Text = "草稿无法读取，已保留原始记录。"; }
        }
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter && (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || title.IsKeyboardFocusWithin)) { e.Handled = true; Save(); } };
        draftTimer.Tick += (_, _) => PersistDraft(); draftTimer.Start();
        Closing += (_, _) => { draftTimer.Stop(); if (!saved && !discard) PersistDraft(); };
        Loaded += (_, _) => title.Focus();
    }
    private UIElement Score(Slider slider, int value)
    {
        var p = new DockPanel(); var number = Ui.Input(value.ToString()); number.Width = 56; DockPanel.SetDock(number, Dock.Right); p.Children.Add(number); p.Children.Add(slider); slider.Value = value;
        slider.ValueChanged += (_, _) => number.Text = ((int)slider.Value).ToString();
        number.LostKeyboardFocus += (_, _) => { if (int.TryParse(number.Text, out var n)) slider.Value = Math.Clamp(n, 0, 100); number.Text = ((int)slider.Value).ToString(); };
        return p;
    }
    private UIElement RichEditor(RichTextBox box, string rtf, string plain, double height)
    {
        box.Height = height; box.Foreground = Brushes.White; box.Background = new SolidColorBrush(Color.FromRgb(36, 46, 65)); box.Padding = new Thickness(7); box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        RichText.Load(box, rtf, plain);
        var panel = new StackPanel(); panel.Children.Add(box); var tools = new WrapPanel { Visibility = Visibility.Collapsed };
        var bold = Ui.Button("B 加粗", () => { box.Focus(); EditingCommands.ToggleBold.Execute(null, box); }); bold.Focusable = false;
        var italic = Ui.Button("I 斜体", () => { box.Focus(); EditingCommands.ToggleItalic.Execute(null, box); }); italic.Focusable = false;
        var color = Ui.Button("文字颜色", () => { using var picker = new System.Windows.Forms.ColorDialog { FullOpen = true }; if (picker.ShowDialog() == System.Windows.Forms.DialogResult.OK) { box.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(Color.FromRgb(picker.Color.R, picker.Color.G, picker.Color.B))); box.Focus(); } }); color.Focusable = false;
        tools.Children.Add(bold); tools.Children.Add(italic); tools.Children.Add(color); panel.Children.Add(tools);
        box.SelectionChanged += (_, _) => tools.Visibility = box.Selection.IsEmpty ? Visibility.Collapsed : Visibility.Visible;
        return panel;
    }
    private int Days() { int mask = 0; for (int i = 0; i < 7; i++) if (weekdays[i].IsChecked == true) mask |= 1 << i; return mask; }
    private TaskItem ReadText()
    {
        var t = task.Copy(); t.Title = RichText.Plain(title); t.TitleRtf = RichText.Save(title); t.Notes = RichText.Plain(notes); t.NotesRtf = RichText.Save(notes);
        t.PlanDate = (date.SelectedDate ?? DateTime.Today).Date; t.Urgency = (int)urgency.Value; t.Importance = (int)importance.Value;
        t.Tags = tags.Text.Split(new[] { ',', '，', ';', '；' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList(); return t;
    }
    private void PersistDraft()
    {
        try { store.SaveDraft(draftKey, JsonSerializer.Serialize(new EditorDraft { Task = ReadText(), Start = start.Text, Deadline = deadline.Text, Tags = tags.Text, Frequency = (string)frequency.SelectedItem, Weekdays = Days(), MonthDay = int.TryParse(monthDay.Text, out var n) ? n : 1 })); }
        catch (Exception e) { error.Text = "草稿保存失败：" + e.Message; }
    }
    private void Save()
    {
        try
        {
            var t = ReadText(); t.PlanTime = null; t.Deadline = null;
            if (!string.IsNullOrWhiteSpace(start.Text))
            { if (!TimeSpan.TryParseExact(start.Text.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out var time) || time >= TimeSpan.FromDays(1)) throw new InvalidOperationException("开始时刻请填写 HH:mm，例如 09:30。"); t.PlanTime = time; }
            if (!string.IsNullOrWhiteSpace(deadline.Text))
            { if (!DateTime.TryParseExact(deadline.Text.Trim(), "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) throw new InvalidOperationException("截止时间请填写 yyyy-MM-dd HH:mm。"); t.Deadline = d; }
            t.Validate();
            RepeatSeries? repeat = null;
            if ((string)frequency.SelectedItem != "不重复")
            {
                if (!int.TryParse(monthDay.Text, out var n)) n = t.PlanDate.Day;
                repeat = new RepeatSeries { Frequency = (string)frequency.SelectedItem, Weekdays = Days(), MonthDay = n, Template = t.Copy() }; repeat.Validate();
            }
            bool future = false;
            if (task.SeriesId is not null)
            {
                var answer = MessageBox.Show(this, "是否应用到这一次及以后？\n“是”：修改这一次及以后的待做任务。\n“否”：只修改这一次。", "重复任务", MessageBoxButton.YesNoCancel);
                if (answer == MessageBoxResult.Cancel) return; future = answer == MessageBoxResult.Yes;
            }
            store.SaveWithRepeat(t, repeat, future); saved = true; Changed = true; DialogResult = true;
        }
        catch (Exception e) { error.Text = e.Message; }
    }
}
