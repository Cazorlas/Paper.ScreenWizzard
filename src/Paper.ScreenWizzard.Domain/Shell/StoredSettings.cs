using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Recorder;
using Paper.ScreenWizzard.Domain.Shared;

namespace Paper.ScreenWizzard.Domain.Shell;

/// <summary>A hotkey as the settings file holds it: either part may be missing.</summary>
public sealed record StoredHotkey(HotkeyModifiers? Modifiers, string? Key);

/// <summary>The recording settings as the file holds them: any may be missing (a file of 0.1.3 has none).</summary>
public sealed record StoredRecorder(
    RecordTargetKind? Target,
    int? MonitorIndex,
    bool? SystemSound,
    bool? Microphone,
    bool? Pointer,
    int? CountdownSeconds,
    int? FramesPerSecond,
    string? VideoFolder)
{
    public static StoredRecorder From(RecorderSettings settings) => new(
        settings.Target,
        settings.MonitorIndex,
        settings.SystemSound,
        settings.Microphone,
        settings.Pointer,
        settings.CountdownSeconds,
        settings.FramesPerSecond,
        settings.VideoFolder);
}

/// <summary>
/// The settings file as it was written, nothing checked yet: a setting the file lacks is null (a file of an older version lacks
/// what was added since, SPEC shell F8). <see cref="SettingsRules.Complete"/> decides what the app runs on.
/// </summary>
public sealed record StoredSettings(
    IReadOnlyDictionary<string, StoredHotkey?>? Hotkeys,
    AfterCaptureAction? AfterCapture,
    string? SaveFolder,
    ImageFormat? Format,
    int? JpgQuality,
    int? DelaySeconds,
    bool? IncludeCursor,
    FullScreenScope? FullScreenScope,
    bool? StartWithWindows,
    AppLanguage? Language,
    AppTheme? Theme,
    PixelPoint? CaptureBarPosition,
    bool? CheckForUpdates = null,
    IReadOnlyDictionary<string, StoredHotkey?>? RecordHotkeys = null,
    StoredRecorder? Recorder = null)
{
    /// <summary>A file that holds no setting at all.</summary>
    public static StoredSettings Empty { get; } = new(null, null, null, null, null, null, null, null, null, null, null, null);

    /// <summary>What a file written from <paramref name="settings"/> holds.</summary>
    public static StoredSettings From(AppSettings settings) => new(
        settings.Hotkeys.ToDictionary(pair => pair.Key.ToString(), pair => (StoredHotkey?)new StoredHotkey(pair.Value.Modifiers, pair.Value.Key)),
        settings.AfterCapture,
        settings.SaveFolder,
        settings.Format,
        settings.JpgQuality,
        settings.DelaySeconds,
        settings.IncludeCursor,
        settings.FullScreenScope,
        settings.StartWithWindows,
        settings.Language,
        settings.Theme,
        settings.CaptureBarPosition,
        settings.CheckForUpdates,
        new Dictionary<string, StoredHotkey?>
        {
            [nameof(RecordHotkey.StartStop)] = new StoredHotkey(settings.RecordHotkeys.StartStop.Modifiers, settings.RecordHotkeys.StartStop.Key),
            [nameof(RecordHotkey.Pause)] = new StoredHotkey(settings.RecordHotkeys.Pause.Modifiers, settings.RecordHotkeys.Pause.Key),
        },
        StoredRecorder.From(settings.Recorder));
}
