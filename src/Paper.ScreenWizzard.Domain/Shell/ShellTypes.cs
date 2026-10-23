using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Recorder;
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

/// <summary>The two recording hotkeys (SPEC recorder, Inputs).</summary>
public enum RecordHotkey
{
    StartStop,
    Pause,
}

/// <summary>The chords of the recording hotkeys; a record, so two settings with the same keys are equal.</summary>
public sealed record RecordingHotkeys(HotkeyChord StartStop, HotkeyChord Pause)
{
    public HotkeyChord this[RecordHotkey key] => key == RecordHotkey.StartStop ? StartStop : Pause;

    public RecordingHotkeys With(RecordHotkey key, HotkeyChord chord) =>
        key == RecordHotkey.StartStop ? this with { StartStop = chord } : this with { Pause = chord };
}

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

/// <summary>Everything the user can set (SPEC shell and SPEC recorder, Inputs). Persisted by the settings store as one document.</summary>
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
    RecordingHotkeys RecordHotkeys,
    RecorderSettings Recorder,
    bool CheckForUpdates = true);
