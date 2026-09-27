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

    public static IReadOnlyList<int> FrameRates { get; } = [15, 30, 60];

    public static IReadOnlyList<int> Countdowns { get; } = [0, 3, 5];

    public static bool IsFrameRate(int fps) => FrameRates.Contains(fps);

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
