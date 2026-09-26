using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Presentation.Editor.Views;

namespace Paper.ScreenWizzard.E2eTests.Adapters;

/// <summary>
/// Where the editor window opens: inside the monitor that holds the pointer, in physical pixels. The function is pure, so it is
/// tested with two made-up monitors that look like this machine's: 1920 x 1080 at 100% and a 2560 x 1440 at 200% to its right (the
/// window, 1120 display units wide, hung outside the second one when it was centred by WPF).
/// </summary>
[TestFixture]
public sealed class EditorWindowPlacementTests
{
    private static readonly MonitorInfo Primary = new(0, new PixelRect(0, 0, 1920, 1080), true, 96);
    private static readonly MonitorInfo Secondary = new(1, new PixelRect(1920, 0, 2560, 1440), false, 192);
    private static readonly IReadOnlyList<MonitorInfo> Both = [Primary, Secondary];

    private static void AssertInside(PixelRect inner, PixelRect outer, string why)
    {
        Assert.That(inner.X, Is.GreaterThanOrEqualTo(outer.X), why + ": left edge");
        Assert.That(inner.Y, Is.GreaterThanOrEqualTo(outer.Y), why + ": top edge");
        Assert.That(inner.X + inner.Width, Is.LessThanOrEqualTo(outer.X + outer.Width), why + ": right edge");
        Assert.That(inner.Y + inner.Height, Is.LessThanOrEqualTo(outer.Y + outer.Height), why + ": bottom edge");
    }

    [Test]
    public void MonitorFor_IsTheMonitorThatHoldsThePointer()
    {
        Assert.That(EditorWindowPlacement.MonitorFor(Both, new PixelPoint(100, 100)), Is.EqualTo(Primary));
        Assert.That(EditorWindowPlacement.MonitorFor(Both, new PixelPoint(2000, 700)), Is.EqualTo(Secondary));
        Assert.That(EditorWindowPlacement.MonitorFor(Both, new PixelPoint(1919, 0)), Is.EqualTo(Primary), "the last pixel of the first monitor");
        Assert.That(EditorWindowPlacement.MonitorFor(Both, new PixelPoint(1920, 0)), Is.EqualTo(Secondary), "the first pixel of the second monitor");
    }

    [Test]
    public void MonitorFor_APointerOutsideEveryMonitor_IsTheNearestOne_AndNoMonitorIsNull()
    {
        Assert.That(EditorWindowPlacement.MonitorFor(Both, new PixelPoint(5000, 500)), Is.EqualTo(Secondary));
        Assert.That(EditorWindowPlacement.MonitorFor(Both, new PixelPoint(-300, 500)), Is.EqualTo(Primary));
        Assert.That(EditorWindowPlacement.MonitorFor([], new PixelPoint(0, 0)), Is.Null);
    }

    [Test]
    public void OnTheSecondMonitorAt200Percent_TheWindowIsInsideIt_AndKeepsItsSizeWhenItFits()
    {
        // 1120 x 700 display units at 200% is 2240 x 1400 pixels: it fits the width of 2560 but not the height of 1440 with a margin
        // for the taskbar, so the height is pulled in and the width is kept.
        var placed = EditorWindowPlacement.Place(Both, new PixelPoint(2500, 300), new PixelSize(1120, 700));

        AssertInside(placed, Secondary.Bounds, "1120 units at 200% on the 2560 x 1440 monitor");
        Assert.That(placed.Width, Is.EqualTo(2240));
        Assert.That(placed.Height, Is.LessThan(1440).And.GreaterThanOrEqualTo(960), "kept off the taskbar, but never below the window's own minimum of 480 units");
        Assert.That(placed.X + (placed.Width / 2), Is.EqualTo(Secondary.Bounds.X + (Secondary.Bounds.Width / 2)).Within(1), "centred horizontally");
        Assert.That(placed.Y + (placed.Height / 2), Is.EqualTo(Secondary.Bounds.Y + (Secondary.Bounds.Height / 2)).Within(1), "centred vertically");
    }

    [Test]
    public void OnThePrimaryMonitorAt100Percent_TheWindowIsCentredAtItsOwnSize()
    {
        var placed = EditorWindowPlacement.Place(Both, new PixelPoint(300, 300), new PixelSize(1120, 700));

        Assert.That(placed, Is.EqualTo(new PixelRect(400, 190, 1120, 700)));
    }

    [Test]
    public void AWindowLargerThanTheMonitor_IsPulledInsideIt_OnBothAxes()
    {
        var small = new MonitorInfo(0, new PixelRect(0, 0, 1280, 720), true, 96);

        var placed = EditorWindowPlacement.Place(small, new PixelSize(3000, 2000));

        AssertInside(placed, small.Bounds, "3000 x 2000 pixels on a 1280 x 720 monitor");
        Assert.That(placed.Width, Is.GreaterThan(0).And.LessThan(1280));
        Assert.That(placed.Height, Is.GreaterThan(0).And.LessThan(720));
    }

    [Test]
    public void AMonitorLeftOfThePrimary_HasNegativeCoordinates_AndTheWindowStillFitsIt()
    {
        var left = new MonitorInfo(1, new PixelRect(-1920, -200, 1920, 1080), false, 96);
        var monitors = new[] { new MonitorInfo(0, new PixelRect(0, 0, 2560, 1440), true, 144), left };

        var placed = EditorWindowPlacement.Place(monitors, new PixelPoint(-1000, 400), new PixelSize(1120, 700));

        AssertInside(placed, left.Bounds, "on the monitor at negative coordinates");
        Assert.That(placed.X, Is.LessThan(0));
    }

    [Test]
    public void TheDesiredSizeIsScaledByTheDpiOfTheMonitorThatIsChosen()
    {
        var at150 = new MonitorInfo(0, new PixelRect(0, 0, 3840, 2160), true, 144);

        var placed = EditorWindowPlacement.Place([at150], new PixelPoint(10, 10), new PixelSize(1120, 700));

        Assert.That((placed.Width, placed.Height), Is.EqualTo((1680, 1050)), "1120 x 700 units at 150%");
    }

    [Test]
    public void NoMonitors_LeavesTheSizeAtTheOriginRatherThanThrowing()
    {
        var placed = EditorWindowPlacement.Place([], new PixelPoint(0, 0), new PixelSize(1120, 700));

        Assert.That(placed, Is.EqualTo(new PixelRect(0, 0, 1120, 700)));
    }
}
