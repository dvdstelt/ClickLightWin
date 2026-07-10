using System.IO;
using System.Threading;
using Velopack;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace ClickLightWin;

public partial class App : Application
{
    // A unique, machine-wide name so a second launch can detect the first.
    private const string SingleInstanceMutexName = "ClickLightWin_SingleInstance_9f3c1e6a";

    private static readonly string ErrorLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ClickLightWin", "error.log");

    private Mutex? _singleInstanceMutex;
    private AppController? _controller;
    private bool _errorNotified;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        // Must run before any app logic: handles Velopack install/update hooks
        // (which run the exe with special args and exit) for the installer build.
        VelopackApp.Build().Run();

        // A headless tray app has no window whose disappearance would signal a
        // crash; log unhandled exceptions and keep running rather than silently
        // vanishing from the tray.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            LogError(args.ExceptionObject as Exception ?? new Exception(args.ExceptionObject?.ToString()));

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

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        LogError(e.Exception);
        e.Handled = true; // keep the tray app alive; the failure is logged

        if (_errorNotified) return;
        _errorNotified = true;
        MessageBox.Show(
            $"ClickLight hit an unexpected error and will keep running.\n\nDetails were written to:\n{ErrorLogPath}",
            "ClickLight", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
    }

    private static void LogError(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ErrorLogPath)!);
            File.AppendAllText(ErrorLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
        }
        catch
        {
            // Logging must never introduce a second failure.
        }
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _controller?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
