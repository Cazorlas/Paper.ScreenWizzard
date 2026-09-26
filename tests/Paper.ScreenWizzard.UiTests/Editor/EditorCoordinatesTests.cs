using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Presentation.ViewModels.Editor;

namespace Paper.ScreenWizzard.UiTests.ImageEditor;

/// <summary>
/// The one function that turns a position in the window into a pixel of the image (plan T13). Everything the session holds is in image
/// pixels with the origin at the image's top-left, whatever the zoom or the monitor's scale: a shape drawn at (150, 100) must land at
/// (150, 100) (SPEC editor, "Lưu và chép").
/// </summary>
[TestFixture]
public sealed class EditorCoordinatesTests
{
    [TestCase(150.5, 100.2, 1.0, 1.0, 150, 100)]
    [TestCase(602.0, 402.0, 4.0, 1.0, 150, 100)]
    [TestCase(599.9, 399.9, 4.0, 1.0, 149, 99)]
    [TestCase(15.0, 10.0, 0.1, 1.0, 150, 100)]
    [TestCase(100.0, 66.7, 1.0, 1.5, 150, 100)]
    [TestCase(400.0, 266.8, 4.0, 1.5, 150, 100)]
    public void ViewToImage_DividesByTheZoomAndMultipliesByTheMonitorScale(double viewX, double viewY, double zoom, double dpiScale, int imageX, int imageY)
    {
        Assert.That(EditorCoordinates.ViewToImage(viewX, viewY, zoom, dpiScale), Is.EqualTo(new PixelPoint(imageX, imageY)));
    }

    [Test]
    public void ViewToImage_AboveOrLeftOfTheImage_IsNegativeNotZero()
    {
        // A drag captured by the mouse may leave the image; -0.5 must be pixel -1, not 0 (truncation would put it on the image).
        Assert.That(EditorCoordinates.ViewToImage(-0.5, -0.5, 1.0, 1.0), Is.EqualTo(new PixelPoint(-1, -1)));
        Assert.That(EditorCoordinates.ViewToImage(-1.0, -3.9, 4.0, 1.0), Is.EqualTo(new PixelPoint(-1, -1)));
    }

    [TestCase(1.0, 1.0)]
    [TestCase(4.0, 1.0)]
    [TestCase(0.25, 1.0)]
    [TestCase(2.0, 1.75)]
    public void ImageToView_ThenViewToImage_ReturnsTheSamePixelWhereverItIsInThePixel(double zoom, double dpiScale)
    {
        foreach (var (x, y) in new[] { (0, 0), (150, 100), (999, 599), (37, 4) })
        {
            // The middle of the pixel, as a mouse pointing at it would be.
            var view = EditorCoordinates.ImageToView(x + 0.5, y + 0.5, zoom, dpiScale);

            Assert.That(EditorCoordinates.ViewToImage(view.X, view.Y, zoom, dpiScale), Is.EqualTo(new PixelPoint(x, y)), $"pixel ({x}, {y}) at {zoom:P0} on a {dpiScale:P0} monitor");
        }
    }

    [TestCase(1.0, 1.0, 1.0)]
    [TestCase(4.0, 1.0, 4.0)]
    [TestCase(1.0, 1.5, 0.6666666666666666)]
    [TestCase(2.0, 2.0, 1.0)]
    public void ViewUnitsPerImagePixel_IsZoomOverMonitorScale(double zoom, double dpiScale, double expected)
    {
        // 100% means one image pixel per PHYSICAL pixel, so on a 150% monitor one image pixel is 2/3 of a display unit.
        Assert.That(EditorCoordinates.ViewUnitsPerImagePixel(zoom, dpiScale), Is.EqualTo(expected).Within(1e-9));
    }
}
