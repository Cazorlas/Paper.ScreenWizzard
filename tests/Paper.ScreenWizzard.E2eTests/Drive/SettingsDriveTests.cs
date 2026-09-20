using FlaUI.Core.AutomationElements;
using FlaUI.Core.WindowsAPI;
using Microsoft.Win32;
using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.E2eTests.Support;

namespace Paper.ScreenWizzard.E2eTests.Drive;

/// <summary>Plan T18, flow 10: the Settings window opened from the bar, on the real exe, with the file it writes read back.</summary>
[TestFixture]
public sealed class SettingsDriveTests : DriveBase
{
    private AutomationElement OpenSettings(AutomationElement bar)
    {
        AppRun.Invoke(bar, "SettingsButton");
        return App.WaitWindow("SettingsWindow", 8);
    }

    private static void Choose(AutomationElement window, string id) => AppRun.Require(window, id).Patterns.SelectionItem.Pattern.Select();

    private static string JsonString(AppRun app, string name)
    {
        using var json = app.ReadSettingsJson();
        return json.RootElement.GetProperty(name).ToString();
    }

    [Test]
    public void SaveFolder_ThatDoesNotExist_AsksToCreateIt_NoKeepsTheOldOne_YesCreatesAndSaves()
    {
        NewApp();
        var bar = StartAndWaitForBar();
        var settings = OpenSettings(bar);
        AppRun.Shot(settings, ShotName("settings-vi", "m0"));
        var newFolder = Path.Combine(App.ScratchPath, "brand-new", "photos");

        // No: nothing is created, the old folder stays in the box and in the file.
        var box = AppRun.Require(settings, "SaveFolderBox");
        box.Patterns.Value.Pattern.SetValue(newFolder);
        AppRun.Invoke(settings, "SaveButton");
        var prompt = App.WaitWindow("PromptDialog", 6);
        var question = AppRun.TextOf(prompt, "PromptText");
        Assert.That(question, Does.Contain(newFolder).And.Contain("Tạo thư mục"), "the prompt names the folder and asks whether to create it");
        AppRun.Shot(prompt, ShotName("create-folder-prompt", "m0"));
        AppRun.Invoke(prompt, "PromptNoButton");
        Assert.That(App.WaitWindowGone("PromptDialog"), Is.True);
        Thread.Sleep(300);
        Assert.That(Directory.Exists(newFolder), Is.False, "answering No creates nothing");
        Assert.That(App.FindWindow("SettingsWindow"), Is.Not.Null, "the Settings window stays open");
        Assert.That(AppRun.TextOf(settings, "SaveFolderBox"), Is.EqualTo(App.SaveFolder), "the box shows the old folder again");
        Assert.That(AppRun.TextOf(settings, "SettingsMessage"), Does.Contain("chưa tồn tại"), "and says the folder does not exist");
        Assert.That(JsonString(App, "saveFolder"), Is.EqualTo(App.SaveFolder), "the file still holds the old folder");

        // Yes: created, saved, the window closes.
        AppRun.Require(settings, "SaveFolderBox").Patterns.Value.Pattern.SetValue(newFolder);
        AppRun.Invoke(settings, "SaveButton");
        prompt = App.WaitWindow("PromptDialog", 6);
        AppRun.Invoke(prompt, "PromptYesButton");
        Assert.That(App.WaitWindowGone("SettingsWindow"), Is.True, "Save closes the window");
        Assert.That(Directory.Exists(newFolder), Is.True, "answering Yes creates the folder");
        Assert.That(JsonString(App, "saveFolder"), Is.EqualTo(newFolder), "and the file holds the new folder");
    }

    [Test]
    public void ARegisteredHotkey_PressedWhileSettingsIsOpen_StartsNoCapture()
    {
        // The chord a user wants to type into a hotkey box is often one the app itself holds; Windows hands it to the app, not to the box,
        // and a capture must not start under the Settings window because of it.
        NewApp();
        var bar = StartAndWaitForBar();
        var settings = OpenSettings(bar);
        Assert.That(settings, Is.Not.Null, "set-up: Settings is open");

        AppRun.PressHotkey(CaptureKind.Rectangle);
        Thread.Sleep(1500);

        Assert.That(App.FindWindow("SelectionOverlay"), Is.Null, "no overlay opened over the Settings window");
        Assert.That(App.FindWindow("SettingsWindow"), Is.Not.Null, "Settings is still there");
    }

    [Test]
    public void Language_English_ChangesTheOpenWindowsAtOnce_AndIsWrittenToTheFile()
    {
        NewApp();
        var bar = StartAndWaitForBar();
        Assert.That(AppRun.Require(bar, "CaptureButton.Rectangle").Name, Is.EqualTo("Vùng chữ nhật"), "set-up: Vietnamese");
        var settings = OpenSettings(bar);
        Assert.That(AppRun.Require(settings, "SaveButton").Name, Is.EqualTo("Lưu"));

        Choose(settings, "Language.English");
        AppRun.Invoke(settings, "SaveButton");
        Assert.That(App.WaitWindowGone("SettingsWindow"), Is.True);
        Thread.Sleep(400);

        Assert.That(AppRun.Require(bar, "CaptureButton.Rectangle").Name, Is.EqualTo("Rectangle"), "the capture bar, still open, now speaks English");
        Assert.That(JsonString(App, "language"), Is.EqualTo("English"));
        AppRun.Shot(bar, ShotName("capturebar-en", "m0"));
        var reopened = OpenSettings(bar);
        Assert.That(AppRun.Require(reopened, "SaveButton").Name, Is.EqualTo("Save"), "and so does the Settings window");
        AppRun.Shot(reopened, ShotName("settings-en", "m0"));
    }

