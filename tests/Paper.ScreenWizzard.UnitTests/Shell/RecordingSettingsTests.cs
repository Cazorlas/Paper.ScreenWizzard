using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Recorder;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UnitTests.Shell.Fakes;
using Paper.ScreenWizzard.UseCases.Shell.Models;

namespace Paper.ScreenWizzard.UnitTests.Shell;

/// <summary>
/// SPEC recorder, Inputs and F8: the recording hotkeys and settings, kept with the others; a file of 0.1.3, which has none of them,
/// still loads (SPEC shell F8).
/// </summary>
[TestFixture]
public sealed class RecordingSettingsTests
{
    private static readonly HotkeyChord CtrlAltR = new(HotkeyModifiers.Control | HotkeyModifiers.Alt, "R");
    private static readonly HotkeyChord CtrlAltP = new(HotkeyModifiers.Control | HotkeyModifiers.Alt, "P");

    private ShellFixture _fixture = null!;

    [SetUp]
    public void SetUp() => _fixture = new ShellFixture();

    private static ShellStartInput Start() => new(true, ShellData.Pictures, "en-US", ShellData.OnePrimary, new PixelSize(400, 48));

    [Test]
    public void TheDefaults_AreCtrlAltR_CtrlAltP_AndTheRecordingBarsChoices()
    {
        var settings = SettingsDefaults.Create(ShellData.Pictures);

        Assert.That(settings.RecordHotkeys, Is.EqualTo(new RecordingHotkeys(CtrlAltR, CtrlAltP)));
        Assert.That(settings.Recorder, Is.EqualTo(new RecorderSettings(RecordTargetKind.Monitor, null, true, false, true, 3, 30, @"C:\Users\Hung\Videos\Paper.ScreenWizzard")));
        Assert.That(SettingsDefaults.Create(ShellData.Pictures, @"D:\Phim").Recorder.VideoFolder, Is.EqualTo(@"D:\Phim\Paper.ScreenWizzard"), "the Videos folder Windows names");
    }

    [Test]
    public void AFileOf013_WithNoRecordingSettings_TakesTheDefaults()
    {
        var stored = StoredSettings.From(ShellData.SpecDefaults()) with { RecordHotkeys = null, Recorder = null };

        var completion = SettingsRules.Complete(stored, ShellData.SpecDefaults());

        Assert.That(completion.Problem, Is.Null);
        Assert.That(completion.Settings!.RecordHotkeys, Is.EqualTo(new RecordingHotkeys(CtrlAltR, CtrlAltP)));
        Assert.That(completion.Settings.Recorder.FramesPerSecond, Is.EqualTo(30));
        Assert.That(completion.Defaulted, Is.EquivalentTo(new[] { "recordHotkeys", "recorder" }));
    }

    [Test]
    public void TheRecordingChoices_AreKept()
    {
        var chosen = ShellData.SpecDefaults() with
        {
            RecordHotkeys = new RecordingHotkeys(new HotkeyChord(HotkeyModifiers.Shift, "F9"), new HotkeyChord(HotkeyModifiers.Shift, "F10")),
            Recorder = new RecorderSettings(RecordTargetKind.Region, 1, false, true, false, 5, 60, @"E:\Quay"),
        };

        var completion = SettingsRules.Complete(StoredSettings.From(chosen), ShellData.SpecDefaults());

        Assert.That(completion.Settings!.RecordHotkeys, Is.EqualTo(chosen.RecordHotkeys));
        Assert.That(completion.Settings.Recorder, Is.EqualTo(chosen.Recorder));
        Assert.That(completion.Defaulted, Is.Empty);
    }

    [Test]
    public void AMissingRecordingSetting_TakesItsDefault_TheOthersAreKept()
    {
        var stored = StoredSettings.From(ShellData.SpecDefaults()) with
        {
            Recorder = new StoredRecorder(RecordTargetKind.Desktop, null, null, true, null, null, 15, null),
            RecordHotkeys = new Dictionary<string, StoredHotkey?> { ["Pause"] = new(HotkeyModifiers.Shift, "F10") },
        };

        var completion = SettingsRules.Complete(stored, ShellData.SpecDefaults());

        var recorder = completion.Settings!.Recorder;
        Assert.That(recorder.Target, Is.EqualTo(RecordTargetKind.Desktop));
        Assert.That(recorder.Microphone, Is.True);
        Assert.That(recorder.FramesPerSecond, Is.EqualTo(15));
        Assert.That(recorder.CountdownSeconds, Is.EqualTo(3), "missing: the default");
        Assert.That(completion.Settings.RecordHotkeys, Is.EqualTo(new RecordingHotkeys(CtrlAltR, new HotkeyChord(HotkeyModifiers.Shift, "F10"))));
        Assert.That(completion.Defaulted, Does.Contain("recorder.countdownSeconds").And.Contain("recordHotkeys.StartStop"));
    }

    [TestCase(25, 3)]
    [TestCase(0, 3)]
    [TestCase(30, 4)]
    [TestCase(30, -1)]
    public void AFrameRateOrCountdownOutsideTheChoices_BreaksTheFile(int fps, int countdown)
    {
        var stored = StoredSettings.From(ShellData.SpecDefaults()) with
        {
            Recorder = new StoredRecorder(RecordTargetKind.Monitor, null, true, false, true, countdown, fps, @"C:\Videos"),
        };

        var completion = SettingsRules.Complete(stored, ShellData.SpecDefaults());

        Assert.That(completion.Settings, Is.Null);
        Assert.That(completion.Problem, Is.Not.Null.And.Not.Empty);
    }

