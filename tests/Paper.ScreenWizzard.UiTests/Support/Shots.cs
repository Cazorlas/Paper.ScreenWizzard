using System.IO;
namespace Paper.ScreenWizzard.UiTests.Support;

/// <summary>Where the screenshots of the ui lane go: a git-ignored folder inside the test project.</summary>
public static class Shots
{
    public static string Folder { get; } = ResolveFolder();

    public static string PathOf(string name) => Path.Combine(Folder, name + ".png");

    private static string ResolveFolder()
    {
        // Walk up from bin/Debug/... to the repository root (the folder that holds the solution) so the folder is the same
        // wherever the tests were started from.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Paper.ScreenWizzard.slnx")))
        {
            directory = directory.Parent;
        }

        var root = directory?.FullName ?? AppContext.BaseDirectory;
        var folder = Path.Combine(root, "tests", "Paper.ScreenWizzard.UiTests", "shots");
        Directory.CreateDirectory(folder);
        return folder;
    }
}
