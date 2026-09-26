using Paper.ScreenWizzard.UseCases.Shared.Models;

namespace Paper.ScreenWizzard.UseCases.Shell.Ports;

/// <summary>Opens a web page in the user's browser.</summary>
public interface IBrowser
{
    PortResult Open(string url);
}
