using System.Windows;
using System.Threading;

namespace Unclock;

public partial class App
{
    static readonly Mutex _single = new(true, "UnclockApp");
    static readonly EventWaitHandle _showEvent = new(false, EventResetMode.AutoReset, "UnclockShowEvent");

    protected override void OnStartup(StartupEventArgs e)
    {
        if (!_single.WaitOne(0, false))
        {
            try { _showEvent.Set(); } catch { }
            Current.Shutdown();
            return;
        }

        Task.Run(() =>
        {
            while (_showEvent.WaitOne())
            {
                Dispatcher.Invoke(() =>
                {
                    if (MainWindow == null) return;
                    MainWindow.Show();
                    if (MainWindow.WindowState == WindowState.Minimized)
                        MainWindow.WindowState = WindowState.Normal;
                    MainWindow.Activate();
                });
            }
        });

        MainWindow = new MainWindow();
        MainWindow.Show();
    }
}
