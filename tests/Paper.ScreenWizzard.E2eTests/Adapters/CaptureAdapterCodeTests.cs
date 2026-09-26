using System.Collections.Concurrent;
using System.Diagnostics;
using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.E2eTests.Support;
using Paper.ScreenWizzard.Infrastructure.Shared;
using static Paper.ScreenWizzard.E2eTests.Support.HotkeyRig;

namespace Paper.ScreenWizzard.E2eTests.Adapters;

/// <summary>
/// The rows of the "When it does not do the job" table of SPEC capture that only a real Windows can answer, at adapter level: what the
/// adapter returns when the world says no. The message and the dialog that stays open are proved at the layers above (unit and ui).
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class CaptureAdapterCodeTests
{
    private HotkeyRig _rig = null!;
    private ClipboardBackup? _backup;
    private ScratchFolder _scratch = null!;

    [SetUp]
    public void SetUp()
    {
        _rig = new HotkeyRig();
        _backup = ClipboardBackup.Take();
        _scratch = new ScratchFolder();
    }

    [TearDown]
    public void TearDown()
    {
        _rig.Dispose();
        _backup?.Restore();
        _scratch.Dispose();
    }

    [Test]
    public void F1_AHotkeyAnotherProgramHoldsIsRefusedWithAReasonThatNamesTheKey_AndTheKindStaysUsable()
    {
        // "Another program" is a second service with its own hidden window: to Windows it is another owner of the same chord.
        var pressed = new ConcurrentQueue<CaptureKind>();
        var owner = _rig.NewService();
        var second = _rig.NewService(pressed);
        Assert.That(Register(owner, CaptureKind.Rectangle, F11).Success, Is.True, "set-up: another owner holds the chord");

        var refused = Register(second, CaptureKind.Rectangle, F11);

        Assert.That(refused.Success, Is.False, "Windows lets one owner have a chord; the second registration must fail, not throw");
        Assert.That(refused.Detail, Is.Not.Null.And.Not.Empty, "the failure carries the system's reason for the log");
        Assert.That(refused.Detail, Does.Contain("Ctrl+Alt+Shift+F11"), "and names the chord that was refused");
        TestContext.Out.WriteLine("Detail: " + refused.Detail);

        // The kind is still usable: another chord registers and works (the capture bar and the tray menu do not depend on the hotkey).
        Assert.That(Register(second, CaptureKind.Rectangle, F13).Success, Is.True);
        PressChord(13);
        Assert.That(WaitFor(pressed, CaptureKind.Rectangle), Is.True);
    }

    [Test]
    public void F4_WritingIntoAPathWhoseParentIsAFileFails_WithThePathAndTheReason()
    {
        var parentIsAFile = Path.Combine(_scratch.Path, "not-a-folder");
        File.WriteAllText(parentIsAFile, "x");
        var target = Path.Combine(parentIsAFile, "Screenshot.png");

        var result = new FileStore().WriteAllBytes(target, [1, 2, 3]);

        Assert.That(result.Success, Is.False, "nothing can be written below a file");
        Assert.That(result.Detail, Does.StartWith(target + ": "), "the detail names the path first, then the system's reason");
        Assert.That(result.Detail!.Length, Is.GreaterThan(target.Length + 3), "and there is a reason after it");
        Assert.That(File.ReadAllText(parentIsAFile), Is.EqualTo("x"), "nothing was damaged");
        TestContext.Out.WriteLine("Detail: " + result.Detail);
    }

    [Test]
    public void F4_WritingOverAReadOnlyFileFails_AndTheFileIsLeftAlone()
    {
        var file = Path.Combine(_scratch.Path, "locked.png");
        File.WriteAllBytes(file, [9, 9, 9]);
        File.SetAttributes(file, FileAttributes.ReadOnly);

        var result = new FileStore().WriteAllBytes(file, [1, 2, 3]);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Detail, Does.StartWith(file + ": "));
        Assert.That(File.ReadAllBytes(file), Is.EqualTo(new byte[] { 9, 9, 9 }));
    }

    [Test]
    public void F5_AClipboardAnotherProgramHolds_FailsAfterItsRetries_WithAReason()
    {
        var picture = Pixels.Pattern(16, 16);
        using var holder = ClipboardHolder.Hold();
        var service = new ClipboardService(tries: 2, delayMilliseconds: 100);
        var watch = Stopwatch.StartNew();

        var result = StaHost.Instance.Invoke(() => service.SetImage(picture));
        watch.Stop();

        Assert.That(result.Success, Is.False, "the clipboard is held open for the whole time, so the copy cannot succeed");
        Assert.That(result.Detail, Is.Not.Null.And.Not.Empty);
        // Measured, not assumed: WPF's own SetImage already waits about a second (ten tries 100 ms apart) before it throws, so the
        // service's tries multiply that. The elapsed time is printed so the report can quote it.
        TestContext.Out.WriteLine($"SetImage on a held clipboard: {watch.ElapsedMilliseconds} ms with 2 tries; Detail: {result.Detail}");
        Assert.That(watch.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(100), "it tried more than once");
    }
}
