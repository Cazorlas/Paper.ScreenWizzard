using Paper.ScreenWizzard.UseCases.Shared.Ports;

namespace Paper.ScreenWizzard.Infrastructure.Common;

/// <summary>Real waiting for the countdown.</summary>
public sealed class TaskDelay : IDelay
{
    public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken) => Task.Delay(duration, cancellationToken);
}