    [TestCase("Record", "R")]
    [TestCase("StartStop", "")]
    public void AnUnknownRecordingHotkeyOrOneWithoutAKey_BreaksTheFile(string name, string key)
    {
        var stored = StoredSettings.From(ShellData.SpecDefaults()) with
        {
            RecordHotkeys = new Dictionary<string, StoredHotkey?> { [name] = new(HotkeyModifiers.Control, key) },
        };

        Assert.That(SettingsRules.Complete(stored, ShellData.SpecDefaults()).Settings, Is.Null);
    }

    [Test]
    public void AnEmptyVideoFolder_BreaksTheFile()
    {
        var stored = StoredSettings.From(ShellData.SpecDefaults()) with
        {
            Recorder = new StoredRecorder(RecordTargetKind.Monitor, null, true, false, true, 3, 30, "  "),
        };

        Assert.That(SettingsRules.Complete(stored, ShellData.SpecDefaults()).Settings, Is.Null);
    }

    [Test]
    public void AtStart_TheRecordingHotkeysAreRegistered()
    {
        _fixture.Store.LoadResult = new SettingsLoadResult(StoredSettings.From(ShellData.SpecDefaults()), SettingsLoadStatus.Loaded, null);

        var result = _fixture.Create().Start(Start());

        Assert.That(_fixture.Hotkeys.RecordRegistered[RecordHotkey.StartStop], Is.EqualTo(CtrlAltR));
        Assert.That(_fixture.Hotkeys.RecordRegistered[RecordHotkey.Pause], Is.EqualTo(CtrlAltP));
        Assert.That(result.Notices, Is.Empty);
    }

    [Test]
    public void F8_ARecordingHotkeyHeldByAnotherProgram_IsSaid_AndTheOthersStillWork()
    {
        _fixture.Store.LoadResult = new SettingsLoadResult(StoredSettings.From(ShellData.SpecDefaults()), SettingsLoadStatus.Loaded, null);
        _fixture.Hotkeys.HeldByOtherPrograms.Add(CtrlAltR);

        var result = _fixture.Create().Start(Start());

        Assert.That(result.Notices.Select(n => n.Key), Is.EqualTo(new[] { "Shell.HotkeyUnavailableAtStart" }));
        Assert.That(result.Notices[0].Arguments, Is.EqualTo(new[] { "Ctrl+Alt+R" }));
        Assert.That(result.HotkeysRegistered, Has.Count.EqualTo(4), "every capture key still works");
        Assert.That(_fixture.Hotkeys.RecordRegistered.ContainsKey(RecordHotkey.Pause), Is.True, "and the pause key");
    }

    [Test]
    public void ARecordingHotkey_TakenByACaptureKind_IsRefused()
    {
        var current = ShellData.SpecDefaults();

        var result = _fixture.Create().ChangeRecordHotkey(current, RecordHotkey.StartStop, ShellData.CtrlPrintScreen);

        Assert.That(result.Accepted, Is.False);
        Assert.That(result.Issue, Is.EqualTo(HotkeyIssue.UsedByOtherKind));
        Assert.That(result.ConflictingKind, Is.EqualTo(CaptureKind.FullScreen));
        Assert.That(result.Settings.RecordHotkeys.StartStop, Is.EqualTo(CtrlAltR), "the old key stays");
    }

    [Test]
    public void ACaptureHotkey_TakenByARecordingOne_IsRefused()
    {
        var result = _fixture.Create().ChangeHotkey(ShellData.SpecDefaults(), CaptureKind.Rectangle, CtrlAltP);

        Assert.That(result.Accepted, Is.False);
        Assert.That(result.Issue, Is.EqualTo(HotkeyIssue.UsedByOtherKind));
        Assert.That(result.Message?.Key, Is.EqualTo("Shell.HotkeyUsedByRecording"));
    }

    [Test]
    public void ANewRecordingHotkey_IsRegistered_AndSaved()
    {
        var chord = new HotkeyChord(HotkeyModifiers.Control | HotkeyModifiers.Shift, "F9");

        var result = _fixture.Create().ChangeRecordHotkey(ShellData.SpecDefaults(), RecordHotkey.Pause, chord);

        Assert.That(result.Accepted, Is.True);
        Assert.That(result.Settings.RecordHotkeys.Pause, Is.EqualTo(chord));
        Assert.That(_fixture.Hotkeys.RecordRegistered[RecordHotkey.Pause], Is.EqualTo(chord));
        Assert.That(_fixture.Store.Saved.Last().RecordHotkeys.Pause, Is.EqualTo(chord));
    }

    [Test]
    public void ASingleLetter_IsRefusedForRecordingToo()
    {
        var result = _fixture.Create().ChangeRecordHotkey(ShellData.SpecDefaults(), RecordHotkey.Pause, new HotkeyChord(HotkeyModifiers.None, "P"));

        Assert.That(result.Issue, Is.EqualTo(HotkeyIssue.NeedsModifier));
    }

    [Test]
    public void TheRecordingBarsChoices_AreKeptWhenTheyChanged_AndNothingIsWrittenWhenTheyDidNot()
    {
        var shell = _fixture.Create();
        var current = ShellData.SpecDefaults();

        Assert.That(shell.KeepRecorderChoices(current, current.Recorder), Is.Null);
        Assert.That(_fixture.Store.Saved, Is.Empty, "the same choices write nothing");

        var choices = current.Recorder with { Target = RecordTargetKind.Region, Microphone = true };
        var kept = shell.KeepRecorderChoices(current, choices);

        Assert.That(kept!.Saved, Is.True);
        Assert.That(kept.Settings.Recorder, Is.EqualTo(choices));
        Assert.That(_fixture.Store.Saved.Single().Recorder, Is.EqualTo(choices), "kept for the next start of the app (SPEC recorder, Inputs)");
    }
}
