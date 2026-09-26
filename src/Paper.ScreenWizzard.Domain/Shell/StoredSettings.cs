using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;

namespace Paper.ScreenWizzard.Domain.Shell;

/// <summary>A hotkey as the settings file holds it: either part may be missing.</summary>
public sealed record StoredHotkey(HotkeyModifiers? Modifiers, string? Key);

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
    PixelPoint? CaptureBarPosition)
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
        settings.CaptureBarPosition);
}
