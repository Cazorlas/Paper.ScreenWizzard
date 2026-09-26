using Paper.ScreenWizzard.Domain.Shared;

namespace Paper.ScreenWizzard.UseCases.Capture.Models;

/// <summary>What a screen read gave back.</summary>
public enum ScreenCaptureIssue
{
    None,
    OutOfMemory,
    Failed,
}

public sealed record ScreenCaptureResult(PixelImage? Image, ScreenCaptureIssue Issue);
