using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.Infrastructure.Shell;

namespace Paper.ScreenWizzard.E2eTests.Support;

/// <summary>
/// One real copy of Paper.ScreenWizzard.exe on the desktop, started with its own data folder, instance name and settings, so it shares
/// nothing with a copy the developer runs: its settings, its Run value and its mutex are all throw-away. The tests drive it the way a person
/// does (global hotkeys, the mouse) and read what came out. It kills only the process it started, and always in <see cref="Dispose"/>.
/// </summary>
public sealed class AppRun : IDisposable
{
    public const HotkeyModifiers Chorded = HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift;

    // Ctrl+Alt+Shift+F13..F16: chords nobody uses (F5 and F6 with Ctrl+Shift open the UniKey panels on this machine). PrintScreen is never sent (it can open the Snipping Tool of Windows).
    public static readonly IReadOnlyDictionary<CaptureKind, int> FunctionKeys = new Dictionary<CaptureKind, int>
    {
        [CaptureKind.Rectangle] = 13,
        [CaptureKind.Freeform] = 14,
        [CaptureKind.Window] = 15,
        [CaptureKind.FullScreen] = 16,
    };

    private readonly UIA3Automation _uia = new();
    private readonly ScratchFolder _scratch = new();
    private readonly List<Process> _started = [];

    public AppRun(Func<AppSettings, AppSettings>? tweak = null, bool seed = true, bool defaultBarPlace = true)
    {
        DataRoot = System.IO.Path.Combine(_scratch.Path, "data");
        SaveFolder = System.IO.Path.Combine(_scratch.Path, "saved");
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(SaveFolder);
        Instance = "Paper.ScreenWizzard.Drive." + Guid.NewGuid().ToString("N");
        var primary = Desk.Monitors().First(m => m.IsPrimary);
        var settings = new AppSettings(
            FunctionKeys.ToDictionary(pair => pair.Key, pair => new HotkeyChord(Chorded, "F" + pair.Value)),
            AfterCaptureAction.ShowDialog,
            SaveFolder,
            ImageFormat.Png,
            90,
            0,
            false,
            FullScreenScope.MonitorUnderCursor,
            false,
            AppLanguage.Vietnamese,
            AppTheme.Light,
            defaultBarPlace ? new PixelPoint(primary.Bounds.X + 40, primary.Bounds.Y + 40) : null,
            // A driven copy never asks GitHub: the test would depend on the network and on what is published.
            false);
        Seeded = tweak is null ? settings : tweak(settings);
        if (seed)
        {
            var saved = new SettingsStore(DataRoot).Save(Seeded);
            Assert.That(saved.Success, Is.True, "set-up: the seeded settings file " + saved.Detail);
        }
    }

    public string DataRoot { get; }

    public string SaveFolder { get; }

    public string Instance { get; }

    public AppSettings Seeded { get; }

    public string SettingsPath => System.IO.Path.Combine(DataRoot, "configs", "settings.json");

    public string ScratchPath => _scratch.Path;

    public Process? Process { get; private set; }

    public UIA3Automation Automation => _uia;

    public static string ExePath()
    {
        var path = System.IO.Path.Combine(Desk.RepositoryRoot(), "src", "Paper.ScreenWizzard.App", "bin", "Debug", "net10.0-windows10.0.19041.0", "Paper.ScreenWizzard.exe");
        Assert.That(File.Exists(path), Is.True, "the exe is built (paperflow build): " + path);
        return path;
    }

    /// <summary>Starts the exe with this run's environment. <paramref name="arguments"/> is for a second copy or --autostart.</summary>
    public Process Start(string? arguments = null)
    {
        var info = new ProcessStartInfo(ExePath())
        {
            UseShellExecute = false,
            WorkingDirectory = System.IO.Path.GetDirectoryName(ExePath())!,
            Arguments = arguments ?? string.Empty,
        };
        info.Environment["PAPER_SCREENWIZZARD_DATA"] = DataRoot;
        info.Environment["PAPER_SCREENWIZZARD_INSTANCE"] = Instance;
        var process = System.Diagnostics.Process.Start(info) ?? throw new InvalidOperationException("the exe did not start");
        _started.Add(process);
        Process ??= process;
        return process;
    }

    public bool IsRunning => Process is { HasExited: false };

    /// <summary>Kills the running copy (it saved what it saves at once) and starts another over the same data folder and instance name.</summary>
    public Process Restart()
    {
        Stop();
        foreach (var process in _started)
        {
            process.Dispose();
        }

        _started.Clear();
        Process = null;
        return Start();
    }

