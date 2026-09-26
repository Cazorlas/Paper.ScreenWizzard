using Paper.ScreenWizzard.UseCases.Capture.Models;
using Paper.ScreenWizzard.UseCases.Shared.Models;

namespace Paper.ScreenWizzard.Presentation.Capture.ViewModels;

/// <summary>
/// The message of a refused capture step. The use case normally names its own; when it sends none, the issue still has words, so
/// a refusal is never a silent nothing (SPEC: nothing fails silently). The keys are the ones the use case itself uses.
/// </summary>
internal static class CaptureMessages
{
    public static NotificationMessage For(CaptureIssue issue, NotificationMessage? provided) => provided ?? issue switch
    {
        CaptureIssue.RegionTooSmall => NotificationMessage.Of("Capture.RegionTooSmall"),
        CaptureIssue.OutlineTooSmall => NotificationMessage.Of("Capture.OutlineTooSmall"),
        CaptureIssue.DisplayChanged => NotificationMessage.Of("Capture.DisplayChanged"),
        CaptureIssue.OutOfMemory => NotificationMessage.Of("Capture.ImageTooLarge"),
        _ => NotificationMessage.Of("Capture.Failed", "unknown reason"),
    };
}
