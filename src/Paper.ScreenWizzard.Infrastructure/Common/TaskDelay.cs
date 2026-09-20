using Paper.ScreenWizzard.UseCases.Common.Ports;

namespace Paper.ScreenWizzard.Infrastructure.Common;

/// <summary>Real waiting for the countdown.</summary>
public sealed class TaskDelay : IDelayPort
{
    public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken) => Task.Delay(duration, cancellationToken);
}
