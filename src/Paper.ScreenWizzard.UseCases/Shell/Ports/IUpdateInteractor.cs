using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UseCases.Shared.Models;
using Paper.ScreenWizzard.UseCases.Shell.Models;

namespace Paper.ScreenWizzard.UseCases.Shell.Ports;

/// <summary>Whether a newer version is out and where to get it (SPEC shell, "Báo bản mới").</summary>
public interface IUpdateInteractor
{
    /// <summary>
    /// Asks for the newest release when the user left the check on. A failure (no network, GitHub refusing) is only logged: the app
    /// works the same without the answer, and the next check tries again.
    /// </summary>
    Task<UpdateCheckResult> CheckAsync(AppSettings settings, string? runningVersion, CancellationToken cancellationToken);

    /// <summary>Opens the release page of the offered version; a page that did not open is said, with its address.</summary>
    NotificationMessage? OpenDownloadPage(UpdateOffer offer);
}
