using System.Diagnostics;
using Paper.ScreenWizzard.UseCases.Recorder.Ports;

namespace Paper.ScreenWizzard.Infrastructure.Recorder;

/// <summary>The performance counter, counted from when the app made this clock: pictures and sounds are stamped on it.</summary>
public sealed class MonotonicClock : IMonotonicClock
{
    private readonly long _start = Stopwatch.GetTimestamp();

    public TimeSpan Now => Stopwatch.GetElapsedTime(_start);
}
