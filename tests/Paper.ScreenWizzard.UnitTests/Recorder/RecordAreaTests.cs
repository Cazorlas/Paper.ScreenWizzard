using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Recorder;
using Paper.ScreenWizzard.Domain.Shared;

namespace Paper.ScreenWizzard.UnitTests.Recorder;

/// <summary>SPEC recorder, "What is recorded" and F7: every choice becomes one even rectangle of the desktop.</summary>
[TestFixture]
public sealed class RecordAreaTests
{
    private static readonly MonitorInfo Left = new(0, new PixelRect(0, 0, 1920, 1080), true, 96);
    private static readonly MonitorInfo Right = new(1, new PixelRect(1920, 0, 1920, 1080), false, 96);
    private static readonly MonitorInfo[] Two = [Left, Right];

    private static RecordAreaResult Resolve(RecordTargetKind kind, int? monitor = null, PixelRect? picked = null, IReadOnlyList<MonitorInfo>? monitors = null, PixelPoint pointer = default) =>
        RecordArea.Resolve(kind, monitor, picked, monitors ?? Two, pointer);

    [Test]
    public void TheSecondMonitor_IsItsOwn1920x1080()
    {
        var result = Resolve(RecordTargetKind.Monitor, monitor: 1);

        Assert.That(result.Area, Is.EqualTo(new PixelRect(1920, 0, 1920, 1080)));
        Assert.That(result.IsUsable, Is.True);
    }

    [Test]
    public void AMonitorNotChosen_IsTheOneUnderThePointer()
    {
        Assert.That(Resolve(RecordTargetKind.Monitor, pointer: new PixelPoint(2500, 300)).Area, Is.EqualTo(Right.Bounds));
        Assert.That(Resolve(RecordTargetKind.Monitor, monitor: 7, pointer: new PixelPoint(10, 10)).Area, Is.EqualTo(Left.Bounds), "a monitor that is gone");
    }

    [Test]
    public void TheWholeDesktop_Is3840x1080_WithNoGap()
    {
        Assert.That(Resolve(RecordTargetKind.Desktop).Area, Is.EqualTo(new PixelRect(0, 0, 3840, 1080)));
    }

    [Test]
    public void ADraggedRegion_IsThatPart_800x450()
    {
        var region = new PixelRect(300, 200, 800, 450);

        Assert.That(Resolve(RecordTargetKind.Region, picked: region).Area, Is.EqualTo(region));
    }

    [Test]
    public void AWindow_IsItsVisibleFrame_1000x700()
    {
        var frame = new PixelRect(400, 150, 1000, 700);

        Assert.That(Resolve(RecordTargetKind.Window, picked: frame).Area, Is.EqualTo(frame));
    }

    [Test]
    public void AnOddSize_LosesOnePixelAtTheRightAndTheBottom()
    {
        Assert.That(Resolve(RecordTargetKind.Region, picked: new PixelRect(10, 20, 801, 451)).Area, Is.EqualTo(new PixelRect(10, 20, 800, 450)));
    }

    [Test]
    public void ARegionPartlyOffEveryMonitor_KeepsThePartOnAMonitor()
    {
        MonitorInfo[] one = [Left];

        Assert.That(Resolve(RecordTargetKind.Region, picked: new PixelRect(1800, 500, 300, 200), monitors: one).Area, Is.EqualTo(new PixelRect(1800, 500, 120, 200)));
        Assert.That(Resolve(RecordTargetKind.Region, picked: new PixelRect(-50, 100, 150, 200), monitors: one).Area, Is.EqualTo(new PixelRect(0, 100, 100, 200)));
    }

    [Test]
    public void AMonitorAt150Percent_KeepsItsRealPixels()
    {
        // The region comes from the screen in physical pixels: 300 × 200 as seen at 150% is 450 × 300.
        MonitorInfo[] scaled = [new(0, new PixelRect(0, 0, 2880, 1620), true, 144)];

        Assert.That(Resolve(RecordTargetKind.Region, picked: new PixelRect(150, 150, 450, 300), monitors: scaled).Area, Is.EqualTo(new PixelRect(150, 150, 450, 300)));
    }

    [TestCase(15, 40)]
    [TestCase(40, 15)]
    [TestCase(17, 15)]
    public void F7_ARegionSmallerThan16_IsRefused(int width, int height)
    {
        var result = Resolve(RecordTargetKind.Region, picked: new PixelRect(100, 100, width, height));

        Assert.That(result.Issue, Is.EqualTo(RecordAreaIssue.TooSmall));
        Assert.That(result.IsUsable, Is.False);
    }

    [Test]
    public void ARegionOf17_IsRecordedAs16()
    {
        var result = Resolve(RecordTargetKind.Region, picked: new PixelRect(100, 100, 17, 17));

        Assert.That(result.Issue, Is.EqualTo(RecordAreaIssue.None));
        Assert.That(result.Area, Is.EqualTo(new PixelRect(100, 100, 16, 16)));
    }

    [Test]
    public void ARegionOffEveryMonitor_OrNothingPicked_IsOffScreen()
    {
        Assert.That(Resolve(RecordTargetKind.Region, picked: new PixelRect(5000, 5000, 300, 300)).Issue, Is.EqualTo(RecordAreaIssue.OffScreen));
        Assert.That(Resolve(RecordTargetKind.Window).Issue, Is.EqualTo(RecordAreaIssue.OffScreen));
        Assert.That(Resolve(RecordTargetKind.Desktop, monitors: []).Issue, Is.EqualTo(RecordAreaIssue.NoMonitor));
    }
}