    [Test]
    public void Escape_InSettings_ClosesWithoutSavingWhatWasChanged()
    {
        NewApp();
        var bar = StartAndWaitForBar();
        var settings = OpenSettings(bar);
        Choose(settings, "Delay.5");
        AppKeys.Type(App, settings, VirtualKeyShort.ESCAPE);
        Assert.That(App.WaitWindowGone("SettingsWindow"), Is.True, "Esc closes the window");
        Assert.That(JsonString(App, "delaySeconds"), Is.EqualTo("0"), "the delay in the file is unchanged");
    }

    [Test]
    public void Hotkey_ALetterAlone_IsRefusedWithTheReason_ThenANewChordTakesEffectAtOnce()
    {
        NewApp();
        var bar = StartAndWaitForBar();
        ShowProbe(Desk.Monitor(0), "hotkey");
        var settings = OpenSettings(bar);

        // A alone on the rectangle box.
        var box = AppRun.Require(settings, "HotkeyBox.Rectangle");
        box.Focus();
        AppKeys.Type(App, settings, VirtualKeyShort.KEY_A);
        Assert.That(AppRun.TextOf(settings, "HotkeyBox.Rectangle"), Is.EqualTo("A"), "the box shows the key that was pressed");
        AppRun.Invoke(settings, "SaveButton");
        Thread.Sleep(500);
        var message = AppRun.TextOf(settings, "SettingsMessage");
        Assert.That(message, Does.Contain("A").And.Contain("cướp việc gõ chữ"), "the refusal names the key and the reason (SPEC shell F4)");
        Assert.That(App.FindWindow("SettingsWindow"), Is.Not.Null, "the window stays open");
        Assert.That(AppRun.TextOf(settings, "HotkeyBox.Rectangle"), Is.EqualTo("Ctrl+Alt+Shift+F13"), "and the box is back to the working chord");
        AppRun.Shot(settings, ShotName("settings-hotkey-refused", "m0"));

        // Ctrl+Alt+Shift+F17 is accepted. F13 no longer captures, F17 does.
        AppRun.Require(settings, "HotkeyBox.Rectangle").Focus();
        AppKeys.Chord(App, settings, VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.SHIFT, VirtualKeyShort.F17);
        Assert.That(AppRun.TextOf(settings, "HotkeyBox.Rectangle"), Is.EqualTo("Ctrl+Alt+Shift+F17"));
        AppRun.Invoke(settings, "SaveButton");
        Assert.That(App.WaitWindowGone("SettingsWindow"), Is.True, "Save closes the window when the chord is accepted");
        using (var json = App.ReadSettingsJson())
        {
            Assert.That(json.RootElement.GetProperty("hotkeys").GetProperty("Rectangle").GetProperty("key").GetString(), Is.EqualTo("F17"), "the file holds the new chord");
        }

        AppRun.PressHotkey(CaptureKind.Rectangle);
        Thread.Sleep(2000);
        Assert.That(App.FindWindow("SelectionOverlay"), Is.Null, "the old chord (F13) no longer captures");
        KeySender.Press(AppRun.Chorded, KeySender.FunctionKey(17));
        var overlay = App.WaitWindow("SelectionOverlay", 8);
        Assert.That(overlay, Is.Not.Null, "the new chord (F17) captures at once, without a restart");
        Desk.RightClickAt(Desk.Cursor().X, Desk.Cursor().Y);
        App.WaitWindowGone("SelectionOverlay");
    }

    [Test]
    public void StartWithWindows_WritesTheRunValueAndRemovesIt_AndNothingIsLeftBehind()
    {
        NewApp();
        var bar = StartAndWaitForBar();
        try
        {
            Assert.That(RunValue(App.Instance), Is.Null, "set-up: no Run value yet");
            var settings = OpenSettings(bar);
            var check = AppRun.Require(settings, "AutostartCheck");
            check.Patterns.Toggle.Pattern.Toggle();
            AppRun.Invoke(settings, "SaveButton");
            Assert.That(App.WaitWindowGone("SettingsWindow"), Is.True);

            var value = RunValue(App.Instance);
            Assert.That(value, Is.Not.Null, "the Run value named after this instance appeared");
            Assert.That(value, Does.Contain("Paper.ScreenWizzard.exe").And.Contain("--autostart"), "it starts the exe hidden: " + value);
            Assert.That(JsonString(App, "startWithWindows"), Is.EqualTo("True"), "and the file remembers the switch");

            settings = OpenSettings(bar);
            Assert.That(AppRun.Require(settings, "AutostartCheck").Patterns.Toggle.Pattern.ToggleState.Value, Is.EqualTo(FlaUI.Core.Definitions.ToggleState.On), "the switch shows on when reopened");
            AppRun.Require(settings, "AutostartCheck").Patterns.Toggle.Pattern.Toggle();
            AppRun.Invoke(settings, "SaveButton");
            Assert.That(App.WaitWindowGone("SettingsWindow"), Is.True);
            Assert.That(RunValue(App.Instance), Is.Null, "turning it off removed the Run value");
            Assert.That(JsonString(App, "startWithWindows"), Is.EqualTo("False"));
        }
        finally
        {
            using var key = Registry.CurrentUser.OpenSubKey(AppRun.RunKeyPath, writable: true);
            key?.DeleteValue(App.Instance, throwOnMissingValue: false);
            var leftovers = key?.GetValueNames().Where(n => n.StartsWith("Paper.ScreenWizzard.Drive.", StringComparison.Ordinal)).ToList() ?? [];
            Assert.That(leftovers, Is.Empty, "no Run value of the drive tests is left in HKCU");
        }
    }

    private static string? RunValue(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(AppRun.RunKeyPath);
        return key?.GetValue(name) as string;
    }
}
