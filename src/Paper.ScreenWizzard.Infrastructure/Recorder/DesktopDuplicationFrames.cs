using System.Runtime.InteropServices;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.UseCases.Recorder.Models;
using Paper.ScreenWizzard.UseCases.Recorder.Ports;
using Paper.ScreenWizzard.UseCases.Shared.Models;
using SharpGen.Runtime;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace Paper.ScreenWizzard.Infrastructure.Recorder;

/// <summary>
/// Pictures of one rectangle of the desktop through DXGI Desktop Duplication (ADR 0004): one duplication for each monitor the rectangle
/// touches, each frame copied to a CPU texture and its part of the rectangle put into one BGRA picture, so a region across two monitors
/// is one picture with no gap. A monitor that sent nothing new keeps its last part. Windows leaves out the app's own windows
/// (<c>WDA_EXCLUDEFROMCAPTURE</c>). The pointer is drawn from the cursor Windows shows (<see cref="PointerPainter"/>), when asked.
/// A lost duplication (a mode change, a monitor unplugged, the secure desktop) is "the display changed" (SPEC recorder F2).
/// </summary>
public sealed class DesktopDuplicationFrames : IScreenFrames, IDisposable
{
    private readonly IMonotonicClock _clock;
    private readonly List<Source> _sources = [];
    private PixelRect _area;
    private bool _pointer;
    private byte[] _picture = [];

    public DesktopDuplicationFrames(IMonotonicClock clock)
    {
        _clock = clock;
    }

    public PortResult Open(PixelRect area, bool pointer)
    {
        Close();
        _area = area;
        _pointer = pointer;
        _picture = new byte[area.Width * area.Height * 4];
        try
        {
            using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
            for (uint a = 0; factory.EnumAdapters1(a, out var adapter).Success; a++)
            {
                using (adapter)
                {
                    for (uint o = 0; adapter.EnumOutputs(o, out var output).Success; o++)
                    {
                        using (output)
                        {
                            var desktop = output.Description.DesktopCoordinates;
                            var bounds = new PixelRect(desktop.Left, desktop.Top, desktop.Right - desktop.Left, desktop.Bottom - desktop.Top);
                            var part = Intersect(area, bounds);
                            if (part.Width > 0 && part.Height > 0)
                            {
                                _sources.Add(Source.Create(adapter, output, bounds, part));
                            }
                        }
                    }
                }
            }
        }
        catch (SharpGenException exception)
        {
            Close();
            return PortResult.Fail("0x" + exception.HResult.ToString("X8", System.Globalization.CultureInfo.InvariantCulture) + ": " + exception.Message);
        }

        if (_sources.Count == 0)
        {
            return PortResult.Fail("no monitor shows this area");
        }

        return PortResult.Ok;
    }

    public ScreenFrame Next(TimeSpan wait)
    {
        var changed = false;
        for (var i = 0; i < _sources.Count; i++)
        {
            var source = _sources[i];
            var timeout = i == 0 ? (uint)Math.Clamp(wait.TotalMilliseconds, 0, 1000) : 0u;
            var result = source.Duplication.AcquireNextFrame(timeout, out var info, out var resource);
            if (result.Code == Vortice.DXGI.ResultCode.WaitTimeout.Code)
            {
                continue;
            }

            if (result.Failure)
            {
                resource?.Dispose();
                return new ScreenFrame(null, _clock.Now, ScreenFrameIssue.DisplayChanged, result.Code == Vortice.DXGI.ResultCode.AccessLost.Code
                    ? "the monitor's picture was lost (mode change, unplugged, or the secure desktop)"
                    : "0x" + result.Code.ToString("X8", System.Globalization.CultureInfo.InvariantCulture));
            }

            try
            {
                // A frame with nothing presented is only a pointer move; the picture of the screen did not change.
                if (info.LastPresentTime != 0 && resource is not null)
                {
                    using var texture = resource.QueryInterface<ID3D11Texture2D>();
                    source.Context.CopyResource(source.Staging, texture);
                    CopyPart(source);
                }

                changed = true;
            }
            finally
            {
                resource?.Dispose();
                source.Duplication.ReleaseFrame();
            }
        }

        if (!changed)
        {
            return new ScreenFrame(null, _clock.Now, ScreenFrameIssue.NoNewFrame);
        }

        var at = _clock.Now;
        var copy = (byte[])_picture.Clone();
        if (_pointer)
        {
            PointerPainter.Paint(copy, _area);
        }

        return new ScreenFrame(new PixelImage(_area.Width, _area.Height, copy), at, ScreenFrameIssue.None);
    }

