using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;

namespace Paper.ScreenWizzard.UseCases.Capture.Ports;

/// <summary>The monitors and the pointer, as plain data.</summary>
public interface IMonitorCatalog
{
    IReadOnlyList<MonitorInfo> GetMonitors();

    PixelPoint GetCursorPosition();

    /// <summary>A string that changes whenever a monitor is added, removed or resized (SPEC capture F6).</summary>
    string GetLayoutSignature();
}
