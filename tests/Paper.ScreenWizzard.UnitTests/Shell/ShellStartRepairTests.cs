using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UnitTests.Shell.Fakes;
using Paper.ScreenWizzard.UseCases.Shared.Models;
using Paper.ScreenWizzard.UseCases.Shell.Models;

namespace Paper.ScreenWizzard.UnitTests.Shell;

/// <summary>
/// SPEC shell, "Cài đặt" and F1: what start-up does when the settings on disk and the outside world disagree. A file that cannot be
/// read is not a corrupt file and is never written over; a chord that would grab a plain key is not registered; the start-with-Windows
/// entry follows the setting, not the other way round.
/// </summary>
[TestFixture]
public sealed class ShellStartRepairTests
{
    private ShellFixture _fixture = null!;

    [SetUp]
    public void SetUp() => _fixture = new ShellFixture();

    private static ShellStartInput StartInput() => new(true, ShellData.Pictures, "en-US", ShellData.OnePrimary, new PixelSize(300, 48));

    [Test]
    public void ASettingsFileThatCannotBeReadStartsOnDefaultsWithItsOwnNoticeAndIsNotWrittenOver()
    {
        _fixture.Store.LoadResult = new SettingsLoadResult(null, SettingsLoadStatus.Unreadable, @"C:\d\settings.json: used by another process");

        var result = _fixture.Create().Start(StartInput());

        Assert.That(result.ContinueRunning, Is.True);
        Assert.That(result.Notices.Select(n => n.Key), Is.EqualTo(new[] { "Shell.SettingsUnreadable" }));
        Assert.That(result.Notices[0].Arguments, Is.EqualTo(new[] { @"C:\d\settings.json: used by another process" }));
        Assert.That(_fixture.Store.Saved, Is.Empty, "the good file behind the lock must survive the start");
    }

    [Test]
    public void AfterAnUnreadableStartASaveIsRefusedWhileTheFileIsStillThereAndGoodOrStillLocked()
    {
        var interactor = _fixture.Create();
        _fixture.Store.LoadResult = new SettingsLoadResult(null, SettingsLoadStatus.Unreadable, "locked");
        var started = interactor.Start(StartInput());

        // The lock is gone and the file is a good document the app never saw: writing defaults over it would destroy it.
        _fixture.Store.LoadResult = new SettingsLoadResult(StoredSettings.From(ShellData.SpecDefaults()), SettingsLoadStatus.Loaded, null);
        var refusedGood = interactor.Apply(started.Settings);

        _fixture.Store.LoadResult = new SettingsLoadResult(null, SettingsLoadStatus.Unreadable, "still locked");
        var refusedLocked = interactor.Apply(started.Settings);

        Assert.That(refusedGood.Saved, Is.False);
        Assert.That(refusedLocked.Saved, Is.False);
        Assert.That(_fixture.Store.Saved, Is.Empty, "nothing was written in either case");
        Assert.That(refusedLocked.Message!.Arguments[0], Does.Contain("still locked"));
    }

    [Test]
    public void AfterAnUnreadableStartASaveGoesThroughOnceTheFileTurnsOutToBeBrokenOrGone()
    {
        var interactor = _fixture.Create();
        _fixture.Store.LoadResult = new SettingsLoadResult(null, SettingsLoadStatus.Unreadable, "locked");
        var started = interactor.Start(StartInput());

        _fixture.Store.LoadResult = new SettingsLoadResult(null, SettingsLoadStatus.Corrupt, "bad json");
        var applied = interactor.Apply(started.Settings);

        Assert.That(applied.Saved, Is.True);
        Assert.That(_fixture.Store.Saved, Has.Count.EqualTo(1));
    }

    [Test]
    public void AfterAnUnreadableStartASaveGoesThroughWhenTheFileTurnsOutToHoldAValueOutsideItsRange()
    {
        var interactor = _fixture.Create();
        _fixture.Store.LoadResult = new SettingsLoadResult(null, SettingsLoadStatus.Unreadable, "locked");
        var started = interactor.Start(StartInput());

        _fixture.Store.LoadResult = new SettingsLoadResult(StoredSettings.From(ShellData.SpecDefaults()) with { DelaySeconds = 99 }, SettingsLoadStatus.Loaded, null);
        var applied = interactor.Apply(started.Settings);

        Assert.That(applied.Saved, Is.True, "a broken file loses nothing when it is written over");
        Assert.That(_fixture.Store.Saved, Has.Count.EqualTo(1));
    }

