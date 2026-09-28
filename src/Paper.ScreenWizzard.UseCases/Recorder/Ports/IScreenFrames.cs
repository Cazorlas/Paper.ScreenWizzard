using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.UseCases.Recorder.Models;
using Paper.ScreenWizzard.UseCases.Shared.Models;

namespace Paper.ScreenWizzard.UseCases.Recorder.Ports;

/// <summary>Pictures of one fixed rectangle of the desktop, as the screen changes. The app's own windows are never in them.</summary>
public interface IScreenFrames
{
    /// <summary>Starts taking pictures of <paramref name="area"/>; a refusal (a driver that does not allow it) is F6.</summary>
    PortResult Open(PixelRect area, bool pointer);

    /// <summary>The next picture, or <see cref="ScreenFrameIssue.NoNewFrame"/> when nothing changed within <paramref name="wait"/>.</summary>
    ScreenFrame Next(TimeSpan wait);

    void Close();
}
