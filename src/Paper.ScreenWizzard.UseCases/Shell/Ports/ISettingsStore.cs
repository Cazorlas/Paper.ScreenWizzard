using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UseCases.Shared.Models;
using Paper.ScreenWizzard.UseCases.Shell.Models;

namespace Paper.ScreenWizzard.UseCases.Shell.Ports;

/// <summary>Reads and writes the one settings document under the user's application data folder.</summary>
public interface ISettingsStore
{
    SettingsLoadResult Load();

    /// <summary>Copies the file over <c>settings.json.bak</c>: the use case found a value outside its range (SPEC shell F1).</summary>
    PortResult KeepAsBackup();

    /// <summary>Writes the document; fails with the system's reason when the folder is read-only or full (SPEC shell F2).</summary>
    PortResult Save(AppSettings settings);
}
