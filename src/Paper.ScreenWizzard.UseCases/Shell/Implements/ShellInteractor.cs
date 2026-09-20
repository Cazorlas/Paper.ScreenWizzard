using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UseCases.Common.Models;
using Paper.ScreenWizzard.UseCases.Common.Ports;
using Paper.ScreenWizzard.UseCases.Shell.Models;
using Paper.ScreenWizzard.UseCases.Shell.Ports;

namespace Paper.ScreenWizzard.UseCases.Shell.Implements;

/// <summary>Everything the shell decides (SPEC shell): startup, hotkey changes, saving settings, autostart, bar placement.</summary>
public sealed class ShellInteractor : IShellInteractor
{
    /// <summary>The gap between the capture bar and the corner of the primary monitor, in physical pixels.</summary>
    private const int BarMargin = 16;

    private readonly ISettingsStorePort _settingsStore;
    private readonly IHotkeyPort _hotkeys;
    private readonly IAutostartPort _autostart;
    private readonly IFileStorePort _files;
    private readonly ILogPort _log;

    public ShellInteractor(
        ISettingsStorePort settingsStore,
        IHotkeyPort hotkeys,
        IAutostartPort autostart,
        IFileStorePort files,
        ILogPort log)
    {
        _settingsStore = settingsStore;
        _hotkeys = hotkeys;
        _autostart = autostart;
        _files = files;
        _log = log;
    }

    public ShellStartResult Start(ShellStartInput input)
    {
        if (!input.IsFirstInstance)
        {
            var idle = CreateDefaultSettings(input.PicturesFolder);
            return new ShellStartResult(
                false,
                idle,
                ResolveLanguage(idle.Language, input.SystemCultureName),
                default,
                false,
                [],
                []);
        }

        var notices = new List<NotificationMessage>();
        var settings = LoadOrDefault(input, notices);

        var registered = new List<CaptureKind>();
        var unavailable = new List<string>();
        foreach (var kind in Enum.GetValues<CaptureKind>())
        {
            if (!settings.Hotkeys.TryGetValue(kind, out var chord))
            {
                continue;
            }

            var result = _hotkeys.Register(kind, chord);
            if (result.Success)
            {
                registered.Add(kind);
            }
            else
            {
                unavailable.Add(HotkeyRules.Format(chord));
                _log.Warning($"Hotkey {HotkeyRules.Format(chord)} for {kind} was not registered: {result.Detail}");
            }
        }

        if (unavailable.Count > 0)
        {
            notices.Add(NotificationMessage.Of("Shell.HotkeyUnavailableAtStart", string.Join(", ", unavailable)));
        }

        return new ShellStartResult(
            true,
            settings,
            ResolveLanguage(settings.Language, input.SystemCultureName),
            PlaceCaptureBar(settings.CaptureBarPosition, input.Monitors, input.CaptureBarSize),
            true,
            registered,
            notices);
    }

    public AppSettings CreateDefaultSettings(string picturesFolder) => SettingsDefaults.Create(picturesFolder);

    public HotkeyChangeResult ChangeHotkey(AppSettings current, CaptureKind kind, HotkeyChord proposed)
    {
        var text = HotkeyRules.Format(proposed);
        if (!HotkeyRules.IsSafe(proposed))
        {
            return Refuse(current, HotkeyIssue.NeedsModifier, null, NotificationMessage.Of("Shell.HotkeyNeedsModifier", text));
        }

        foreach (var other in current.Hotkeys)
        {
            if (other.Key != kind && HotkeyRules.AreSame(other.Value, proposed))
            {
                return Refuse(
                    current,
                    HotkeyIssue.UsedByOtherKind,
                    other.Key,
                    NotificationMessage.Of("Shell.HotkeyUsedByOtherKind", other.Key.ToString()));
            }
        }

        var registration = _hotkeys.Register(kind, proposed);
        if (!registration.Success)
        {
            _log.Warning($"Hotkey {text} for {kind} was refused: {registration.Detail}");
            return Refuse(current, HotkeyIssue.HeldByAnotherProgram, null, NotificationMessage.Of("Shell.HotkeyHeldByAnotherProgram", text));
        }

        var hotkeys = new Dictionary<CaptureKind, HotkeyChord>(current.Hotkeys) { [kind] = proposed };
        var changed = current with { Hotkeys = hotkeys };

        var saved = _settingsStore.Save(changed);
        NotificationMessage? message = saved.Success ? null : NotSaved(saved);
        return new HotkeyChangeResult(true, HotkeyIssue.None, null, changed, message);
    }

