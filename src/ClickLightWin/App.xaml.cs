using Application = System.Windows.Application;

namespace ClickLightWin;

public partial class App : Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        // Headless startup: no main window. AppController wiring (tray, mouse hook,
        // overlays) lands in Milestone 1. Until the tray exists, quit via the debugger
        // or by ending the process.
    }
}
