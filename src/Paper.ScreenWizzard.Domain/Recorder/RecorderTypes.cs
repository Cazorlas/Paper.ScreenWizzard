using Paper.ScreenWizzard.Domain.Shared;

namespace Paper.ScreenWizzard.Domain.Recorder;

/// <summary>What the recording bar records (SPEC recorder, Inputs).</summary>
public enum RecordTargetKind
{
    Monitor,
    Region,
    Window,
    Desktop,
}

/// <summary>A sound that can go into the video.</summary>
public enum SoundSource
{
    System,
    Microphone,
}

/// <summary>
/// The recording bar's choices and the recording settings, kept for the next recording (SPEC recorder, Inputs).
/// </summary>
/// <param name="MonitorIndex">The monitor chosen on the bar; null is "the monitor under the pointer".</param>
public sealed record RecorderSettings(
    RecordTargetKind Target,
    int? MonitorIndex,
    bool SystemSound,
    bool Microphone,
    bool Pointer,
    int CountdownSeconds,
    int FramesPerSecond,
    string VideoFolder);

/// <summary>The limits and defaults of recording (SPEC recorder, Inputs and F7).</summary>
public static class RecorderRules
{
    public const string VideoFolderName = "Paper.ScreenWizzard";

    /// <summary>A region smaller than this on either side is refused (F7).</summary>
    public const int MinimumSide = 16;

    public const int SampleRate = 48_000;

    public const int Channels = 2;

    /// <summary>The AAC sound track: 192 kbit/s.</summary>
    public const int SoundBytesPerSecond = 24_000;

    public static IReadOnlyList<int> FrameRates { get; } = [15, 30, 60];

    public static IReadOnlyList<int> Countdowns { get; } = [0, 3, 5];

    public static bool IsFrameRate(int fps) => FrameRates.Contains(fps);

    /// <summary>
    /// The H.264 bitrate: a sixteenth of a bit a pixel a frame, between 1 and 40 Mbit/s. 1920 × 1080 at 30 fps is about 3.9 Mbit/s, which
    /// with the sound keeps 10 minutes near the 300 MB of SPEC recorder (Assumptions).
    /// </summary>
    public static long VideoBitsPerSecond(PixelSize size, int framesPerSecond) =>
        Math.Clamp((long)size.Width * size.Height * framesPerSecond / 16, 1_000_000L, 40_000_000L);

    public static bool IsCountdown(int seconds) => Countdowns.Contains(seconds);

    public static RecorderSettings Defaults(string videosFolder) => new(
        RecordTargetKind.Monitor,
        null,
        true,
        false,
        true,
        3,
        30,
        Path.Combine(videosFolder, VideoFolderName));
}
