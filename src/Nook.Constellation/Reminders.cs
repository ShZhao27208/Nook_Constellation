using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Toolkit.Uwp.Notifications;
namespace Nook.Constellation;
public sealed class NotificationBatch
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public List<string> TaskIds { get; set; } = new();
}
public static class ReminderEngine
{
    public static List<ReminderRecord> Due(IEnumerable<TaskItem> tasks, IEnumerable<ReminderRecord> sent, Preferences settings, DateTime now)
    {
        var known = sent.Select(r => r.Key).ToHashSet(); var due = new List<ReminderRecord>();
        void Add(TaskItem task, string kind, DateTime at, string text)
        {
            var key = task.Id + ":" + kind + ":" + at.Ticks;
            if (at <= now && !known.Contains(key)) due.Add(new ReminderRecord { Key = key, TaskId = task.Id, At = now, Message = text + " · " + task.Title });
        }
        foreach (var task in tasks.Where(t => t.Deleted is null && t.State != TaskState.Completed))
        {
            if (task.SnoozedUntil is { } snooze) Add(task, "snooze", snooze, "稍后提醒");
            if (task.State != TaskState.Doing)
            {
                if (task.Start is { } start) Add(task, "start", start, "该开始任务了");
                else Add(task, "day", task.PlanDate.Date.AddHours(settings.SummaryHour).AddMinutes(settings.SummaryMinute), "今天要做的任务");
            }
            if (task.Deadline is { } deadline) Add(task, "deadline", deadline.AddMinutes(-settings.DeadlineMinutes), deadline < now ? "任务已超过截止时间" : "任务即将截止");
        }
        return due;
    }
}
public partial class MainWindow
{
    private bool notificationsReady;
    private void InitializeNotifications()
    {
        if (Preferences.OverrideDirectory is not null && !App.NotificationTest) return;
        try
        {
            ToastNotificationManagerCompat.OnActivated += args => Dispatcher.BeginInvoke(new Action(() => Guard(() =>
            {
                var arguments = ToastArguments.Parse(args.Argument);
                string action = arguments.Contains("action") ? arguments["action"] : "open";
                string batchId = arguments.Contains("batch") ? arguments["batch"] : "";
                var idList = store.Read<NotificationBatch>("notification").FirstOrDefault(b => b.Id == batchId)?.TaskIds.ToArray() ?? Array.Empty<string>();
                if (action == "snooze")
                {
                    foreach (var task in store.Tasks.Where(t => idList.Contains(t.Id) && t.Deleted is null && t.State != TaskState.Completed)) { task.SnoozedUntil = DateTime.Now.AddMinutes(10); store.Save(task); }
                }
                else
                {
                    ShowWidget();
                    if (idList.Length == 1 && store.Tasks.FirstOrDefault(t => t.Id == idList[0] && t.Deleted is null) is { } task) Edit(task);
                    else OpenReminderHistory();
                }
            })));
            notificationsReady = true;
        }
        catch (Exception e) { StatusText.ToolTip = "系统通知不可用：" + e.Message; }
    }
    private void CheckReminders()
    {
        if (Preferences.OverrideDirectory is not null && !App.NotificationTest) return;
        var due = ReminderEngine.Due(store.Tasks, store.Read<ReminderRecord>("reminder"), preferences, DateTime.Now);
        if (due.Count == 0) return;
        string title = due.Count == 1 ? "隅星集 · 任务提醒" : $"隅星集 · {due.Count} 条待办提醒";
        string body = string.Join("\n", due.Take(3).Select(r => r.Message));
        // Persist full membership so snoozing a large summary never silently drops tasks.
        var batch = new NotificationBatch { TaskIds = due.Select(r => r.TaskId).Distinct().ToList() };
        store.Put("notification", batch.Id, batch);
        if (notificationsReady)
        {
            new ToastContentBuilder().AddArgument("action", "open").AddArgument("batch", batch.Id).AddText(title).AddText(body)
                .AddButton(new ToastButton().SetContent("打开任务").AddArgument("action", "open").AddArgument("batch", batch.Id))
                .AddButton(new ToastButton().SetContent("稍后提醒 10 分钟").AddArgument("action", "snooze").AddArgument("batch", batch.Id))
                .Show(toast => { toast.Tag = "nook-reminders"; toast.Group = "tasks"; });
        }
        else
        {
            tray.ShowBalloonTip(10000, title, body, System.Windows.Forms.ToolTipIcon.Info);
        }
        foreach (var reminder in due) store.Put("reminder", reminder.Key, reminder);
    }
    private void OpenReminderHistory()
    {
        var window = Ui.Dialog(this, "提醒记录", 580, 620); var list = new StackPanel { Margin = new Thickness(18) };
        var records = store.Read<ReminderRecord>("reminder").OrderByDescending(r => r.At).Take(200).ToList();
        if (records.Count == 0) list.Children.Add(Ui.Label("暂时没有提醒记录。"));
        foreach (var record in records)
        {
            list.Children.Add(Ui.Label(record.At.ToString("yyyy-MM-dd HH:mm") + "\n" + record.Message));
            if (store.Tasks.FirstOrDefault(t => t.Id == record.TaskId && t.Deleted is null) is { } task)
            {
                var buttons = new WrapPanel(); buttons.Children.Add(Ui.Button("打开任务", () => Edit(task)));
                if (task.State != TaskState.Completed) buttons.Children.Add(Ui.Button("10 分钟后提醒", () => Change(task, t => t.SnoozedUntil = DateTime.Now.AddMinutes(10))));
                list.Children.Add(buttons);
            }
        }
        window.Content = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; window.ShowDialog();
    }
}