    [Test]
    public void F8_AMissingSettingStartsWithoutNoticeAndWithoutBackup()
    {
        var written = ShellData.SpecDefaults() with { Theme = AppTheme.Dark, DelaySeconds = 3 };
        _fixture.Store.LoadResult = new SettingsLoadResult(StoredSettings.From(written) with { JpgQuality = null, SaveFolder = null }, SettingsLoadStatus.Loaded, null);

        var result = _fixture.Create().Start(StartInput());

        Assert.That(result.Settings.JpgQuality, Is.EqualTo(90), "SPEC shell Inputs: JPG quality 90");
        Assert.That(result.Settings.SaveFolder, Is.EqualTo(@"C:\Users\Hung\Pictures\Paper.ScreenWizzard"), "SPEC shell Inputs: the Pictures folder, Paper.ScreenWizzard");
        Assert.That(result.Settings.Theme, Is.EqualTo(AppTheme.Dark), "the settings the file has are kept");
        Assert.That(result.Settings.DelaySeconds, Is.EqualTo(3));
        Assert.That(result.Notices.Select(n => n.Key), Does.Not.Contain("Shell.SettingsCorrupt"));
        Assert.That(result.Notices, Is.Empty, "a missing setting is normal after an update: no notice");
        Assert.That(_fixture.Store.BackupCalls, Is.EqualTo(0), "no .bak");
        Assert.That(_fixture.Store.Saved, Is.Empty, "the file is not rewritten at start");
        var info = string.Join("\n", _fixture.Log.Lines.Where(l => l.StartsWith("I ", StringComparison.Ordinal)));
        Assert.That(info, Does.Contain("jpgQuality").And.Contain("saveFolder"), "one line in the log names what was missing");
    }

    [Test]
    public void F1_AnOutOfRangeFileIsKeptAsBakAndStartsOnDefaultsWithTheCorruptNotice()
    {
        var written = ShellData.SpecDefaults() with { SaveFolder = @"D:\Ảnh", JpgQuality = 0 };
        _fixture.Store.LoadResult = new SettingsLoadResult(StoredSettings.From(written), SettingsLoadStatus.Loaded, null);

        var result = _fixture.Create().Start(StartInput());

        Assert.That(_fixture.Store.BackupCalls, Is.EqualTo(1), "the broken file is kept as .bak");
        Assert.That(result.Notices.Select(n => n.Key), Does.Contain("Shell.SettingsCorrupt"));
        Assert.That(result.Settings.SaveFolder, Is.EqualTo(@"C:\Users\Hung\Pictures\Paper.ScreenWizzard"), "the app runs on the defaults");
        Assert.That(result.Settings.JpgQuality, Is.EqualTo(90));
        Assert.That(_fixture.Store.Saved, Is.Empty, "as with any broken file, nothing is written at start");
    }

    [Test]
    public void AHotkeyOfAPlainLetterInTheFileIsNotRegisteredTheDefaultTakesItsPlaceAndTheNoticeNamesIt()
    {
        var settings = ShellData.SpecDefaults();
        var hotkeys = new Dictionary<CaptureKind, HotkeyChord>(settings.Hotkeys)
        {
            [CaptureKind.Rectangle] = ShellData.Chord(HotkeyModifiers.None, "A"),
        };
        _fixture.Store.LoadResult = new SettingsLoadResult(StoredSettings.From(settings with { Hotkeys = hotkeys }), SettingsLoadStatus.Loaded, null);

        var result = _fixture.Create().Start(StartInput());

        Assert.That(_fixture.Hotkeys.Registered[CaptureKind.Rectangle], Is.EqualTo(ShellData.PrintScreen));
        Assert.That(result.Settings.Hotkeys[CaptureKind.Rectangle], Is.EqualTo(ShellData.PrintScreen));
        Assert.That(result.Notices.Select(n => n.Key), Does.Contain("Shell.HotkeyUnsafeAtStart"));
        Assert.That(result.Notices.First(n => n.Key == "Shell.HotkeyUnsafeAtStart").Arguments, Is.EqualTo(new[] { "A" }));
    }

    [Test]
    public void StartWithWindowsOnInTheSettingsAndNoRunEntryWritesTheEntryAgain()
    {
        var settings = ShellData.SpecDefaults() with { StartWithWindows = true };
        _fixture.Store.LoadResult = new SettingsLoadResult(StoredSettings.From(settings), SettingsLoadStatus.Loaded, null);
        _fixture.Autostart.Enabled = false;

        _fixture.Create().Start(StartInput());

        Assert.That(_fixture.Autostart.SetEnabledCalls, Is.EqualTo(new[] { true }));
        Assert.That(_fixture.Autostart.Enabled, Is.True);
    }

    [Test]
    public void StartWithWindowsOffInTheSettingsAndARunEntryLeftBehindRemovesTheEntry()
    {
        var settings = ShellData.SpecDefaults() with { StartWithWindows = false };
        _fixture.Store.LoadResult = new SettingsLoadResult(StoredSettings.From(settings), SettingsLoadStatus.Loaded, null);
        _fixture.Autostart.Enabled = true;

        _fixture.Create().Start(StartInput());

        Assert.That(_fixture.Autostart.SetEnabledCalls, Is.EqualTo(new[] { false }));
        Assert.That(_fixture.Autostart.Enabled, Is.False);
    }

    [Test]
    public void StartWithWindowsThatAlreadyAgreesWithTheEntryTouchesNothing()
    {
        var settings = ShellData.SpecDefaults() with { StartWithWindows = true };
        _fixture.Store.LoadResult = new SettingsLoadResult(StoredSettings.From(settings), SettingsLoadStatus.Loaded, null);
        _fixture.Autostart.Enabled = true;

        _fixture.Create().Start(StartInput());

        Assert.That(_fixture.Autostart.SetEnabledCalls, Is.Empty);
    }
}
