using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UseCases.Shared.Models;

namespace Paper.ScreenWizzard.UseCases.Shell.Models;

public enum SettingsLoadStatus
{
    /// <summary>The file was read as a settings document; whether its values are in range is the use case's question.</summary>
    Loaded,

    /// <summary>There was no file yet (first run).</summary>
    Missing,

    /// <summary>The file is not a settings document (not JSON, a value of the wrong type); the adapter already kept it as .bak (SPEC shell F1).</summary>
    Corrupt,

    /// <summary>The file exists but could not be read (locked, no permission): it may be perfectly good, so it is never written over.</summary>
    Unreadable,
}

/// <param name="Stored">The settings as the file holds them when <see cref="SettingsLoadStatus.Loaded"/>; a setting the file lacks is null.</param>
public sealed record SettingsLoadResult(StoredSettings? Stored, SettingsLoadStatus Status, string? Detail);

/// <summary>What <c>Start</c> needs from the outside world, gathered by the entry point.</summary>
public sealed record ShellStartInput(
    bool IsFirstInstance,
    string PicturesFolder,
    string SystemCultureName,
    IReadOnlyList<MonitorInfo> Monitors,
    PixelSize CaptureBarSize);

public sealed record ShellStartResult(
    bool ContinueRunning,
    AppSettings Settings,
    ResolvedLanguage Language,
    PixelPoint CaptureBarPosition,
    bool ShowCaptureBar,
    IReadOnlyList<CaptureKind> HotkeysRegistered,
    IReadOnlyList<NotificationMessage> Notices);

public enum HotkeyIssue
{
    None,

    /// <summary>A single letter or digit with no modifier would steal typing (SPEC shell, "Phím tắt").</summary>
    NeedsModifier,

    /// <summary>Another capture kind of this app already has that chord.</summary>
    UsedByOtherKind,

    /// <summary>Windows or another program holds the chord.</summary>
    HeldByAnotherProgram,
}

/// <param name="ConflictingKind">The kind that already has the chord, for the message, when the issue is UsedByOtherKind.</param>
public sealed record HotkeyChangeResult(
    bool Accepted,
    HotkeyIssue Issue,
    CaptureKind? ConflictingKind,
    AppSettings Settings,
    NotificationMessage? Message);

public sealed record SettingsApplyResult(bool Saved, AppSettings Settings, NotificationMessage? Message);

public sealed record AutostartChangeResult(bool Enabled, AppSettings Settings, NotificationMessage? Message);

public enum FolderCheck
{
    Exists,
    MissingCanCreate,
    Invalid,
}

public sealed record FolderCreateResult(bool Created, string? Detail);

/// <summary>What a second launch of the app makes the first one do (SPEC shell F6).</summary>
public sealed record SecondInstanceResult(bool ShowCaptureBar);
