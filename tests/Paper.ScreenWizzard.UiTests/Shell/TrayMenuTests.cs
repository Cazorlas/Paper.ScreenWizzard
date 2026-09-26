using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.Presentation.Shell.ViewModels;
using Paper.ScreenWizzard.UiTests.Support;
using Paper.ScreenWizzard.UseCases.Shared.Models;

namespace Paper.ScreenWizzard.UiTests.Shell;

/// <summary>The content of the tray menu (plan T5): the wireframe's list, its shortcut column and the tick of the capture bar line.</summary>
[TestFixture]
public sealed class TrayMenuTests : UiTestBase
{
    private static TrayMenuViewModel Create(bool captureBarVisible = true) =>
        new(ShellTestData.DefaultHotkeys, captureBarVisible);

    [Test]
    public void Menu_HoldsTheEightLinesOfTheSpecInOrder()
    {
        var menu = Create();

        Assert.That(
            menu.Items.Select(item => item.TextKey),
            Is.EqualTo(new[]
            {
                "Tray.CaptureRectangle",
                "Tray.CaptureFreeform",
                "Tray.CaptureWindow",
                "Tray.CaptureFullScreen",
                "Tray.OpenImage",
                "Tray.CaptureBar",
                "Tray.Settings",
                "Tray.Exit",
            }));
    }

    [Test]
    public void Menu_WithANewVersionOut_HasTheDownloadLineAboveExit()
    {
        var menu = Create();
        var raised = 0;
        menu.UpdateRequested += (_, _) => raised++;

        menu.SetUpdateAvailable(true);

        Assert.That(menu.Items.Select(item => item.TextKey).TakeLast(3), Is.EqualTo(new[] { "Tray.Settings", "Tray.Update", "Tray.Exit" }));
        menu.Items.Single(item => item.TextKey == "Tray.Update").Command.Execute(null);
        Assert.That(raised, Is.EqualTo(1), "the line asks the shell to open the download page");
    }

    [Test]
    public void Menu_CaptureLines_ShowTheHotkeyOfTheirKind()
    {
        var menu = Create();

        Assert.That(
            menu.Items.Take(4).Select(item => item.ShortcutText),
            Is.EqualTo(new[] { "PrintScreen", "Shift+PrintScreen", "Alt+PrintScreen", "Ctrl+PrintScreen" }));
        Assert.That(menu.Items.Skip(4).Select(item => item.ShortcutText), Is.All.Empty, "only the capture lines have a shortcut");
    }

    [Test]
    public void Menu_ChangedHotkey_UpdatesTheShortcutColumn()
    {
        var menu = Create();
        var changed = new Dictionary<CaptureKind, HotkeyChord>(ShellTestData.DefaultHotkeys)
        {
            [CaptureKind.Rectangle] = new(HotkeyModifiers.Control | HotkeyModifiers.Shift, "1"),
        };

        menu.SetHotkeys(changed);

        Assert.That(menu.Items, Is.Not.Empty, "the menu has no lines");
        Assert.That(menu.Items[0].ShortcutText, Is.EqualTo("Ctrl+Shift+1"));
    }

    [Test]
    public void Menu_CaptureBarLine_IsTickedWhileTheBarIsVisible()
    {
        var menu = Create(captureBarVisible: true);
        Assert.That(menu.Items.Select(item => item.TextKey), Does.Contain("Tray.CaptureBar"), "the menu has no capture bar line");
        var line = menu.Items.Single(item => item.TextKey == "Tray.CaptureBar");
        Assert.That(line.IsCheckable, Is.True);
        Assert.That(line.IsChecked, Is.True);

        menu.SetCaptureBarVisible(false);

        Assert.That(line.IsChecked, Is.False, "hiding the bar removes the tick");
        Assert.That(menu.Items.Where(item => item.IsCheckable).Count(), Is.EqualTo(1), "only that line is a toggle");
    }

    [Test]
    public void Menu_Lines_RaiseTheirRequests()
    {
        var menu = Create();
        var captured = new List<CaptureKind>();
        var others = new List<string>();
        menu.CaptureRequested += captured.Add;
        menu.OpenImageRequested += (_, _) => others.Add("open");
        menu.ToggleCaptureBarRequested += (_, _) => others.Add("toggle");
        menu.SettingsRequested += (_, _) => others.Add("settings");
        menu.ExitRequested += (_, _) => others.Add("exit");

        foreach (var item in menu.Items)
        {
            item.Command.Execute(null);
        }

        Assert.That(captured, Is.EqualTo(new[] { CaptureKind.Rectangle, CaptureKind.Freeform, CaptureKind.Window, CaptureKind.FullScreen }));
        Assert.That(others, Is.EqualTo(new[] { "open", "toggle", "settings", "exit" }));
    }

    [TestCase(ResolvedLanguage.Vietnamese)]
    [TestCase(ResolvedLanguage.English)]
    public void Menu_EveryLine_HasTextInBothLanguages(ResolvedLanguage language)
    {
        var host = WpfHost.Instance;
        host.Invoke(() => host.Language.Apply(language));
        var items = Create().Items;
        Assert.That(items, Is.Not.Empty, "the menu has no lines");

        foreach (var item in items)
        {
            var text = host.Invoke(() => host.Language.GetString(item.TextKey));
            Assert.That(text, Is.Not.EqualTo(item.TextKey).And.Not.Empty, $"'{item.TextKey}' has no {language} text");
        }
    }
}
