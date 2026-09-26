namespace Paper.ScreenWizzard.UseCases.Capture.Ports;

/// <summary>Waiting, so a countdown is a fake in a test and not five real seconds.</summary>
public interface IDelay
{
    Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken);
}
