namespace Paper.ScreenWizzard.Domain.Recorder;

/// <summary>
/// Turns the time a frame or a sound was taken (one monotonic clock for both) into its time in the video. A pause is cut out: what
/// is taken while paused has no video time, and everything after it moves back by the pause (SPEC recorder, "Pause and stop").
/// </summary>
public sealed class RecordingTimeline
{
    private readonly List<(TimeSpan From, TimeSpan To)> _pauses = [];
    private TimeSpan? _start;
    private TimeSpan? _pausedAt;

    public bool IsStarted => _start is not null;

    public bool IsPaused => _pausedAt is not null;

    public void Start(TimeSpan now)
    {
        _start = now;
        _pauses.Clear();
        _pausedAt = null;
    }

    public void Pause(TimeSpan now)
    {
        if (_start is not null && _pausedAt is null)
        {
            _pausedAt = now;
        }
    }

    public void Resume(TimeSpan now)
    {
        if (_pausedAt is { } from)
        {
            _pauses.Add((from, now < from ? from : now));
            _pausedAt = null;
        }
    }

    /// <summary>The video time of something taken at <paramref name="at"/>; null before the start or inside a pause.</summary>
    public TimeSpan? MediaTime(TimeSpan at)
    {
        if (_start is not { } start || at < start)
        {
            return null;
        }

        if (_pausedAt is { } pausedAt && at >= pausedAt)
        {
            return null;
        }

        var cut = TimeSpan.Zero;
        foreach (var (from, to) in _pauses)
        {
            if (at >= from && at < to)
            {
                return null;
            }

            if (at >= to)
            {
                cut += to - from;
            }
        }

        return at - start - cut;
    }

    /// <summary>How long the video is at <paramref name="now"/>: the time recorded, pauses left out.</summary>
    public TimeSpan Elapsed(TimeSpan now)
    {
        if (_start is not { } start)
        {
            return TimeSpan.Zero;
        }

        var end = _pausedAt ?? now;
        var cut = _pauses.Aggregate(TimeSpan.Zero, (sum, pause) => sum + (pause.To - pause.From));
        var elapsed = end - start - cut;
        return elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
    }
}

/// <summary>Which frame slots a picture fills.</summary>
/// <param name="First">The first slot not yet written.</param>
/// <param name="Count">
/// How many slots, up to and including the picture's own; 0 when the slot is already written (the picture is dropped). Slots before
/// the last are gaps: they repeat the previous picture.
/// </param>
public readonly record struct FrameSlots(long First, int Count)
{
    public long Last => First + Count - 1;
}

/// <summary>
/// Keeps the video at a fixed number of frames a second: a picture late by several frames fills the gap (the previous one is repeated),
/// a second picture for the same frame is dropped. The screen sends no picture while nothing changes, so the gap is filled from the
/// clock too.
/// </summary>
public sealed class FrameClock
{
    private readonly int _fps;
    private long _next;

    public FrameClock(int fps)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fps);
        _fps = fps;
    }

    public TimeSpan SlotLength => TimeOf(1);

    /// <summary>The length of the video written so far.</summary>
    public TimeSpan End => TimeOf(_next);

    public FrameSlots Place(TimeSpan mediaTime)
    {
        var slot = (long)Math.Floor(mediaTime.TotalSeconds * _fps);
        if (slot < _next)
        {
            return new FrameSlots(_next, 0);
        }

        var first = _next;
        _next = slot + 1;
        return new FrameSlots(first, (int)Math.Min(int.MaxValue, slot - first + 1));
    }

    public TimeSpan TimeOf(long slot) => TimeSpan.FromTicks(slot * TimeSpan.TicksPerSecond / _fps);
}
