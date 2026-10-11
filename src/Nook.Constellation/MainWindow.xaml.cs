using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
namespace Nook.Constellation;
public partial class MainWindow : Window
{
    private readonly Preferences preferences;
    private readonly Forms.NotifyIcon tray;
    private readonly Forms.ToolStripMenuItem lockItem;
    private readonly Forms.ToolStripMenuItem topItem;
    private bool locked;
    private bool exiting;
    private const int ExtendedStyle = -20;
    private const long TransparentStyle = 0x20, LayeredStyle = 0x80000, ToolWindowStyle = 0x80;
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? className, string title);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hwnd, int message, IntPtr wparam, IntPtr lparam);
    public static void ActivateExisting() { var handle = FindWindow(null, "隅星集 · Nook Constellation"); if (handle != IntPtr.Zero) PostMessage(handle, 0x8001, IntPtr.Zero, IntPtr.Zero); }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetStyle(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetStyle(IntPtr hwnd, int index, IntPtr value);
    public MainWindow()
    {
        InitializeComponent();
        preferences = Preferences.Load();
        RichText.DefaultSize = Math.Clamp(preferences.FontSize, 10, 24); FontSize = RichText.DefaultSize;
        Topmost = preferences.AlwaysOnTop;
        InitializeTasks();
        tray = new Forms.NotifyIcon { Text = "隅星集 · Nook Constellation", Icon = new System.Drawing.Icon(Application.GetResourceStream(new Uri("pack://application:,,,/tray.ico")).Stream), Visible = true };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示隅星集", null, (_, _) => Dispatcher.Invoke(ShowWidget));
        lockItem = new Forms.ToolStripMenuItem("锁定（鼠标穿透）", null, (_, _) => Dispatcher.Invoke(() => SetLocked(!locked)));
        topItem = new Forms.ToolStripMenuItem("始终置顶", null, (_, _) => Dispatcher.Invoke(() => SetTopmost(!Topmost))) { Checked = Topmost };
        menu.Items.Add(lockItem); menu.Items.Add(topItem);
        menu.Items.Add("设置", null, (_, _) => Dispatcher.Invoke(OpenSettings));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(RequestExit));
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowWidget);
        SourceInitialized += (_, _) =>
        {
            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook((IntPtr hwnd, int msg, IntPtr wp, IntPtr lp, ref bool handled) => { if (msg == 0x8001) { ShowWidget(); handled = true; } if (msg == 0x8002) { RequestExit(); handled = true; } return IntPtr.Zero; });
            SetLocked(false); PositionWindow();
        };
        SystemEvents.DisplaySettingsChanged += DisplayChanged;
        SystemParameters.StaticPropertyChanged += WorkAreaChanged;
        try { preferences.ApplyAutoStart(); preferences.Save(); }
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { MessageBox.Show("开机启动或设置保存失败：" + e.Message, "隅星集"); }
    }
    private void PositionWindow()
    {
        var area = SystemParameters.WorkArea;
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;
        Width = Math.Min(preferences.Expanded ? screenWidth / 2 : 380, Math.Max(100, area.Width - 24));
        Height = Math.Min(preferences.Expanded ? screenHeight / 2 : 280, Math.Max(100, area.Height - 24));
        Left = area.Right - Width - 12; Top = area.Bottom - Height - 12;
        ExpandButton.Content = preferences.Expanded ? "▣" : "⛶";
    }
    private void DisplayChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(PositionWindow);
    private void WorkAreaChanged(object? sender, PropertyChangedEventArgs e)
    { if (e.PropertyName == nameof(SystemParameters.WorkArea)) Dispatcher.BeginInvoke(PositionWindow); }
    private void ShowWidget() { Show(); SetLocked(false); PositionWindow(); Activate(); }
    private void SetLocked(bool value)
    {
        locked = value;
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
        {
            long style = GetStyle(handle, ExtendedStyle).ToInt64() | LayeredStyle | ToolWindowStyle;
            style = value ? style | TransparentStyle : style & ~TransparentStyle;
            SetStyle(handle, ExtendedStyle, new IntPtr(style));
        }
        lockItem.Checked = value;
        lockItem.Text = value ? "解锁（恢复操作）" : "锁定（鼠标穿透）";
    }
    private void SavePreferences()
    {
        try { preferences.Save(); }
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException)
        { MessageBox.Show("设置保存失败：" + e.Message, "隅星集"); }
    }
    private void SetTopmost(bool value) { Topmost = value; preferences.AlwaysOnTop = value; topItem.Checked = value; SavePreferences(); }
    private void ExpandClick(object sender, RoutedEventArgs e) { preferences.Expanded = !preferences.Expanded; PositionWindow(); SavePreferences(); RenderTasks(); }
    private void HideClick(object sender, RoutedEventArgs e) => Hide();
    private void SettingsClick(object sender, RoutedEventArgs e) => OpenSettings();
    private void RequestExit() { exiting = true; Close(); Application.Current.Shutdown(); }
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!exiting) { e.Cancel = true; Hide(); }
        base.OnClosing(e);
    }
    protected override void OnClosed(EventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= DisplayChanged;
        SystemParameters.StaticPropertyChanged -= WorkAreaChanged;
        StopTasks();
        tray.Visible = false; tray.Icon?.Dispose(); tray.Dispose();
        base.OnClosed(e);
    }
}
