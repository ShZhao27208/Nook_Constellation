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
    public static readonly Brush Paper = new SolidColorBrush(Color.FromRgb(244, 241, 234));
    public static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(23, 33, 31));
    public static readonly Brush Pine = new SolidColorBrush(Color.FromRgb(31, 74, 67));
    public static readonly Brush Quiet = new SolidColorBrush(Color.FromRgb(70, 80, 77));
    public static readonly Brush Rule = new SolidColorBrush(Color.FromRgb(203, 207, 197));
    public static void ApplyTheme(FrameworkElement element) => element.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Nook.Constellation;component/EditorTheme.xaml", UriKind.Relative) });
    public static void ApplyEditorTheme(Window window)
    {
        ApplyTheme(window);
        window.Background = Paper; window.Foreground = Ink; window.FontFamily = new FontFamily("Microsoft YaHei UI"); window.FontSize = 13;
    }
    public static UIElement Heading(string title, string subtitle)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
        panel.Children.Add(new TextBlock { Text = title, FontFamily = new FontFamily("Noto Serif SC, SimSun"), FontSize = 26, Foreground = Pine });
        panel.Children.Add(new TextBlock { Text = subtitle, Foreground = Quiet, Margin = new Thickness(0, 7, 0, 0), TextWrapping = TextWrapping.Wrap });
        return new Border { Child = panel, BorderBrush = Rule, BorderThickness = new Thickness(0, 0, 0, 1), Margin = new Thickness(0, 0, 0, 14) };
    }
    public static Border Card(UIElement child) => new() { Child = child, Background = new SolidColorBrush(Color.FromRgb(255, 253, 248)), BorderBrush = Rule, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(18) };
    public static TextBlock Section(string text) => new() { Text = text, Foreground = Pine, FontFamily = new FontFamily("Noto Serif SC, SimSun"), FontSize = 18, Margin = new Thickness(0, 14, 0, 8) };
    public static TextBlock Label(string text) => new() { Text = text, Foreground = Muted, Margin = new Thickness(0, 10, 0, 5), TextWrapping = TextWrapping.Wrap };
    public static TextBlock FieldLabel(string text) => new() { Text = text, Foreground = Quiet, Margin = new Thickness(0, 10, 0, 6), TextWrapping = TextWrapping.Wrap };
    public static Button Button(string text, Action action)
    { var b = new Button { Content = text, MinHeight = 30 }; b.Click += (_, _) => action(); return b; }
    public static TextBox Input(string text = "") => new() { Text = text, Padding = new Thickness(7), Margin = new Thickness(0, 2, 0, 5), Foreground = Ink, Background = new SolidColorBrush(Color.FromRgb(255, 253, 248)), BorderBrush = Rule };
    public static ComboBox Choice(IEnumerable<string> values, string selected)
    { var box = new ComboBox { ItemsSource = values, SelectedItem = selected, MinHeight = 28, Margin = new Thickness(0, 3, 0, 6) }; return box; }
    public static void Error(Exception e) => MessageBox.Show(e.Message, "隅星集", MessageBoxButton.OK, MessageBoxImage.Warning);
}
public sealed class EditorDraft
{
    public TaskItem Task { get; set; } = new();
    public string PendingSubtask { get; set; } = "";
    public string Start { get; set; } = "";
    public string Deadline { get; set; } = "";
    public string Tags { get; set; } = "";
    public string Frequency { get; set; } = "不重复";
    public int Weekdays { get; set; } = 127;
    public int MonthDay { get; set; } = 1;
}
public sealed partial class TaskEditor : Window
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
    private readonly TextBlock error = new() { Foreground = new SolidColorBrush(Color.FromRgb(167, 58, 42)), TextWrapping = TextWrapping.Wrap };
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
        Width = Math.Min(1120, SystemParameters.WorkArea.Width - 32); Height = Math.Min(820, SystemParameters.WorkArea.Height - 40);
        Ui.ApplyEditorTheme(this); ShowInTaskbar = false; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var root = new DockPanel { Margin = new Thickness(24) };
        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(Ui.Button("放弃草稿", () => { discard = true; store.Remove("draft", draftKey); Close(); }));
        var save = Ui.Button("保存任务", Save); save.Background = Ui.Pine; save.Foreground = Brushes.White; save.MinWidth = 110; buttons.Children.Add(save); var footer = new DockPanel { Margin = new Thickness(0, 16, 0, 0) };
        DockPanel.SetDock(buttons, Dock.Right); footer.Children.Add(buttons);
        footer.Children.Add(new TextBlock { Text = "草稿自动保留   ·   Ctrl+Enter 保存", Foreground = Ui.Quiet, VerticalAlignment = VerticalAlignment.Center });
        var footerLine = new Border { Child = footer, BorderBrush = Ui.Rule, BorderThickness = new Thickness(0, 1, 0, 0), Margin = new Thickness(0, 16, 0, 0) };
        DockPanel.SetDock(footerLine, Dock.Bottom); root.Children.Add(footerLine);
        DockPanel.SetDock(error, Dock.Bottom); root.Children.Add(error);
        var panel = new StackPanel(); Content = root;
        var heading = Ui.Heading(existing is null ? "写下一件事" : "理清一件事", "隅星集  /  NOOK CONSTELLATION"); DockPanel.SetDock(heading, Dock.Top); root.Children.Insert(0, heading);
        panel.Children.Add(Ui.Section("任务标题")); panel.Children.Add(RichEditor(title, task.TitleRtf, task.Title, 64));
        var detail = new StackPanel(); detail.Children.Add(Ui.Section("轻重缓急"));
        detail.Children.Add(Ui.FieldLabel("紧急性（横轴）")); detail.Children.Add(Score(urgency, task.Urgency));
        detail.Children.Add(Ui.FieldLabel("重要性（纵轴）")); detail.Children.Add(Score(importance, task.Importance));
        detail.Children.Add(Ui.Section("时间安排"));
        detail.Children.Add(Ui.FieldLabel("计划日期")); date.SelectedDate = task.PlanDate; detail.Children.Add(date);
        var dates = new WrapPanel(); dates.Children.Add(Ui.Button("今天", () => date.SelectedDate = DateTime.Today)); dates.Children.Add(Ui.Button("明天", () => date.SelectedDate = DateTime.Today.AddDays(1))); detail.Children.Add(dates);
        detail.Children.Add(Ui.FieldLabel("开始时刻 · 可不设置")); start.Text = task.PlanTime?.ToString(@"hh\:mm") ?? ""; detail.Children.Add(TimeInput(start, false));
        detail.Children.Add(Ui.FieldLabel("截止时间 · 可不设置")); deadline.Text = task.Deadline?.ToString("yyyy-MM-dd HH:mm") ?? ""; detail.Children.Add(TimeInput(deadline, true));
        detail.Children.Add(Ui.FieldLabel("标签 · 用逗号分隔")); tags.Text = string.Join(", ", task.Tags); detail.Children.Add(tags);
        detail.Children.Add(Ui.FieldLabel("重复")); detail.Children.Add(frequency);
        var weekly = new WrapPanel(); var names = new[] { "日", "一", "二", "三", "四", "五", "六" };
        for (int i = 0; i < 7; i++) { weekdays[i] = new CheckBox { Content = "周" + names[i], Foreground = Ui.Ink, Margin = new Thickness(4), IsChecked = true }; weekly.Children.Add(weekdays[i]); }
        detail.Children.Add(weekly); var monthlyLabel = Ui.FieldLabel("每月几号（1–31，无此日期取月末）"); detail.Children.Add(monthlyLabel); detail.Children.Add(monthDay); monthDay.Text = task.PlanDate.Day.ToString();
        void RepeatVisibility() { weekly.Visibility = (string?)frequency.SelectedItem == "每周" ? Visibility.Visible : Visibility.Collapsed; monthDay.Visibility = monthlyLabel.Visibility = (string?)frequency.SelectedItem == "每月" ? Visibility.Visible : Visibility.Collapsed; }
        frequency.SelectionChanged += (_, _) => RepeatVisibility(); RepeatVisibility();
        if (task.SeriesId is { } sid && store.Series.FirstOrDefault(s => s.Id == sid) is { } series)
        { frequency.SelectedItem = series.Frequency; monthDay.Text = series.MonthDay.ToString(); for (int i = 0; i < 7; i++) weekdays[i].IsChecked = (series.Weekdays & (1 << i)) != 0; }

        var fields = new Grid(); fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });
        var writing = new StackPanel();
        while (panel.Children.Count > 0) { var child = panel.Children[0]; panel.Children.RemoveAt(0); writing.Children.Add(child); }
        writing.Children.Add(BuildSubtasks());
        writing.Children.Add(Ui.Section("备注")); writing.Children.Add(RichEditor(notes, task.NotesRtf, task.Notes, 210));
        var writingScroll = new ScrollViewer { Content = writing, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(0, 0, 20, 0) };
        fields.Children.Add(writingScroll);
        var detailScroll = new ScrollViewer { Content = detail, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(20, 0, 4, 0) };
        var sidebar = new Border { Child = detailScroll, BorderBrush = Ui.Rule, BorderThickness = new Thickness(1, 0, 0, 0) };
        Grid.SetColumn(sidebar, 1); fields.Children.Add(sidebar); root.Children.Add(fields);
        if (store.Draft(draftKey) is { } json)
        {
            try
            {
                var draft = JsonSerializer.Deserialize<EditorDraft>(json)!;
                RichText.Load(title, draft.Task.TitleRtf, draft.Task.Title); RichText.Load(notes, draft.Task.NotesRtf, draft.Task.Notes);
                RichText.UseEditorColors(title); RichText.UseEditorColors(notes);
                pendingSubtask.Text = draft.PendingSubtask; task.Subtasks = draft.Task.Subtasks ?? new(); RefreshSubtasks();
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
    private UIElement TimeInput(TextBox storage, bool withDate)
    {
        var panel = new StackPanel();
        var row = new WrapPanel(); var enabled = new CheckBox { Content = withDate ? "设置截止" : "设置开始", Foreground = Ui.Ink, VerticalAlignment = VerticalAlignment.Center };
        var calendar = new DatePicker { SelectedDate = DateTime.Today, Width = 130 };
        var hour = Ui.Choice(Enumerable.Range(0,24).Select(n => n.ToString("00")), "09"); hour.Width = 52; hour.IsEditable = true;
        var minute = Ui.Choice(Enumerable.Range(0,60).Select(n => n.ToString("00")), "00"); minute.Width = 52; minute.IsEditable = true;
        panel.Children.Add(enabled); if(withDate) row.Children.Add(calendar); row.Children.Add(hour); row.Children.Add(Ui.FieldLabel(":")); row.Children.Add(minute); panel.Children.Add(row);
        bool syncing = false;
        void FromText() {
            if(syncing) return; syncing = true;
            enabled.IsChecked = !string.IsNullOrWhiteSpace(storage.Text);
            DateTime parsed;
            if(DateTime.TryParseExact(storage.Text, withDate ? "yyyy-MM-dd HH:mm" : "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)) { calendar.SelectedDate = withDate ? parsed.Date : DateTime.Today; hour.Text = parsed.Hour.ToString("00"); minute.Text = parsed.Minute.ToString("00"); }
            hour.IsEnabled = minute.IsEnabled = calendar.IsEnabled = enabled.IsChecked == true; syncing = false;
        }
        void Update() { if(syncing) return; syncing = true; hour.IsEnabled = minute.IsEnabled = calendar.IsEnabled = enabled.IsChecked == true; storage.Text = enabled.IsChecked == true ? (withDate ? (calendar.SelectedDate ?? DateTime.Today).ToString("yyyy-MM-dd ") : "") + hour.Text + ":" + minute.Text : ""; syncing = false; }
        enabled.Checked += (_,_) => Update(); enabled.Unchecked += (_,_) => Update(); calendar.SelectedDateChanged += (_,_) => Update();
        hour.DropDownClosed += (_,_) => Update(); minute.DropDownClosed += (_,_) => Update(); hour.LostKeyboardFocus += (_,_) => Update(); minute.LostKeyboardFocus += (_,_) => Update();
        storage.TextChanged += (_,_) => FromText(); FromText();
        var quick = new WrapPanel();
        void Set(DateTime value) { if(!withDate) date.SelectedDate = value.Date; storage.Text = value.ToString(withDate ? "yyyy-MM-dd HH:mm" : "HH:mm"); }
        quick.Children.Add(Ui.Button("现在", () => Set(DateTime.Now))); quick.Children.Add(Ui.Button("30分钟后", () => Set(DateTime.Now.AddMinutes(30)))); quick.Children.Add(Ui.Button("明天9点", () => Set(DateTime.Today.AddDays(1).AddHours(9)))); quick.Children.Add(Ui.Button("清除", () => storage.Text = "")); panel.Children.Add(quick);
        return panel;
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
        box.Height = height; box.Foreground = Ui.Ink; box.Background = Ui.Paper; box.BorderBrush = Ui.Rule; box.Padding = new Thickness(12); box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        RichText.Load(box, rtf, plain); RichText.UseEditorColors(box);
        var panel = new StackPanel(); var tools = new WrapPanel { Margin = new Thickness(0, 0, 0, 5) };
        var bold = Ui.Button("B", () => { box.Focus(); EditingCommands.ToggleBold.Execute(null, box); }); bold.Focusable = false; bold.ToolTip = "加粗"; bold.FontWeight = FontWeights.Bold;
        var italic = Ui.Button("I", () => { box.Focus(); EditingCommands.ToggleItalic.Execute(null, box); }); italic.Focusable = false; italic.ToolTip = "斜体"; italic.FontStyle = FontStyles.Italic;
        var color = Ui.Button("颜色", () => { var a = box.Selection.Start; var b = box.Selection.End; using var picker = new System.Windows.Forms.ColorDialog { FullOpen = true }; if (picker.ShowDialog() == System.Windows.Forms.DialogResult.OK) { box.Selection.Select(a, b); box.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(Color.FromRgb(picker.Color.R, picker.Color.G, picker.Color.B))); box.Focus(); } }); color.Focusable = false;
        tools.Children.Add(bold); tools.Children.Add(italic); tools.Children.Add(color); panel.Children.Add(tools);
        TextPointer selectionStart = box.Selection.Start, selectionEnd = box.Selection.End;
        box.SelectionChanged += (_, _) => { if (box.IsKeyboardFocusWithin) { selectionStart = box.Selection.Start; selectionEnd = box.Selection.End; } };
        var sizes = Ui.Choice(new[] { "10", "12", "13", "14", "16", "18", "20", "24", "28", "32" }, "13"); sizes.Width = 64; sizes.ToolTip = "选中文字的字号";
        sizes.SelectionChanged += (_, _) => { if (double.TryParse(sizes.SelectedItem?.ToString(), out var size)) { box.Selection.Select(selectionStart, selectionEnd); box.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, size); box.Focus(); } };
        tools.Children.Add(sizes);
        var reset = Ui.Button("恢复默认", () => { box.Selection.Select(selectionStart, selectionEnd); box.Selection.ClearAllProperties(); RichText.ResetEditorColor(box.Selection); box.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, RichText.DefaultSize); box.Focus(); }); reset.Focusable = false; tools.Children.Add(reset);
        foreach (var tool in tools.Children.OfType<Button>()) { tool.Padding = new Thickness(8, 3, 8, 3); tool.MinHeight = 28; tool.BorderThickness = new Thickness(0); }
        panel.Children.Add(box);
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
        try { store.SaveDraft(draftKey, JsonSerializer.Serialize(new EditorDraft { PendingSubtask = pendingSubtask.Text, Task = ReadText(), Start = start.Text, Deadline = deadline.Text, Tags = tags.Text, Frequency = (string)frequency.SelectedItem, Weekdays = Days(), MonthDay = int.TryParse(monthDay.Text, out var n) ? n : 1 })); }
        catch (Exception e) { error.Text = "草稿保存失败：" + e.Message; }
    }
    private void Save()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(pendingSubtask.Text)) { task.Subtasks.Add(new Subtask { Title = pendingSubtask.Text.Trim() }); pendingSubtask.Clear(); RefreshSubtasks(); }
            var t = ReadText(); t.PlanTime = null; t.Deadline = null;
            if (!string.IsNullOrWhiteSpace(start.Text))
            { if (!TimeSpan.TryParseExact(start.Text.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out var time) || time >= TimeSpan.FromDays(1)) throw new InvalidOperationException("开始时刻请填写 HH:mm，例如 09:30。"); t.PlanTime = time; }
            if (!string.IsNullOrWhiteSpace(deadline.Text))
            { if (!DateTime.TryParseExact(deadline.Text.Trim(), "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) throw new InvalidOperationException("截止时间请填写 yyyy-MM-dd HH:mm。"); t.Deadline = d; }
            t.Validate();
            if (t.State == TaskState.Completed && t.Subtasks.Any(s => !s.Done)) { t.State = TaskState.Pending; t.Completed = null; }
            if (t.Subtasks.Count > 0 && t.Subtasks.All(s => s.Done) && t.State != TaskState.Completed && MessageBox.Show(this, "所有子任务已完成，是否同时完成父任务？", "子任务进度", MessageBoxButton.YesNo) == MessageBoxResult.Yes) { t.State = TaskState.Completed; t.Completed = DateTime.Now; t.SnoozedUntil = null; }
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
        catch (Exception e) { error.Text = "未保存：" + e.Message; }
    }
}