    public SettingsApplyResult Apply(AppSettings settings)
    {
        var saved = _settingsStore.Save(settings);
        return saved.Success
            ? new SettingsApplyResult(true, settings, null)
            : new SettingsApplyResult(false, settings, NotSaved(saved));
    }

    public AutostartChangeResult SetAutostart(AppSettings current, bool enabled)
    {
        var result = _autostart.SetEnabled(enabled);
        if (result.Success)
        {
            return new AutostartChangeResult(enabled, current with { StartWithWindows = enabled }, null);
        }

        _log.Warning($"Autostart entry was not changed to {enabled}: {result.Detail}");
        return new AutostartChangeResult(
            current.StartWithWindows,
            current,
            NotificationMessage.Of("Shell.AutostartFailed", result.Detail ?? string.Empty));
    }

    public FolderCheck CheckSaveFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !IsAbsolutePath(folder))
        {
            return FolderCheck.Invalid;
        }

        return _files.DirectoryExists(folder) ? FolderCheck.Exists : FolderCheck.MissingCanCreate;
    }

    public FolderCreateResult CreateSaveFolder(string folder)
    {
        var result = _files.CreateDirectory(folder);
        return new FolderCreateResult(result.Success, result.Detail);
    }

    public SecondInstanceResult OnSecondInstanceLaunched() => new(true);

    public PixelPoint PlaceCaptureBar(PixelPoint? saved, IReadOnlyList<MonitorInfo> monitors, PixelSize barSize)
    {
        if (saved is { } place && monitors.Any(m => Intersects(place, barSize, m.Bounds)))
        {
            return place;
        }

        var primary = monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.FirstOrDefault();
        if (primary is null)
        {
            return saved ?? default;
        }

        var bounds = primary.Bounds;
        return new PixelPoint(bounds.X + bounds.Width - barSize.Width - BarMargin, bounds.Y + BarMargin);
    }

    public ResolvedLanguage ResolveLanguage(AppLanguage language, string systemCultureName) => language switch
    {
        AppLanguage.Vietnamese => ResolvedLanguage.Vietnamese,
        AppLanguage.English => ResolvedLanguage.English,
        _ => systemCultureName.StartsWith("vi", StringComparison.OrdinalIgnoreCase)
            ? ResolvedLanguage.Vietnamese
            : ResolvedLanguage.English,
    };

    private static HotkeyChangeResult Refuse(AppSettings current, HotkeyIssue issue, CaptureKind? conflicting, NotificationMessage message) =>
        new(false, issue, conflicting, current, message);

    private static NotificationMessage NotSaved(PortResult result) =>
        NotificationMessage.Of("Shell.SettingsNotSaved", result.Detail ?? string.Empty);

    private static bool Intersects(PixelPoint place, PixelSize size, PixelRect bounds) =>
        place.X < bounds.X + bounds.Width
        && place.X + size.Width > bounds.X
        && place.Y < bounds.Y + bounds.Height
        && place.Y + size.Height > bounds.Y;

    /// <summary>An absolute path with no character a folder name cannot hold.</summary>
    private static bool IsAbsolutePath(string path)
    {
        if (!Path.IsPathFullyQualified(path))
        {
            return false;
        }

        var root = Path.GetPathRoot(path) ?? string.Empty;
        var invalid = Path.GetInvalidFileNameChars();
        var separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
        return path[root.Length..]
            .Split(separators)
            .All(segment => segment.IndexOfAny(invalid) < 0);
    }

    private AppSettings LoadOrDefault(ShellStartInput input, List<NotificationMessage> notices)
    {
        var loaded = _settingsStore.Load();
        if (loaded.Status == SettingsLoadStatus.Loaded && loaded.Settings is { } settings)
        {
            return settings;
        }

        var defaults = CreateDefaultSettings(input.PicturesFolder);
        if (loaded.Status == SettingsLoadStatus.Corrupt)
        {
            // The adapter already kept the unreadable file as .bak (SPEC shell F1).
            _log.Warning($"The settings file could not be read: {loaded.Detail}");
            notices.Add(NotificationMessage.Of("Shell.SettingsCorrupt"));
            return defaults;
        }

        var saved = _settingsStore.Save(defaults);
        if (!saved.Success)
        {
            _log.Warning($"The first settings could not be written: {saved.Detail}");
            notices.Add(NotSaved(saved));
        }

        return defaults;
    }
}
