using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.Presentation.Shell.ViewModels;
using Paper.ScreenWizzard.Presentation.Shell.Views;
using Paper.ScreenWizzard.UiTests.Support;
using Paper.ScreenWizzard.UseCases.Shell.Models;

namespace Paper.ScreenWizzard.UiTests.Shell;

/// <summary>
/// The Settings window built on mock data: a view model fed the defaults of SPEC shell and a fake use case, the real language
/// and theme services, the real Yes/No prompt, and a recording notification port. Nothing of the host is involved.
/// </summary>
public sealed class SettingsRig : IDisposable
{
    private SettingsRig(WpfHost host)
    {
        Host = host;
        Shell = new FakeShellInteractor();
        Picker = new FakeFolderPicker();
        Notifications = new RecordingNotifications();
    }

    public WpfHost Host { get; }

    public void Dispose() => Session.Dispose();

    public FakeShellInteractor Shell { get; }

    public FakeFolderPicker Picker { get; }

    public RecordingNotifications Notifications { get; }

    public SettingsViewModel ViewModel { get; private set; } = null!;

    public WindowSession Session { get; private set; } = null!;

    /// <summary>Builds the window and shows it. Configure <see cref="Shell"/> first through <paramref name="configure"/>.</summary>
    public static SettingsRig Open(
        ResolvedLanguage language = ResolvedLanguage.Vietnamese,
        AppTheme theme = AppTheme.Light,
        Action<SettingsRig>? configure = null)
    {
        var host = WpfHost.Instance;
        var rig = new SettingsRig(host);
        configure?.Invoke(rig);
        rig.Session = host.Show(
            () =>
            {
                rig.ViewModel = new SettingsViewModel(
                    rig.Shell,
                    ShellTestData.DefaultSettings(),
                    rig.Picker,
                    new SettingsPrompts(host.Language),
                    host.Appearance,
                    rig.Notifications,
                    host.Language,
                    "vi-VN");
                return new SettingsWindow(rig.ViewModel);
            },
            language,
            theme);
        return rig;
    }
}
