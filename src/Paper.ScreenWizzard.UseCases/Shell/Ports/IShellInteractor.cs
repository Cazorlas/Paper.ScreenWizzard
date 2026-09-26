using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UseCases.Shell.Models;

namespace Paper.ScreenWizzard.UseCases.Shell.Ports;

/// <summary>Everything the shell decides: startup, hotkey changes, settings, autostart, the tray's second-launch rule.</summary>
public interface IShellInteractor
{
    ShellStartResult Start(ShellStartInput input);

    /// <summary>The settings a new user gets: the defaults of SPEC shell, Inputs.</summary>
    AppSettings CreateDefaultSettings(string picturesFolder);

    HotkeyChangeResult ChangeHotkey(AppSettings current, CaptureKind kind, HotkeyChord proposed);

    /// <summary>Saves settings the user changed; reports a failed save instead of hiding it (SPEC shell F2).</summary>
    SettingsApplyResult Apply(AppSettings settings);

    AutostartChangeResult SetAutostart(AppSettings current, bool enabled);

    FolderCheck CheckSaveFolder(string folder);

    FolderCreateResult CreateSaveFolder(string folder);

    SecondInstanceResult OnSecondInstanceLaunched();

    /// <summary>Where the capture bar opens: the saved place if a monitor still shows it, else the top right of the primary monitor.</summary>
    PixelPoint PlaceCaptureBar(PixelPoint? saved, IReadOnlyList<MonitorInfo> monitors, PixelSize barSize);

    ResolvedLanguage ResolveLanguage(AppLanguage language, string systemCultureName);
}
