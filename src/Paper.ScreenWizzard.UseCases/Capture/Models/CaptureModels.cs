using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.UseCases.Capture.Ports;
using Paper.ScreenWizzard.UseCases.Shared.Models;

namespace Paper.ScreenWizzard.UseCases.Capture.Models;

/// <summary>What one capture run is asked to do, built from the settings and the key the user pressed.</summary>
public sealed record CaptureRequest(CaptureKind Kind, int DelaySeconds, bool IncludeCursor, FullScreenScope FullScreenScope);

/// <summary>Why a capture step did not give an image (SPEC capture, "When it does not do the job").</summary>
public enum CaptureIssue
{
    None,

    /// <summary>F2: the dragged rectangle is under 3 x 3 pixels.</summary>
    RegionTooSmall,

    /// <summary>F3: the freeform outline has fewer than 3 distinct points or less than 9 square pixels.</summary>
    OutlineTooSmall,

    /// <summary>F6: a monitor was added, removed or resized while the user was choosing.</summary>
    DisplayChanged,

    /// <summary>F9: the image is too large to hold.</summary>
    OutOfMemory,

    /// <summary>The screen could not be read at all.</summary>
    Failed,
}

/// <summary>The result of taking the snapshot: a session to choose in, or why not.</summary>
public sealed record CaptureBeginResult(ICaptureSession? Session, CaptureIssue Issue);

/// <summary>A finished selection: the image and the region it covers, or the issue that stopped it.</summary>
public sealed record CaptureOutcome(PixelImage? Image, PixelRect? Region, CaptureIssue Issue, NotificationMessage? Message);

/// <summary>The window the pointer is over, for the highlight and its title.</summary>
public sealed record WindowHit(bool Found, PixelRect Frame, string Title, bool IsWholeMonitor);

/// <summary>What happens to a fresh image: ask the user, or go straight to these destinations (SPEC capture, "Sau khi chụp").</summary>
public sealed record AfterCapturePlan(bool ShowDialog, IReadOnlyList<CaptureDestination> Destinations);

/// <summary>Where the "Save as" box starts.</summary>
public sealed record SaveAsSuggestion(string Folder, string FileName);

/// <summary>The outcome of one dialog button or automatic destination (SPEC capture F4, F5).</summary>
/// <param name="Delivered">True when the image reached the destination.</param>
/// <param name="KeepDialogOpen">True when the dialog must stay so the user can pick another button.</param>
public sealed record CaptureDeliveryResult(
    bool Delivered,
    bool KeepDialogOpen,
    string? Path,
    NotificationMessage? Message);
