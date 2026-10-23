using System.ComponentModel;
using System.Diagnostics;
using Paper.ScreenWizzard.UseCases.Recorder.Ports;
using Paper.ScreenWizzard.UseCases.Shared.Models;

namespace Paper.ScreenWizzard.Infrastructure.Recorder;

/// <summary>Opens a file in the program Windows has for its type, or shows it selected in Explorer.</summary>
public sealed class FileLauncher : IFileLauncher
{
    public PortResult Open(string path) => Run(new ProcessStartInfo(path) { UseShellExecute = true });

    public PortResult ShowInFolder(string path) =>
        Run(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = true });

    private static PortResult Run(ProcessStartInfo start)
    {
        try
        {
            using var process = Process.Start(start);
            return PortResult.Ok;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            return PortResult.Fail(exception.Message);
        }
    }
}
