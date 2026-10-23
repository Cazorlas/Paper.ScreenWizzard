using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Recorder;
using Paper.ScreenWizzard.Domain.Shared;

namespace Paper.ScreenWizzard.Domain.Shell;

/// <summary>The settings a new user gets (SPEC shell and SPEC recorder, Inputs).</summary>
public static class SettingsDefaults
{
    public const string SaveFolderName = "Paper.ScreenWizzard";

    public static RecordingHotkeys RecordHotkeys { get; } = new(
        new HotkeyChord(HotkeyModifiers.Control | HotkeyModifiers.Alt, "R"),
        new HotkeyChord(HotkeyModifiers.Control | HotkeyModifiers.Alt, "P"));

    /// <param name="videosFolder">The user's Videos folder; unknown, it is taken beside the Pictures folder, as Windows lays them out.</param>
    public static AppSettings Create(string picturesFolder, string? videosFolder = null) => new(
        new Dictionary<CaptureKind, HotkeyChord>
        {
            [CaptureKind.Rectangle] = new(HotkeyModifiers.None, "PrintScreen"),
            [CaptureKind.Freeform] = new(HotkeyModifiers.Shift, "PrintScreen"),
            [CaptureKind.Window] = new(HotkeyModifiers.Alt, "PrintScreen"),
            [CaptureKind.FullScreen] = new(HotkeyModifiers.Control, "PrintScreen"),
        },
        AfterCaptureAction.ShowDialog,
        Path.Combine(picturesFolder, SaveFolderName),
        ImageFormat.Png,
        90,
        0,
        false,
        FullScreenScope.MonitorUnderCursor,
        false,
        AppLanguage.System,
        AppTheme.System,
        null,
        RecordHotkeys,
        RecorderRules.Defaults(videosFolder ?? VideosBeside(picturesFolder)),
        true);

    private static string VideosBeside(string picturesFolder) =>
        Path.Combine(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(picturesFolder)) ?? picturesFolder, "Videos");
}
