using System.Windows;

namespace DCSLogViewer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Ensure the app fully terminates when the main window closes
        ShutdownMode = ShutdownMode.OnMainWindowClose;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);

        // Force kill the process to ensure no background threads linger
        // (DispatcherTimers, FileSystemWatchers, etc.)
        Environment.Exit(0);
    }
}
