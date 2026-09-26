using Paper.ScreenWizzard.UseCases.Capture.Ports;

namespace Paper.ScreenWizzard.Infrastructure.Capture;

/// <summary>Real waiting for the countdown.</summary>
public sealed class TaskDelay : IDelay
{
    public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken) => Task.Delay(duration, cancellationToken);
}
