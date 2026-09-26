using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UnitTests.Shell.Fakes;

namespace Paper.ScreenWizzard.UnitTests.Shell;

/// <summary>
/// SPEC shell, "Cài đặt được giữ lại", F1 and F8: what a settings file may lack and what makes it broken. A file written by an older
/// version lacks the settings added since; that is normal and costs the user nothing. Only a value outside its range breaks the file.
/// </summary>
[TestFixture]
public sealed class SettingsRulesTests
{
    // A file an older version wrote: everything the user set, and nothing else.
    private static StoredSettings Written() => StoredSettings.From(Chosen());

    // Every setting away from its default, so a setting that silently falls back to the default shows.
    private static AppSettings Chosen() => ShellData.SpecDefaults() with
    {
        Hotkeys = new Dictionary<CaptureKind, HotkeyChord>
        {
            [CaptureKind.Rectangle] = ShellData.Chord(HotkeyModifiers.Control | HotkeyModifiers.Shift, "1"),
            [CaptureKind.Freeform] = ShellData.Chord(HotkeyModifiers.None, "F9"),
            [CaptureKind.Window] = ShellData.Chord(HotkeyModifiers.Alt, "F10"),
            [CaptureKind.FullScreen] = ShellData.Chord(HotkeyModifiers.Control, "F11"),
        },
        AfterCapture = AfterCaptureAction.ClipboardAndFile,
        SaveFolder = @"D:\Ảnh",
        Format = ImageFormat.Jpg,
        JpgQuality = 55,
        DelaySeconds = 5,
        IncludeCursor = true,
        FullScreenScope = FullScreenScope.AllMonitors,
        StartWithWindows = true,
        Language = AppLanguage.Vietnamese,
        Theme = AppTheme.Dark,
        CaptureBarPosition = new PixelPoint(-800, 20),
        CheckForUpdates = false,
    };

    [Test]
    public void AFileWithEverySetting_IsTakenAsItIs_WithNothingDefaulted()
    {
        var expected = Chosen();

        var completion = SettingsRules.Complete(StoredSettings.From(expected), ShellData.SpecDefaults());

        Assert.That(completion.Problem, Is.Null);
        Assert.That(completion.Defaulted, Is.Empty);
        var settings = completion.Settings!;
        Assert.That(settings.Hotkeys, Is.EquivalentTo(expected.Hotkeys));
        Assert.That(settings with { Hotkeys = expected.Hotkeys }, Is.EqualTo(expected), "every other setting as the file has it");
    }

    [Test]
    public void F8_AMissingSettingTakesItsDefault_TheOthersAreKept()
    {
        var stored = Written() with { DelaySeconds = null, Theme = null };

        var completion = SettingsRules.Complete(stored, ShellData.SpecDefaults());

        Assert.That(completion.Problem, Is.Null);
        var settings = completion.Settings!;
        Assert.That(settings.DelaySeconds, Is.EqualTo(0), "SPEC shell Inputs: delay 0 seconds");
        Assert.That(settings.Theme, Is.EqualTo(AppTheme.System), "SPEC shell Inputs: theme follows Windows");
        Assert.That(settings.SaveFolder, Is.EqualTo(@"D:\Ảnh"), "a setting the file has is kept");
        Assert.That(settings.JpgQuality, Is.EqualTo(55));
        Assert.That(settings.Language, Is.EqualTo(AppLanguage.Vietnamese));
        Assert.That(completion.Defaulted, Is.EquivalentTo(new[] { "delaySeconds", "theme" }));
    }

    [Test]
    public void F8_EverySettingMissing_IsTheDefaults()
    {
        var defaults = ShellData.SpecDefaults();

        var completion = SettingsRules.Complete(StoredSettings.Empty, defaults);

        Assert.That(completion.Problem, Is.Null);
        var settings = completion.Settings!;
        Assert.That(settings.Hotkeys, Is.EquivalentTo(defaults.Hotkeys));
        Assert.That(settings with { Hotkeys = defaults.Hotkeys }, Is.EqualTo(defaults));
    }

    [Test]
    public void F8_AFileFromBeforeTheUpdateCheck_HasTheCheckOn()
    {
        var completion = SettingsRules.Complete(Written() with { CheckForUpdates = null }, ShellData.SpecDefaults());

        Assert.That(completion.Settings!.CheckForUpdates, Is.True, "0.1.2 and older did not write it; the default is on");
        Assert.That(completion.Defaulted, Is.EquivalentTo(new[] { "checkForUpdates" }));
    }

    [Test]
    public void F8_AMissingHotkeyTableGivesTheDefaultKeys()
    {
        var completion = SettingsRules.Complete(Written() with { Hotkeys = null }, ShellData.SpecDefaults());

        Assert.That(completion.Problem, Is.Null);
        var hotkeys = completion.Settings!.Hotkeys;
        Assert.That(hotkeys[CaptureKind.Rectangle], Is.EqualTo(ShellData.PrintScreen));
        Assert.That(hotkeys[CaptureKind.FullScreen], Is.EqualTo(ShellData.CtrlPrintScreen));
        Assert.That(completion.Defaulted, Is.EquivalentTo(new[] { "hotkeys" }));
    }

