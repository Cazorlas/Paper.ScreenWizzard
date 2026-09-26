using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UnitTests.Shell.Fakes;
using Paper.ScreenWizzard.UseCases.Common.Models;
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
        _fixture.Store.LoadResult = new SettingsLoadResult(ShellData.SpecDefaults(), SettingsLoadStatus.Loaded, null);
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
    public void AHotkeyOfAPlainLetterInTheFileIsNotRegisteredTheDefaultTakesItsPlaceAndTheNoticeNamesIt()
    {
        var settings = ShellData.SpecDefaults();
        var hotkeys = new Dictionary<CaptureKind, HotkeyChord>(settings.Hotkeys)
        {
            [CaptureKind.Rectangle] = ShellData.Chord(HotkeyModifiers.None, "A"),
        };
        _fixture.Store.LoadResult = new SettingsLoadResult(settings with { Hotkeys = hotkeys }, SettingsLoadStatus.Loaded, null);

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
        _fixture.Store.LoadResult = new SettingsLoadResult(settings, SettingsLoadStatus.Loaded, null);
        _fixture.Autostart.Enabled = false;

        _fixture.Create().Start(StartInput());

        Assert.That(_fixture.Autostart.SetEnabledCalls, Is.EqualTo(new[] { true }));
        Assert.That(_fixture.Autostart.Enabled, Is.True);
    }

    [Test]
    public void StartWithWindowsOffInTheSettingsAndARunEntryLeftBehindRemovesTheEntry()
    {
        var settings = ShellData.SpecDefaults() with { StartWithWindows = false };
        _fixture.Store.LoadResult = new SettingsLoadResult(settings, SettingsLoadStatus.Loaded, null);
        _fixture.Autostart.Enabled = true;

        _fixture.Create().Start(StartInput());

        Assert.That(_fixture.Autostart.SetEnabledCalls, Is.EqualTo(new[] { false }));
        Assert.That(_fixture.Autostart.Enabled, Is.False);
    }

    [Test]
    public void StartWithWindowsThatAlreadyAgreesWithTheEntryTouchesNothing()
    {
        var settings = ShellData.SpecDefaults() with { StartWithWindows = true };
        _fixture.Store.LoadResult = new SettingsLoadResult(settings, SettingsLoadStatus.Loaded, null);
        _fixture.Autostart.Enabled = true;

        _fixture.Create().Start(StartInput());

        Assert.That(_fixture.Autostart.SetEnabledCalls, Is.Empty);
    }
}
