using System.Globalization;
using Paper.ScreenWizzard.Presentation.Shared.ViewModels;

namespace Paper.ScreenWizzard.Presentation.Recorder.ViewModels;

/// <summary>The recorded time as the user reads it, in the tray's tooltip and the "Recorded" window (SPEC recorder, steps 4 and 5).</summary>
public static class RecordingTimeText
{
    /// <summary>"01:23", or "1:02:03" from an hour on.</summary>
    public static string Format(TimeSpan time) =>
        time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture) : time.ToString(@"mm\:ss", CultureInfo.InvariantCulture);

    /// <summary>"Đang quay 01:23" or "Tạm dừng quay 01:23", in the language in use.</summary>
    public static string Tooltip(ILocalizer localizer, bool paused, TimeSpan elapsed) =>
        string.Format(CultureInfo.CurrentCulture, localizer.GetString(paused ? "Tray.TooltipPaused" : "Tray.TooltipRecording"), Format(elapsed));
}
