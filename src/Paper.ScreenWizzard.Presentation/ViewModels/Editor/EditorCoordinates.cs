using Paper.ScreenWizzard.Domain.Geometry;

namespace Paper.ScreenWizzard.Presentation.ViewModels.Editor;

/// <summary>
/// The ONE place a position in the window becomes a pixel of the image and back. The session holds image pixels, origin at the image's
/// top-left, whatever the zoom or the monitor's scale, so a shape drawn at (150, 100) is at (150, 100) in the file (SPEC editor, "Lưu và chép").
/// The window shows the image <c>zoom</c> times its size in PHYSICAL pixels (100% is one image pixel per physical pixel), and WPF lays out in
/// display units, which are <c>dpiScale</c> physical pixels each, so one image pixel is <c>zoom / dpiScale</c> display units wide.
/// </summary>
public static class EditorCoordinates
{
    /// <summary>How many display units one image pixel is wide at this zoom on a monitor of this scale (1.0 at 96 dpi, 1.5 at 150%).</summary>
    public static double ViewUnitsPerImagePixel(double zoom, double dpiScale) => zoom / dpiScale;

    /// <summary>
    /// The image pixel under a point given in display units from the image's top-left corner. The pixel that contains the point: rounding down,
    /// so a point left of or above the image is -1 and not 0, which matters when the mouse is captured and leaves the image mid-drag.
    /// </summary>
    public static PixelPoint ViewToImage(double viewX, double viewY, double zoom, double dpiScale)
    {
        var unit = ViewUnitsPerImagePixel(zoom, dpiScale);
        return new PixelPoint((int)Math.Floor(viewX / unit), (int)Math.Floor(viewY / unit));
    }

    /// <summary>The display-unit position of a (possibly fractional) image position, for drawing and for placing the text box.</summary>
    public static (double X, double Y) ImageToView(double imageX, double imageY, double zoom, double dpiScale)
    {
        var unit = ViewUnitsPerImagePixel(zoom, dpiScale);
        return (imageX * unit, imageY * unit);
    }
}