    /// <summary>The outer rectangle of a window of the exe in physical pixels, from GetWindowRect.</summary>
    public static PixelRect WindowRectOf(AutomationElement window)
    {
        Native.GetWindowRect((IntPtr)window.Properties.NativeWindowHandle.Value, out var rect);
        return new PixelRect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    // ---- windows, found by UI Automation in the exe's own process ----

    /// <summary>
    /// The visible top-level windows of the exe (owned dialogs included), found with EnumWindows and the process id and only then given to UI
    /// Automation by handle. Asking UI Automation for the desktop's children by process id touches every window of the machine, and one of the
    /// developer's programs answering slowly made that take 10 to 30 s per call.
    /// </summary>
    public IReadOnlyList<AutomationElement> TopWindows(Process? process = null)
    {
        var pid = (uint)(process ?? Process)!.Id;
        var handles = new List<IntPtr>();
        DriveNative.EnumWindows(
            (window, _) =>
            {
                DriveNative.GetWindowThreadProcessId(window, out var owner);
                if (owner == pid && DriveNative.IsWindowVisible(window))
                {
                    handles.Add(window);
                }

                return true;
            },
            IntPtr.Zero);
        var found = new List<AutomationElement>();
        foreach (var handle in handles)
        {
            try
            {
                found.Add(_uia.FromHandle(handle));
            }
            catch (Exception)
            {
                // The window closed between the enumeration and the lookup.
            }
        }

        return found;
    }

    public IReadOnlyList<AutomationElement> Windows(string automationId) =>
        TopWindows().Where(w => SafeId(w) == automationId).ToList();

    public AutomationElement? FindWindow(string automationId) => Windows(automationId).FirstOrDefault();

    public AutomationElement WaitWindow(string automationId, double seconds = 8)
    {
        var found = Retry.WhileNull(() => FindWindow(automationId), TimeSpan.FromSeconds(seconds), TimeSpan.FromMilliseconds(100)).Result;
        Assert.That(found, Is.Not.Null, $"window '{automationId}' never appeared; the exe's windows: [{Describe()}]");
        return found!;
    }

    public bool WaitWindowGone(string automationId, double seconds = 5) =>
        Retry.WhileTrue(() => Windows(automationId).Count > 0, TimeSpan.FromSeconds(seconds), TimeSpan.FromMilliseconds(100)).Success;

    /// <summary>The AutomationIds and names of every top-level window of the exe, for a failure message.</summary>
    public string Describe()
    {
        try
        {
            return string.Join(", ", TopWindows().Select(w => $"{SafeId(w)}/'{w.Name}'"));
        }
        catch (Exception exception)
        {
            return "could not list: " + exception.Message;
        }
    }

    public static AutomationElement Require(AutomationElement scope, string automationId, double seconds = 5)
    {
        var found = Retry.WhileNull(() => scope.FindFirstDescendant(cf => cf.ByAutomationId(automationId)), TimeSpan.FromSeconds(seconds), TimeSpan.FromMilliseconds(100)).Result;
        Assert.That(found, Is.Not.Null, $"'{automationId}' never appeared in the window");
        return found!;
    }

    public static AutomationElement? Find(AutomationElement scope, string automationId) =>
        scope.FindFirstDescendant(cf => cf.ByAutomationId(automationId));

    /// <summary>What the element shows: an edit box's value, else its name (a TextBlock's name is its text).</summary>
    public static string TextOf(AutomationElement scope, string automationId)
    {
        var element = Require(scope, automationId);
        var value = element.Patterns.Value.PatternOrDefault;
        return value is not null ? value.Value.Value ?? string.Empty : element.Name ?? string.Empty;
    }

    /// <summary>Invokes a button through UI Automation, as a screen-reader user would; nothing is clicked on the desktop.</summary>
    public static void Invoke(AutomationElement scope, string automationId)
    {
        var element = Require(scope, automationId);
        var invoke = element.Patterns.Invoke.PatternOrDefault;
        Assert.That(invoke, Is.Not.Null, $"'{automationId}' offers no Invoke pattern");
        invoke!.Invoke();
        Thread.Sleep(150);
    }

    /// <summary>The rectangle of an element in physical pixels.</summary>
    public static PixelRect BoundsOf(AutomationElement element)
    {
        var r = element.BoundingRectangle;
        return new PixelRect(r.X, r.Y, r.Width, r.Height);
    }

    public static string SafeId(AutomationElement element)
    {
        try
        {
            return element.AutomationId ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    // ---- what the exe wrote ----

    public IReadOnlyList<string> SavedFiles() =>
        Directory.Exists(SaveFolder) ? Directory.GetFiles(SaveFolder).Select(System.IO.Path.GetFileName).OfType<string>().OrderBy(n => n, StringComparer.Ordinal).ToList() : [];

    public static bool IsScreenshotName(string name, string extension = ".png") =>
        System.Text.RegularExpressions.Regex.IsMatch(name, @"^Screenshot \d{4}-\d{2}-\d{2} \d{2}\.\d{2}\.\d{2}( \(\d+\))?" + System.Text.RegularExpressions.Regex.Escape(extension) + "$");

    public JsonDocument ReadSettingsJson() => JsonDocument.Parse(File.ReadAllBytes(SettingsPath));

    /// <summary>Waits for a screenshot file to appear in the save folder (the write happens on a click), returns its full path.</summary>
    public string WaitForSavedFile(double seconds = 5, string extension = ".png")
    {
        var found = Retry.WhileNull(
            () => SavedFiles().Where(n => IsScreenshotName(n, extension)).Select(n => System.IO.Path.Combine(SaveFolder, n)).FirstOrDefault(),
            TimeSpan.FromSeconds(seconds),
            TimeSpan.FromMilliseconds(100)).Result;
        Assert.That(found, Is.Not.Null, $"no file named 'Screenshot yyyy-MM-dd HH.mm.ss{extension}' appeared in the save folder; it holds: [{string.Join(", ", SavedFiles())}]");
        return found!;
    }

    // ---- the hotkeys ----

    /// <summary>Presses the chord of a capture kind on the real keyboard queue (Ctrl+Alt+Shift+F13..F16).</summary>
    public static void PressHotkey(CaptureKind kind) => KeySender.Press(Chorded, KeySender.FunctionKey(FunctionKeys[kind]));

    // ---- pictures of the exe's own windows ----

    /// <summary>The picture of one window of the exe (never the desktop), to tests/.../shots/&lt;name&gt;.png; the caller opens and judges it.</summary>
    public static string Shot(AutomationElement window, string name)
    {
        var path = Desk.ShotPath(name);

        // The window's visible frame (DWM), not its UI Automation rectangle: that one includes the invisible border, and a picture of it holds a strip of
        // whatever the developer has behind the window.
        var handle = (IntPtr)window.Properties.NativeWindowHandle.Value;
        if (DriveNative.DwmGetWindowAttribute(handle, DriveNative.DwmExtendedFrameBounds, out var frame, System.Runtime.InteropServices.Marshal.SizeOf<Native.Rect>()) == 0
            && frame.Right > frame.Left && frame.Bottom > frame.Top)
        {
            Capture.Rectangle(new System.Drawing.Rectangle(frame.Left, frame.Top, frame.Right - frame.Left, frame.Bottom - frame.Top)).ToFile(path);
        }
        else
        {
            Capture.Element(window).ToFile(path);
        }

        return path;
    }

    /// <summary>The picture of a rectangle of the desktop that only holds the test's own windows (the overlay drawn over the test window).</summary>
    public static string ShotOf(PixelRect area, string name)
    {
        var path = Desk.ShotPath(name);
        Capture.Rectangle(new System.Drawing.Rectangle(area.X, area.Y, area.Width, area.Height)).ToFile(path);
        return path;
    }

    // ---- ending ----

    /// <summary>Kills the copies this run started and waits for them to be gone, then deletes the temp folder. Never touches another process.</summary>
    public void Stop()
    {
        foreach (var process in _started)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                process.WaitForExit(10_000);
            }
            catch (Exception)
            {
                // Already gone.
            }
        }
    }

    // The "start with Windows" value of this run's throw-away instance name, whatever a test left behind.
    private void RemoveRunValue()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.DeleteValue(Instance, throwOnMissingValue: false);
        }
        catch (Exception)
        {
            // Nothing to remove, or the key is not writable: the test that cares reads the key itself.
        }
    }

    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public void Dispose()
    {
        try
        {
            Stop();
        }
        finally
        {
            _uia.Dispose();
            RemoveRunValue();
            foreach (var process in _started)
            {
                process.Dispose();
            }

            _started.Clear();
            _scratch.Dispose();
        }
    }
}
