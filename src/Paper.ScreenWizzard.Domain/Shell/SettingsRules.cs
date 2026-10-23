using System.Globalization;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Recorder;

namespace Paper.ScreenWizzard.Domain.Shell;

/// <summary>What a stored setting may hold, and what the app runs on when the file lacks one (SPEC shell, "Cài đặt được giữ lại").</summary>
public static class SettingsRules
{
    public const int JpgQualityMin = 1;

    public const int JpgQualityMax = 100;

    /// <summary>The longest delay the Settings window offers (SPEC capture, Inputs: 0, 3, 5, 10).</summary>
    public const int DelaySecondsMax = 10;

    public static bool IsJpgQuality(int quality) => quality is >= JpgQualityMin and <= JpgQualityMax;

    public static bool IsDelay(int seconds) => seconds is >= 0 and <= DelaySecondsMax;

    /// <summary>
    /// A setting the file lacks takes its value from <paramref name="defaults"/> and is named in <see cref="SettingsCompletion.Defaulted"/>
    /// (F8); a value outside its range breaks the whole file (F1), as a value of the wrong type already did.
    /// </summary>
    public static SettingsCompletion Complete(StoredSettings stored, AppSettings defaults)
    {
        var defaulted = new List<string>();

        T Take<T>(T? value, T fallback, string name)
            where T : struct
        {
            if (value is { } present)
            {
                return present;
            }

            defaulted.Add(name);
            return fallback;
        }

        Dictionary<CaptureKind, HotkeyChord> hotkeys;
        if (stored.Hotkeys is null)
        {
            hotkeys = new Dictionary<CaptureKind, HotkeyChord>(defaults.Hotkeys);
            defaulted.Add("hotkeys");
        }
        else
        {
            hotkeys = new Dictionary<CaptureKind, HotkeyChord>();
            foreach (var (name, chord) in stored.Hotkeys)
            {
                // One name as the store writes it: Enum.TryParse alone would also take "7" or "Rectangle, Freeform".
                if (!Enum.GetNames<CaptureKind>().Contains(name) || !Enum.TryParse<CaptureKind>(name, out var kind))
                {
                    return SettingsCompletion.Broken($"'{name}' is not a kind of capture");
                }

                if (chord is null || string.IsNullOrWhiteSpace(chord.Key))
                {
                    return SettingsCompletion.Broken($"the hotkey of {name} has no key");
                }

                hotkeys[kind] = new HotkeyChord(chord.Modifiers ?? HotkeyModifiers.None, chord.Key);
            }

            foreach (var (kind, chord) in defaults.Hotkeys)
            {
                if (hotkeys.TryAdd(kind, chord))
                {
                    defaulted.Add("hotkeys." + kind);
                }
            }
        }

        var saveFolder = stored.SaveFolder;
        if (saveFolder is null)
        {
            saveFolder = defaults.SaveFolder;
            defaulted.Add("saveFolder");
        }
        else if (string.IsNullOrWhiteSpace(saveFolder))
        {
            return SettingsCompletion.Broken("the save folder is empty");
        }

        var jpgQuality = Take(stored.JpgQuality, defaults.JpgQuality, "jpgQuality");
        if (!IsJpgQuality(jpgQuality))
        {
            return SettingsCompletion.Broken(string.Create(CultureInfo.InvariantCulture, $"the JPG quality {jpgQuality} is not between {JpgQualityMin} and {JpgQualityMax}"));
        }

        var delaySeconds = Take(stored.DelaySeconds, defaults.DelaySeconds, "delaySeconds");
        if (!IsDelay(delaySeconds))
        {
            return SettingsCompletion.Broken(string.Create(CultureInfo.InvariantCulture, $"the delay {delaySeconds} is not between 0 and {DelaySecondsMax} seconds"));
        }

        var recordHotkeys = defaults.RecordHotkeys;
        if (stored.RecordHotkeys is null)
        {
            defaulted.Add("recordHotkeys");
        }
        else
        {
            foreach (var (name, chord) in stored.RecordHotkeys)
            {
                if (!Enum.GetNames<RecordHotkey>().Contains(name) || !Enum.TryParse<RecordHotkey>(name, out var key))
                {
                    return SettingsCompletion.Broken($"'{name}' is not a recording hotkey");
                }

                if (chord is null || string.IsNullOrWhiteSpace(chord.Key))
                {
                    return SettingsCompletion.Broken($"the recording hotkey {name} has no key");
                }

                recordHotkeys = recordHotkeys.With(key, new HotkeyChord(chord.Modifiers ?? HotkeyModifiers.None, chord.Key));
            }

            foreach (var key in Enum.GetValues<RecordHotkey>())
            {
                if (!stored.RecordHotkeys.ContainsKey(key.ToString()))
                {
                    defaulted.Add("recordHotkeys." + key);
                }
            }
        }

        var recorder = CompleteRecorder(stored.Recorder, defaults.Recorder, defaulted, out var recorderProblem);
        if (recorder is null)
        {
            return SettingsCompletion.Broken(recorderProblem!);
        }

        var settings = new AppSettings(
            hotkeys,
            Take(stored.AfterCapture, defaults.AfterCapture, "afterCapture"),
            saveFolder,
            Take(stored.Format, defaults.Format, "format"),
            jpgQuality,
            delaySeconds,
            Take(stored.IncludeCursor, defaults.IncludeCursor, "includeCursor"),
            Take(stored.FullScreenScope, defaults.FullScreenScope, "fullScreenScope"),
            Take(stored.StartWithWindows, defaults.StartWithWindows, "startWithWindows"),
            Take(stored.Language, defaults.Language, "language"),
            Take(stored.Theme, defaults.Theme, "theme"),
            // Where the bar was dragged; a file that never saw a drag has none, which is the default too.
            stored.CaptureBarPosition,
            recordHotkeys,
            recorder,
            Take(stored.CheckForUpdates, defaults.CheckForUpdates, "checkForUpdates"));
        return new SettingsCompletion(settings, null, defaulted);
    }

