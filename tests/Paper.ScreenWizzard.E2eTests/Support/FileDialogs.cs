using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using NUnit.Framework;

namespace Paper.ScreenWizzard.E2eTests.Support;

/// <summary>
/// The native "Save as" and "Open" boxes the exe opens, driven through UI Automation: they are Win32 common dialogs (class #32770) owned by the
/// exe, with the file name box AutomationId 1001, the default button 1 and Cancel 2 (measured on this Windows 11).
/// </summary>
public static class FileDialogs
{
    public static AutomationElement Wait(AppRun app, double seconds = 8)
    {
        var dialog = Retry.WhileNull(
            () => app.TopWindows().FirstOrDefault(w => SafeClass(w) == "#32770"),
            TimeSpan.FromSeconds(seconds),
            TimeSpan.FromMilliseconds(150)).Result;
        Assert.That(dialog, Is.Not.Null, "the native file box did not open; the exe's windows: [" + app.Describe() + "]");
        Thread.Sleep(400);
        return dialog!;
    }

    private static AutomationElement NameBox(AutomationElement dialog)
    {
        // Save box: the Edit with id 1001. Open box: the Edit with id 1148 (its combo box has the same id).
        var box = Retry.WhileNull(
            () => dialog.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit))
                .FirstOrDefault(e => (e.AutomationId == "1001" || e.AutomationId == "1148") && e.Patterns.Value.IsSupported),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMilliseconds(150)).Result;
        Assert.That(box, Is.Not.Null, "the file name box (1001) with a value pattern was not found in the native box");
        return box!;
    }

    public static string FileNameShown(AutomationElement dialog) => NameBox(dialog).Patterns.Value.Pattern.Value.Value ?? string.Empty;

    public static string TitleOf(AutomationElement dialog) => dialog.Name ?? string.Empty;

    /// <summary>Types a path into the file name box (through its value pattern) and presses the default button, as Enter would.</summary>
    public static void Accept(AutomationElement dialog, string path)
    {
        var box = NameBox(dialog);
        box.Patterns.Value.Pattern.SetValue(path);
        Thread.Sleep(150);
        AppRun.Invoke(dialog, "1");
    }

    public static void Cancel(AutomationElement dialog) => AppRun.Invoke(dialog, "2");

    /// <summary>Types a path into the name box and presses Enter in the box, for the Open box whose default button is a split button.</summary>
    public static void AcceptWithEnter(AppRun app, AutomationElement dialog, string path)
    {
        var box = NameBox(dialog);
        box.Patterns.Value.Pattern.SetValue(path);
        Thread.Sleep(150);
        box.Focus();
        AppKeys.Type(app, dialog, FlaUI.Core.WindowsAPI.VirtualKeyShort.ENTER);
    }

    public static bool WaitGone(AppRun app, double seconds = 5) =>
        Retry.WhileTrue(() => app.TopWindows().Any(w => SafeClass(w) == "#32770"), TimeSpan.FromSeconds(seconds), TimeSpan.FromMilliseconds(150)).Success;

    private static string SafeClass(AutomationElement element)
    {
        try
        {
            return element.ClassName ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
