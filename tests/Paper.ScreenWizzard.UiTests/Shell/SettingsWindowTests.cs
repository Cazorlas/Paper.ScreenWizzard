using System.IO;
using FlaUI.Core.WindowsAPI;
using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UiTests.Support;
using Paper.ScreenWizzard.UseCases.Common.Models;
using Paper.ScreenWizzard.UseCases.Shell.Models;

namespace Paper.ScreenWizzard.UiTests.Shell;

/// <summary>
/// The Settings window on mock data (plan T5): SPEC shell "Dùng được bằng bàn phím", "Ngôn ngữ và giao diện", and the rows
/// F2 to F5 of "When it does not do the job" as the user sees them.
/// </summary>
[TestFixture]
public sealed class SettingsWindowTests : UiTestBase
{
    /// <summary>
    /// The visible order of the Settings controls: the window has two columns, and Tab reads the left one top to bottom, then
    /// the right one, then Save and Cancel. A group of radio buttons is one stop (its chosen button) and the quality box is
    /// disabled for PNG, so Tab skips it.
    /// </summary>
    private static readonly string[] _visibleOrder =
    [
        "HotkeyBox.Rectangle",
        "HotkeyBox.Freeform",
        "HotkeyBox.Window",
        "HotkeyBox.FullScreen",
        "AfterCapture.ShowDialog",
        "AutostartCheck",
        "SaveFolderBox",
        "BrowseFolderButton",
        "Format.Png",
        "Delay.0",
        "IncludeCursorCheck",
        "Scope.MonitorUnderCursor",
        "Language.System",
        "Theme.System",
        "SaveButton",
        "CancelButton",
    ];

    [Test]
    public void APrintScreenKeyUpIsRecordedInTheHotkeyBoxBecauseWindowsSendsNoKeyDownForIt()
    {
        // The harness never sends the real PrintScreen key (it would capture the desktop), so the key-up event is raised on the box.
        var recorded = WpfHost.Instance.Invoke(() =>
        {
            var box = new Paper.ScreenWizzard.Presentation.Views.Shell.HotkeyCaptureBox();
            var window = new System.Windows.Window { Content = box, ShowActivated = false, ShowInTaskbar = false, Width = 200, Height = 100 };
            window.Show();
            try
            {
                var source = System.Windows.PresentationSource.FromVisual(box);
                var args = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, source!, 0, System.Windows.Input.Key.Snapshot)
                {
                    RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyUpEvent,
                };
                box.RaiseEvent(args);
                return box.Chord;
            }
            finally
            {
                window.Close();
            }
        });

