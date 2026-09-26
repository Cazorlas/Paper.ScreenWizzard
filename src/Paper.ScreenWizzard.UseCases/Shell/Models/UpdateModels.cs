using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UseCases.Shared.Models;

namespace Paper.ScreenWizzard.UseCases.Shell.Models;

/// <param name="Tag">The tag of the newest published release ("v0.1.3"); null when there is none or it could not be asked.</param>
/// <param name="Detail">Why there is no tag, for the log.</param>
public sealed record ReleaseFeedResult(string? Tag, string? Detail);

/// <summary>A version newer than the running one, and the page it is downloaded from.</summary>
public sealed record UpdateOffer(AppVersion Version, string PageUrl);

/// <param name="Offer">The newer version; null when there is none, the check is off or the answer is not known.</param>
/// <param name="Notice">Said once per version while the app runs; later checks that find the same version keep the offer quiet.</param>
public sealed record UpdateCheckResult(UpdateOffer? Offer, NotificationMessage? Notice)
{
    public static UpdateCheckResult None { get; } = new(null, null);
}
