using Application = System.Windows.Application;

namespace ClickLightWin;

public partial class App : Application
{
    private AppController? _controller;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        _controller = new AppController();
        _controller.Start();
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _controller?.Dispose();
        base.OnExit(e);
    }
}
