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

        var found = await FindAsync(runningVersion, cancellationToken);
        if (found.Offer is not { } offer)
        {
            return UpdateCheckResult.None;
        }

        if (_announced == offer.Version)
        {
            return new UpdateCheckResult(offer, null);
        }

        _announced = offer.Version;
        return new UpdateCheckResult(offer, Announce(offer));
    }

    public async Task<UpdateCheckResult> CheckNowAsync(string? runningVersion, CancellationToken cancellationToken)
    {
        // The user asked: GitHub is asked even with the daily check off, and the answer is always said, whatever it is.
        var found = await FindAsync(runningVersion, cancellationToken);
        if (found.Offer is { } offer)
        {
            _announced = offer.Version;
            return new UpdateCheckResult(offer, Announce(offer));
        }

        return new UpdateCheckResult(null, found.Running is { } running && found.Problem is null
            ? NotificationMessage.Of("Shell.UpToDate", running.ToString())
            : NotificationMessage.Of("Shell.UpdateCheckFailed", found.Problem ?? string.Empty));
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

    private static NotificationMessage Announce(UpdateOffer offer) => NotificationMessage.Of("Shell.UpdateAvailable", offer.Version.ToString());

    // What GitHub says against the running version: a newer one (Offer), none (neither), or why it cannot tell (Problem).
    private async Task<(AppVersion? Running, UpdateOffer? Offer, string? Problem)> FindAsync(string? runningVersion, CancellationToken cancellationToken)
    {
        // A build that carries no x.y.z (a developer's build) has nothing to compare with, and GitHub is not asked.
        if (!AppVersion.TryParse(runningVersion, out var running))
        {
            _log.Info($"The running version '{runningVersion}' is not x.y.z; no update check.");
            return (null, null, $"this build has no version number ('{runningVersion}')");
        }

        var latest = await _feed.GetLatestAsync(cancellationToken);
        if (latest.Tag is null)
        {
            _log.Info($"The update check got no answer: {latest.Detail}");
            return (running, null, latest.Detail ?? "no answer");
        }

        if (!AppVersion.TryParse(latest.Tag, out var version))
        {
            _log.Warning($"The newest release is tagged '{latest.Tag}', which is not vx.y.z; it is not offered.");
            return (running, null, $"the newest release is tagged '{latest.Tag}'");
        }

        if (version <= running)
        {
            return (running, null, null);
        }

        _log.Info($"Version {version} is out; this is {running}.");
        return (running, new UpdateOffer(version, UpdateRules.ReleasePage(version)), null);
    }
}
