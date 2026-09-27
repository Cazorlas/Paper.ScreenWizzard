using Paper.ScreenWizzard.UseCases.Shared.Models;

namespace Paper.ScreenWizzard.UseCases.Recorder.Ports;

/// <summary>Opens a saved file in the program Windows has for it, or shows it selected in its folder.</summary>
public interface IFileLauncher
{
    PortResult Open(string path);

    PortResult ShowInFolder(string path);
}
