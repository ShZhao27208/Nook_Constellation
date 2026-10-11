using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
namespace Nook.Constellation;
public sealed class TaskStore : IDisposable
{
    private readonly SqliteConnection connection;
    public TaskStore(string directory)
    {
        Directory.CreateDirectory(directory);
        connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path.Combine(directory, "tasks.db") }.ToString());
        connection.Open();
        Execute("PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000; CREATE TABLE IF NOT EXISTS documents (kind TEXT NOT NULL, id TEXT NOT NULL, body TEXT NOT NULL, PRIMARY KEY(kind,id)); PRAGMA user_version=1;");
    }
    private void Execute(string sql) { using var command = connection.CreateCommand(); command.CommandText = sql; command.ExecuteNonQuery(); }
    public List<T> Read<T>(string kind)
    {
        using var c = connection.CreateCommand(); c.CommandText = "SELECT body FROM documents WHERE kind=$kind"; c.Parameters.AddWithValue("$kind", kind);
        using var reader = c.ExecuteReader(); var result = new List<T>();
        while (reader.Read()) result.Add(JsonSerializer.Deserialize<T>(reader.GetString(0))!);
        return result;
    }
    public void Put<T>(string kind, string id, T value, SqliteTransaction? transaction = null)
    {
        using var c = connection.CreateCommand(); c.Transaction = transaction;
        c.CommandText = "INSERT INTO documents(kind,id,body) VALUES($k,$i,$b) ON CONFLICT(kind,id) DO UPDATE SET body=excluded.body";
        c.Parameters.AddWithValue("$k", kind); c.Parameters.AddWithValue("$i", id); c.Parameters.AddWithValue("$b", JsonSerializer.Serialize(value)); c.ExecuteNonQuery();
    }
    public void Remove(string kind, string id, SqliteTransaction? transaction = null)
    {
        using var c = connection.CreateCommand(); c.Transaction = transaction; c.CommandText = "DELETE FROM documents WHERE kind=$k AND id=$i";
        c.Parameters.AddWithValue("$k", kind); c.Parameters.AddWithValue("$i", id); c.ExecuteNonQuery();
    }
    public List<TaskItem> Tasks => Read<TaskItem>("task");
    public List<RepeatSeries> Series => Read<RepeatSeries>("series");
    public void Save(TaskItem task) { task.Validate(); task.Updated = DateTime.Now; Put("task", task.Id, task); }
    public void Materialize(DateTime today)
    {
        using var tx = connection.BeginTransaction();
        foreach (var series in Series)
        {
            var limit = today.Date.AddDays(60);
            int count = 0;
            while (series.NextDate.Date <= limit && (series.EndBefore is null || series.NextDate.Date < series.EndBefore.Value.Date))
            {
                if (++count > 50000) throw new InvalidOperationException("重复任务跨度过长，请检查导入日期。");
                if (series.Matches(series.NextDate))
                {
                    var task = series.Occurrence(series.NextDate);
                    // NextDate is advanced atomically with task inserts; restart never duplicates an occurrence.
                    Put("task", task.Id, task, tx);
                }
                series.NextDate = series.NextDate.Date.AddDays(1);
            }
            Put("series", series.Id, series, tx);
        }
        foreach (var task in Tasks.Where(t => t.Deleted < today.AddDays(-30))) Remove("task", task.Id, tx);
        tx.Commit();
    }
    public void SaveWithRepeat(TaskItem task, RepeatSeries? repeat, bool future)
    {
        task.Validate(); repeat?.Validate();
        var previous = Tasks.FirstOrDefault(t => t.Id == task.Id);
        using var tx = connection.BeginTransaction();
        if (future && previous?.SeriesId is { } oldId)
        {
            var old = Series.First(s => s.Id == oldId); old.EndBefore = previous.PlanDate.Date;
            Put("series", old.Id, old, tx);
            foreach (var item in Tasks.Where(t => t.SeriesId == oldId && t.PlanDate.Date >= previous.PlanDate.Date && t.Id != task.Id && t.State == TaskState.Pending && t.Deleted is null)) Remove("task", item.Id, tx);
            task.SeriesId = null;
        }
        if (repeat is not null && (previous?.SeriesId is null || future))
        {
            repeat.Template = task.Copy(); repeat.NextDate = task.PlanDate.Date.AddDays(1);
            task.SeriesId = repeat.Id; Put("series", repeat.Id, repeat, tx);
        }
        task.Updated = DateTime.Now;
        Put("task", task.Id, task, tx); Remove("draft", task.Id, tx); if (previous is null) Remove("draft", "new", tx);
        tx.Commit(); Materialize(DateTime.Today);
    }
    public void Delete(TaskItem task, bool future)
    {
        using var tx = connection.BeginTransaction();
        if (future && task.SeriesId is { } id)
        {
            var series = Series.First(s => s.Id == id); series.EndBefore = task.PlanDate.Date; Put("series", id, series, tx);
            foreach (var t in Tasks.Where(t => t.SeriesId == id && t.PlanDate >= task.PlanDate && t.Deleted is null)) { t.Deleted = DateTime.Now; t.Updated = DateTime.Now; Put("task", t.Id, t, tx); }
        }
        else { task.Deleted = DateTime.Now; task.Updated = DateTime.Now; Put("task", task.Id, task, tx); }
        tx.Commit();
    }
    public Snapshot Export(Preferences settings) => new() { Tasks = Tasks, Series = Series, Reminders = Read<ReminderRecord>("reminder"), Settings = settings, Notifications = Read<NotificationBatch>("notification"), Drafts = Read<KeyValuePair<string, string>>("draft").ToDictionary(x => x.Key, x => x.Value) };
    public static Snapshot Parse(string path)
    {
        if (new FileInfo(path).Length > 50 * 1024 * 1024) throw new InvalidOperationException("导入文件不能超过 50 MB。");
        var data = JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(path)) ?? throw new InvalidOperationException("文件为空。");
        if (data.Schema != 1 || data.Tasks is null || data.Series is null || data.Reminders is null || data.Drafts is null || data.Notifications is null) throw new InvalidOperationException("不支持的备份格式。");
        if (data.Tasks.Count > 100000 || data.Tasks.Select(t => t.Id).Distinct().Count() != data.Tasks.Count || data.Series.Select(t => t.Id).Distinct().Count() != data.Series.Count) throw new InvalidOperationException("备份含重复标识或记录过多。");
        foreach (var t in data.Tasks) t.Validate();
        foreach (var s in data.Series) s.Validate();
        if (data.Tasks.Any(t => t.SeriesId is not null && !data.Series.Any(s => s.Id == t.SeriesId))) throw new InvalidOperationException("备份缺少重复任务规则。");
        return data;
    }
    public void Import(Snapshot data, bool replace)
    {
        using var tx = connection.BeginTransaction();
        var current = Tasks.ToDictionary(t => t.Id);
        if (replace) { using var c = connection.CreateCommand(); c.Transaction = tx; c.CommandText = "DELETE FROM documents"; c.ExecuteNonQuery(); }
        foreach (var t in data.Tasks)
            if (replace || !current.TryGetValue(t.Id, out var old) || t.Updated > old.Updated) Put("task", t.Id, t, tx);
        var series = Series.ToDictionary(s => s.Id);
        foreach (var s in data.Series)
        {
            // Keep the furthest materialization cursor on merge, otherwise generated tasks may overwrite completion state.
            if (!replace && series.TryGetValue(s.Id, out var old)) { s.NextDate = s.NextDate > old.NextDate ? s.NextDate : old.NextDate; s.EndBefore = old.EndBefore ?? s.EndBefore; }
            Put("series", s.Id, s, tx);
        }
        foreach (var reminder in data.Reminders) Put("reminder", reminder.Key, reminder, tx);
        foreach (var batch in data.Notifications) Put("notification", batch.Id, batch, tx);
        foreach (var draft in data.Drafts) Put("draft", draft.Key, draft, tx);
        tx.Commit();
    }
    public void SaveDraft(string id, string json) => Put("draft", id, new KeyValuePair<string,string>(id, json));
    public string? Draft(string id) => Read<KeyValuePair<string,string>>("draft").FirstOrDefault(d => d.Key == id).Value;
    public static void WriteJson(string path, Snapshot data)
    {
        var temp = path + ".tmp"; File.WriteAllText(temp, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true })); File.Move(temp, path, true);
    }
    public static void WriteMarkdown(string path, Snapshot snapshot)
    {
        var b = new StringBuilder("# 隅星集 · 任务导出\n\n");
        foreach (var t in snapshot.Tasks.OrderBy(t => t.PlanDate))
        {
            b.AppendLine($"## {(t.State == TaskState.Completed ? "[x]" : "[ ]")} {t.Title.Replace("\n", " ")}");
            foreach(var sub in t.Subtasks) b.AppendLine($"- [{(sub.Done ? "x" : " ")}] {sub.Title} · 紧急 {sub.Urgency ?? t.Urgency} / 重要 {sub.Importance ?? t.Importance} · 开始 {sub.Start ?? t.Start:yyyy-MM-dd HH:mm} · 截止 {sub.Deadline ?? t.Deadline:yyyy-MM-dd HH:mm}\n  {sub.Notes}");
            b.AppendLine($"\n- 分类：{t.Quadrant}；紧急性 {t.Urgency}，重要性 {t.Importance}\n- 状态：{t.State}{(t.Deleted is not null ? "（回收站）" : "")}\n- 计划：{t.PlanDate:yyyy-MM-dd} {t.PlanTime}\n- 截止：{t.Deadline:yyyy-MM-dd HH:mm}\n- 标签：{string.Join("、", t.Tags)}\n\n{t.Notes}\n");
        }
        File.WriteAllText(path, b.ToString());
    }
    public void Dispose() => connection.Dispose();
}
