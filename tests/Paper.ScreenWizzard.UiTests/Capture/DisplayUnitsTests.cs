using NUnit.Framework;
using Paper.ScreenWizzard.Presentation.ViewModels.Capture;
using Paper.ScreenWizzard.Domain.Geometry;

namespace Paper.ScreenWizzard.UiTests.ScreenCapture;

/// <summary>
/// The one place WPF's device-independent units become physical pixels and back (plan Decisions: "đổi ở đúng một chỗ").
/// SPEC capture: "Cho màn hình đặt 150%, kéo vùng rộng 300 x cao 200 đơn vị → ảnh rộng 450, cao 300 pixel".
/// </summary>
[TestFixture]
public sealed class DisplayUnitsTests
{
    [TestCase(1.0, 300, 200)]
    [TestCase(1.25, 375, 250)]
    [TestCase(1.5, 450, 300)]
    [TestCase(2.0, 600, 400)]
    public void ToPixels_ARegionOf300x200Units_GivesTheRightPhysicalSize(double scale, int width, int height)
    {
        var size = DisplayUnits.SizeToPixels(300, 200, scale);

        Assert.That(size, Is.EqualTo(new PixelSize(width, height)));
    }

    [Test]
    public void ToPixels_At150Percent_A300x200RegionIs450x300Pixels()
    {
        var size = DisplayUnits.SizeToPixels(300, 200, DisplayUnits.ScaleFromDpi(144));

        Assert.That(size, Is.EqualTo(new PixelSize(450, 300)));
    }

    [Test]
    public void ToPixels_At100Percent_A300x200RegionIs300x200Pixels()
    {
        var size = DisplayUnits.SizeToPixels(300, 200, DisplayUnits.ScaleFromDpi(96));

        Assert.That(size, Is.EqualTo(new PixelSize(300, 200)));
    }

    [Test]
    public void ToPixels_WithNoScaleGiven_IsOnePixelPerUnit()
    {
        Assert.That(DisplayUnits.SizeToPixels(300, 200), Is.EqualTo(new PixelSize(300, 200)));
        Assert.That(DisplayUnits.ToPixels(37.0), Is.EqualTo(37));
    }

    [TestCase(96, 1.0)]
    [TestCase(120, 1.25)]
    [TestCase(144, 1.5)]
    [TestCase(192, 2.0)]
    public void ScaleFromDpi_IsTheDpiOver96(int dpi, double scale)
    {
        Assert.That(DisplayUnits.ScaleFromDpi(dpi), Is.EqualTo(scale).Within(1e-9));
    }

    [Test]
    public void ToPixels_RoundsToTheNearestPixel()
    {
        Assert.That(DisplayUnits.ToPixels(100.4, 1.5), Is.EqualTo(151), "150.6 pixels");
        Assert.That(DisplayUnits.ToPixels(101, 1.25), Is.EqualTo(126), "126.25 pixels");
        Assert.That(DisplayUnits.ToPixels(-10, 1.5), Is.EqualTo(-15), "a negative desktop coordinate converts the same way");
    }

    [Test]
    public void ToUnits_IsTheInverseOfToPixels()
    {
        Assert.That(DisplayUnits.ToUnits(450, 1.5), Is.EqualTo(300).Within(1e-9));
        Assert.That(DisplayUnits.ToUnits(300, 1.0), Is.EqualTo(300).Within(1e-9));
        Assert.That(DisplayUnits.ToUnits(DisplayUnits.ToPixels(300, 2.0), 2.0), Is.EqualTo(300).Within(1e-9));
    }

    [Test]
    public void ToPixels_TwoMonitorsOfDifferentDpi_DoNotInfluenceEachOther()
    {
        // The scale is an argument, never remembered: converting for a 150% monitor then a 100% one must not leak the first.
        var onScaled = DisplayUnits.SizeToPixels(300, 200, 1.5);
        var onPlain = DisplayUnits.SizeToPixels(300, 200, 1.0);
        var onScaledAgain = DisplayUnits.SizeToPixels(300, 200, 1.5);

        Assert.That(onScaled, Is.EqualTo(new PixelSize(450, 300)));
        Assert.That(onPlain, Is.EqualTo(new PixelSize(300, 200)));
        Assert.That(onScaledAgain, Is.EqualTo(onScaled));
    }
}
