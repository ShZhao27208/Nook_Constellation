using System;
using System.Threading;
using System.Windows;
namespace Nook.Constellation;
public partial class App : Application
{
    private Mutex? instance;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        instance = new Mutex(true, @"Local\NookConstellation", out bool created);
        if (!created) { Shutdown(); return; }
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
