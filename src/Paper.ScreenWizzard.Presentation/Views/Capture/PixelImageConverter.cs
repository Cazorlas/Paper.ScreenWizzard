using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Paper.ScreenWizzard.Domain.Geometry;

namespace Paper.ScreenWizzard.Presentation.Views.Capture;

/// <summary>Turns the plain pixel array that crosses the ports into a frozen WPF bitmap, one bitmap pixel per image pixel.</summary>
public static class PixelImageBitmap
{
    public static BitmapSource ToBitmapSource(PixelImage image)
    {
        // Bgra32 is straight (not premultiplied) alpha, the same as PixelImage. 96 dpi: a bitmap pixel is one display unit, and the
        // element that shows it sets its own size when it needs the picture to cover exact physical pixels.
        var bitmap = BitmapSource.Create(image.Width, image.Height, 96, 96, PixelFormats.Bgra32, null, image.Bgra, image.Width * 4);
        bitmap.Freeze();
        return bitmap;
    }
}

/// <summary>Binding converter for <see cref="PixelImageBitmap"/>: the dialog's thumbnail is bound to the view model's PixelImage.</summary>
public sealed class PixelImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is PixelImage image ? PixelImageBitmap.ToBitmapSource(image) : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("a thumbnail is shown, never edited");
}
