using System.ComponentModel;
using System.Diagnostics;
using Paper.ScreenWizzard.UseCases.Shared.Models;
using Paper.ScreenWizzard.UseCases.Shell.Ports;

namespace Paper.ScreenWizzard.Infrastructure.Shell;

/// <summary>Opens a page in the browser Windows has for https links (shell execute of the address).</summary>
public sealed class Browser : IBrowser
{
    public PortResult Open(string url)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return PortResult.Ok;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            return PortResult.Fail(exception.Message);
        }
    }
}
