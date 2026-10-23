namespace Paper.ScreenWizzard.UseCases.Recorder.Ports;

/// <summary>Whether a window is still there.</summary>
public interface IWindowPresence
{
    bool IsOpen(long windowHandle);
}
