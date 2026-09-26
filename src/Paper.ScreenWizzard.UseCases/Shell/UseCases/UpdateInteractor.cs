using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UseCases.Shared.Models;
using Paper.ScreenWizzard.UseCases.Shared.Ports;
using Paper.ScreenWizzard.UseCases.Shell.Models;
using Paper.ScreenWizzard.UseCases.Shell.Ports;

namespace Paper.ScreenWizzard.UseCases.Shell.UseCases;

/// <summary>Everything the app decides about a new version (SPEC shell, "Báo bản mới"): whether to ask, what counts as newer, when to say it.</summary>
public sealed class UpdateInteractor : IUpdateInteractor
{
    private readonly IReleaseFeed _feed;
    private readonly IBrowser _browser;
    private readonly ILog _log;
    private AppVersion? _announced;

    public UpdateInteractor(IReleaseFeed feed, IBrowser browser, ILog log)
    {
        _feed = feed;
        _browser = browser;
        _log = log;
    }

    public async Task<UpdateCheckResult> CheckAsync(AppSettings settings, string? runningVersion, CancellationToken cancellationToken)
    {
        if (!settings.CheckForUpdates)
        {
            return UpdateCheckResult.None;
        }

        // A build that carries no x.y.z (a developer's build) has nothing to compare with, and GitHub is not asked.
        if (!AppVersion.TryParse(runningVersion, out var running))
        {
            _log.Info($"The running version '{runningVersion}' is not x.y.z; no update check.");
            return UpdateCheckResult.None;
        }

        var latest = await _feed.GetLatestAsync(cancellationToken);
        if (latest.Tag is null)
        {
            _log.Info($"The update check got no answer: {latest.Detail}");
            return UpdateCheckResult.None;
        }

        if (!AppVersion.TryParse(latest.Tag, out var version))
        {
            _log.Warning($"The newest release is tagged '{latest.Tag}', which is not vx.y.z; it is not offered.");
            return UpdateCheckResult.None;
        }

        if (version <= running)
        {
            return UpdateCheckResult.None;
        }

        var offer = new UpdateOffer(version, UpdateRules.ReleasePage(version));
        if (_announced == version)
        {
            return new UpdateCheckResult(offer, null);
        }

        _announced = version;
        _log.Info($"Version {version} is out; this is {running}.");
        return new UpdateCheckResult(offer, NotificationMessage.Of("Shell.UpdateAvailable", version.ToString()));
    }

    public NotificationMessage? OpenDownloadPage(UpdateOffer offer)
    {
        var opened = _browser.Open(offer.PageUrl);
        if (opened.Success)
        {
            return null;
        }

        _log.Warning($"The page {offer.PageUrl} did not open: {opened.Detail}");
        return NotificationMessage.Of("Shell.UpdatePageNotOpened", opened.Detail ?? string.Empty, offer.PageUrl);
    }
}