    [Test]
    public void F8_AMissingKindTakesItsDefaultKey()
    {
        var hotkeys = new Dictionary<string, StoredHotkey?>
        {
            ["Rectangle"] = new(HotkeyModifiers.Control | HotkeyModifiers.Shift, "1"),
            ["Freeform"] = new(HotkeyModifiers.None, "F9"),
        };

        var completion = SettingsRules.Complete(Written() with { Hotkeys = hotkeys }, ShellData.SpecDefaults());

        Assert.That(completion.Problem, Is.Null);
        var chords = completion.Settings!.Hotkeys;
        Assert.That(chords[CaptureKind.Rectangle], Is.EqualTo(ShellData.Chord(HotkeyModifiers.Control | HotkeyModifiers.Shift, "1")));
        Assert.That(chords[CaptureKind.Window], Is.EqualTo(ShellData.AltPrintScreen));
        Assert.That(chords[CaptureKind.FullScreen], Is.EqualTo(ShellData.CtrlPrintScreen));
        Assert.That(completion.Defaulted, Is.EquivalentTo(new[] { "hotkeys.Window", "hotkeys.FullScreen" }));
    }

    [Test]
    public void F8_AHotkeyWithoutModifiersIsTheKeyAlone()
    {
        var hotkeys = new Dictionary<string, StoredHotkey?> { ["Rectangle"] = new(null, "PrintScreen") };

        var completion = SettingsRules.Complete(Written() with { Hotkeys = hotkeys }, ShellData.SpecDefaults());

        Assert.That(completion.Settings!.Hotkeys[CaptureKind.Rectangle], Is.EqualTo(ShellData.PrintScreen));
    }

    [TestCase(0)]
    [TestCase(101)]
    [TestCase(-5)]
    public void F1_JpgQualityOutsideOneToHundredIsBroken(int quality)
    {
        var completion = SettingsRules.Complete(Written() with { JpgQuality = quality }, ShellData.SpecDefaults());

        Assert.That(completion.Settings, Is.Null);
        Assert.That(completion.Problem, Does.Contain(quality.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    [TestCase(1)]
    [TestCase(100)]
    public void JpgQualityAtTheEdgesIsKept(int quality)
    {
        var completion = SettingsRules.Complete(Written() with { JpgQuality = quality }, ShellData.SpecDefaults());

        Assert.That(completion.Settings!.JpgQuality, Is.EqualTo(quality));
    }

    [TestCase(-1)]
    [TestCase(11)]
    [TestCase(99999)]
    public void F1_DelayBelowZeroOrAboveTenIsBroken(int seconds)
    {
        var completion = SettingsRules.Complete(Written() with { DelaySeconds = seconds }, ShellData.SpecDefaults());

        Assert.That(completion.Settings, Is.Null);
        Assert.That(completion.Problem, Is.Not.Null.And.Not.Empty);
    }

    [TestCase(0)]
    [TestCase(10)]
    public void DelayAtTheEdgesIsKept(int seconds)
    {
        var completion = SettingsRules.Complete(Written() with { DelaySeconds = seconds }, ShellData.SpecDefaults());

        Assert.That(completion.Settings!.DelaySeconds, Is.EqualTo(seconds));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void F1_AnEmptySaveFolderIsBroken(string folder)
    {
        var completion = SettingsRules.Complete(Written() with { SaveFolder = folder }, ShellData.SpecDefaults());

        Assert.That(completion.Settings, Is.Null);
        Assert.That(completion.Problem, Is.Not.Null.And.Not.Empty);
    }

    [TestCase("Scrolling", "PrintScreen")]
    [TestCase("7", "PrintScreen")]
    [TestCase("1", "PrintScreen")]
    [TestCase("Rectangle, Freeform", "PrintScreen")]
    [TestCase("rectangle", "PrintScreen")]
    [TestCase("Rectangle", "")]
    [TestCase("Rectangle", null)]
    public void F1_AHotkeyForAnUnknownKindOrWithoutAKeyIsBroken(string kind, string? key)
    {
        var hotkeys = new Dictionary<string, StoredHotkey?> { [kind] = new(HotkeyModifiers.Control, key) };

        var completion = SettingsRules.Complete(Written() with { Hotkeys = hotkeys }, ShellData.SpecDefaults());

        Assert.That(completion.Settings, Is.Null);
        Assert.That(completion.Problem, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void F1_ANullHotkeyIsBroken()
    {
        var hotkeys = new Dictionary<string, StoredHotkey?> { ["Rectangle"] = null };

        var completion = SettingsRules.Complete(Written() with { Hotkeys = hotkeys }, ShellData.SpecDefaults());

        Assert.That(completion.Settings, Is.Null);
    }

    [Test]
    public void TheLimitsAreTheOnesTheSettingsWindowOffers()
    {
        // SPEC capture, Inputs: the delay is chosen among 0, 3, 5, 10; the JPG box reads "1-100".
        Assert.That(SettingsRules.IsDelay(10), Is.True);
        Assert.That(SettingsRules.IsDelay(11), Is.False);
        Assert.That(SettingsRules.IsJpgQuality(1) && SettingsRules.IsJpgQuality(100), Is.True);
        Assert.That(SettingsRules.IsJpgQuality(0) || SettingsRules.IsJpgQuality(101), Is.False);
    }
}
