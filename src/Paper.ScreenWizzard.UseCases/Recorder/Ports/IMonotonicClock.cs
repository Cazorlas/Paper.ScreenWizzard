namespace Paper.ScreenWizzard.UseCases.Recorder.Ports;

/// <summary>A clock that only goes forward, shared by the pictures and the sounds so they stay in time (SPEC recorder, "Sound").</summary>
public interface IMonotonicClock
{
    TimeSpan Now { get; }
}
