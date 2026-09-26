using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;

namespace Paper.ScreenWizzard.Domain.Shell;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8,
}

/// <summary>A hotkey: modifiers plus one key, named as the user reads it ("PrintScreen", "F9", "A", "1").</summary>
public readonly record struct HotkeyChord(HotkeyModifiers Modifiers, string Key);

public enum AppLanguage
{
    System,
    Vietnamese,
    English,
}

public enum AppTheme
{
    System,
    Light,
    Dark,
}

/// <summary>Everything the user can set (SPEC shell, Inputs). Persisted by the settings store as one document.</summary>
public sealed record AppSettings(
    IReadOnlyDictionary<CaptureKind, HotkeyChord> Hotkeys,
    AfterCaptureAction AfterCapture,
    string SaveFolder,
    ImageFormat Format,
    int JpgQuality,
    int DelaySeconds,
    bool IncludeCursor,
    FullScreenScope FullScreenScope,
    bool StartWithWindows,
    AppLanguage Language,
    AppTheme Theme,
    PixelPoint? CaptureBarPosition,
    bool CheckForUpdates = true);
