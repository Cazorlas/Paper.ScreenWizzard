using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Common;

namespace Paper.ScreenWizzard.Domain.Shell;

/// <summary>The settings a new user gets (SPEC shell, Inputs).</summary>
public static class SettingsDefaults
{
    public const string SaveFolderName = "Paper.ScreenWizzard";

    public static AppSettings Create(string picturesFolder) => new(
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
        null);
}
