using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UnitTests.Shell.Fakes;
using Paper.ScreenWizzard.UseCases.Shell.Models;

namespace Paper.ScreenWizzard.UnitTests.Shell;

/// <summary>SPEC shell, "Phím tắt": each "Cho ... →" line is one test; F4 is the one held by another program.</summary>
[TestFixture]
public sealed class ShellHotkeySpecTests
{
    private ShellFixture _fixture = null!;
    private AppSettings _current = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = new ShellFixture();
        _current = ShellData.SpecDefaults();
        ShellData.RegisterAll(_fixture.Hotkeys, _current);
    }

    // Cho đặt một chữ cái đơn, ví dụ A, không kèm phím nào -> bị từ chối, phím tắt cũ giữ nguyên
    [Test]
    public void SingleLetterAWithoutModifierIsRejectedAndTheOldChordStays()
    {
        var result = _fixture.Create().ChangeHotkey(_current, CaptureKind.Rectangle, ShellData.Chord(HotkeyModifiers.None, "A"));

        Assert.That(result.Accepted, Is.False);
        Assert.That(result.Issue, Is.EqualTo(HotkeyIssue.NeedsModifier));
        Assert.That(result.Message?.Key, Is.EqualTo("Shell.HotkeyNeedsModifier"));
        Assert.That(result.Message?.Arguments, Is.EqualTo(new[] { "A" }));
        Assert.That(result.Settings, Is.SameAs(_current));
        Assert.That(_fixture.Hotkeys.RegisterCalls, Is.Empty);
        Assert.That(_fixture.Hotkeys.Registered[CaptureKind.Rectangle], Is.EqualTo(ShellData.PrintScreen));
        Assert.That(_fixture.Store.Saved, Is.Empty);
    }

    // Decision: Shift alone only types a capital, so it does not make a letter safe.
    [Test]
    public void ShiftPlusLetterAloneIsRejected()
    {
        var result = _fixture.Create().ChangeHotkey(_current, CaptureKind.Rectangle, ShellData.Chord(HotkeyModifiers.Shift, "A"));

        Assert.That(result.Issue, Is.EqualTo(HotkeyIssue.NeedsModifier));
        Assert.That(result.Message?.Arguments, Is.EqualTo(new[] { "Shift+A" }));
    }

    // Decision: a digit and any other non-function key without a modifier are refused too.
    [TestCase("1")]
    [TestCase("Space")]
    public void DigitOrOtherKeyWithoutModifierIsRejected(string key)
    {
        var result = _fixture.Create().ChangeHotkey(_current, CaptureKind.Rectangle, ShellData.Chord(HotkeyModifiers.None, key));

        Assert.That(result.Accepted, Is.False);
        Assert.That(result.Issue, Is.EqualTo(HotkeyIssue.NeedsModifier));
    }

    // Cho đặt Ctrl+Shift+A -> nhận
    [Test]
    public void CtrlShiftAIsAcceptedRegisteredAndSaved()
    {
        var proposed = ShellData.Chord(HotkeyModifiers.Control | HotkeyModifiers.Shift, "A");

        var result = _fixture.Create().ChangeHotkey(_current, CaptureKind.Rectangle, proposed);

        Assert.That(result.Accepted, Is.True);
        Assert.That(result.Issue, Is.EqualTo(HotkeyIssue.None));
        Assert.That(result.Message, Is.Null);
        Assert.That(result.Settings.Hotkeys[CaptureKind.Rectangle], Is.EqualTo(proposed));
        Assert.That(result.Settings.Hotkeys[CaptureKind.Freeform], Is.EqualTo(ShellData.ShiftPrintScreen));
        Assert.That(_fixture.Hotkeys.Registered[CaptureKind.Rectangle], Is.EqualTo(proposed));
        Assert.That(_fixture.Store.Saved, Has.Count.EqualTo(1));
        Assert.That(_fixture.Store.Saved[0].Hotkeys[CaptureKind.Rectangle], Is.EqualTo(proposed));
    }

    // Cho đặt PrintScreen một mình -> nhận
    [Test]
    public void PrintScreenAloneIsAccepted()
    {
        var elsewhere = _current with
        {
            Hotkeys = new Dictionary<CaptureKind, HotkeyChord>(_current.Hotkeys)
            {
                [CaptureKind.Rectangle] = ShellData.Chord(HotkeyModifiers.Control | HotkeyModifiers.Shift, "A"),
            },
        };

        var result = _fixture.Create().ChangeHotkey(elsewhere, CaptureKind.Rectangle, ShellData.PrintScreen);

        Assert.That(result.Accepted, Is.True);
        Assert.That(result.Settings.Hotkeys[CaptureKind.Rectangle], Is.EqualTo(ShellData.PrintScreen));
    }

    // Cho đặt F9 một mình -> nhận
    [Test]
    public void FunctionKeyF9AloneIsAccepted()
    {
        var f9 = ShellData.Chord(HotkeyModifiers.None, "F9");

        var result = _fixture.Create().ChangeHotkey(_current, CaptureKind.Window, f9);

        Assert.That(result.Accepted, Is.True);
        Assert.That(result.Settings.Hotkeys[CaptureKind.Window], Is.EqualTo(f9));
        Assert.That(_fixture.Hotkeys.Registered[CaptureKind.Window], Is.EqualTo(f9));
    }

    // Cho đặt phím trùng với phím của kiểu chụp khác trong ứng dụng -> bị từ chối, nêu tên kiểu đang giữ phím đó
    [Test]
    public void ChordUsedByAnotherCaptureKindIsRejectedNamingThatKind()
    {
        var result = _fixture.Create().ChangeHotkey(_current, CaptureKind.Freeform, ShellData.PrintScreen);

        Assert.That(result.Accepted, Is.False);
        Assert.That(result.Issue, Is.EqualTo(HotkeyIssue.UsedByOtherKind));
        Assert.That(result.ConflictingKind, Is.EqualTo(CaptureKind.Rectangle));
        Assert.That(result.Message?.Key, Is.EqualTo("Shell.HotkeyUsedByOtherKind"));
        Assert.That(result.Message?.Arguments, Is.EqualTo(new[] { "Rectangle" }));
        Assert.That(result.Settings, Is.SameAs(_current));
        Assert.That(_fixture.Hotkeys.RegisterCalls, Is.Empty);
        Assert.That(_fixture.Hotkeys.Registered[CaptureKind.Freeform], Is.EqualTo(ShellData.ShiftPrintScreen));
    }

    // Cho đặt phím mà Windows hay ứng dụng khác đã giữ -> bị từ chối, nêu rõ không đăng ký được, phím cũ giữ nguyên
    [Test]
    public void F4_ChangedHotkeyHeldByAnotherProgramKeepsTheOldOne()
    {
        var winL = ShellData.Chord(HotkeyModifiers.Windows, "L");
        _fixture.Hotkeys.HeldByOtherPrograms.Add(winL);

        var result = _fixture.Create().ChangeHotkey(_current, CaptureKind.Rectangle, winL);

        Assert.That(result.Accepted, Is.False);
        Assert.That(result.Issue, Is.EqualTo(HotkeyIssue.HeldByAnotherProgram));
        Assert.That(result.Message?.Key, Is.EqualTo("Shell.HotkeyHeldByAnotherProgram"));
        Assert.That(result.Message?.Arguments, Is.EqualTo(new[] { "Win+L" }));
        Assert.That(result.Settings, Is.SameAs(_current));
        Assert.That(_fixture.Hotkeys.Registered[CaptureKind.Rectangle], Is.EqualTo(ShellData.PrintScreen));
        Assert.That(_fixture.Store.Saved, Is.Empty);
    }

    // Cho đổi phím vùng chữ nhật từ PrintScreen sang Ctrl+Shift+1 -> PrintScreen không còn chụp, Ctrl+Shift+1 chụp
    [Test]
    public void ChangedHotkeyTakesEffectAtOnce()
    {
        var pressed = new List<CaptureKind>();
        _fixture.Hotkeys.Pressed += pressed.Add;
        var ctrlShift1 = ShellData.Chord(HotkeyModifiers.Control | HotkeyModifiers.Shift, "1");

        var result = _fixture.Create().ChangeHotkey(_current, CaptureKind.Rectangle, ctrlShift1);
        _fixture.Hotkeys.Press(ShellData.PrintScreen);
        var afterOldChord = pressed.Count;
        _fixture.Hotkeys.Press(ctrlShift1);

        Assert.That(result.Accepted, Is.True);
        Assert.That(afterOldChord, Is.EqualTo(0));
        Assert.That(pressed, Is.EqualTo(new[] { CaptureKind.Rectangle }));
    }
}
