using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
namespace Nook.Constellation;
public enum TaskState { Pending, Doing, Completed }
public sealed class TaskItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string TitleRtf { get; set; } = "";
    public string Notes { get; set; } = "";
    public string NotesRtf { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public int Urgency { get; set; } = 50;
    public int Importance { get; set; } = 50;
    public DateTime PlanDate { get; set; } = DateTime.Today;
    public TimeSpan? PlanTime { get; set; }
    public DateTime? Deadline { get; set; }
    public TaskState State { get; set; }
    public DateTime Created { get; set; } = DateTime.Now;
    public DateTime Updated { get; set; } = DateTime.Now;
    public DateTime? Completed { get; set; }
    public DateTime? Deleted { get; set; }
    public string? SeriesId { get; set; }
    public DateTime? SnoozedUntil { get; set; }
    public double X => (Urgency - 50) / 10.0;
    public double Y => (Importance - 50) / 10.0;
    public string Quadrant => Importance >= 50 ? (Urgency > 50 ? "重要且紧急" : "重要不紧急") : (Urgency > 50 ? "不重要但紧急" : "不重要也不紧急");
    public DateTime? Start => PlanTime is { } time ? PlanDate.Date + time : null;
    public bool IsOverdue(DateTime now) => Deadline < now || PlanDate.Date < now.Date || (State == TaskState.Pending && Start < now);
    public string TimeDescription(DateTime now)
    {
        var text = "计划 " + PlanDate.ToString("MM-dd") + (PlanTime is { } t ? " " + t.ToString(@"hh\:mm") : " · 当天");
        if (PlanDate.Date < now.Date) text += " · 计划已过期";
        else if (State == TaskState.Pending && Start < now) text += " · 开始时间已过";
        if (Deadline is { } d) text += "\n截止 " + d.ToString("MM-dd HH:mm") + (d < now ? " · 截止已过期" : "");
        return text;
    }
    public int Group(DateTime now) => State == TaskState.Doing ? 0 : IsOverdue(now) ? 1 : PlanDate.Date <= now.Date ? 2 : 3;
    public TaskItem Copy() => JsonSerializer.Deserialize<TaskItem>(JsonSerializer.Serialize(this))!;
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Title)) throw new InvalidOperationException("请填写任务标题。");
        if (Title.Length > 4000 || Notes.Length > 100000 || TitleRtf.Length > 200000 || NotesRtf.Length > 2000000) throw new InvalidOperationException("任务文字过长。");
        if (Urgency is < 0 or > 100 || Importance is < 0 or > 100) throw new InvalidOperationException("分数必须在 0–100 之间。");
        if (PlanTime is { } time && (time < TimeSpan.Zero || time >= TimeSpan.FromDays(1))) throw new InvalidOperationException("开始时刻无效。");
        if (Deadline < (Start ?? PlanDate.Date)) throw new InvalidOperationException("截止时间不能早于计划开始时间。");
        if (string.IsNullOrWhiteSpace(Id) || !Enum.IsDefined(State) || Tags is null || Tags.Any(x => x is null || x.Length > 100)) throw new InvalidOperationException("任务格式无效。");
    }
}
public sealed class RepeatSeries
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Frequency { get; set; } = "每天";
    public int Weekdays { get; set; } = 127;
    public int MonthDay { get; set; } = 1;
    public DateTime NextDate { get; set; }
    public DateTime? EndBefore { get; set; }
    public TaskItem Template { get; set; } = new();
    public bool Matches(DateTime date) => Frequency switch
    {
        "每天" => true,
        "每周" => (Weekdays & (1 << (int)date.DayOfWeek)) != 0,
        "每月" => date.Day == Math.Min(MonthDay, DateTime.DaysInMonth(date.Year, date.Month)),
        _ => false
    };
    public TaskItem Occurrence(DateTime date)
    {
        var task = Template.Copy();
        task.Id = Id + "-" + date.ToString("yyyyMMdd"); task.SeriesId = Id;
        if (task.Deadline is { } d) task.Deadline = date.Date + (d - task.PlanDate.Date);
        task.PlanDate = date.Date; task.Created = DateTime.Now; task.Updated = DateTime.Now;
        task.State = TaskState.Pending; task.Deleted = null; task.Completed = null; task.SnoozedUntil = null;
        return task;
    }
    public void Validate()
    {
        Template.Validate();
        if (Frequency is not ("每天" or "每周" or "每月") || Weekdays is < 1 or > 127 || MonthDay is < 1 or > 31 || string.IsNullOrWhiteSpace(Id)) throw new InvalidOperationException("重复规则无效。");
    }
}
public sealed class ReminderRecord
{
    public string Key { get; set; } = "";
    public string TaskId { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime At { get; set; }
}
public sealed class Snapshot
{
    public int Schema { get; set; } = 1;
    public DateTime Exported { get; set; } = DateTime.Now;
    public List<TaskItem> Tasks { get; set; } = new();
    public List<RepeatSeries> Series { get; set; } = new();
    public List<ReminderRecord> Reminders { get; set; } = new();
    public List<NotificationBatch> Notifications { get; set; } = new();
    public Preferences? Settings { get; set; }
    public Dictionary<string, string> Drafts { get; set; } = new();
}
