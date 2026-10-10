using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
namespace Nook.Constellation;
public static class SelfTests
{
    public static int Run()
    {
        string directory = Preferences.OverrideDirectory ?? Path.Combine(Path.GetTempPath(), "NookTests-" + Guid.NewGuid().ToString("N"));
        Preferences.OverrideDirectory = directory;
        Directory.CreateDirectory(directory); var checks = new List<string>();
        void Check(bool condition, string label) { if (!condition) throw new Exception(label); checks.Add(label); }
        try
        {
            var now = new DateTime(2026, 10, 11, 12, 0, 0);
            var task = new TaskItem { Title = "坐标测试", Urgency = 60, Importance = 60, PlanDate = now.Date };
            Check(task.X == 1 && task.Y == 1 && task.Quadrant == "重要且紧急", "60/60 映射 (1,1)");
            task.Urgency = 40; Check(task.X == -1 && task.Y == 1 && task.Quadrant == "重要不紧急", "40/60 映射 (-1,1)");
            task.Urgency = task.Importance = 50; Check(task.X == 0 && task.Y == 0 && task.Quadrant == "重要不紧急", "默认原点与分类边界");
            task.Urgency = 101; bool failed = false; try { task.Validate(); } catch (InvalidOperationException) { failed = true; } Check(failed, "拒绝越界评分"); task.Urgency = 50;
            task.PlanTime = TimeSpan.FromHours(13); task.Deadline = now; failed = false; try { task.Validate(); } catch (InvalidOperationException) { failed = true; } Check(failed, "拒绝截止早于开始"); task.PlanTime = null; task.Deadline = null;
            var settings = new Preferences();
            var due = ReminderEngine.Due(new[] { task }, Array.Empty<ReminderRecord>(), settings, now); Check(due.Count == 1, "9点后无时刻任务立即提醒");
            Check(ReminderEngine.Due(new[] { task }, due, settings, now.AddMinutes(1)).Count == 0, "提醒持久化去重");
            task.State = TaskState.Completed; Check(ReminderEngine.Due(new[] { task }, Array.Empty<ReminderRecord>(), settings, now).Count == 0, "完成任务不提醒"); task.State = TaskState.Pending;
            task.Deleted = now; Check(ReminderEngine.Due(new[] { task }, Array.Empty<ReminderRecord>(), settings, now).Count == 0, "删除任务不提醒"); task.Deleted = null;
            task.PlanTime = TimeSpan.FromHours(11); task.Deadline = now.AddMinutes(10);
            Check(ReminderEngine.Due(new[] { task }, Array.Empty<ReminderRecord>(), settings, now).Count == 2, "开始和截止前提醒独立补偿");
            task.State = TaskState.Doing; Check(ReminderEngine.Due(new[] { task }, Array.Empty<ReminderRecord>(), settings, now).Count == 1, "正在做保留截止提醒");
            task.PlanTime = null; task.Deadline = null; task.State = TaskState.Pending;
            task.SnoozedUntil = now.AddMinutes(10); Check(ReminderEngine.Due(new[] { task }, due, settings, now.AddMinutes(9)).Count == 0, "稍后提醒不会提前触发");
            Check(ReminderEngine.Due(new[] { task }, due, settings, now.AddMinutes(10)).Count == 1, "稍后提醒按时触发"); task.SnoozedUntil = null;
            var monthly = new RepeatSeries { Frequency = "每月", MonthDay = 31, Template = task.Copy() };
            Check(monthly.Matches(new DateTime(2024, 2, 29)) && monthly.Matches(new DateTime(2025, 2, 28)) && !monthly.Matches(new DateTime(2025, 2, 27)), "每月31日处理闰年和月末");
            var weekly = new RepeatSeries { Frequency = "每周", Weekdays = 1 << (int)DayOfWeek.Monday };
            Check(weekly.Matches(new DateTime(2026, 10, 12)) && !weekly.Matches(now), "每周指定星期匹配");
            var rich = new RichTextBox(); RichText.Load(rich, "", "富文本 round trip"); rich.SelectAll(); rich.Selection.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);
            var rtf = RichText.Save(rich); var restored = new RichTextBox(); RichText.Load(restored, rtf, "");
            Check(RichText.Plain(restored) == "富文本 round trip" && new TextRange(restored.Document.ContentStart, restored.Document.ContentEnd).GetPropertyValue(TextElement.FontWeightProperty).Equals(FontWeights.Bold), "富文本加粗往返恢复");
            using (var store = new TaskStore(directory))
            {
                store.Save(task); task.Title = "外部对象变化"; Check(store.Tasks.Single().Title == "坐标测试", "保存后对象修改不会暗改数据库");
                store.SaveDraft("new", "draft"); Check(store.Draft("new") == "draft", "草稿持久保存");
                var seed = new TaskItem { Title = "每天重复", PlanDate = DateTime.Today.AddDays(-2) };
                store.SaveWithRepeat(seed, new RepeatSeries { Frequency = "每天", Template = seed.Copy() }, false);
                int count = store.Tasks.Count; store.Materialize(DateTime.Today); Check(count == store.Tasks.Count, "重复实例生成幂等");
                var occurrence = store.Tasks.First(t => t.SeriesId is not null && t.PlanDate == DateTime.Today); occurrence.State = TaskState.Completed; store.Save(occurrence); store.Materialize(DateTime.Today);
                Check(store.Tasks.Single(t => t.Id == occurrence.Id).State == TaskState.Completed, "重复生成保留已完成状态");
                store.SaveDraft("new", "draft");
                var snapshot = store.Export(settings); string path = Path.Combine(directory, "roundtrip.json"); TaskStore.WriteJson(path, snapshot); var parsed = TaskStore.Parse(path);
                Check(parsed.Tasks.Count == count && parsed.Drafts["new"] == "draft", "JSON备份含任务与草稿");
                store.Import(parsed, false); Check(store.Tasks.Count == count, "导入按ID去重");
                var oldCursor = parsed.Series[0]; oldCursor.NextDate = DateTime.Today.AddDays(-5); store.Import(parsed, false); store.Materialize(DateTime.Today);
                Check(store.Tasks.Single(t => t.Id == occurrence.Id).State == TaskState.Completed, "合并旧备份不重置重复任务完成状态");
                var target = store.Tasks.First(t => t.SeriesId is not null && t.PlanDate > DateTime.Today); store.Delete(target, true);
                Check(store.Tasks.Where(t => t.SeriesId == target.SeriesId && t.PlanDate >= target.PlanDate).All(t => t.Deleted is not null), "批量删除本次及以后");
                store.Import(snapshot, true); Check(store.Tasks.All(t => t.Deleted is null), "完整备份恢复删除任务");
                var edit = store.Tasks.First(t => t.SeriesId is not null && t.PlanDate > DateTime.Today); edit.Title = "修改未来";
                store.SaveWithRepeat(edit, new RepeatSeries { Frequency = "每周", Weekdays = 127, Template = edit.Copy() }, true);
                Check(store.Tasks.Any(t => t.Id == occurrence.Id && t.State == TaskState.Completed), "修改未来保留过去历史");
                TaskStore.WriteMarkdown(Path.Combine(directory, "export.md"), store.Export(settings)); Check(File.ReadAllText(Path.Combine(directory, "export.md")).Contains("隅星集"), "Markdown导出");
                File.WriteAllText(Path.Combine(directory, "bad.json"), "{\"Schema\":99}"); failed = false; try { TaskStore.Parse(Path.Combine(directory, "bad.json")); } catch (InvalidOperationException) { failed = true; } Check(failed, "拒绝未知备份版本");
            }
            using (var reopened = new TaskStore(directory)) Check(reopened.Tasks.Count > 0, "数据库关闭重开后数据保持");
            var window = new MainWindow(); window.Show(); window.UpdateLayout();
            try
            {
                var type = typeof(MainWindow); var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                void Call(string name, params object[] args) => type.GetMethod(name, flags)!.Invoke(window, args);
                var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                long Style() => ((IntPtr)type.GetMethod("GetStyle", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.Invoke(null, new object[] { hwnd, -20 })!).ToInt64();
                Check(window.AllowsTransparency && window.ResizeMode == ResizeMode.NoResize && window.WindowStyle == WindowStyle.None, "原生透明窗口禁用拖动缩放边框");
                Check(Math.Abs(window.Left + window.Width - (SystemParameters.WorkArea.Right - 12)) < 1, "窗口固定在工作区右下角");
                Call("SetLocked", true); Check((Style() & 0x20) != 0, "锁定写入鼠标穿透原生样式");
                Call("SetLocked", false); Check((Style() & 0x20) == 0, "解锁移除鼠标穿透原生样式");
                Call("SetTopmost", true); Check(window.Topmost, "置顶状态生效"); Call("SetTopmost", false);
                Call("ExpandClick", window, new RoutedEventArgs()); window.UpdateLayout();
                Check(Math.Abs(window.Width - SystemParameters.PrimaryScreenWidth / 2) < 1 && Math.Abs(window.Height - SystemParameters.PrimaryScreenHeight / 2) < 1, "放大为屏幕四分之一面积");
                Call("HideClick", window, new RoutedEventArgs()); Check(!window.IsVisible, "关闭入口隐藏窗口");
                Call("ShowWidget"); Check(window.IsVisible, "托盘显示逻辑恢复窗口");
                Check(Preferences.Load().Expanded, "窗口设置持久化");
            }
            finally { typeof(MainWindow).GetField("exiting", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(window, true); window.Close(); }
            File.WriteAllText(Path.Combine(directory, "test-results.json"), JsonSerializer.Serialize(new { passed = true, count = checks.Count, checks }, new JsonSerializerOptions { WriteIndented = true })); return 0;
        }
        catch (Exception e)
        { File.WriteAllText(Path.Combine(directory, "test-results.json"), JsonSerializer.Serialize(new { passed = false, checks, error = e.ToString() }, new JsonSerializerOptions { WriteIndented = true })); return 1; }
    }
}
