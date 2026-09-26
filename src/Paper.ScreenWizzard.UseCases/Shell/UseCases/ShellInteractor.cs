using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UseCases.Shared.Models;
using Paper.ScreenWizzard.UseCases.Shared.Ports;
using Paper.ScreenWizzard.UseCases.Shell.Models;
using Paper.ScreenWizzard.UseCases.Shell.Ports;

namespace Paper.ScreenWizzard.UseCases.Shell.UseCases;

/// <summary>Everything the shell decides (SPEC shell): startup, hotkey changes, saving settings, autostart, bar placement.</summary>
public sealed class ShellInteractor : IShellInteractor
{
    /// <summary>The gap between the capture bar and the corner of the primary monitor, in physical pixels.</summary>
    private const int BarMargin = 16;

    private readonly ISettingsStore _settingsStore;
    private readonly IHotkeys _hotkeys;
    private readonly IAutostart _autostart;
    private readonly IFileStore _files;
    private readonly ILog _log;
    private bool _settingsUnreadable;

    public ShellInteractor(
        ISettingsStore settingsStore,
        IHotkeys hotkeys,
        IAutostart autostart,
        IFileStore files,
        ILog log)
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
        settings = ReplaceUnsafeHotkeys(settings, input, notices);
        FollowSettingsWithAutostart(settings);

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

        var saved = Persist(changed);
        NotificationMessage? message = saved.Success ? null : NotSaved(saved);
        return new HotkeyChangeResult(true, HotkeyIssue.None, null, changed, message);
    }

    public SettingsApplyResult Apply(AppSettings settings)
    {
        var saved = Persist(settings);
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

    // A file that could not be read at start may be perfectly good, so the defaults the app runs on must not replace it. The file is
    // looked at again at the moment of a save: still locked or now readable and good -> refuse; broken (kept as .bak: by the adapter
    // for a wrong type, here for a value outside its range) or gone -> nothing is lost by writing.
    private PortResult Persist(AppSettings settings)
    {
        if (_settingsUnreadable)
        {
            var again = _settingsStore.Load();
            switch (again.Status)
            {
                case SettingsLoadStatus.Loaded when again.Stored is { } stored && SettingsRules.Complete(stored, settings).Settings is not null:
                    return PortResult.Fail("the settings file could not be read when the app started and is kept as it is; restart the app to use it");
                case SettingsLoadStatus.Loaded:
                    _settingsStore.KeepAsBackup();
                    _settingsUnreadable = false;
                    break;
                case SettingsLoadStatus.Unreadable:
                    return PortResult.Fail(again.Detail ?? "the settings file is still unreadable");
                default:
                    _settingsUnreadable = false;
                    break;
            }
        }

        return _settingsStore.Save(settings);
    }

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

    // A hand-edited file can hold a chord ChangeHotkey would have refused (a plain letter would swallow every press of it in every
    // program); the default of that kind takes its place, and the notice names what was refused.
    private AppSettings ReplaceUnsafeHotkeys(AppSettings settings, ShellStartInput input, List<NotificationMessage> notices)
    {
        Dictionary<CaptureKind, HotkeyChord>? repaired = null;
        foreach (var (kind, chord) in settings.Hotkeys)
        {
            if (HotkeyRules.IsSafe(chord))
            {
                continue;
            }

            repaired ??= new Dictionary<CaptureKind, HotkeyChord>(settings.Hotkeys);
            repaired[kind] = CreateDefaultSettings(input.PicturesFolder).Hotkeys[kind];
            _log.Warning($"Hotkey {HotkeyRules.Format(chord)} for {kind} in the settings is not safe; the default is used.");
            notices.Add(NotificationMessage.Of("Shell.HotkeyUnsafeAtStart", HotkeyRules.Format(chord)));
        }

        return repaired is null ? settings : settings with { Hotkeys = repaired };
    }

    // The start-with-Windows entry is the user's list of startup programs, which the user (or a moved exe) can change behind our back;
    // the setting is what the user chose, so the entry follows it (SPEC shell, "Khởi động cùng Windows").
    private void FollowSettingsWithAutostart(AppSettings settings)
    {
        if (_autostart.IsEnabled() == settings.StartWithWindows)
        {
            return;
        }

        var result = _autostart.SetEnabled(settings.StartWithWindows);
        if (!result.Success)
        {
            _log.Warning($"The start-with-Windows entry could not be set to {settings.StartWithWindows}: {result.Detail}");
        }
    }

    private AppSettings LoadOrDefault(ShellStartInput input, List<NotificationMessage> notices)
    {
        var loaded = _settingsStore.Load();
        _settingsUnreadable = loaded.Status == SettingsLoadStatus.Unreadable;
        var defaults = CreateDefaultSettings(input.PicturesFolder);
        if (loaded.Status == SettingsLoadStatus.Loaded && loaded.Stored is { } stored)
        {
            var completion = SettingsRules.Complete(stored, defaults);
            if (completion.Settings is { } settings)
            {
                // A file of an older version lacks what was added since: normal, so the log says it and the user is not told (F8).
                if (completion.Defaulted.Count > 0)
                {
                    _log.Info($"The settings file lacks {string.Join(", ", completion.Defaulted)}; the defaults are used for them.");
                }

                return settings;
            }

            // A value outside its range breaks the file as a value of the wrong type does (F1): kept as .bak, not written over at start.
            var kept = _settingsStore.KeepAsBackup();
            _log.Warning($"The settings file is broken: {completion.Problem}" + (kept.Success ? string.Empty : $"; the .bak copy failed: {kept.Detail}"));
            notices.Add(NotificationMessage.Of("Shell.SettingsCorrupt"));
            return defaults;
        }

        if (loaded.Status == SettingsLoadStatus.Unreadable)
        {
            // The file may be good and only locked: the defaults are not saved, so it is still there at the next start.
            _log.Warning($"The settings file could not be read: {loaded.Detail}");
            notices.Add(NotificationMessage.Of("Shell.SettingsUnreadable", loaded.Detail ?? string.Empty));
            return defaults;
        }

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
