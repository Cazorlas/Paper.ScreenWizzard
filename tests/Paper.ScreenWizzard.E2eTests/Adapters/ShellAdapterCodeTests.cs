using System.Collections.Concurrent;
using Microsoft.Win32;
using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.E2eTests.Support;
using Paper.ScreenWizzard.Infrastructure.Shell;
using static Paper.ScreenWizzard.E2eTests.Support.HotkeyRig;

namespace Paper.ScreenWizzard.E2eTests.Adapters;

/// <summary>
/// The rows of the "When it does not do the job" table of SPEC shell that only a real Windows can answer, at adapter level: what the
/// adapter returns when the world says no. shell/F1 (a corrupt file) is in <see cref="StoresTests"/> under a descriptive name because
/// its row is proved twice, and shell/F4 and F5 belong to the unit and ui lanes.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class ShellAdapterCodeTests
{
    private HotkeyRig _rig = null!;
    private ScratchFolder _scratch = null!;

    [SetUp]
    public void SetUp()
    {
        _rig = new HotkeyRig();
        _scratch = new ScratchFolder();
    }

    [TearDown]
    public void TearDown()
    {
        _rig.Dispose();
        _scratch.Dispose();
    }

    [Test]
    public void F2_SettingsThatCannotBeWrittenFail_WithTheFullPathOfTheFileFirst()
    {
        // The data root is a FILE, so <root>\configs cannot be created below it: the same refusal a read-only or full disk gives.
        var dataRoot = Path.Combine(_scratch.Path, "root-is-a-file");
        File.WriteAllText(dataRoot, "x");
        var expectedFile = Path.Combine(dataRoot, "configs", "settings.json");

        var result = new SettingsStore(dataRoot).Save(StoresTests.EveryFieldChanged());

        Assert.That(result.Success, Is.False);
        Assert.That(result.Detail, Does.StartWith(expectedFile + ": "), "the shell message must be able to name the path, and it is the first thing in the detail");
        Assert.That(result.Detail!.Length, Is.GreaterThan(expectedFile.Length + 3), "followed by the system's reason");
        TestContext.Out.WriteLine("Detail: " + result.Detail);
    }

    [Test]
    public void F3_AnEntryThatCannotBeWrittenIsAFailureWithAReason_NotAnException()
    {
        // Provoked without touching the real Run key: a registry key path with a segment over 255 characters cannot be created, and an
        // executable path that pushes the command line past the 260 characters of the Run value cannot be stored.
        var tooLongKey = @"Software\Paper.ScreenWizzard.E2E\" + new string('k', 300);
        var name = "Paper.ScreenWizzard.E2E." + Guid.NewGuid().ToString("N");

        var badKey = new AutostartService(name, @"C:\Fake\Fake.exe", tooLongKey).SetEnabled(true);
        var tooLongCommand = new AutostartService(name, @"C:\" + new string('d', 280) + @"\Fake.exe", @"Software\Paper.ScreenWizzard.E2E\Run").SetEnabled(true);

        Assert.That(badKey.Success, Is.False);
        Assert.That(badKey.Detail, Is.Not.Null.And.Not.Empty);
        Assert.That(tooLongCommand.Success, Is.False, "a command line over 260 characters is not stored");
        Assert.That(tooLongCommand.Detail, Does.Contain("260"));
        using var leftover = Registry.CurrentUser.OpenSubKey(@"Software\Paper.ScreenWizzard.E2E");
        Assert.That(leftover, Is.Null, "a refused entry leaves nothing behind in the registry");
        TestContext.Out.WriteLine("Detail (key): " + badKey.Detail);
        TestContext.Out.WriteLine("Detail (command): " + tooLongCommand.Detail);
    }

    [Test]
    public void F6_AsecondLaunchIsRefused_AndTheFirstCopyIsToldAboutIt()
    {
        var name = "Paper.ScreenWizzard.E2E." + Guid.NewGuid().ToString("N");
        using var first = new SingleInstance(name);
        using var second = new SingleInstance(name);
        using var told = new ManualResetEventSlim();
        var raisedOn = new ConcurrentQueue<int>();
        first.SecondInstanceLaunched += () =>
        {
            raisedOn.Enqueue(Environment.CurrentManagedThreadId);
            told.Set();
        };
        Assert.That(first.TryBecomeFirstInstance(), Is.True);
        Assert.That(second.TryBecomeFirstInstance(), Is.False, "no second copy starts");

        second.NotifyFirstInstance();

        Assert.That(told.Wait(TimeSpan.FromSeconds(5)), Is.True, "the first copy was not told that somebody launched the app again");
        Assert.That(raisedOn, Has.Count.EqualTo(1));
        Assert.That(raisedOn.First(), Is.Not.EqualTo(Environment.CurrentManagedThreadId), "the event comes from a waiting thread; the app marshals it to the UI thread");

        // Asking again wakes the first copy again (every launch shows the bar).
        told.Reset();
        second.NotifyFirstInstance();
        Assert.That(told.Wait(TimeSpan.FromSeconds(5)), Is.True);
    }

    [Test]
    public void F7_HotkeysThatCannotBeRegisteredAtStartAreRefusedOneByOne_AndTheOthersStillWork()
    {
        var pressed = new ConcurrentQueue<CaptureKind>();
        var owner = _rig.NewService();
        var app = _rig.NewService(pressed);
        Assert.That(Register(owner, CaptureKind.Freeform, F14).Success, Is.True, "set-up: another owner holds F14");

        var rectangle = Register(app, CaptureKind.Rectangle, F13);
        var freeform = Register(app, CaptureKind.Freeform, F14);
        var window = Register(app, CaptureKind.Window, F15);

        Assert.That(rectangle.Success, Is.True);
        Assert.That(freeform.Success, Is.False, "the held chord is refused, with the chord named for the message");
        Assert.That(freeform.Detail, Does.Contain("Ctrl+Alt+Shift+F14"));
        Assert.That(window.Success, Is.True, "a refusal does not stop the next registration");
        PressChord(15);
        Assert.That(WaitFor(pressed, CaptureKind.Window), Is.True, "the registrations that worked still fire");
        PressChord(13);
        Assert.That(WaitFor(pressed, CaptureKind.Rectangle), Is.True);
    }
}
