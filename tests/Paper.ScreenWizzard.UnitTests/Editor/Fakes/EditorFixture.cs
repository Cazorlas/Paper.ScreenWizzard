using System.Text;
using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Editor;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UnitTests.Capture.Fakes;
using Paper.ScreenWizzard.UseCases.Editor.Ports;
using Paper.ScreenWizzard.UseCases.Editor.UseCases;
using Paper.ScreenWizzard.UseCases.Shared.Models;
using Paper.ScreenWizzard.UseCases.Shared.Ports;
using Paper.ScreenWizzard.UseCases.Shared.UseCases;

namespace Paper.ScreenWizzard.UnitTests.Editor.Fakes;

/// <summary>Files as a dictionary, with a write time that moves on every write, and "another program" helpers for F9.</summary>
public sealed class EditorFileStore : IFileStorePort
{
    private static readonly DateTime _start = new(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc);

    private int _tick;

    public HashSet<string> Directories { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, byte[]> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, DateTime> Times { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> WrittenPaths { get; } = [];

    /// <summary>When set, every write fails with this reason.</summary>
    public string? WriteFailure { get; set; }

    /// <summary>When set, every read fails with this reason.</summary>
    public string? ReadFailure { get; set; }

    /// <summary>A file that already exists on disk, as if it had been there before the app started.</summary>
    public void Seed(string path, byte[] bytes)
    {
        var folder = Path.GetDirectoryName(path);
        if (folder is not null)
        {
            Directories.Add(folder);
        }

        Files[path] = bytes;
        Times[path] = _start.AddSeconds(_tick++);
    }

    /// <summary>Another program rewrites the file: new bytes and a newer write time.</summary>
    public void ChangeBehindTheBack(string path)
    {
        Files[path] = [1, 2, 3];
        Times[path] = _start.AddSeconds(_tick++);
    }

    /// <summary>Another program deletes the file.</summary>
    public void Remove(string path)
    {
        Files.Remove(path);
        Times.Remove(path);
    }

    public bool DirectoryExists(string path) => Directories.Contains(path);

    public PortResult CreateDirectory(string path)
    {
        Directories.Add(path);
        return PortResult.Ok;
    }

    public bool FileExists(string path) => Files.ContainsKey(path);

    public PortResult WriteAllBytes(string path, byte[] bytes)
    {
        WrittenPaths.Add(path);
        if (WriteFailure is not null)
        {
            return PortResult.Fail(WriteFailure);
        }

        var folder = Path.GetDirectoryName(path);
        if (folder is null || !Directories.Contains(folder))
        {
            return PortResult.Fail("Could not find a part of the path '" + path + "'.");
        }

        Files[path] = bytes;
        Times[path] = _start.AddSeconds(_tick++);
        return PortResult.Ok;
    }

    public PortBytesResult ReadAllBytes(string path)
    {
        if (ReadFailure is not null)
        {
            return new PortBytesResult(false, null, ReadFailure);
        }

        return Files.TryGetValue(path, out var bytes) ? new PortBytesResult(true, bytes, null) : new PortBytesResult(false, null, "missing");
    }

    public DateTime? GetLastWriteTimeUtc(string path) => Times.TryGetValue(path, out var time) ? time : null;
}

/// <summary>Records what it was asked to encode; decodes to whatever the test set.</summary>
public sealed class EditorCodec : IImageCodecPort
{
    public ImageDecodeResult DecodeResult { get; set; } = new(null, ImageDecodeIssue.NotAnImage);

    public List<byte[]> DecodeCalls { get; } = [];

    public List<EncodeCall> Calls { get; } = [];

    public ImageDecodeResult Decode(byte[] fileBytes)
    {
        DecodeCalls.Add(fileBytes);
        return DecodeResult;
    }

    public byte[] Encode(PixelImage image, ImageFormat format, int jpgQuality)
    {
        var result = Encoding.ASCII.GetBytes($"{format}:{image.Width}x{image.Height}:#{Calls.Count + 1}");
        Calls.Add(new EncodeCall(image.Width, image.Height, image.Bgra.ToArray(), format, jpgQuality, result));
        return result;
    }
}

public sealed class EditorClipboard : IClipboardPort
{
    public PortResult SetResult { get; set; } = PortResult.Ok;

    public ClipboardImageResult GetResult { get; set; } = new(false, null, "The clipboard holds no image.");

    public List<PixelImage> SetCalls { get; } = [];

    public PortResult SetImage(PixelImage image)
    {
        SetCalls.Add(image with { Bgra = image.Bgra.ToArray() });
        return SetResult;
    }

    public ClipboardImageResult GetImage() => GetResult;
}

/// <summary>The editor interactor wired to plain-data fakes, with the real <see cref="ImageDelivery"/> on top of them.</summary>
public sealed class EditorFixture
{
    private EditorInteractor? _interactor;

    public EditorFileStore Files { get; } = new();

    public EditorCodec Codec { get; } = new();

    public EditorClipboard Clipboard { get; } = new();

    public FakeClock Clock { get; } = new();

    public FakeLog Log { get; } = new();

    public EditorInteractor Interactor => _interactor ??= new EditorInteractor(
        Files,
        Codec,
        Clipboard,
        new ImageDelivery(Clock, Files, Codec, Clipboard),
        Clock,
        Log);

    /// <summary>A session on a blank (all zero) image; a fresh capture, so no source path.</summary>
    public IEditorSession NewSession(int width = 100, int height = 60) => Interactor.Open(EditorData.Blank(width, height), null);

    public IEditorSession NewSession(PixelImage image, string? sourcePath = null) => Interactor.Open(image, sourcePath);

    /// <summary>Puts a file on the fake disk, tells the codec it is this image, and opens it; fails at an assertion when there is no session.</summary>
    public IEditorSession OpenFileOnDisk(string path, PixelImage image)
    {
        Files.Seed(path, [9, 9, 9]);
        Codec.DecodeResult = new ImageDecodeResult(image, ImageDecodeIssue.None);
        var result = Interactor.OpenFile(path);
        Assert.That(result.Session, Is.Not.Null, "OpenFile gave no session (issue " + result.Issue + ")");
        return result.Session!;
    }
}

/// <summary>Numbers and shapes written out from SPEC editor, so a test never asks the code for its own expected value.</summary>
public static class EditorData
{
    public const string SaveFolder = @"C:\Users\Hung\Pictures\Paper.ScreenWizzard";

    public static readonly RgbaColor Red = new(255, 0, 0, 255);

    public static readonly RgbaColor Blue = new(0, 0, 255, 255);

    public static Guid Id(int n) => new(n, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    public static PixelImage Blank(int width, int height) => new(width, height, new byte[width * height * 4]);

    /// <summary>
    /// Every pixel's colour depends only on its position: R is x, G is y (low bytes), B tells the 256-pixel blocks apart
    /// (16 per block column plus the block row), A is 255. So a crop is checked against hand-typed values.
    /// </summary>
    public static PixelImage Positional(int width, int height)
    {
        var bytes = new byte[width * height * 4];
        var at = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                bytes[at++] = (byte)((((x >> 8) & 0x0F) * 16) + ((y >> 8) & 0x0F));
                bytes[at++] = (byte)y;
                bytes[at++] = (byte)x;
                bytes[at++] = 255;
            }
        }

        return new PixelImage(width, height, bytes);
    }

    /// <summary>
    /// A checkerboard of two pixels that average (per channel B, G, R, A) to exactly 50, 100, 20, 150:
    /// (x + y) even = 100, 200, 40, 200 and odd = 0, 0, 0, 100. Any cell with an even pixel count averages to that.
    /// </summary>
    public static PixelImage Checker(int width, int height)
    {
        var bytes = new byte[width * height * 4];
        var at = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var even = (x + y) % 2 == 0;
                bytes[at++] = (byte)(even ? 100 : 0);
                bytes[at++] = (byte)(even ? 200 : 0);
                bytes[at++] = (byte)(even ? 40 : 0);
                bytes[at++] = (byte)(even ? 200 : 100);
            }
        }

        return new PixelImage(width, height, bytes);
    }

    /// <summary>The four bytes of one pixel: B, G, R, A.</summary>
    public static byte[] PixelAt(PixelImage image, int x, int y)
    {
        var at = ((y * image.Width) + x) * 4;
        return [image.Bgra[at], image.Bgra[at + 1], image.Bgra[at + 2], image.Bgra[at + 3]];
    }

    public static RectangleAnnotation Rect(int id, int x, int y, int width, int height, int thickness = 4) =>
        new(Id(id), Red, thickness, new PixelRect(x, y, width, height));

    public static StepAnnotation Step(int id, int number, int cx = 20, int cy = 20) =>
        new(Id(id), Red, 4, new PixelPoint(cx, cy), number, 18);

    public static AppSettings Settings(ImageFormat format = ImageFormat.Png, int jpgQuality = 90, string folder = SaveFolder) =>
        CaptureData.Settings(AfterCaptureAction.ShowDialog, format, jpgQuality, 0, false, FullScreenScope.MonitorUnderCursor, folder);
}
