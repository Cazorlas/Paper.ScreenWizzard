using System.Collections.Concurrent;
using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.E2eTests.Support;
using static Paper.ScreenWizzard.E2eTests.Support.HotkeyRig;

namespace Paper.ScreenWizzard.E2eTests.Adapters;

/// <summary>
/// The real RegisterHotKey, with real keystrokes from SendInput. Every chord here is Ctrl+Alt+Shift+F11 or F13..F15, which nobody
/// uses; PrintScreen is never sent (a real one can open the Snipping Tool or fire the developer's own running copy of the app).
/// The F-coded cases (capture/F1, shell/F7) are in <see cref="CaptureAdapterCodeTests"/> and <see cref="ShellAdapterCodeTests"/>.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class HotkeyServiceTests
{
    private HotkeyRig _rig = null!;

    [SetUp]
    public void NewRig() => _rig = new HotkeyRig();

    [TearDown]
    public void DisposeRig() => _rig.Dispose();

    [Test]
    public void UnusedChord_RegistersAndARealKeystrokeRaisesPressedWithTheRightKind()
    {
        var pressed = new ConcurrentQueue<CaptureKind>();
        var service = _rig.NewService(pressed);

        var registered = Register(service, CaptureKind.Window, F11);
        Assert.That(registered.Success, Is.True, registered.Detail);
        PressChord(11);

        Assert.That(WaitFor(pressed, CaptureKind.Window), Is.True, "Ctrl+Alt+Shift+F11 did not raise Pressed(Window)");
        Assert.That(pressed.Distinct(), Is.EqualTo(new[] { CaptureKind.Window }), "only the kind that owns the chord is raised");
    }

    [Test]
    public void EachKindKeepsItsOwnChord()
    {
        var pressed = new ConcurrentQueue<CaptureKind>();
        var service = _rig.NewService(pressed);
        Assert.That(Register(service, CaptureKind.Rectangle, F13).Success, Is.True);
        Assert.That(Register(service, CaptureKind.FullScreen, F14).Success, Is.True);

        PressChord(14);
        Assert.That(WaitFor(pressed, CaptureKind.FullScreen), Is.True);
        PressChord(13);
        Assert.That(WaitFor(pressed, CaptureKind.Rectangle), Is.True);

        Assert.That(pressed.ToArray(), Is.EqualTo(new[] { CaptureKind.FullScreen, CaptureKind.Rectangle }));
    }

    [Test]
    public void DebuggerKeyF12_IsRefusedBeforeWindowsIsAskedBecauseItIsReservedForTheDebugger()
    {
        var service = _rig.NewService();

        var refused = Register(service, CaptureKind.Rectangle, new HotkeyChord(Chorded, "F12"));
        var alone = Register(service, CaptureKind.Freeform, new HotkeyChord(HotkeyModifiers.None, "F12"));

        Assert.That(refused.Success, Is.False);
        Assert.That(refused.Detail, Does.Contain("F12").And.Contain("debugger"));
        Assert.That(alone.Success, Is.False);
        Assert.That(alone.Detail, Does.Contain("debugger"));
    }

    [Test]
    public void KeyNames_MapToTheVirtualKeysWindowsUses()
    {
        // The names the Settings window writes (WPF's Key names, PrintScreen, letters, digits). Nothing is registered or pressed here.
        (string Name, int Vk)[] expected =
        [
            ("PrintScreen", 0x2C), ("F1", 0x70), ("F9", 0x78), ("F11", 0x7A), ("F24", 0x87),
            ("A", 0x41), ("z", 0x5A), ("0", 0x30), ("9", 0x39),
            ("Space", 0x20), ("Insert", 0x2D), ("Delete", 0x2E), ("Prior", 0x21), ("PageUp", 0x21), ("Next", 0x22), ("Left", 0x25),
            ("OemTilde", 0xC0), ("NumPad5", 0x65), ("Multiply", 0x6A),
        ];
        foreach (var (name, vk) in expected)
        {
            Assert.That(Paper.ScreenWizzard.Infrastructure.Shell.HotkeyService.TryVirtualKey(name, out var actual), Is.True, name);
            Assert.That(actual, Is.EqualTo(vk), name);
        }

        foreach (var bad in new[] { "", " ", "112", "None", "NotAKey", "F25", "Ctrl" })
        {
            Assert.That(Paper.ScreenWizzard.Infrastructure.Shell.HotkeyService.TryVirtualKey(bad, out _), Is.False, "'" + bad + "' is not a key");
        }
    }

    [Test]
    public void KeyNamesOtherThanFunctionKeys_RegisterAndAreReleasedAgain()
    {
        var service = _rig.NewService();

        foreach (var name in new[] { "Space", "OemTilde", "Prior", "Insert", "NumPad5", "Q", "7" })
        {
            Assert.That(Register(service, CaptureKind.Rectangle, new HotkeyChord(Chorded, name)).Success, Is.True, name);
        }
    }

    [Test]
    public void UnknownKeyName_IsRefusedWithAReason()
    {
        var service = _rig.NewService();

        var refused = Register(service, CaptureKind.Rectangle, new HotkeyChord(Chorded, "NotAKey"));

        Assert.That(refused.Success, Is.False);
        Assert.That(refused.Detail, Does.Contain("NotAKey"));
    }

    [Test]
    public void ChangingAKindsChord_KeepsTheOldOneWhenTheNewIsRefused_AndReleasesTheOldWhenTheNewWorks()
    {
        var pressed = new ConcurrentQueue<CaptureKind>();
        var owner = _rig.NewService();
        var service = _rig.NewService(pressed);
        Assert.That(Register(owner, CaptureKind.FullScreen, F14).Success, Is.True, "set-up: another owner holds F14");
        Assert.That(Register(service, CaptureKind.Rectangle, F11).Success, Is.True);

        // Refused: the old chord keeps working (shell F4).
        Assert.That(Register(service, CaptureKind.Rectangle, F14).Success, Is.False);
        PressChord(11);
        Assert.That(WaitFor(pressed, CaptureKind.Rectangle), Is.True, "the old chord still works after a refused change");
        pressed.Clear();

        // Accepted: the old chord is released at once and the new one works.
        Assert.That(Register(service, CaptureKind.Rectangle, F13).Success, Is.True);
        PressChord(13);
        Assert.That(WaitFor(pressed, CaptureKind.Rectangle), Is.True, "the new chord works");
        pressed.Clear();
        PressChord(11);
        Assert.That(WaitFor(pressed, CaptureKind.Rectangle, 700), Is.False, "the old chord is released");

        // ... and free for anybody else to take.
        var third = _rig.NewService();
        Assert.That(Register(third, CaptureKind.Window, F11).Success, Is.True, "F11 is free again");
    }

    [Test]
    public void Unregister_ReleasesTheChord_AndDisposeReleasesEverything()
    {
        var pressed = new ConcurrentQueue<CaptureKind>();
        var service = _rig.NewService(pressed);
        Assert.That(Register(service, CaptureKind.Rectangle, F11).Success, Is.True);
        Assert.That(Register(service, CaptureKind.Freeform, F13).Success, Is.True);

        StaHost.Instance.Invoke(() => service.Unregister(CaptureKind.Rectangle));
        var other = _rig.NewService();
        Assert.That(Register(other, CaptureKind.Rectangle, F11).Success, Is.True, "F11 was released by Unregister");

        StaHost.Instance.Invoke(service.Dispose);
        Assert.That(Register(other, CaptureKind.Freeform, F13).Success, Is.True, "F13 was released by Dispose");
    }
}
