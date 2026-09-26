namespace Paper.ScreenWizzard.UseCases.Shared.Models;

public enum DeliveryIssue
{
    None,
    FolderNotWritable,
    ClipboardBusy,

    /// <summary>The encoder could not make the file (no memory for the picture, a size the format cannot hold).</summary>
    EncodeFailed,
}

/// <param name="Path">The file written, when there is one.</param>
/// <param name="Detail">The system's reason on failure.</param>
public sealed record DeliveryResult(bool Success, DeliveryIssue Issue, string? Path, string? Detail);
