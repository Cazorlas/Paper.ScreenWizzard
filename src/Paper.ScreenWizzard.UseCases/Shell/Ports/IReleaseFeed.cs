using Paper.ScreenWizzard.UseCases.Shell.Models;

namespace Paper.ScreenWizzard.UseCases.Shell.Ports;

/// <summary>The newest published release of the app, as the place that publishes it says.</summary>
public interface IReleaseFeed
{
    Task<ReleaseFeedResult> GetLatestAsync(CancellationToken cancellationToken);
}
