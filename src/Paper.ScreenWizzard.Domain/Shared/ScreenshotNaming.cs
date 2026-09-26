using System.Globalization;

namespace Paper.ScreenWizzard.Domain.Common;

/// <summary>The automatic file name of a capture (SPEC capture, "Ảnh đi ra đúng như đã chụp").</summary>
public static class ScreenshotNaming
{
    /// <summary>"Screenshot yyyy-MM-dd HH.mm.ss.png", then "... (2).png", "... (3).png" while that name is <paramref name="isTaken"/>.</summary>
    public static string FileName(DateTime now, ImageFormat format, Func<string, bool> isTaken)
    {
        var stem = "Screenshot " + now.ToString("yyyy-MM-dd HH.mm.ss", CultureInfo.InvariantCulture);
        var extension = format == ImageFormat.Jpg ? ".jpg" : ".png";
        var name = stem + extension;
        for (var number = 2; isTaken(name); number++)
        {
            name = stem + " (" + number.ToString(CultureInfo.InvariantCulture) + ")" + extension;
        }

        return name;
    }

    /// <summary>Whether a file can be written back under its own name: .png, .jpg or .jpeg. A .bmp is read only (SPEC editor, "Định dạng").</summary>
    public static bool CanWriteInPlace(string path) => Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg";

    /// <summary>The path itself when its extension is one the app writes, else the path with ".png" added: no file holds bytes its extension does not name.</summary>
    public static string WithWritableExtension(string path) => CanWriteInPlace(path) ? path : path + ".png";

    /// <summary>The format a chosen file name asks for: .png, .jpg or .jpeg; anything else is PNG.</summary>
    public static ImageFormat FormatOfPath(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".jpg" or ".jpeg" ? ImageFormat.Jpg : ImageFormat.Png;
}
