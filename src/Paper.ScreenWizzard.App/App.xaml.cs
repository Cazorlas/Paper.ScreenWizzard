using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Paper.ScreenWizzard.App.Startup;

namespace Paper.ScreenWizzard.App;

/// <summary>
/// The entry of the exe. It reads the two environment variables and the flag, builds the container (<see cref="CompositionRoot"/>) and
/// hands over to <see cref="AppShell"/>; a second copy of the app leaves here with exit code 0 and no window. The app has no main window
/// and lives in the tray (<c>ShutdownMode="OnExplicitShutdown"</c>), so it ends only through "Exit" in the tray menu.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _services;
    private AppShell? _shell;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Nothing dies silently: an exception nobody caught is logged and shown, and the app keeps running when it can.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            args.SetObserved();
            Report(args.Exception, "a background task");
        };

        try
        {
            var options = StartupOptions.From(e.Args, Environment.GetEnvironmentVariable);
            _services = CompositionRoot.Build(options, () => Shutdown());
            _shell = _services.GetRequiredService<AppShell>();
            if (!_shell.Start())
            {
                // A second copy: the first one was told, and this one ends with success and no window (SPEC shell F6).
                Shutdown(0);
            }
        }
        catch (Exception exception)
        {
            // The app could not start at all: the tray icon and the hotkeys are what it is, so say so and end with a failure code.
            Report(exception, "start-up");
            MessageBox.Show(exception.Message, "Paper.ScreenWizzard", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // The tray icon first (a ghost icon stays in the tray otherwise), then the hotkeys and the mutex, through the container.
        _shell?.Dispose();
        _services?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Report(e.Exception, "the window loop");
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            Report(exception, "a background thread");
        }
    }

    private void Report(Exception exception, string where)
    {
        if (_shell is not null)
        {
            _shell.ReportFailure(exception, where);
        }
        else
        {
            // Before the shell exists there is nobody to show it to yet, but the log can still hold it.
            _services?.GetService<UseCases.Common.Ports.ILogPort>()?.Error("Unhandled error in " + where, exception);
        }
    }
}