    public void Close()
    {
        foreach (var source in _sources)
        {
            source.Dispose();
        }

        _sources.Clear();
    }

    public void Dispose() => Close();

    // The part of this monitor inside the area goes to its place in the picture.
    private void CopyPart(Source source)
    {
        var mapped = source.Context.Map(source.Staging, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
        try
        {
            var part = source.Part;
            var fromX = part.X - source.Bounds.X;
            var fromY = part.Y - source.Bounds.Y;
            var toX = part.X - _area.X;
            var toY = part.Y - _area.Y;
            var rowBytes = part.Width * 4;
            for (var row = 0; row < part.Height; row++)
            {
                var from = mapped.DataPointer + ((fromY + row) * (int)mapped.RowPitch) + (fromX * 4);
                Marshal.Copy(from, _picture, ((toY + row) * _area.Width * 4) + (toX * 4), rowBytes);
            }
        }
        finally
        {
            source.Context.Unmap(source.Staging, 0);
        }
    }

    private static PixelRect Intersect(PixelRect a, PixelRect b)
    {
        var left = Math.Max(a.X, b.X);
        var top = Math.Max(a.Y, b.Y);
        var right = Math.Min(a.X + a.Width, b.X + b.Width);
        var bottom = Math.Min(a.Y + a.Height, b.Y + b.Height);
        return right <= left || bottom <= top ? default : new PixelRect(left, top, right - left, bottom - top);
    }

    /// <summary>One monitor: its duplication, a device on its adapter, and the CPU texture its frames are read through.</summary>
    private sealed class Source : IDisposable
    {
        private Source(ID3D11Device device, ID3D11DeviceContext context, IDXGIOutputDuplication duplication, ID3D11Texture2D staging, PixelRect bounds, PixelRect part)
        {
            Device = device;
            Context = context;
            Duplication = duplication;
            Staging = staging;
            Bounds = bounds;
            Part = part;
        }

        public ID3D11Device Device { get; }

        public ID3D11DeviceContext Context { get; }

        public IDXGIOutputDuplication Duplication { get; }

        public ID3D11Texture2D Staging { get; }

        public PixelRect Bounds { get; }

        public PixelRect Part { get; }

        public static Source Create(IDXGIAdapter1 adapter, IDXGIOutput output, PixelRect bounds, PixelRect part)
        {
            D3D11.D3D11CreateDevice(
                adapter,
                DriverType.Unknown,
                DeviceCreationFlags.BgraSupport,
                new[] { FeatureLevel.Level_11_0, FeatureLevel.Level_10_1, FeatureLevel.Level_10_0 },
                out ID3D11Device device,
                out ID3D11DeviceContext context).CheckError();
            using var output1 = output.QueryInterface<IDXGIOutput1>();
            var duplication = output1.DuplicateOutput(device);
            var staging = device.CreateTexture2D(new Texture2DDescription
            {
                Width = (uint)bounds.Width,
                Height = (uint)bounds.Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Staging,
                BindFlags = BindFlags.None,
                CPUAccessFlags = CpuAccessFlags.Read,
                MiscFlags = ResourceOptionFlags.None,
            });
            return new Source(device, context, duplication, staging, bounds, part);
        }

        public void Dispose()
        {
            Staging.Dispose();
            Duplication.Dispose();
            Context.Dispose();
            Device.Dispose();
        }
    }
}
