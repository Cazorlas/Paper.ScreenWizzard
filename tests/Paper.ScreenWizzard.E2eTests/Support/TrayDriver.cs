using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using NUnit.Framework;

namespace Paper.ScreenWizzard.E2eTests.Support;

/// <summary>
/// The system tray as UI Automation shows it on Windows 11: the taskbar's "Show Hidden Icons" button opens a flyout that lists every icon that is
/// not pinned to the bar, one button per icon named with its tooltip. The tests only read the taskbar and open that flyout by pattern (no mouse
/// click on the taskbar); the one real click is the right button on the icon of the exe they started, which is what a user does to open its menu.
/// The menu is the exe's own window, so it is found among the exe's windows and driven with its accessibility actions.
/// </summary>
public sealed class TrayDriver
{
    public const string IconName = "Paper.ScreenWizzard";

    private readonly AppRun _app;

    public TrayDriver(AppRun app)
    {
        _app = app;
    }

    private AutomationElement Taskbar()
    {
        var handle = DriveNative.FindWindow("Shell_TrayWnd", null);
        Assert.That(handle, Is.Not.EqualTo(IntPtr.Zero), "the taskbar (Shell_TrayWnd) exists");
        return _app.Automation.FromHandle(handle);
    }

    private AutomationElement? Chevron() => Taskbar().FindFirstDescendant(cf => cf.ByAutomationId("SystemTrayIcon"));

    private bool OverflowIsOpen() => Chevron()?.Name?.Contains("Hide", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>Opens the hidden-icons flyout when it is closed, and waits for it to list its buttons.</summary>
    private void OpenOverflow()
    {
        if (OverflowIsOpen())
        {
            return;
        }

        var chevron = Chevron();
        Assert.That(chevron, Is.Not.Null, "the taskbar's 'Show Hidden Icons' button was not found through UI Automation");
        chevron!.Patterns.Invoke.Pattern.Invoke();
        Thread.Sleep(500);
    }

    public void CloseOverflow()
    {
        if (OverflowIsOpen())
        {
            Chevron()?.Patterns.Invoke.Pattern.Invoke();
            Thread.Sleep(300);
        }
    }

    /// <summary>The tray button of this exe (searched in the taskbar and, after opening it, in the hidden-icons flyout), or null.</summary>
    public AutomationElement? FindIcon(double seconds = 6)
    {
        OpenOverflow();
        return Retry.WhileNull(
            () =>
            {
                foreach (var cls in new[] { "TopLevelWindowForOverflowXamlIsland", "Shell_TrayWnd" })
                {
                    var handle = DriveNative.FindWindow(cls, null);
                    if (handle == IntPtr.Zero)
                    {
                        continue;
                    }

                    var found = _app.Automation.FromHandle(handle)
                        .FindAllDescendants(cf => cf.ByAutomationId("NotifyItemIcon"))
                        .FirstOrDefault(b => string.Equals(b.Name?.Trim(), IconName, StringComparison.Ordinal));
                    if (found is not null)
                    {
                        return found;
                    }
                }

                return null;
            },
            TimeSpan.FromSeconds(seconds),
            TimeSpan.FromMilliseconds(400)).Result;
    }

    /// <summary>Right-clicks the icon and returns the menu the exe drew.</summary>
    public AutomationElement OpenMenu()
    {
        var icon = FindIcon();
        Assert.That(icon, Is.Not.Null, "no tray icon named '" + IconName + "' was found through UI Automation");
        var r = icon!.BoundingRectangle;
        Desk.RightClickAt(r.X + (r.Width / 2), r.Y + (r.Height / 2));
        var menu = Retry.WhileNull(
            () => _app.TopWindows().FirstOrDefault(w => w.ControlType == FlaUI.Core.Definitions.ControlType.Menu),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMilliseconds(150)).Result;
        Assert.That(menu, Is.Not.Null, "the tray icon's context menu did not open; the exe's windows: [" + _app.Describe() + "]");
        return menu!;
    }

    public static IReadOnlyList<string> ItemNames(AutomationElement menu) =>
        menu.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.MenuItem)).Select(i => i.Name).ToList();

    /// <summary>Opens the menu and chooses the item with this text.</summary>
    public void Choose(string itemText)
    {
        var menu = OpenMenu();
        ChooseIn(menu, itemText);
        CloseOverflow();
    }

    public static void ChooseIn(AutomationElement menu, string itemText)
    {
        var item = menu.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.MenuItem)).FirstOrDefault(i => i.Name == itemText);
        Assert.That(item, Is.Not.Null, $"the tray menu has no item '{itemText}'; it has [{string.Join(", ", ItemNames(menu))}]");
        // A real click on the item (the menu is the exe's own popup). Not the accessibility action: DoDefaultAction is a call into the app that does not
        // return while the handler runs, and a handler that opens a modal box (Open image) leaves every later UI Automation call to that process timing out.
        var r = item!.BoundingRectangle;
        Desk.ClickAt(r.X + (r.Width / 2), r.Y + (r.Height / 2));
        Thread.Sleep(500);
    }
}
