using NUnit.Framework;

namespace Paper.ScreenWizzard.UiTests.Support;

/// <summary>
/// Base of every ui test class: closes the windows a test left open, before and after it, so the next one starts clean.
/// The tests share one desktop, so they never run in parallel.
/// </summary>
public abstract class UiTestBase
{
    [SetUp]
    public void CloseLeftoverWindows() => WpfHost.Instance.CloseAllWindows();

    [TearDown]
    public void CloseWindowsAfterTheTest() => WpfHost.Instance.CloseAllWindows();
}
