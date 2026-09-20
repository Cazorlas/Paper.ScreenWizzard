using Paper.ScreenWizzard.Domain.Common;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.UseCases.Common.Models;

namespace Paper.ScreenWizzard.UseCases.Common.Ports;

/// <summary>The local time, so a file name is decided by a test and not by the wall clock.</summary>
public interface IClockPort
{
    DateTime Now { get; }
}

/// <summary>Waiting, so a countdown is a fake in a test and not five real seconds.</summary>
public interface IDelayPort
{
    Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken);
}

public interface ILogPort
{
    void Info(string message);

    void Warning(string message);

    void Error(string message, Exception? exception);
}

/// <summary>Tells the user something: a small message that fades, or an error they must see.</summary>
public interface INotificationPort
{
    void ShowToast(NotificationMessage message);

    void ShowError(NotificationMessage message);
}

public interface IClipboardPort
{
    /// <summary>Puts the image on the clipboard, retrying while another program holds it (SPEC capture F5).</summary>
    PortResult SetImage(PixelImage image);

    ClipboardImageResult GetImage();
}

public interface IFileStorePort
{
    bool DirectoryExists(string path);

    PortResult CreateDirectory(string path);

    bool FileExists(string path);

    PortResult WriteAllBytes(string path, byte[] bytes);

    PortBytesResult ReadAllBytes(string path);

    /// <summary>The last write time in UTC, or null when the file does not exist.</summary>
    DateTime? GetLastWriteTimeUtc(string path);
}

/// <summary>Turns bytes into pixels and back. Decoding refuses what is not an image; encoding is PNG or JPG.</summary>
public interface IImageCodecPort
{
    ImageDecodeResult Decode(byte[] fileBytes);

    byte[] Encode(PixelImage image, ImageFormat format, int jpgQuality);
}
