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
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetStyle(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetStyle(IntPtr hwnd, int index, IntPtr value);
    public MainWindow()
    {
        InitializeComponent();
        preferences = Preferences.Load();
        Topmost = preferences.AlwaysOnTop;
        tray = new Forms.NotifyIcon { Text = "隅星集 · Nook Constellation", Icon = System.Drawing.SystemIcons.Application, Visible = true };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示隅星集", null, (_, _) => Dispatcher.Invoke(ShowWidget));
        lockItem = new Forms.ToolStripMenuItem("锁定（鼠标穿透）", null, (_, _) => Dispatcher.Invoke(() => SetLocked(!locked)));
        topItem = new Forms.ToolStripMenuItem("始终置顶", null, (_, _) => Dispatcher.Invoke(() => SetTopmost(!Topmost))) { Checked = Topmost };
        menu.Items.Add(lockItem); menu.Items.Add(topItem);
        menu.Items.Add("设置", null, (_, _) => Dispatcher.Invoke(OpenSettings));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(() => { exiting = true; Close(); Application.Current.Shutdown(); }));
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowWidget);
        SourceInitialized += (_, _) => { SetLocked(false); PositionWindow(); };
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
    private void ExpandClick(object sender, RoutedEventArgs e) { preferences.Expanded = !preferences.Expanded; PositionWindow(); SavePreferences(); }
    private void HideClick(object sender, RoutedEventArgs e) => Hide();
    private void SettingsClick(object sender, RoutedEventArgs e) => OpenSettings();
    private void OpenSettings()
    {
        ShowWidget();
        var panel = new StackPanel { Margin = new Thickness(22) };
        var top = new CheckBox { Content = "始终置顶", IsChecked = preferences.AlwaysOnTop, Margin = new Thickness(0, 8, 0, 8) };
        var startup = new CheckBox { Content = "开机启动", IsChecked = preferences.AutoStart, Margin = new Thickness(0, 8, 0, 8) };
        top.Click += (_, _) => SetTopmost(top.IsChecked == true);
        startup.Click += (_, _) =>
        {
            bool old = preferences.AutoStart;
            preferences.AutoStart = startup.IsChecked == true;
            try { preferences.ApplyAutoStart(); SavePreferences(); }
            catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException)
            { preferences.AutoStart = old; startup.IsChecked = old; MessageBox.Show("无法修改开机启动：" + e.Message, "隅星集"); }
        };
        panel.Children.Add(top); panel.Children.Add(startup);
        panel.Children.Add(new TextBlock { Text = "锁定与解锁请使用系统托盘菜单。", Margin = new Thickness(0, 16, 0, 0) });
        new Window { Title = "隅星集 · 设置", Owner = this, Content = panel, Width = 340, Height = 210, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false }.ShowDialog();
    }
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!exiting) { e.Cancel = true; Hide(); }
        base.OnClosing(e);
    }
    protected override void OnClosed(EventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= DisplayChanged;
        SystemParameters.StaticPropertyChanged -= WorkAreaChanged;
        tray.Visible = false; tray.Dispose();
        base.OnClosed(e);
    }
}
