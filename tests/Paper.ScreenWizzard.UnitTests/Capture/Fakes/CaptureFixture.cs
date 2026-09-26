using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UseCases.Capture.Models;
using Paper.ScreenWizzard.UseCases.Capture.Ports;
using Paper.ScreenWizzard.UseCases.Capture.UseCases;
using Paper.ScreenWizzard.UseCases.Shared.UseCases;

namespace Paper.ScreenWizzard.UnitTests.Capture.Fakes;

/// <summary>The capture interactor wired to plain-data fakes. One interactor per fixture: it keeps the "one session at a time" state.</summary>
public sealed class CaptureFixture
{
    private CaptureInteractor? _interactor;

    public CaptureFixture()
    {
        Screen = new FakeScreenSource(Events);
        Delay = new FakeDelayPort(Events);
        Monitors.Monitors.AddRange(CaptureData.SingleMonitor);
    }

    /// <summary>Everything that happened, in order: countdown reports, waits, screen reads.</summary>
    public List<string> Events { get; } = [];

    public FakeScreenSource Screen { get; }

    public FakeWindowCatalog Windows { get; } = new();

    public FakeMonitorCatalog Monitors { get; } = new();

    public FakeDelayPort Delay { get; }

    public FakeClipboard Clipboard { get; } = new();

    public FakeFileStore Files { get; } = new();

    public FakeCodec Codec { get; } = new();

    public FakeClock Clock { get; } = new();

    public FakeLog Log { get; } = new();

    public CaptureInteractor Interactor => _interactor ??= Create();

    public ImageDelivery Delivery() => new(Clock, Files, Codec, Clipboard);

    /// <summary>Replaces the monitors of the desktop.</summary>
    public void UseMonitors(IEnumerable<MonitorInfo> monitors)
    {
        Monitors.Monitors.Clear();
        Monitors.Monitors.AddRange(monitors);
    }

    /// <summary>Takes the snapshot and returns the session; the test fails here, at an assertion, when there is none.</summary>
    public async Task<ICaptureSession> BeginAsync(CaptureRequest request)
    {
        var result = await Interactor.BeginAsync(request, null, CancellationToken.None);
        Assert.That(result.Session, Is.Not.Null, "BeginAsync gave no session (issue " + result.Issue + ")");
        return result.Session!;
    }

    private CaptureInteractor Create() => new(Screen, Windows, Monitors, Delay, Delivery(), Clock, Files, Log);
}

/// <summary>Numbers and shapes written out from SPEC capture, so a test never asks the code for its own expected value.</summary>
public static class CaptureData
{
    public const string SaveFolder = @"C:\Users\Hung\Pictures\Paper.ScreenWizzard";

    public static MonitorInfo Monitor(int index, int x, int y, int width, int height, bool primary, int dpi = 96) =>
        new(index, new PixelRect(x, y, width, height), primary, dpi);

    /// <summary>One 1920 x 1080 monitor at the origin.</summary>
    public static IReadOnlyList<MonitorInfo> SingleMonitor { get; } = [Monitor(0, 0, 0, 1920, 1080, true)];

    /// <summary>Two 1920 x 1080 monitors side by side: x 0..1920 and 1920..3840.</summary>
    public static IReadOnlyList<MonitorInfo> SideBySide { get; } =
        [Monitor(0, 0, 0, 1920, 1080, true), Monitor(1, 1920, 0, 1920, 1080, false)];

    /// <summary>Two 1920 x 1080 monitors, the second one left of the primary: x -1920..0 and 0..1920.</summary>
    public static IReadOnlyList<MonitorInfo> LeftAndPrimary { get; } =
        [Monitor(0, 0, 0, 1920, 1080, true), Monitor(1, -1920, 0, 1920, 1080, false)];

    public static WindowInfo Window(
        long handle,
        string title,
        PixelRect frame,
        int zOrder,
        bool visible = true,
        bool minimized = false,
        bool cloaked = false,
        bool overlay = false,
        bool desktop = false) =>
        new(handle, title, frame, visible, minimized, cloaked, overlay, zOrder, desktop);

    public static CaptureRequest Request(
        CaptureKind kind,
        int delaySeconds = 0,
        bool includeCursor = false,
        FullScreenScope scope = FullScreenScope.MonitorUnderCursor) =>
        new(kind, delaySeconds, includeCursor, scope);

    public static AppSettings Settings(
        AfterCaptureAction after = AfterCaptureAction.ShowDialog,
        ImageFormat format = ImageFormat.Png,
        int jpgQuality = 90,
        int delaySeconds = 0,
        bool includeCursor = false,
        FullScreenScope scope = FullScreenScope.MonitorUnderCursor,
        string folder = SaveFolder) =>
        new(
            new Dictionary<CaptureKind, HotkeyChord>(),
            after,
            folder,
            format,
            jpgQuality,
            delaySeconds,
            includeCursor,
            scope,
            false,
            AppLanguage.System,
            AppTheme.System,
            null);

    /// <summary>A plain image whose bytes are all different from each other (a ramp), so a copy that changes anything shows.</summary>
    public static PixelImage Ramp(int width, int height)
    {
        var bytes = new byte[width * height * 4];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)((i * 7) + 3);
        }

        return new PixelImage(width, height, bytes);
    }

    /// <summary>The first pixel of <paramref name="image"/> that differs from the screen at (x0, y0), or -1 when all match.</summary>
    public static int FirstPixelDifferingFromScreen(PixelImage image, int x0, int y0)
    {
        if (image.Bgra.Length != image.Width * image.Height * 4)
        {
            return -2;
        }

        for (var row = 0; row < image.Height; row++)
        {
            for (var column = 0; column < image.Width; column++)
            {
                var (b, g, r, a) = FakeScreenSource.ColorAt(x0 + column, y0 + row);
                var at = ((row * image.Width) + column) * 4;
                if (image.Bgra[at] != b || image.Bgra[at + 1] != g || image.Bgra[at + 2] != r || image.Bgra[at + 3] != a)
                {
                    return (row * image.Width) + column;
                }
            }
        }

        return -1;
    }

    /// <summary>The four bytes of one pixel: B, G, R, A.</summary>
    public static byte[] PixelAt(PixelImage image, int x, int y)
    {
        var at = ((y * image.Width) + x) * 4;
        return [image.Bgra[at], image.Bgra[at + 1], image.Bgra[at + 2], image.Bgra[at + 3]];
    }
}
