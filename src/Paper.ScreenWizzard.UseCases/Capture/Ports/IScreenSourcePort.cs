using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.UseCases.Capture.Models;

namespace Paper.ScreenWizzard.UseCases.Capture.Ports;

/// <summary>Reads the pixels of the virtual desktop, physical pixels, in one go.</summary>
public interface IScreenSourcePort
{
    /// <summary>Captures <paramref name="area"/> of the virtual desktop; the pointer is drawn into it when asked.</summary>
    ScreenCaptureResult Capture(PixelRect area, bool includeCursor);
}