        Assert.That(recorded.Key, Is.EqualTo("PrintScreen"));
    }

    [Test]
    public void Keyboard_TabThroughSettings_FollowsTheVisibleOrder()
    {
        using var rig = SettingsRig.Open();
        rig.Session.Focus(_visibleOrder[0]);

        var reached = rig.Session.TabThrough(_visibleOrder.Length - 1);

        Assert.That(reached, Is.EqualTo(_visibleOrder.Skip(1).ToArray()), "Tab must visit the controls in the order they are drawn");
    }

    [TestCase(AppTheme.Light)]
    [TestCase(AppTheme.Dark)]
    public void Keyboard_FocusedButton_ShowsAVisibleFocusRing(AppTheme theme)
    {
        using var rig = SettingsRig.Open(theme: theme);
        var focusBrush = ResourceFiles.Load($"Themes/{theme}.xaml")["Brush.Focus"];
        Assert.That(focusBrush, Is.InstanceOf<System.Windows.Media.SolidColorBrush>(), $"Brush.Focus is missing from the {theme} theme");
        var ring = ((System.Windows.Media.SolidColorBrush)focusBrush).Color;

        rig.Session.Focus("BrowseFolderButton");
        var without = rig.Session.CountPixels("CancelButton", ring);
        rig.Session.Focus("CancelButton");
        var with = rig.Session.CountPixels("CancelButton", ring);

        Assert.That(without, Is.Zero, "an unfocused button must not draw the focus ring");
        Assert.That(with, Is.GreaterThan(20), "the focused button must draw a ring in the theme's focus colour, or Tab is invisible");
    }

    // Found by the main session reading the T10 report: Brush.Focus and Brush.Accent are the same colour, and the ring is drawn
    // inside the button, so on the accent-filled Save button the ring was the fill itself: Tab was invisible there. The existing
    // test above only measures the grey Cancel button. A ring on an accent button must carry a colour that is NOT the fill.
    [TestCase(AppTheme.Light)]
    [TestCase(AppTheme.Dark)]
    public void Keyboard_FocusedAccentButton_ShowsARingThatIsNotTheFillColour(AppTheme theme)
    {
        using var rig = SettingsRig.Open(theme: theme);
        var themeDictionary = ResourceFiles.Load($"Themes/{theme}.xaml");
        var windowBrush = themeDictionary["Brush.Window"];
        Assert.That(windowBrush, Is.InstanceOf<System.Windows.Media.SolidColorBrush>(), $"Brush.Window is missing from the {theme} theme");
        var gap = ((System.Windows.Media.SolidColorBrush)windowBrush).Color;

        rig.Session.Focus("BrowseFolderButton");
        var without = rig.Session.CountPixels("SaveButton", gap);
        rig.Session.Focus("SaveButton");
        var with = rig.Session.CountPixels("SaveButton", gap);

        Assert.That(with - without, Is.GreaterThan(20), "the focused accent button must draw a ring that differs from its fill, or Tab is invisible on it");
    }

    [Test]
    public void Keyboard_EnterInSettings_SavesAndCloses()
    {
        using var rig = SettingsRig.Open();
        rig.Session.Focus("IncludeCursorCheck");

        rig.Session.Press(VirtualKeyShort.ENTER);

        Assert.That(rig.Session.WaitUntilClosed(TimeSpan.FromSeconds(3)), Is.True, "Enter must close the window");
        Assert.That(rig.Shell.Applied, Has.Count.EqualTo(1), "Enter must save exactly once");
    }

    [Test]
    public void Keyboard_EscapeInSettings_ClosesAndDropsTheUnsavedChange()
    {
        using var rig = SettingsRig.Open();
        rig.Session.Click("IncludeCursorCheck");
        Assert.That(rig.Session.IsToggledOn("IncludeCursorCheck"), Is.True, "the click should have ticked the box");

        rig.Session.Press(VirtualKeyShort.ESCAPE);

        Assert.That(rig.Session.WaitUntilClosed(TimeSpan.FromSeconds(3)), Is.True, "Esc must close the window");
        Assert.That(rig.Shell.Applied, Is.Empty, "Esc must not save");
        Assert.That(rig.Shell.HotkeyChanges, Is.Empty, "Esc must not touch the hotkeys");
    }

    [Test]
    public void Keyboard_EveryButtonOfSettings_HasAName()
    {
        using var rig = SettingsRig.Open();

        var buttons = rig.Session.ButtonNames();

        Assert.That(buttons.Select(b => b.AutomationId), Is.SupersetOf(new[] { "BrowseFolderButton", "SaveButton", "CancelButton" }), "the window lacks its own buttons");

        // The scroll bar's own arrows (PART_..., PageUp, PageDown) belong to WPF and only appear on a screen too small for the window.
        var ours = buttons.Where(b => !b.AutomationId.StartsWith("PART_", StringComparison.Ordinal) && b.AutomationId is not ("PageUp" or "PageDown"));
        Assert.That(ours.Where(b => string.IsNullOrWhiteSpace(b.Name)).Select(b => b.AutomationId), Is.Empty, "buttons with no accessible name");
    }

    [Test]
    public void Settings_ShowsTheDefaultsOfTheSpec()
    {
        using var rig = SettingsRig.Open();

        Assert.Multiple(() =>
        {
            Assert.That(rig.Session.TextOf("HotkeyBox.Rectangle"), Is.EqualTo("PrintScreen"));
            Assert.That(rig.Session.TextOf("HotkeyBox.Freeform"), Is.EqualTo("Shift+PrintScreen"));
            Assert.That(rig.Session.TextOf("HotkeyBox.Window"), Is.EqualTo("Alt+PrintScreen"));
            Assert.That(rig.Session.TextOf("HotkeyBox.FullScreen"), Is.EqualTo("Ctrl+PrintScreen"));
            Assert.That(rig.Session.IsSelected("AfterCapture.ShowDialog"), Is.True);
            Assert.That(rig.Session.TextOf("SaveFolderBox"), Is.EqualTo(ShellTestData.SaveFolder));
            Assert.That(rig.Session.IsSelected("Format.Png"), Is.True);
            Assert.That(rig.Session.TextOf("JpgQualityBox"), Is.EqualTo("90"));
            Assert.That(rig.Session.IsSelected("Delay.0"), Is.True);
            Assert.That(rig.Session.IsToggledOn("IncludeCursorCheck"), Is.False);
            Assert.That(rig.Session.IsSelected("Scope.MonitorUnderCursor"), Is.True);
            Assert.That(rig.Session.IsToggledOn("AutostartCheck"), Is.False);
            Assert.That(rig.Session.IsSelected("Language.System"), Is.True);
            Assert.That(rig.Session.IsSelected("Theme.System"), Is.True);
        });
    }

    [Test]
    public void Settings_QualityBox_IsOnlyEnabledForJpg()
    {
        using var rig = SettingsRig.Open();
        Assert.That(rig.Session.IsEnabled("JpgQualityBox"), Is.False, "PNG has no quality");

        rig.Session.Click("Format.Jpg");

        Assert.That(rig.Session.IsEnabled("JpgQualityBox"), Is.True);
    }

    [Test]
    public void Settings_HotkeyBox_ShowsTheChordThatWasPressed()
    {
        using var rig = SettingsRig.Open();
        rig.Session.Focus("HotkeyBox.Rectangle");

        rig.Session.PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_1);

        Assert.That(rig.Session.TextOf("HotkeyBox.Rectangle"), Is.EqualTo("Ctrl+Shift+1"));
    }

    [Test]
    public void Settings_Browse_PutsThePickedFolderInTheBox()
    {
        using var rig = SettingsRig.Open(configure: r => r.Picker.Answer = @"E:\Shots");

        rig.Session.Click("BrowseFolderButton");

        Assert.That(rig.Session.TextOf("SaveFolderBox"), Is.EqualTo(@"E:\Shots"));
        Assert.That(rig.Picker.Asked, Is.EqualTo(new[] { ShellTestData.SaveFolder }), "the picker should start at the current folder");
    }

    [Test]
    public void Settings_Save_SendsWhatWasEditedToTheUseCase()
    {
        using var rig = SettingsRig.Open();
        rig.Session.Click("Format.Jpg");
        rig.Session.Click("Delay.5");
        rig.Session.Click("IncludeCursorCheck");

        rig.Session.Click("SaveButton");

        Assert.That(rig.Session.WaitUntilClosed(TimeSpan.FromSeconds(3)), Is.True);
        var saved = rig.Shell.Applied.Single();
        Assert.Multiple(() =>
        {
            Assert.That(saved.Format, Is.EqualTo(Domain.Shared.ImageFormat.Jpg));
            Assert.That(saved.DelaySeconds, Is.EqualTo(5));
            Assert.That(saved.IncludeCursor, Is.True);
        });
    }

    [Test]
    public void F4_HotkeyRefused_ShowsTheReasonAndKeepsTheOldChord()
    {
        var message = NotificationMessage.Of("Shell.HotkeyNeedsModifier", "A");
        using var rig = SettingsRig.Open(configure: r => r.Shell.RefuseHotkeyWith = (HotkeyIssue.NeedsModifier, null, message));
        rig.Session.Focus("HotkeyBox.Rectangle");
        rig.Session.Press(VirtualKeyShort.KEY_A);
        Assert.That(rig.Session.TextOf("HotkeyBox.Rectangle"), Is.EqualTo("A"), "the box should show the chord being tried");

        rig.Session.Press(VirtualKeyShort.ENTER);

        var shown = rig.Session.TextOf("SettingsMessage");
        Assert.Multiple(() =>
        {
            Assert.That(shown, Is.EqualTo(rig.Host.Language.Format(message)), "the message is the localized text of the use case's message");
            Assert.That(shown, Does.Not.StartWith("Shell."), "a raw resource key was shown to the user");
            Assert.That(shown, Does.Contain("A"), "the message must name the chord that was refused");
            Assert.That(rig.Session.TextOf("HotkeyBox.Rectangle"), Is.EqualTo("PrintScreen"), "the old chord stays displayed");
            Assert.That(rig.Session.IsOpen, Is.True, "the window stays open so the user can try another chord");
            Assert.That(rig.Shell.Applied, Is.Empty, "nothing was saved");
        });
        rig.Session.Screenshot("shell-settings-f4-light");
    }

    [Test]
    public void F4_ChordUsedByAnotherKind_NamesThatKindInTheLanguageInUse()
    {
        var message = NotificationMessage.Of("Shell.HotkeyUsedByOtherKind", nameof(CaptureKind.Freeform));
        using var rig = SettingsRig.Open(configure: r => r.Shell.RefuseHotkeyWith = (HotkeyIssue.UsedByOtherKind, CaptureKind.Freeform, message));
        rig.Session.Focus("HotkeyBox.Rectangle");
        rig.Session.PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_2);
        rig.Session.Press(VirtualKeyShort.ENTER);

        Assert.That(rig.Session.TextOf("SettingsMessage"), Does.Contain("Vùng tự do"), "the kind's Vietnamese name, not 'Freeform'");
    }

    [Test]
    public void F5_MissingFolder_AsksToCreateIt_AndNoKeepsTheOldFolder()
    {
        using var rig = SettingsRig.Open(configure: r => r.Shell.FolderAnswer = FolderCheck.MissingCanCreate);
        rig.Session.SetText("SaveFolderBox", @"D:\New\Shots");

        rig.Session.Click("SaveButton");

        using var dialog = rig.Session.AttachToWindow("PromptDialog");
        Assert.Multiple(() =>
        {
            Assert.That(dialog.TextOf("PromptText"), Does.Contain(@"D:\New\Shots"), "the question names the folder");
            Assert.That(dialog.Exists("PromptYesButton"), Is.True);
            Assert.That(dialog.Exists("PromptNoButton"), Is.True);
        });
        dialog.Screenshot("shell-prompt-f5-light");

        dialog.Click("PromptNoButton");

        Assert.Multiple(() =>
        {
            Assert.That(rig.Session.TextOf("SaveFolderBox"), Is.EqualTo(ShellTestData.SaveFolder), "No keeps the old folder in the box");
            Assert.That(rig.Shell.FoldersCreated, Is.Empty, "No must not create anything");
            Assert.That(rig.Shell.Applied, Is.Empty, "the refused value must not be saved");
            Assert.That(rig.Session.IsOpen, Is.True);
        });
    }

    [Test]
    public void F5_MissingFolder_YesCreatesItAndSaves()
    {
        using var rig = SettingsRig.Open(configure: r => r.Shell.FolderAnswer = FolderCheck.MissingCanCreate);
        rig.Session.SetText("SaveFolderBox", @"D:\New\Shots");
        rig.Session.Click("SaveButton");

        using var dialog = rig.Session.AttachToWindow("PromptDialog");
        dialog.Click("PromptYesButton");

        Assert.That(rig.Session.WaitUntilClosed(TimeSpan.FromSeconds(3)), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(rig.Shell.FoldersCreated, Is.EqualTo(new[] { @"D:\New\Shots" }));
            Assert.That(rig.Shell.Applied.Single().SaveFolder, Is.EqualTo(@"D:\New\Shots"));
        });
    }

    [Test]
    public void F3_AutostartFails_TheSwitchGoesBackAndTheReasonShows()
    {
        var message = NotificationMessage.Of("Shell.AutostartFailed", "access denied");
        using var rig = SettingsRig.Open(configure: r => r.Shell.AutostartFailsWith = message);
        rig.Session.Click("AutostartCheck");
        Assert.That(rig.Session.IsToggledOn("AutostartCheck"), Is.True);

        rig.Session.Click("SaveButton");

        Assert.Multiple(() =>
        {
            Assert.That(rig.Session.IsToggledOn("AutostartCheck"), Is.False, "the switch returns to its old state");
            Assert.That(rig.Session.TextOf("SettingsMessage"), Does.Contain("access denied"));
        });
    }

    // Found by the main session reading T6: Apply received the OLD start-with-Windows value and SetAutostart, which does not
    // save, ran after it, so a switch Windows accepted was never written to the settings file.
    [Test]
    public void AutostartSwitchWindowsAccepted_IsWhatTheSettingsFileGets()
    {
        using var rig = SettingsRig.Open();
        rig.Session.Click("AutostartCheck");

        rig.Session.Click("SaveButton");

        Assert.Multiple(() =>
        {
            Assert.That(rig.Shell.AutostartChanges, Is.EqualTo(new[] { true }), "Windows is asked to add the entry once");
            Assert.That(rig.Shell.Applied, Has.Count.EqualTo(1));
            Assert.That(rig.Shell.Applied[0].StartWithWindows, Is.True, "the file must record the switch the user set");
        });
    }

    [Test]
    public void F2_SettingsFileNotWritten_TellsTheUserAndStillCloses()
    {
        var message = NotificationMessage.Of("Shell.SettingsNotSaved", "the file is read-only");
        using var rig = SettingsRig.Open(configure: r => r.Shell.ApplyFailsWith = message);

        rig.Session.Click("SaveButton");

        Assert.That(rig.Session.WaitUntilClosed(TimeSpan.FromSeconds(3)), Is.True, "the app keeps the settings for this session, so the window closes");
        Assert.That(rig.Notifications.Errors, Is.EqualTo(new[] { message }), "the user must be told the change will be lost");
    }

    [Test]
    public void Language_SwitchInSettings_ChangesTheTextOfTheOpenWindowAtOnce()
    {
        using var rig = SettingsRig.Open(ResolvedLanguage.Vietnamese);
        Assert.That(rig.Session.NameOf("SaveButton"), Is.EqualTo("Lưu"));

        rig.Host.Invoke(() => rig.Host.Language.Apply(ResolvedLanguage.English));
        rig.Host.Settle();

        Assert.That(rig.Session.NameOf("SaveButton"), Is.EqualTo("Save"), "the text must change without reopening the window");
        Assert.That(rig.Session.NameOf("CancelButton"), Is.EqualTo("Cancel"));
    }

    [Test]
    public void Language_ChoosingEnglishAndSaving_SwitchesEveryWindow()
    {
        using var rig = SettingsRig.Open(ResolvedLanguage.Vietnamese);
        rig.Session.Click("Language.English");

        rig.Session.Click("SaveButton");

        Assert.That(rig.Session.WaitUntilClosed(TimeSpan.FromSeconds(3)), Is.True);
        Assert.That(rig.Host.Language.Current, Is.EqualTo(ResolvedLanguage.English));
        Assert.That(rig.Host.Language.GetString("Settings.Save"), Is.EqualTo("Save"));
    }

    [Test]
    public void Theme_ChoosingDarkAndSaving_SwitchesTheTheme()
    {
        using var rig = SettingsRig.Open();
        rig.Session.Click("Theme.Dark");

        rig.Session.Click("SaveButton");

        Assert.That(rig.Session.WaitUntilClosed(TimeSpan.FromSeconds(3)), Is.True);
        Assert.That(rig.Host.Theme.IsDark, Is.True);
    }

    [TestCase(ResolvedLanguage.Vietnamese, AppTheme.Light, "vi-light")]
    [TestCase(ResolvedLanguage.Vietnamese, AppTheme.Dark, "vi-dark")]
    [TestCase(ResolvedLanguage.English, AppTheme.Light, "en-light")]
    [TestCase(ResolvedLanguage.English, AppTheme.Dark, "en-dark")]
    public void Screenshot_Settings_IsTakenForBothThemesAndLanguages(ResolvedLanguage language, AppTheme theme, string suffix)
    {
        using var rig = SettingsRig.Open(language, theme);

        var path = rig.Session.Screenshot($"shell-settings-{suffix}");

        Assert.That(new FileInfo(path).Length, Is.GreaterThan(5_000), "the picture is nearly empty: the window drew nothing");
        Assert.That(rig.Session.Exists("SaveButton"), Is.True, "open the picture: the Save button is missing from the window");
    }
}
