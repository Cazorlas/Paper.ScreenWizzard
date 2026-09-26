using Paper.ScreenWizzard.UseCases.Shared.Models;

namespace Paper.ScreenWizzard.UseCases.Shell.Ports;

/// <summary>The "start with Windows" entry of the current user.</summary>
public interface IAutostartPort
{
    bool IsEnabled();

    PortResult SetEnabled(bool enabled);
}