    // The recording settings (SPEC recorder, Inputs): missing ones take their default, one outside its choices breaks the file.
    private static RecorderSettings? CompleteRecorder(StoredRecorder? stored, RecorderSettings defaults, List<string> defaulted, out string? problem)
    {
        problem = null;
        if (stored is null)
        {
            defaulted.Add("recorder");
            return defaults;
        }

        T Take<T>(T? value, T fallback, string name)
            where T : struct
        {
            if (value is { } present)
            {
                return present;
            }

            defaulted.Add("recorder." + name);
            return fallback;
        }

        var fps = Take(stored.FramesPerSecond, defaults.FramesPerSecond, "framesPerSecond");
        if (!RecorderRules.IsFrameRate(fps))
        {
            problem = string.Create(CultureInfo.InvariantCulture, $"{fps} frames a second is not one of {string.Join(", ", RecorderRules.FrameRates)}");
            return null;
        }

        var countdown = Take(stored.CountdownSeconds, defaults.CountdownSeconds, "countdownSeconds");
        if (!RecorderRules.IsCountdown(countdown))
        {
            problem = string.Create(CultureInfo.InvariantCulture, $"a countdown of {countdown} seconds is not one of {string.Join(", ", RecorderRules.Countdowns)}");
            return null;
        }

        var folder = stored.VideoFolder;
        if (folder is null)
        {
            folder = defaults.VideoFolder;
            defaulted.Add("recorder.videoFolder");
        }
        else if (string.IsNullOrWhiteSpace(folder))
        {
            problem = "the video folder is empty";
            return null;
        }

        return new RecorderSettings(
            Take(stored.Target, defaults.Target, "target"),
            stored.MonitorIndex,
            Take(stored.SystemSound, defaults.SystemSound, "systemSound"),
            Take(stored.Microphone, defaults.Microphone, "microphone"),
            Take(stored.Pointer, defaults.Pointer, "pointer"),
            countdown,
            fps,
            folder);
    }
}

/// <summary>The settings the app runs on and the settings that took their default; or, for a broken file, why it is broken.</summary>
public sealed record SettingsCompletion(AppSettings? Settings, string? Problem, IReadOnlyList<string> Defaulted)
{
    public static SettingsCompletion Broken(string problem) => new(null, problem, []);
}
