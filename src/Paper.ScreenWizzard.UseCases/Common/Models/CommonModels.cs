using Paper.ScreenWizzard.Domain.Shared;

namespace Paper.ScreenWizzard.UseCases.Common.Models;

/// <summary>What a port answers when it only has to say whether it worked.</summary>
/// <param name="Detail">The system's own reason, in English, for the error text the user reads; null on success.</param>
public sealed record PortResult(bool Success, string? Detail)
{
    public static PortResult Ok { get; } = new(true, null);

    public static PortResult Fail(string detail) => new(false, detail);
}

/// <summary>A file read: the bytes, or why not.</summary>
public sealed record PortBytesResult(bool Success, byte[]? Bytes, string? Detail);

/// <summary>Why a file could not be turned into an image (SPEC editor F2, F8).</summary>
public enum ImageDecodeIssue
{
    None,
    NotAnImage,
    TooLarge,
}

public sealed record ImageDecodeResult(PixelImage? Image, ImageDecodeIssue Issue);

/// <summary>The clipboard read: an image, or none (SPEC editor F3).</summary>
/// <param name="ReadFailed">True when the clipboard could not be read (held by another program, an error), as against having no picture.</param>
public sealed record ClipboardImageResult(bool HasImage, PixelImage? Image, string? Detail, bool ReadFailed = false);

/// <summary>
/// A message for the user that is not yet text: the interactor picks the key and the values, and the presentation layer
/// turns it into the user's language. The use case never holds a sentence.
/// </summary>
public sealed record NotificationMessage(string Key, IReadOnlyList<string> Arguments)
{
    public static NotificationMessage Of(string key, params string[] arguments) => new(key, arguments);
}
