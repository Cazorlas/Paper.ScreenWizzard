using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UseCases.Shared.Models;
using Paper.ScreenWizzard.UseCases.Shared.Ports;
using Paper.ScreenWizzard.UseCases.Shell.Models;
using Paper.ScreenWizzard.UseCases.Shell.Ports;

namespace Paper.ScreenWizzard.UnitTests.Shell.Fakes;

/// <summary>An in-memory settings file: a successful Save is what the next Load reads, as the real file would be.</summary>
public sealed class FakeSettingsStore : ISettingsStore
{
    public SettingsLoadResult LoadResult { get; set; } = new(null, SettingsLoadStatus.Missing, null);

    public PortResult SaveResult { get; set; } = PortResult.Ok;

    public int LoadCalls { get; private set; }

    public List<AppSettings> Saved { get; } = [];

    public PortResult BackupResult { get; set; } = PortResult.Ok;

    public int BackupCalls { get; private set; }

    public SettingsLoadResult Load()
    {
        LoadCalls++;
        return LoadResult;
    }

    public PortResult Save(AppSettings settings)
    {
        Saved.Add(settings);
        if (SaveResult.Success)
        {
            LoadResult = new SettingsLoadResult(StoredSettings.From(settings), SettingsLoadStatus.Loaded, null);
        }

        return SaveResult;
    }

    public PortResult KeepAsBackup()
    {
        BackupCalls++;
        return BackupResult;
    }
}

/// <summary>Registered chords per kind, with a set of chords "another program" holds.</summary>
public sealed class FakeHotkeys : IHotkeys
{
    private readonly Dictionary<CaptureKind, HotkeyChord> _registered = [];

    public IReadOnlyDictionary<CaptureKind, HotkeyChord> Registered => _registered;

    public HashSet<HotkeyChord> HeldByOtherPrograms { get; } = [];

    public List<(CaptureKind Kind, HotkeyChord Chord)> RegisterCalls { get; } = [];

    public event Action<CaptureKind>? Pressed;

    public PortResult Register(CaptureKind kind, HotkeyChord chord)
    {
        RegisterCalls.Add((kind, chord));
        if (HeldByOtherPrograms.Contains(chord))
        {
            return PortResult.Fail("Hot key is already registered by another program.");
        }

        _registered[kind] = chord;
        return PortResult.Ok;
    }

    public void Unregister(CaptureKind kind) => _registered.Remove(kind);

    /// <summary>The user presses a chord: every kind registered on it fires.</summary>
    public void Press(HotkeyChord chord)
    {
        foreach (var pair in _registered.Where(p => p.Value.Equals(chord)).ToList())
        {
            Pressed?.Invoke(pair.Key);
        }
    }
}

public sealed class FakeAutostart : IAutostart
{
    public bool Enabled { get; set; }

    public PortResult Result { get; set; } = PortResult.Ok;

    public List<bool> SetEnabledCalls { get; } = [];

    public bool IsEnabled() => Enabled;

    public PortResult SetEnabled(bool enabled)
    {
        SetEnabledCalls.Add(enabled);
        if (Result.Success)
        {
            Enabled = enabled;
        }

        return Result;
    }
}

/// <summary>Folders as a set of paths; only the members the shell uses do anything.</summary>
public sealed class FakeFileStore : IFileStore
{
    public HashSet<string> Directories { get; } = new(StringComparer.OrdinalIgnoreCase);

    public PortResult CreateResult { get; set; } = PortResult.Ok;

    public List<string> CreateDirectoryCalls { get; } = [];

    public bool DirectoryExists(string path) => Directories.Contains(path);

    public PortResult CreateDirectory(string path)
    {
        CreateDirectoryCalls.Add(path);
        if (CreateResult.Success)
        {
            Directories.Add(path);
        }

        return CreateResult;
    }

    public bool FileExists(string path) => false;

    public PortResult WriteAllBytes(string path, byte[] bytes) => PortResult.Ok;

    public PortBytesResult ReadAllBytes(string path) => new(false, null, "not used by the shell");

    public DateTime? GetLastWriteTimeUtc(string path) => null;
}

public sealed class FakeLog : ILog
{
    public List<string> Lines { get; } = [];

    public void Info(string message) => Lines.Add("I " + message);

    public void Warning(string message) => Lines.Add("W " + message);

    public void Error(string message, Exception? exception) => Lines.Add("E " + message);
}
