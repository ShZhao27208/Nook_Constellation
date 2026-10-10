using System;
using System.Threading;
using System.Windows;
namespace Nook.Constellation;
public partial class App : Application
{
    private Mutex? instance;
    public static bool NotificationTest { get; private set; }
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (Array.IndexOf(e.Args, "--data-dir") is var index && index >= 0 && index + 1 < e.Args.Length)
            Preferences.OverrideDirectory = System.IO.Path.GetFullPath(e.Args[index + 1]);
        NotificationTest = Preferences.OverrideDirectory is not null && Array.IndexOf(e.Args, "--notification-test") >= 0;
        if (Array.IndexOf(e.Args, "--self-test") >= 0)
        {
            int code = SelfTests.Run(); Shutdown(code); return;
        }
        DispatcherUnhandledException += (_, args) => { System.Windows.MessageBox.Show(args.Exception.Message, "隅星集 · 操作失败"); args.Handled = true; };
        instance = new Mutex(true, @"Local\NookConstellation" + (Preferences.OverrideDirectory is null ? "" : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Preferences.OverrideDirectory))).Substring(0, 16)), out bool created);
        if (!created) { Nook.Constellation.MainWindow.ActivateExisting(); Shutdown(); return; }
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        instance?.Dispose();
        base.OnExit(e);
    }
}
