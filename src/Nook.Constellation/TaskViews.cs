using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Controls.Primitives;
using System.Windows.Shapes;
using System.Windows.Threading;
namespace Nook.Constellation;
public partial class MainWindow
{
    private TaskStore store = null!;
    private DispatcherTimer timer = null!;
    private List<TaskItem> undoTasks = new();
    private RepeatSeries? undoSeries;
    private DateTime undoUntil;
    private DateTime lastDay;
    private bool initialized;
    private event Action? TasksChanged;
    private void InitializeTasks()
    {
        store = new TaskStore(Preferences.DataDirectory); store.Materialize(DateTime.Today); lastDay = DateTime.Today;
        if (App.NotificationTest && store.Tasks.Count == 0) { var start = DateTime.Now.AddMinutes(-1); store.Save(new TaskItem { Title = "隅星集通知功能测试", PlanDate = start.Date, PlanTime = start.TimeOfDay }); }
        initialized = true;
        hoverClose.Tick += (_,_) => { hoverClose.Stop(); detailsPopup.IsOpen = false; };
        Deactivated += (_,_) => { detailsPopup.IsOpen = false; };
        SearchBox.TextChanged += (_, _) => RenderTasks(); TagsBox.TextChanged += (_, _) => RenderTasks(); ScopeBox.SelectionChanged += (_, _) => RenderTasks();
        MatrixCanvas.SizeChanged += (_, _) => RenderTasks();
        PreviewMouseLeftButtonDown += (_, e) => Burst(e.GetPosition(Particles));
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        timer.Tick += (_, _) => Guard(() => { if (lastDay != DateTime.Today) { store.Materialize(DateTime.Today); lastDay = DateTime.Today; } BackupDaily(); CheckReminders(); if (!IsMouseOver && !detailsPopup.IsOpen && !OwnedWindows.Cast<Window>().Any()) RenderTasks(); });
        timer.Start(); Loaded += (_, _) => Guard(() => { InitializeNotifications(); BackupDaily(); CheckReminders(); if (!IsMouseOver && !detailsPopup.IsOpen && !OwnedWindows.Cast<Window>().Any()) RenderTasks(); });
    }
    private void StopTasks() { hoverClose.Stop(); detailsPopup.IsOpen = false; timer.Stop(); store.Dispose(); }
    private void Guard(Action action) { try { action(); } catch (Exception e) { StatusText.Text = "操作失败 · 查看详情"; StatusText.ToolTip = e.Message; System.IO.File.AppendAllText(System.IO.Path.Combine(Preferences.DataDirectory, "errors.log"), DateTime.Now + " " + e + Environment.NewLine); } }
    private List<TaskItem> VisibleTasks()
    {
        var scope = (ScopeBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "今天";
        var search = SearchBox.Text.Trim(); var tags = TagsBox.Text.Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return store.Tasks.Where(t => scope switch { "回收站" => t.Deleted is not null, "已完成" => t.Deleted is null && t.State == TaskState.Completed, "全部" => t.Deleted is null && t.State != TaskState.Completed, _ => t.Deleted is null && t.State != TaskState.Completed && t.PlanDate.Date <= DateTime.Today })
            .Where(t => search.Length == 0 || (t.Title + " " + t.Notes + " " + string.Join(" ", t.Tags)).Contains(search, StringComparison.OrdinalIgnoreCase))
            .Where(t => tags.Length == 0 || t.Tags.Intersect(tags, StringComparer.OrdinalIgnoreCase).Any()).OrderBy(t => t.Group(DateTime.Now)).ThenBy(t => t.Start ?? t.PlanDate.Date.AddDays(1).AddTicks(-1)).ThenBy(t => t.Created).ToList();
    }
    private string lastRender = "";
    private readonly Popup detailsPopup = new() { AllowsTransparency = true, StaysOpen = true, Placement = PlacementMode.Right, PopupAnimation = PopupAnimation.None };
    private readonly DispatcherTimer hoverClose = new() { Interval = TimeSpan.FromMilliseconds(160) };
    private void AttachDetails(FrameworkElement anchor, List<TaskItem> tasks)
    {
        anchor.ToolTip = null;
        anchor.MouseEnter += (_,_) => {
            hoverClose.Stop();
            var content = new StackPanel { Margin = new Thickness(22) };
            foreach(var t in tasks) {
                var taskContent = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
                var title = RichText.Preview(t.TitleRtf, t.Title, paper: true); title.Margin = new Thickness(0, 0, 0, 8); taskContent.Children.Add(title);
                taskContent.Children.Add(Ui.FieldLabel(t.Quadrant + $" · 紧急 {t.Urgency} / 重要 {t.Importance}"));
                taskContent.Children.Add(Ui.FieldLabel(t.TimeDescription(DateTime.Now)));
                if(t.Tags.Count > 0) taskContent.Children.Add(Ui.FieldLabel(string.Join(" · ", t.Tags.Select(tag => "#" + tag))));
                taskContent.Children.Add(SubtaskList(t, true));
                if (!string.IsNullOrWhiteSpace(t.Notes)) {
                    taskContent.Children.Add(Ui.Section("备注"));
                    taskContent.Children.Add(RichText.Preview(t.NotesRtf, t.Notes, paper: true));
                }
                var edit = Ui.Button("编辑任务  →", () => { detailsPopup.IsOpen = false; Edit(t); });
                edit.HorizontalAlignment = HorizontalAlignment.Left; edit.Margin = new Thickness(0, 16, 0, 0); taskContent.Children.Add(edit);
                content.Children.Add(new Border { Child = taskContent, BorderBrush = Ui.Rule, BorderThickness = new Thickness(0, 0, 0, 1) });
            }
            var surface = new Border { Background = Ui.Paper, BorderBrush = Ui.Rule, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Child = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, Width = Math.Min(420, SystemParameters.WorkArea.Width - 32), MaxHeight = Math.Min(560, SystemParameters.WorkArea.Height - 32) };
            Ui.ApplyTheme(surface);
            surface.MouseEnter += (_,_) => hoverClose.Stop(); surface.MouseLeave += (_,_) => hoverClose.Start();
            detailsPopup.PlacementTarget = anchor; detailsPopup.Child = surface; detailsPopup.IsOpen = true;
        };
        anchor.MouseLeave += (_,_) => hoverClose.Start();
        anchor.PreviewMouseLeftButtonDown += (_,_) => detailsPopup.IsOpen = false;
        anchor.Unloaded += (_,_) => { if(detailsPopup.PlacementTarget == anchor) detailsPopup.IsOpen = false; };
    }
    private void RenderTasks()
    {
        if (!initialized) return;
        var tasks = VisibleTasks(); var scope = (ScopeBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
        bool flow = preferences.View == "任务流" || scope is "回收站" or "已完成";
        var signature = System.Text.Json.JsonSerializer.Serialize(tasks) + scope + preferences.View + preferences.Expanded + preferences.FontSize + MatrixCanvas.ActualWidth + "/" + MatrixCanvas.ActualHeight + DateTime.Now.ToString("yyyyMMddHHmm");
        UndoButton.Visibility = DateTime.Now < undoUntil ? Visibility.Visible : Visibility.Collapsed;
        if (signature == lastRender) return;
        lastRender = signature;
        MatrixCanvas.Visibility = flow ? Visibility.Collapsed : Visibility.Visible;
        MatrixColumn.Width = new GridLength(flow ? 0 : 1, flow ? GridUnitType.Pixel : GridUnitType.Star);
        // In expanded quadrant mode the matrix owns the complete content area.
        // Task-flow mode remains the detail view when selected explicitly.
        DetailsColumn.Width = flow ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        FlowScroller.Visibility = flow ? Visibility.Visible : Visibility.Collapsed;
        ViewButton.Content = preferences.View == "任务流" ? "⊞" : "≡";
        StatusText.Text = tasks.Count + " 个任务";
        UndoButton.Visibility = DateTime.Now < undoUntil ? Visibility.Visible : Visibility.Collapsed;
        TaskList.Children.Clear();
        if (flow)
        {
            int group = -1;
            foreach (var t in tasks)
            {
                int next = t.Group(DateTime.Now);
                if (scope is not ("已完成" or "回收站") && next != group) { group = next; TaskList.Children.Add(Ui.Label(new[] { "正在做", "已逾期", "今天待做", "未来任务" }[group])); }
                TaskList.Children.Add(TaskCard(t, preferences.Expanded));
            }
            if (tasks.Count == 0) TaskList.Children.Add(Ui.Label("没有符合条件的任务\n点击 ＋ 记下第一件事"));
        }
        if (!flow) DrawMatrix(tasks);
        TasksChanged?.Invoke();
    }
    private void DrawMatrix(List<TaskItem> tasks)
    {
        MatrixCanvas.Children.Clear(); double w = MatrixCanvas.ActualWidth, h = MatrixCanvas.ActualHeight;
        if (w < 50 || h < 40) return;
        void Line(double x1, double y1, double x2, double y2, Brush brush, double opacity = 1)
        { MatrixCanvas.Children.Add(new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = brush, Opacity = opacity, StrokeThickness = 1, IsHitTestVisible = false }); }
        void Text(string value, double x, double y, int size = 10)
        { var t = new TextBlock { Text = value, FontSize = size, Foreground = Brushes.White, Opacity = 0.8, IsHitTestVisible = false }; Canvas.SetLeft(t, x); Canvas.SetTop(t, y); MatrixCanvas.Children.Add(t); }
        double pad = 20, iw = w - pad * 2, ih = h - pad * 2;
        Point Position(TaskItem t) => new(pad + t.Urgency / 100.0 * iw, pad + (100 - t.Importance) / 100.0 * ih);
        Line(pad, h / 2, w - 4, h / 2, Ui.Muted); Line(w / 2, h - pad, w / 2, 3, Ui.Muted);
        Text("↑ 重要", w / 2 + 3, 0); Text("紧急 →", Math.Max(0, w - 43), h / 2 + 1);
        Text("重要不紧急", 0, 1); Text("重要且紧急", Math.Max(w / 2 + 45, w - 72), 1);
        Text("不重要也不紧急", 0, h - 14); Text("不重要但紧急", Math.Max(w / 2 + 5, w - 78), h - 14);
        if (preferences.Expanded)
        { for (int i = -5; i <= 5; i++) { Text(i.ToString(), pad + (i + 5) / 10.0 * iw - 3, h / 2 + 1, 9); if (i != 0) Text(i.ToString(), w / 2 + 3, pad + (5 - i) / 10.0 * ih - 5, 9); } }
        int cols = Math.Max(2, (int)(iw / 105)), rows = Math.Max(2, (int)(ih / 44));
        var groups = tasks.GroupBy(t => (x: Math.Clamp((int)(t.Urgency / 100.0 * cols), 0, cols - 1), y: Math.Clamp((int)((100 - t.Importance) / 100.0 * rows), 0, rows - 1)));
        foreach (var group in groups)
        {
            var items = group.ToList(); var first = items[0]; var point = Position(first);
            double width = iw / cols - 5, x = pad + group.Key.x * iw / cols, y = pad + group.Key.y * ih / rows + 5;
            foreach (var task in items)
            {
                var real = Position(task); Line(real.X, real.Y, x + width / 2, y + 11, Ui.Muted, 0.5);
                var dot = new Ellipse { Width = 5, Height = 5, Fill = task.State == TaskState.Doing ? Brushes.Gold : Brushes.LightSkyBlue, ToolTip = $"{task.Title} · ({task.X:0.#}, {task.Y:0.#}) · {task.Quadrant}" };
                AttachDetails(dot, new List<TaskItem> { task });
                Canvas.SetLeft(dot, real.X - 2.5); Canvas.SetTop(dot, real.Y - 2.5); MatrixCanvas.Children.Add(dot);
            }
            var label = items.Count == 1 ? RichText.Preview(first.TitleRtf, first.Title) : new TextBlock { Text = items.Count + " 个任务 · 点击展开", Foreground = Brushes.White, FontSize = 11 };
            if (items.Count == 1 && first.Subtasks.Count > 0) label.Inlines.Add(new System.Windows.Documents.Run("  [" + first.Progress + "]"));
            label.MaxHeight = Math.Max(29, RichText.DefaultSize * 2.5); label.TextTrimming = TextTrimming.CharacterEllipsis;
            var card = new Border { Width = Math.Max(35, width), Padding = new Thickness(4, 2, 4, 2), CornerRadius = new CornerRadius(5), Background = new SolidColorBrush(Color.FromArgb(70, 22, 32, 50)), BorderThickness = new Thickness(items.Any(t => t.State == TaskState.Doing) ? 1.5 : 0.5), BorderBrush = items.Any(t => t.State == TaskState.Doing) ? Brushes.Gold : Ui.Muted, Child = label, Cursor = Cursors.Hand, ToolTip = items.Count == 1 ? Details(first) : string.Join("\n", items.Select(t => t.Title)) };
            AttachDetails(card, items);
            card.MouseLeftButtonUp += (_, _) => { if (items.Count == 1) Edit(first); else OpenCluster(items); };
            if (items.Count == 1) AttachTaskActions(card, first);
            Canvas.SetLeft(card, x); Canvas.SetTop(card, y); MatrixCanvas.Children.Add(card);
        }
        if (tasks.Count == 0) Text("点击 ＋，让想做的事各就其位", Math.Max(3, w / 2 - 94), h / 2 + 20, 11);
    }
    private string Details(TaskItem t) => t.Title + "\n" + t.Quadrant + $" · 紧急 {t.Urgency} / 重要 {t.Importance}\n" + t.TimeDescription(DateTime.Now) + "\n" + string.Join(" · ", t.Tags) + "\n" + t.Notes;
    private Border TaskCard(TaskItem task, bool full)
    {
        var panel = new StackPanel(); var title = RichText.Preview(task.TitleRtf, task.Title, 13); title.Margin = new Thickness(0, 0, 0, 4); panel.Children.Add(title);
        var time = Ui.Label(task.TimeDescription(DateTime.Now)); time.Margin = new Thickness(0, 2, 0, 2); time.FontSize = 11; panel.Children.Add(time);
        if (task.Tags.Count > 0) { var tags = Ui.Label(string.Join("  ", task.Tags.Select(t => "#" + t))); tags.Margin = new Thickness(0, 2, 0, 2); panel.Children.Add(tags); }
        if (full)
        {
            panel.Children.Add(Ui.Label($"{task.Quadrant} · 紧急 {task.Urgency} / 重要 {task.Importance}"));
            if (!string.IsNullOrWhiteSpace(task.Notes)) panel.Children.Add(new ScrollViewer { Content = RichText.Preview(task.NotesRtf, task.Notes), MaxHeight = 160, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        }
        panel.Children.Add(SubtaskList(task));
        var actions = new WrapPanel();
        if (task.Deleted is not null) actions.Children.Add(Ui.Button("恢复任务", () => Change(task, t => t.Deleted = null)));
        else if (task.State == TaskState.Completed) actions.Children.Add(Ui.Button("恢复待做", () => Change(task, t => { t.State = TaskState.Pending; t.Completed = null; })));
        else
        {
            actions.Children.Add(Ui.Button(task.State == TaskState.Doing ? "暂停" : "开始做", () => ToggleDoing(task)));
            actions.Children.Add(Ui.Button("完成", () => Change(task, t => { t.State = TaskState.Completed; t.Completed = DateTime.Now; t.SnoozedUntil = null; })));
        }
        if (task.Deleted is null) { actions.Children.Add(Ui.Button("编辑", () => Edit(task))); actions.Children.Add(Ui.Button("删除", () => DeleteTask(task))); }
        panel.Children.Add(actions);
        var card = new Border { Child = panel, Background = new SolidColorBrush(Color.FromArgb(65, 20, 30, 46)), BorderBrush = task.State == TaskState.Doing ? Brushes.Gold : new SolidColorBrush(Color.FromArgb(130, 135, 157, 185)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(8), Margin = new Thickness(3, 3, 3, 7), ToolTip = Details(task) };
        card.MouseLeftButtonUp += (_, e) => { if (e.OriginalSource is not Button && FindButton(e.OriginalSource as DependencyObject) is null) Edit(task); };
        AttachDetails(card, new List<TaskItem> { task }); AttachTaskActions(card, task); return card;
    }
    private static Button? FindButton(DependencyObject? node)
    { while (node is not null) { if (node is Button button) return button; node = node is Visual ? VisualTreeHelper.GetParent(node) : null; } return null; }
    private void AttachTaskActions(FrameworkElement element, TaskItem task)
    {
        element.PreviewMouseRightButtonDown += (_, e) => { if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && task.Deleted is null && task.State != TaskState.Completed) { e.Handled = true; ToggleDoing(task); } };
        var menu = new ContextMenu();
        void Item(string label, Action action) { var item = new MenuItem { Header = label }; item.Click += (_, _) => action(); menu.Items.Add(item); }
        Item("查看 / 编辑", () => Edit(task));
        if (task.Deleted is null && task.State != TaskState.Completed) { Item("开始做 / 暂停", () => ToggleDoing(task)); Item("完成", () => Change(task, t => { t.State = TaskState.Completed; t.Completed = DateTime.Now; })); }
        if (task.Deleted is null) Item("删除", () => DeleteTask(task));
        element.ContextMenu = menu;
    }
    private void OpenCluster(List<TaskItem> tasks)
    {
        var window = Ui.Dialog(this, "此区域的任务", 580, 680); var list = new StackPanel { Margin = new Thickness(16) };
        var ids = tasks.Select(t => t.Id).ToHashSet();
        void Refresh() { list.Children.Clear(); foreach (var t in store.Tasks.Where(t => ids.Contains(t.Id) && t.Deleted is null && t.State != TaskState.Completed)) list.Children.Add(TaskCard(t, true)); if (list.Children.Count == 0) list.Children.Add(Ui.Label("此区域的任务已处理完成。")); }
        Refresh(); TasksChanged += Refresh;
        window.Content = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        try { window.ShowDialog(); } finally { TasksChanged -= Refresh; } RenderTasks();
    }
    private void Edit(TaskItem? task)
    {
        if (task?.Deleted is not null) return;
        var current = task is null ? null : store.Tasks.FirstOrDefault(t => t.Id == task.Id);
        if (task is not null && (current is null || current.Deleted is not null)) return;
        new TaskEditor(this, store, current).ShowDialog(); Guard(() => { CheckReminders(); RenderTasks(); });
    }
    private void Change(TaskItem task, Action<TaskItem> action)
    { Guard(() => {
        var current = store.Tasks.FirstOrDefault(t => t.Id == task.Id); if (current is null) return;
        var previous = current.State; action(current);
        if (previous != TaskState.Completed && current.State == TaskState.Completed && current.Subtasks.Any(s => !s.Done)) {
            if (MessageBox.Show(this, "还有未完成的子任务，是否一并完成？", "完成父任务", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            foreach (var sub in current.Subtasks) sub.Done = true;
        }
        store.Save(current); detailsPopup.IsOpen = false; RenderTasks();
    }); }
    private UIElement SubtaskList(TaskItem parent, bool paper = false)
    {
        TextBlock Label(string text) => paper ? Ui.FieldLabel(text) : Ui.Label(text);
        var panel = new StackPanel(); if(parent.Subtasks.Count == 0) return panel;
        panel.Children.Add(Label("子任务 · " + parent.Progress));
        foreach(var sub in parent.Subtasks) {
            var label = new StackPanel(); label.Children.Add(new TextBlock { Text = sub.Title, TextWrapping = TextWrapping.Wrap, Foreground = paper ? Ui.Ink : Brushes.White });
            label.Children.Add(Label($"紧急 {sub.Urgency ?? parent.Urgency} · 重要 {sub.Importance ?? parent.Importance}" + (sub.Start is {} start ? $" · 开始 {start:MM-dd HH:mm}" : " · 沿用父任务开始时间") + (sub.Deadline is {} end ? $" · 截止 {end:MM-dd HH:mm}" : "")));
            if(!string.IsNullOrWhiteSpace(sub.Notes)) label.Children.Add(Label(sub.Notes));
            var check = new CheckBox { Content = label, IsChecked = sub.Done, IsEnabled = parent.Deleted is null, Margin = new Thickness(0,4,0,4) };
            check.Click += (_,e) => { e.Handled = true; Guard(() => {
                var current = store.Tasks.FirstOrDefault(t => t.Id == parent.Id && t.Deleted is null); if(current is null) return;
                var item = current.Subtasks.FirstOrDefault(s => s.Id == sub.Id); if(item is null) return;
                item.Done = check.IsChecked == true;
                if(!item.Done && current.State == TaskState.Completed) { current.State = TaskState.Pending; current.Completed = null; }
                if(current.Subtasks.All(s => s.Done) && current.State != TaskState.Completed && MessageBox.Show(this, "所有子任务已完成，是否同时完成父任务？", "子任务进度", MessageBoxButton.YesNo) == MessageBoxResult.Yes) { current.State = TaskState.Completed; current.Completed = DateTime.Now; current.SnoozedUntil = null; }
                store.Save(current); detailsPopup.IsOpen = false; RenderTasks();
            }); };
            check.MouseLeftButtonUp += (_,e) => e.Handled = true;
            panel.Children.Add(check);
        }
        return panel;
    }
    private void ToggleDoing(TaskItem task) => Change(task, t => t.State = t.State == TaskState.Doing ? TaskState.Pending : TaskState.Doing);
    private void DeleteTask(TaskItem task)
    {
        bool future = false;
        if (task.SeriesId is not null)
        { var answer = MessageBox.Show(this, "删除这一次及以后？\n“否”只删除这一次。", "删除重复任务", MessageBoxButton.YesNoCancel); if (answer == MessageBoxResult.Cancel) return; future = answer == MessageBoxResult.Yes; }
        Guard(() =>
        {
            undoTasks = store.Tasks.Where(t => t.Id == task.Id || (future && t.SeriesId == task.SeriesId && t.PlanDate >= task.PlanDate && t.Deleted is null)).Select(t => t.Copy()).ToList();
            undoSeries = future ? store.Series.FirstOrDefault(s => s.Id == task.SeriesId) : null;
            store.Delete(task, future); undoUntil = DateTime.Now.AddSeconds(10); RenderTasks();
        });
    }
    private void UndoClick(object sender, RoutedEventArgs e) => Guard(() => { if (DateTime.Now >= undoUntil) return; foreach (var task in undoTasks) store.Save(task); if (undoSeries is not null) store.Put("series", undoSeries.Id, undoSeries); undoUntil = DateTime.MinValue; RenderTasks(); });
    private void AddClick(object sender, RoutedEventArgs e) => Edit(null);
    private void ClearFilter(object sender, RoutedEventArgs e) { SearchBox.Clear(); TagsBox.Clear(); }
    private void ViewClick(object sender, RoutedEventArgs e) { preferences.View = preferences.View == "四象限" ? "任务流" : "四象限"; SavePreferences(); RenderTasks(); }
    private void Burst(Point point)
    {
        if (!preferences.Particles || locked || Particles.Children.Count > 80) return;
        for (int i = 0; i < 10; i++)
        {
            double angle = Random.Shared.NextDouble() * Math.PI * 2, radius = Random.Shared.Next(12, 40);
            var star = new Ellipse { Width = 3, Height = 3, Fill = i % 2 == 0 ? Brushes.LightSkyBlue : Brushes.Gold };
            Canvas.SetLeft(star, point.X); Canvas.SetTop(star, point.Y); Particles.Children.Add(star);
            var duration = TimeSpan.FromMilliseconds(450);
            star.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(point.X, point.X + Math.Cos(angle) * radius, duration));
            star.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(point.Y, point.Y + Math.Sin(angle) * radius, duration));
            var fade = new DoubleAnimation(1, 0, duration); fade.Completed += (_, _) => Particles.Children.Remove(star); star.BeginAnimation(OpacityProperty, fade);
        }
    }
}
