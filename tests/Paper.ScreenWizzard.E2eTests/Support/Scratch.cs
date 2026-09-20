namespace Paper.ScreenWizzard.E2eTests.Support;

/// <summary>A throw-away folder under the temp folder, deleted on dispose. The tests never touch the user's real data folder.</summary>
public sealed class ScratchFolder : IDisposable
{
    public ScratchFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PaperScreenWizzardE2E", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(Path, true);
        }
        catch (Exception)
        {
            // A leftover temp folder is harmless.
        }
    }
}
