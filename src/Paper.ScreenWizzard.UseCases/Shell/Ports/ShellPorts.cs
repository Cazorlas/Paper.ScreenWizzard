using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UseCases.Common.Models;
using Paper.ScreenWizzard.UseCases.Shell.Models;

namespace Paper.ScreenWizzard.UseCases.Shell.Ports;

/// <summary>Reads and writes the one settings document under the user's application data folder.</summary>
public interface ISettingsStorePort
{
    SettingsLoadResult Load();

    /// <summary>Writes the document; fails with the system's reason when the folder is read-only or full (SPEC shell F2).</summary>
    PortResult Save(AppSettings settings);
}

/// <summary>Global hotkeys. A chord another program holds is a failed Register, not an exception.</summary>
public interface IHotkeyPort
{
    /// <summary>
    /// Registers <paramref name="chord"/> for <paramref name="kind"/>, replacing that kind's earlier chord. It is atomic: when
    /// Windows or another program holds the new chord the result is a failure and the earlier chord stays registered
    /// (SPEC shell F4: "phím cũ vẫn hoạt động").
    /// </summary>
    PortResult Register(CaptureKind kind, HotkeyChord chord);

    void Unregister(CaptureKind kind);

    /// <summary>Raised on the UI thread when a registered hotkey is pressed.</summary>
    event Action<CaptureKind>? Pressed;
}

/// <summary>The "start with Windows" entry of the current user.</summary>
public interface IAutostartPort
{
    bool IsEnabled();

    PortResult SetEnabled(bool enabled);
}

/// <summary>Keeps two copies of the app from running at once.</summary>
public interface ISingleInstancePort
{
    /// <summary>True for the first copy; false when another copy already runs.</summary>
    bool TryBecomeFirstInstance();

    /// <summary>Tells the first copy that somebody launched the app again.</summary>
    void NotifyFirstInstance();

    event Action? SecondInstanceLaunched;
}

/// <summary>The monitors and the pointer, as plain data.</summary>
public interface IMonitorCatalogPort
{
    IReadOnlyList<MonitorInfo> GetMonitors();

    PixelPoint GetCursorPosition();

    /// <summary>A string that changes whenever a monitor is added, removed or resized (SPEC capture F6).</summary>
    string GetLayoutSignature();
}

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
