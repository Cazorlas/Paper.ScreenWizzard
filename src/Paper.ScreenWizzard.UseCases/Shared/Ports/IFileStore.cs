using Paper.ScreenWizzard.UseCases.Shared.Models;

namespace Paper.ScreenWizzard.UseCases.Shared.Ports;

public interface IFileStore
{
    bool DirectoryExists(string path);

    PortResult CreateDirectory(string path);

    bool FileExists(string path);

    PortResult WriteAllBytes(string path, byte[] bytes);

    PortBytesResult ReadAllBytes(string path);

    /// <summary>The last write time in UTC, or null when the file does not exist.</summary>
    DateTime? GetLastWriteTimeUtc(string path);
}
