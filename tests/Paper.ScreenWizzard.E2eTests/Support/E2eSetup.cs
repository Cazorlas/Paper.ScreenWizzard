using NUnit.Framework;
using Paper.ScreenWizzard.E2eTests.Support;

// In the root namespace so the fixture covers every test of the assembly (a SetUpFixture applies to its namespace and the ones below it).
namespace Paper.ScreenWizzard.E2eTests;

/// <summary>
/// Puts the test process in the same DPI mode as the real exe (per-monitor v2, declared by its manifest) before any window exists.
/// Without it the test host is DPI-unaware or system-aware, and on this machine (100% primary, 200% secondary) every coordinate an
/// adapter returns would be virtualised and no pixel check could be exact. The mode is verified, not assumed: a failed call is
/// fine only when the process is already per-monitor v2.
/// </summary>
[SetUpFixture]
public sealed class E2eSetup
{
    [OneTimeSetUp]
    public void PerMonitorV2Dpi()
    {
        Native.SetProcessDpiAwarenessContext(Native.DpiPerMonitorAwareV2);
        var current = Native.GetThreadDpiAwarenessContext();
        Assert.That(
            Native.AreDpiAwarenessContextsEqual(current, Native.DpiPerMonitorAwareV2),
            Is.True,
            "the test process is not per-monitor v2 DPI aware, so physical pixel checks would be virtualised");
    }
}
