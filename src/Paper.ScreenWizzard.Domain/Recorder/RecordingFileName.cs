using System.Globalization;

namespace Paper.ScreenWizzard.Domain.Recorder;

/// <summary>"Recording 2026-09-27 14.03.05.mp4", then " (2)", " (3)" while a name is taken (SPEC recorder, "The file").</summary>
public static class RecordingFileName
{
    public const string Extension = ".mp4";

    /// <summary>The suffix of the file while it is being written; it becomes the real name only when the video is complete (F3).</summary>
    public const string PartSuffix = ".part";

    public static string For(DateTime started, Func<string, bool> isTaken)
    {
        var stem = "Recording " + started.ToString("yyyy-MM-dd HH.mm.ss", CultureInfo.InvariantCulture);
        var name = stem + Extension;
        for (var n = 2; isTaken(name) || isTaken(name + PartSuffix); n++)
        {
            name = string.Create(CultureInfo.InvariantCulture, $"{stem} ({n}){Extension}");
        }

        return name;
    }
}
