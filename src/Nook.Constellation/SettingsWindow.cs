using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
namespace Nook.Constellation;
public partial class MainWindow
{
    private string BackupDirectory => Path.Combine(Preferences.DataDirectory, "backups");
    private void BackupDaily()
    {
        Directory.CreateDirectory(BackupDirectory); var path = Path.Combine(BackupDirectory, "daily-" + DateTime.Today.ToString("yyyy-MM-dd") + ".json");
        if (!File.Exists(path)) TaskStore.WriteJson(path, store.Export(preferences));
        foreach (var old in new DirectoryInfo(BackupDirectory).GetFiles("daily-*.json").OrderByDescending(f => f.Name).Skip(30)) old.Delete();
    }
    private string BackupNow(string prefix)
    {
        Directory.CreateDirectory(BackupDirectory); var path = Path.Combine(BackupDirectory, prefix + "-" + DateTime.Now.ToString("yyyy-MM-dd-HHmmss-fff") + ".json");
        TaskStore.WriteJson(path, store.Export(preferences)); return path;
    }
    private void OpenSettings()
    {
        ShowWidget(); var window = Ui.Dialog(this, "设置与数据", 500, 700); var panel = new StackPanel { Margin = new Thickness(22) };
        window.Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        void Toggle(string label, bool value, Action<bool> change)
        {
            var box = new CheckBox { Content = label, IsChecked = value, Foreground = Brushes.White, Margin = new Thickness(0, 8, 0, 8) };
            box.Click += (_, _) => { try { change(box.IsChecked == true); SavePreferences(); } catch (Exception e) { box.IsChecked = value; Ui.Error(e); } }; panel.Children.Add(box);
        }
        Toggle("始终置顶", preferences.AlwaysOnTop, SetTopmost);
        Toggle("开机启动", preferences.AutoStart, value => { var old = preferences.AutoStart; preferences.AutoStart = value; try { preferences.ApplyAutoStart(); } catch { preferences.AutoStart = old; throw; } });
        Toggle("点击粒子效果", preferences.Particles, value => preferences.Particles = value);
        panel.Children.Add(Ui.Label("默认视图")); var view = Ui.Choice(new[] { "四象限", "任务流" }, preferences.View); panel.Children.Add(view); view.SelectionChanged += (_, _) => { preferences.View = (string)view.SelectedItem; SavePreferences(); RenderTasks(); };
        panel.Children.Add(Ui.Label("截止前提醒（分钟，0–10080）")); var lead = Ui.Input(preferences.DeadlineMinutes.ToString()); panel.Children.Add(lead);
        panel.Children.Add(Ui.Label("无具体时刻任务的每日汇总（HH:mm）")); var summary = Ui.Input($"{preferences.SummaryHour:00}:{preferences.SummaryMinute:00}"); panel.Children.Add(summary);
        panel.Children.Add(Ui.Button("保存提醒设置", () =>
        {
            if (!int.TryParse(lead.Text, out var minutes) || minutes is < 0 or > 10080 || !TimeSpan.TryParseExact(summary.Text, @"hh\:mm", System.Globalization.CultureInfo.InvariantCulture, out var time) || time >= TimeSpan.FromDays(1)) { MessageBox.Show(window, "请检查分钟数与汇总时刻格式。", "隅星集"); return; }
            preferences.DeadlineMinutes = minutes; preferences.SummaryHour = time.Hours; preferences.SummaryMinute = time.Minutes; SavePreferences(); Guard(CheckReminders);
        }));
        panel.Children.Add(Ui.Button("查看提醒记录", OpenReminderHistory));
        panel.Children.Add(Ui.Label("数据保存在本机，每日自动备份，保留最近 30 份。"));
        var exports = new WrapPanel(); exports.Children.Add(Ui.Button("导出 JSON", () => ExportData(false))); exports.Children.Add(Ui.Button("导出 Markdown", () => ExportData(true))); panel.Children.Add(exports);
        var imports = new WrapPanel(); imports.Children.Add(Ui.Button("导入并合并", () => ImportData(false))); imports.Children.Add(Ui.Button("从备份恢复", () => ImportData(true))); panel.Children.Add(imports);
        panel.Children.Add(Ui.Button("立即备份", () => { try { MessageBox.Show(window, "已保存：\n" + BackupNow("manual"), "隅星集"); } catch (Exception e) { Ui.Error(e); } }));
        panel.Children.Add(Ui.Button("打开数据目录", () => Process.Start(new ProcessStartInfo(Preferences.DataDirectory) { UseShellExecute = true })));
        panel.Children.Add(Ui.Label("锁定后鼠标可穿透整个组件，请通过系统托盘解锁。\n关闭窗口不会退出；退出请使用托盘菜单。\n\n隅星集 · Nook Constellation\n" + typeof(App).Assembly.GetName().Version));
        window.ShowDialog();
    }
    private void ExportData(bool markdown)
    {
        var dialog = new SaveFileDialog { Filter = markdown ? "Markdown|*.md" : "JSON 完整备份|*.json", FileName = "隅星集-" + DateTime.Now.ToString("yyyyMMdd-HHmm") };
        if (dialog.ShowDialog(this) != true) return;
        try { if (markdown) TaskStore.WriteMarkdown(dialog.FileName, store.Export(preferences)); else TaskStore.WriteJson(dialog.FileName, store.Export(preferences)); }
        catch (Exception e) { Ui.Error(e); }
    }
    private void ImportData(bool replace)
    {
        var dialog = new OpenFileDialog { Filter = "JSON 完整备份|*.json", InitialDirectory = replace ? BackupDirectory : "" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var data = TaskStore.Parse(dialog.FileName); var ids = store.Tasks.Select(t => t.Id).ToHashSet();
            int duplicates = data.Tasks.Count(t => ids.Contains(t.Id));
            string message = $"文件包含 {data.Tasks.Count} 个任务、{data.Series.Count} 条重复规则。\n已有同 ID 任务 {duplicates} 个。\n" + (replace ? "恢复将替换当前数据与设置。操作前会自动备份。" : "合并按 ID 去重，同 ID 保留修改时间较新的任务；当前设置保持不变。") + "\n\n是否继续？";
            if (MessageBox.Show(this, message, "导入预览", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            BackupNow("before-import"); store.Import(data, replace);
            if (replace && data.Settings is { } settings)
            {
                preferences.View = settings.View == "任务流" ? "任务流" : "四象限"; preferences.Particles = settings.Particles;
                preferences.DeadlineMinutes = Math.Clamp(settings.DeadlineMinutes, 0, 10080); preferences.SummaryHour = Math.Clamp(settings.SummaryHour, 0, 23); preferences.SummaryMinute = Math.Clamp(settings.SummaryMinute, 0, 59);
                preferences.Expanded = settings.Expanded; preferences.AutoStart = settings.AutoStart; SetTopmost(settings.AlwaysOnTop); preferences.ApplyAutoStart(); SavePreferences(); PositionWindow();
            }
            undoUntil = DateTime.MinValue; store.Materialize(DateTime.Today); RenderTasks(); MessageBox.Show(this, "数据已导入。", "隅星集");
        }
        catch (Exception e) { Ui.Error(e); }
    }
}
