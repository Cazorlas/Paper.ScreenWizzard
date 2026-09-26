using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.Infrastructure.Capture;
using Paper.ScreenWizzard.Infrastructure.Shared;
using Paper.ScreenWizzard.Infrastructure.Shell;
using Paper.ScreenWizzard.Presentation.Rendering;
using Paper.ScreenWizzard.Presentation.ViewModels.Capture;
using Paper.ScreenWizzard.Presentation.ViewModels.Editor;
using Paper.ScreenWizzard.Presentation.ViewModels.Shell;
using Paper.ScreenWizzard.Presentation.Views.Capture;
using Paper.ScreenWizzard.Presentation.Views.Capture.Services;
using Paper.ScreenWizzard.Presentation.Views.Editor;
using Paper.ScreenWizzard.Presentation.Views.Shell.Services;
using Paper.ScreenWizzard.UseCases.Capture.Ports;
using Paper.ScreenWizzard.UseCases.Capture.UseCases;
using Paper.ScreenWizzard.UseCases.Editor.Ports;
using Paper.ScreenWizzard.UseCases.Editor.UseCases;
using Paper.ScreenWizzard.UseCases.Shared.Ports;
using Paper.ScreenWizzard.UseCases.Shared.UseCases;
using Paper.ScreenWizzard.UseCases.Shell.Ports;
using Paper.ScreenWizzard.UseCases.Shell.UseCases;

namespace Paper.ScreenWizzard.App.Startup;

/// <summary>
/// The DI container: which adapter answers which port, which interactor uses which ports, and which view services the flows lean on. It is
/// the only place that names an Infrastructure class next to a Presentation one (ADR 0001: the entry host connects every layer and decides
/// nothing). Every OS-visible name - the data folder, the mutex, the Run value, the hotkey window - comes from <see cref="StartupOptions"/>,
/// so a copy started with its own environment variables shares nothing with the real one.
/// </summary>
public static class CompositionRoot
{
    public static ServiceProvider Build(StartupOptions options, Action shutdown)
    {
        var services = new ServiceCollection();

        services.AddSingleton(options);
        services.AddSingleton(shutdown);

        // Adapters: one per port. The hotkey service and the single-instance guard are registered as themselves too, so the container disposes them.
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IDelay, TaskDelay>();
        services.AddSingleton<ILog>(_ => new FileLogger(options.DataRoot));
        services.AddSingleton<IFileStore, FileStore>();
        services.AddSingleton<IImageCodec, ImageCodec>();
        services.AddSingleton<IClipboard, ClipboardService>();
        services.AddSingleton<IScreenSource, ScreenSource>();
        services.AddSingleton<IWindowCatalog, WindowCatalog>();
        services.AddSingleton<IMonitorCatalog, MonitorCatalog>();
        services.AddSingleton<ISettingsStore>(_ => new SettingsStore(options.DataRoot));
        services.AddSingleton(_ => new HotkeyService("Paper.ScreenWizzard.Hotkeys." + options.InstanceName));
        services.AddSingleton<IHotkeys>(sp => sp.GetRequiredService<HotkeyService>());
        services.AddSingleton<IAutostart>(_ => new AutostartService(options.InstanceName));
        services.AddSingleton(_ => new SingleInstance(options.InstanceName));
        services.AddSingleton<ISingleInstance>(sp => sp.GetRequiredService<SingleInstance>());

        // Use cases.
        services.AddSingleton<IImageDelivery, ImageDelivery>();
        services.AddSingleton<IShellInteractor, ShellInteractor>();
        services.AddSingleton<ICaptureInteractor, CaptureInteractor>();
        services.AddSingleton<IEditorInteractor, EditorInteractor>();

        // Presentation services.
        services.AddSingleton<LanguageService>();
        services.AddSingleton<ILocalizer>(sp => sp.GetRequiredService<LanguageService>());
        services.AddSingleton<ThemeService>();
        services.AddSingleton<IAppearanceService, AppearanceService>();
        services.AddSingleton<INotifications, NotificationPresenter>();
        services.AddSingleton<IFolderPickerService, FolderPickerService>();
        services.AddSingleton<ISettingsPrompts, SettingsPrompts>();
        services.AddSingleton<IFileDialogService, FileDialogService>();
        services.AddSingleton<IEditorPrompts, WpfEditorPrompts>();
        services.AddSingleton<IEditorFlattener, WpfImageFlattener>();
        services.AddSingleton<ICaptureViews, WpfCaptureViews>();
        services.AddSingleton<IEditorViews>(sp => new WpfEditorViews(sp.GetRequiredService<IMonitorCatalog>()));

        // The settings the running app uses, read by the flows on every capture. Defaults until the shell has loaded the real ones.
        services.AddSingleton(_ => new SettingsHolder(SettingsDefaults.Create(PicturesFolder())));
        services.AddSingleton<Func<AppSettings>>(sp =>
        {
            var holder = sp.GetRequiredService<SettingsHolder>();
            return () => holder.Current;
        });

        services.AddSingleton<CaptureFlow>();
        services.AddSingleton(sp => new EditorServices(
            sp.GetRequiredService<IEditorInteractor>(),
            sp.GetRequiredService<INotifications>(),
            sp.GetRequiredService<IFileDialogService>(),
            sp.GetRequiredService<IEditorPrompts>(),
            sp.GetRequiredService<IEditorFlattener>(),
            sp.GetRequiredService<ILocalizer>(),
            sp.GetRequiredService<Func<AppSettings>>()));
        services.AddSingleton<EditorFlow>();
        services.AddSingleton<AppShell>();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private static string PicturesFolder()
    {
        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        return string.IsNullOrWhiteSpace(pictures)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Pictures")
            : pictures;
    }
}
