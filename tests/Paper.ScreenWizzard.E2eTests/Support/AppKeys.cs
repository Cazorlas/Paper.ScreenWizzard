using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using NUnit.Framework;

namespace Paper.ScreenWizzard.E2eTests.Support;

/// <summary>
/// Real keys, sent only into the exe's own windows. A key typed into whichever program is in front is dangerous on a working machine (a
/// Vietnamese input method rewrites what is typed, a CAD program takes shortcuts), so every send first checks that the foreground window
/// belongs to the exe this test started, and fails the test when it does not.
/// </summary>
public static class AppKeys
{
    public static bool ForegroundBelongsTo(AppRun app)
    {
        var foreground = DriveNative.GetForegroundWindow();
        DriveNative.GetWindowThreadProcessId(foreground, out var pid);
        return app.Process is { } process && pid == (uint)process.Id;
    }

    /// <summary>Brings a window of the exe to the front (a few attempts: Windows refuses while another program has just taken the front).</summary>
    public static void BringToFront(AppRun app, AutomationElement window)
    {
        var handle = (IntPtr)window.Properties.NativeWindowHandle.Value;
        for (var attempt = 0; attempt < 4 && DriveNative.GetForegroundWindow() != handle; attempt++)
        {
            var foregroundThread = DriveNative.GetWindowThreadProcessId(DriveNative.GetForegroundWindow(), out _);
            var thisThread = DriveNative.GetCurrentThreadId();
            var attached = foregroundThread != 0 && foregroundThread != thisThread && DriveNative.AttachThreadInput(thisThread, foregroundThread, true);
            try
            {
                DriveNative.SetForegroundWindow(handle);
            }
            finally
            {
                if (attached)
                {
                    DriveNative.AttachThreadInput(thisThread, foregroundThread, false);
                }
            }

            Retry.WhileFalse(() => DriveNative.GetForegroundWindow() == handle, TimeSpan.FromMilliseconds(600), TimeSpan.FromMilliseconds(50));
        }

        Assert.That(AppKeys.ForegroundBelongsTo(app), Is.True, "Windows would not bring the exe's window to the front; real keys would go to another program");
    }

    public static void Type(AppRun app, AutomationElement window, params VirtualKeyShort[] keys)
    {
        BringToFront(app, window);
        foreach (var key in keys)
        {
            Keyboard.Type(key);
            Thread.Sleep(80);
        }
    }

    public static void Chord(AppRun app, AutomationElement window, params VirtualKeyShort[] keys)
    {
        BringToFront(app, window);
        Keyboard.TypeSimultaneously(keys);
        Thread.Sleep(150);
    }

    /// <summary>Types text with the keyboard into the focused control of an exe window (never into a foreign window).</summary>
    public static void Text(AppRun app, AutomationElement window, string text)
    {
        BringToFront(app, window);
        Keyboard.Type(text);
        Thread.Sleep(150);
    }
}
