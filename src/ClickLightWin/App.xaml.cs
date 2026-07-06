using System.Threading;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace ClickLightWin;

public partial class App : Application
{
    // A unique, machine-wide name so a second launch can detect the first.
    private const string SingleInstanceMutexName = "ClickLightWin_SingleInstance_9f3c1e6a";

    private Mutex? _singleInstanceMutex;
    private AppController? _controller;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            // Another instance already owns the mutex; this one must not run.
            MessageBox.Show("ClickLight is already running.", "ClickLight",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            Shutdown();
            return;
        }

        base.OnStartup(e);
        _controller = new AppController();
        _controller.Start();
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _controller?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
