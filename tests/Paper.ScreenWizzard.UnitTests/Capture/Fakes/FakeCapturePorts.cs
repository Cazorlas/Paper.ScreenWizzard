using System.Text;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Common;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.UseCases.Capture.Ports;
using Paper.ScreenWizzard.UseCases.Common.Models;
using Paper.ScreenWizzard.UseCases.Common.Ports;
using Paper.ScreenWizzard.UseCases.Shell.Ports;

namespace Paper.ScreenWizzard.UnitTests.Capture.Fakes;

/// <summary>
/// A screen whose every pixel has a colour that depends only on its virtual-desktop position, so a cropped image can be
/// checked pixel by pixel against where it came from. It renders whatever area it is asked for, the way the real adapter would.
/// </summary>
public sealed class FakeScreenSource : IScreenSourcePort
{
    private readonly List<string> _events;

    public FakeScreenSource(List<string> events) => _events = events;

    public List<(PixelRect Area, bool IncludeCursor)> Calls { get; } = [];

    /// <summary>When set, this is what every read gives back instead of the rendered screen.</summary>
    public ScreenCaptureResult? Override { get; set; }

    /// <summary>The colour of the screen at one desktop pixel. R is x, G is y (low bytes); B tells the 256-pixel block apart.</summary>
    public static (byte B, byte G, byte R, byte A) ColorAt(int x, int y) =>
        ((byte)((((x >> 8) & 0x0F) * 16) + ((y >> 8) & 0x0F)), (byte)y, (byte)x, 255);

    public ScreenCaptureResult Capture(PixelRect area, bool includeCursor)
    {
        Calls.Add((area, includeCursor));
        _events.Add("capture");
        if (Override is not null)
        {
            return Override;
        }

        var bytes = new byte[area.Width * area.Height * 4];
        var offset = 0;
        for (var row = 0; row < area.Height; row++)
        {
            for (var column = 0; column < area.Width; column++)
            {
                var (b, g, r, a) = ColorAt(area.X + column, area.Y + row);
                bytes[offset++] = b;
                bytes[offset++] = g;
                bytes[offset++] = r;
                bytes[offset++] = a;
            }
        }

        return new ScreenCaptureResult(new PixelImage(area.Width, area.Height, bytes), ScreenCaptureIssue.None);
    }
}

public sealed class FakeWindowCatalog : IWindowCatalogPort
{
    public List<WindowInfo> Windows { get; } = [];

    public IReadOnlyList<WindowInfo> GetWindows() => Windows.ToList();
}

public sealed class FakeMonitorCatalog : IMonitorCatalogPort
{
    public List<MonitorInfo> Monitors { get; } = [];

    public PixelPoint Cursor { get; set; }

    public string Signature { get; set; } = "layout-1";

    public IReadOnlyList<MonitorInfo> GetMonitors() => Monitors.ToList();

    public PixelPoint GetCursorPosition() => Cursor;

    public string GetLayoutSignature() => Signature;
}

/// <summary>Immediate by default; in manual mode each wait stays pending until the test completes it.</summary>
public sealed class FakeDelayPort : IDelayPort
{
    private readonly List<string> _events;

    public FakeDelayPort(List<string> events) => _events = events;

    public bool Manual { get; set; }

    public List<TimeSpan> Durations { get; } = [];

    public List<TaskCompletionSource> Pending { get; } = [];

    public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
    {
        Durations.Add(duration);
        _events.Add($"delay {duration.TotalSeconds:0}s");
        if (!Manual)
        {
            return Task.CompletedTask;
        }

        var wait = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Pending.Add(wait);
        return wait.Task;
    }

    public void CompleteAll()
    {
        foreach (var wait in Pending.ToList())
        {
            wait.TrySetResult();
        }

        Pending.Clear();
    }
}

/// <summary>A synchronous progress sink (Progress&lt;T&gt; would post to the thread pool and reorder the events).</summary>
public sealed class FakeProgress : IProgress<int>
{
    private readonly List<string> _events;

    public FakeProgress(List<string> events) => _events = events;

    public List<int> Reports { get; } = [];

    public void Report(int value)
    {
        Reports.Add(value);
        _events.Add($"report {value}");
    }
}

public sealed class FakeClipboard : IClipboardPort
{
    public PortResult SetResult { get; set; } = PortResult.Ok;

    public List<PixelImage> SetCalls { get; } = [];

    public PortResult SetImage(PixelImage image)
    {
        SetCalls.Add(image with { Bgra = image.Bgra.ToArray() });
        return SetResult;
    }

    public ClipboardImageResult GetImage() => new(false, null, "not used by capture");
}

/// <summary>Folders as a set of paths and files as a dictionary; a write into a folder that is not there fails like the disk does.</summary>
public sealed class FakeFileStore : IFileStorePort
{
    public HashSet<string> Directories { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, byte[]> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> CreateDirectoryCalls { get; } = [];

    public List<string> WrittenPaths { get; } = [];

    /// <summary>When set, every write fails with this reason (a read-only folder).</summary>
    public string? WriteFailure { get; set; }

    public bool DirectoryExists(string path) => Directories.Contains(path);

    public PortResult CreateDirectory(string path)
    {
        CreateDirectoryCalls.Add(path);
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
        return PortResult.Ok;
    }

    public PortBytesResult ReadAllBytes(string path) =>
        Files.TryGetValue(path, out var bytes) ? new PortBytesResult(true, bytes, null) : new PortBytesResult(false, null, "missing");

    public DateTime? GetLastWriteTimeUtc(string path) => Files.ContainsKey(path) ? DateTime.UnixEpoch : null;
}

/// <summary>One call to Encode, with a private copy of the pixels the codec was handed.</summary>
public sealed record EncodeCall(int Width, int Height, byte[] Bgra, ImageFormat Format, int Quality, byte[] Result);

/// <summary>A codec that does not compress: it records what it was given and answers with marker bytes.</summary>
public sealed class FakeCodec : IImageCodecPort
{
    public List<EncodeCall> Calls { get; } = [];

    /// <summary>When set, Encode throws it: a picture too big for the memory or for the format.</summary>
    public Exception? EncodeFailure { get; set; }

    public ImageDecodeResult Decode(byte[] fileBytes) => new(null, ImageDecodeIssue.NotAnImage);

    public byte[] Encode(PixelImage image, ImageFormat format, int jpgQuality)
    {
        if (EncodeFailure is not null)
        {
            throw EncodeFailure;
        }

        var result = Encoding.ASCII.GetBytes($"{format}:{image.Width}x{image.Height}:#{Calls.Count + 1}");
        Calls.Add(new EncodeCall(image.Width, image.Height, image.Bgra.ToArray(), format, jpgQuality, result));
        return result;
    }
}

public sealed class FakeClock : IClockPort
{
    public DateTime Now { get; set; } = new(2026, 9, 20, 14, 3, 5);
}

public sealed class FakeLog : ILogPort
{
    public List<string> Lines { get; } = [];

    public void Info(string message) => Lines.Add("I " + message);

    public void Warning(string message) => Lines.Add("W " + message);

    public void Error(string message, Exception? exception) => Lines.Add("E " + message);
}
