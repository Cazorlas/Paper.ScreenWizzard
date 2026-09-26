using Microsoft.Win32;
using Paper.ScreenWizzard.UseCases.Shared.Models;
using Paper.ScreenWizzard.UseCases.Shell.Ports;

namespace Paper.ScreenWizzard.Infrastructure.Shell;

/// <summary>
/// The "start with Windows" entry of the current user: a string value under
/// <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>, named <c>valueName</c>, holding the quoted path of the exe and
/// <c>--autostart</c>. The current user's hive needs no elevation. A Run value is a command line of at most 260 characters (the Run key
/// page), so a longer one is refused instead of being written where Windows would ignore it. The name, the exe and the key are
/// constructor parameters so a test uses throw-away values and never the real entry.
/// </summary>
public sealed class AutostartService : IAutostartPort
{
    public const string DefaultRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string AutostartFlag = "--autostart";
    private const int MaxCommandLength = 260;

    private readonly string _valueName;
    private readonly string _exePath;
    private readonly string _runKeyPath;

    public AutostartService(string valueName = "Paper.ScreenWizzard", string? exePath = null, string? runKeyPath = null)
    {
        _valueName = valueName;
        _exePath = exePath ?? Environment.ProcessPath ?? string.Empty;
        _runKeyPath = runKeyPath ?? DefaultRunKeyPath;
    }

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(_runKeyPath);
            return key?.GetValue(_valueName) is string { Length: > 0 };
        }
        catch (Exception)
        {
            return false;
        }
    }

    public PortResult SetEnabled(bool enabled)
    {
        try
        {
            return enabled ? Write() : Remove();
        }
        catch (Exception exception)
        {
            return PortResult.Fail($@"HKCU\{_runKeyPath}\{_valueName}: {exception.Message}");
        }
    }

    private PortResult Write()
    {
        if (string.IsNullOrWhiteSpace(_exePath))
        {
            return PortResult.Fail("The path of the program is not known, so there is nothing to start with Windows");
        }

        var command = $"\"{_exePath}\" {AutostartFlag}";
        if (command.Length > MaxCommandLength)
        {
            return PortResult.Fail($"The command line is {command.Length} characters and a Windows Run entry holds at most {MaxCommandLength}: {command}");
        }

        using var key = Registry.CurrentUser.CreateSubKey(_runKeyPath, writable: true)
            ?? throw new InvalidOperationException("the registry key could not be created or opened");
        key.SetValue(_valueName, command, RegistryValueKind.String);
        return PortResult.Ok;
    }

    private PortResult Remove()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_runKeyPath, writable: true);

        // No key means no entry: there is nothing to remove, which is what the caller asked for.
        key?.DeleteValue(_valueName, throwOnMissingValue: false);
        return PortResult.Ok;
    }
}
