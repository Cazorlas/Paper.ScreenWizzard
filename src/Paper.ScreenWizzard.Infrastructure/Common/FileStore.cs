using Paper.ScreenWizzard.UseCases.Common.Models;
using Paper.ScreenWizzard.UseCases.Common.Ports;

namespace Paper.ScreenWizzard.Infrastructure.Common;

/// <summary>
/// System.IO behind the file port. It decides nothing: no folder is created for a write, nothing is renamed to avoid a clash. Every failure
/// is <c>PortResult.Fail(path + ": " + the system's reason)</c>, so a message can name the file the reason belongs to.
/// </summary>
public sealed class FileStore : IFileStorePort
{
    public bool DirectoryExists(string path)
    {
        try
        {
            return Directory.Exists(path);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public PortResult CreateDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            return PortResult.Ok;
        }
        catch (Exception exception)
        {
            return Failed(path, exception);
        }
    }

    public bool FileExists(string path)
    {
        try
        {
            return File.Exists(path);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public PortResult WriteAllBytes(string path, byte[] bytes)
    {
        try
        {
            File.WriteAllBytes(path, bytes);
            return PortResult.Ok;
        }
        catch (Exception exception)
        {
            return Failed(path, exception);
        }
    }

    public PortBytesResult ReadAllBytes(string path)
    {
        try
        {
            return new PortBytesResult(true, File.ReadAllBytes(path), null);
        }
        catch (Exception exception)
        {
            return new PortBytesResult(false, null, path + ": " + exception.Message);
        }
    }

    public DateTime? GetLastWriteTimeUtc(string path)
    {
        try
        {
            return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static PortResult Failed(string path, Exception exception) => PortResult.Fail(path + ": " + exception.Message);
}
