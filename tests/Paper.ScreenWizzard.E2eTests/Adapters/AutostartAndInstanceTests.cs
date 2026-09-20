using Microsoft.Win32;
using NUnit.Framework;
using Paper.ScreenWizzard.Infrastructure.Shell;

namespace Paper.ScreenWizzard.E2eTests.Adapters;

/// <summary>
/// The Run-key entry and the one-copy-only guard, under names made up for the test: never the app's real Run value, mutex or event, so
/// a developer's own running copy is not disturbed and a test that dies half way leaves nothing the real app would read.
/// </summary>
[TestFixture]
public sealed class AutostartAndInstanceTests
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string FakeExe = @"C:\Program Files\Fake ScreenWizzard\Fake.exe";

    private static string ValueOf(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(name) as string ?? string.Empty;
    }

    private static void Remove(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }

    [Test]
    public void Autostart_WritesTheQuotedExeAndTheFlag_ReadsItBack_AndRemovesIt()
    {
        var name = "Paper.ScreenWizzard.E2E." + Guid.NewGuid().ToString("N");
        try
        {
            var service = new AutostartService(name, FakeExe);
            Assert.That(service.IsEnabled(), Is.False, "nothing is registered yet");

            var enabled = service.SetEnabled(true);

            Assert.That(enabled.Success, Is.True, enabled.Detail);
            Assert.That(ValueOf(name), Is.EqualTo("\"" + FakeExe + "\" --autostart"), "the registry value is the quoted path and the flag");
            Assert.That(service.IsEnabled(), Is.True);
            Assert.That(new AutostartService(name, FakeExe).IsEnabled(), Is.True, "a second service object sees the same entry");
            Assert.That(new AutostartService(name + ".other", FakeExe).IsEnabled(), Is.False, "another value name is another entry");

            var disabled = service.SetEnabled(false);

            Assert.That(disabled.Success, Is.True, disabled.Detail);
            Assert.That(ValueOf(name), Is.Empty, "the value is gone");
            Assert.That(service.IsEnabled(), Is.False);
            Assert.That(service.SetEnabled(false).Success, Is.True, "removing what is not there is not an error");
        }
        finally
        {
            Remove(name);
        }
    }

    [Test]
    public void Autostart_EnablingTwice_KeepsOneEntry()
    {
        var name = "Paper.ScreenWizzard.E2E." + Guid.NewGuid().ToString("N");
        try
        {
            var service = new AutostartService(name, FakeExe);

            Assert.That(service.SetEnabled(true).Success, Is.True);
            Assert.That(service.SetEnabled(true).Success, Is.True);

            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            Assert.That(key!.GetValueNames().Count(n => n == name), Is.EqualTo(1));
        }
        finally
        {
            Remove(name);
        }
    }

    [Test]
    public void SingleInstance_TheFirstCopyWins_TheSecondIsRefused_AndTheNameIsFreedWhenTheFirstEnds()
    {
        var name = "Paper.ScreenWizzard.E2E." + Guid.NewGuid().ToString("N");
        using var first = new SingleInstance(name);
        using var second = new SingleInstance(name);

        Assert.That(first.TryBecomeFirstInstance(), Is.True);
        Assert.That(second.TryBecomeFirstInstance(), Is.False);
        Assert.That(new SingleInstance(name + ".different").TryBecomeFirstInstance(), Is.True, "another name is another app");

        first.Dispose();
        using var third = new SingleInstance(name);
        Assert.That(third.TryBecomeFirstInstance(), Is.True, "once the first copy has ended the next launch is the first");
    }
}
