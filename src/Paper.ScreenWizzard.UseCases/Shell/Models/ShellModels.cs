using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UseCases.Common.Models;

namespace Paper.ScreenWizzard.UseCases.Shell.Models;

public enum SettingsLoadStatus
{
    /// <summary>The file was read.</summary>
    Loaded,

    /// <summary>There was no file yet (first run).</summary>
    Missing,

    /// <summary>The file could not be read; the adapter already kept the bad file as .bak (SPEC shell F1).</summary>
    Corrupt,
}

public sealed record SettingsLoadResult(AppSettings? Settings, SettingsLoadStatus Status, string? Detail);

/// <summary>What <c>Start</c> needs from the outside world, gathered by the entry point.</summary>
public sealed record ShellStartInput(
    bool IsFirstInstance,
    string PicturesFolder,
    string SystemCultureName,
    IReadOnlyList<MonitorInfo> Monitors,
    PixelSize CaptureBarSize);

/// <summary>The language the windows use once "follow Windows" is resolved.</summary>
public enum ResolvedLanguage
{
    Vietnamese,
    English,
}

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
