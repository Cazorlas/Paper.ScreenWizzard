using Paper.ScreenWizzard.Domain.Capture;

namespace Paper.ScreenWizzard.UseCases.Capture.Ports;

/// <summary>The top-level windows at this instant, topmost first, each with its visible frame (no invisible shadow).</summary>
public interface IWindowCatalog
{
    IReadOnlyList<WindowInfo> GetWindows();
}
