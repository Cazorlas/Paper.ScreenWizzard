using Paper.ScreenWizzard.Domain.Recorder;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.UseCases.Shared.Models;

namespace Paper.ScreenWizzard.UseCases.Recorder.Models;

/// <param name="Area">The rectangle to record, from <see cref="RecordArea.Resolve"/>.</param>
/// <param name="WindowHandle">The recorded window, watched so that closing it stops the recording (F1); null for any other choice.</param>
public sealed record RecordRequest(PixelRect Area, long? WindowHandle, RecorderSettings Settings);

public enum RecorderState
{
    Idle,
    CountingDown,
    Recording,
    Paused,
    Finishing,
}

public enum RecordStartIssue
{
    None,

    /// <summary>A recording is already running.</summary>
    Busy,

    /// <summary>The area is smaller than 16 × 16 or off every monitor (F7).</summary>
    AreaUnusable,

    /// <summary>Stopped during the countdown: nothing recorded, no file.</summary>
    Cancelled,

    /// <summary>The video folder cannot be made or written (F3).</summary>
    FolderNotWritable,

    /// <summary>The screen cannot be captured, or no video encoder (F6).</summary>
    CannotRecord,
}

/// <param name="Notices">What goes on without the user's choice: a missing microphone (F4), no system sound (F5).</param>
/// <param name="Message">Why it did not start, for the box (F3, F6, F7).</param>
public sealed record RecordStartResult(
    RecordStartIssue Issue,
    string? FilePath,
    IReadOnlyList<NotificationMessage> Notices,
    NotificationMessage? Message)
{
    public bool Started => Issue == RecordStartIssue.None;
}

/// <summary>Why a recording ended.</summary>
public enum RecordingEnd
{
    /// <summary>The user stopped it (or the app is exiting).</summary>
    Stopped,

    /// <summary>The recorded window was closed (F1).</summary>
    WindowClosed,

    /// <summary>The monitor went away or its mode changed (F2).</summary>
    DisplayChanged,

    /// <summary>Writing the file failed (F3): nothing half-written is left.</summary>
    WriteFailed,
}

/// <param name="FilePath">The saved video; null when nothing was saved (F3).</param>
/// <param name="Message">What the "Recorded" window or the notice says besides the file (F1, F2, F3).</param>
public sealed record RecordingResult(
    RecordingEnd End,
    string? FilePath,
    PixelSize Size,
    TimeSpan Duration,
    NotificationMessage? Message)
{
    public bool Saved => FilePath is not null;
}

public enum ScreenFrameIssue
{
    None,

    /// <summary>Nothing on the screen changed within the wait: the previous picture still holds.</summary>
    NoNewFrame,

    /// <summary>The monitor went away or changed mode (F2).</summary>
    DisplayChanged,
}

/// <param name="Image">The area, BGRA, the size of the area; null unless <see cref="Issue"/> is None.</param>
/// <param name="At">When it was taken, on the clock of <see cref="Ports.IMonotonicClock"/>.</param>
public sealed record ScreenFrame(PixelImage? Image, TimeSpan At, ScreenFrameIssue Issue, string? Detail = null);

/// <param name="SystemDetail">Why system sound is not recorded; null when it is, or was not asked for.</param>
/// <param name="MicrophoneDetail">Why the microphone is not recorded; null when it is, or was not asked for.</param>
public sealed record SoundStartResult(bool SystemSound, bool Microphone, string? SystemDetail, string? MicrophoneDetail);

public enum VideoWriterIssue
{
    None,

    /// <summary>The disk or the folder refused (full, read-only, gone): F3.</summary>
    Disk,

    /// <summary>No encoder for H.264 or AAC on this Windows: F6.</summary>
    Encoder,
}

public sealed record VideoWriterResult(VideoWriterIssue Issue, string? Detail)
{
    public static VideoWriterResult Ok { get; } = new(VideoWriterIssue.None, null);

    public bool Success => Issue == VideoWriterIssue.None;
}
