using Paper.ScreenWizzard.Domain.Shared;

namespace Paper.ScreenWizzard.Presentation.ViewModels.Capture;

/// <summary>
/// The one place where WPF's device-independent units (1/96 inch) and the physical pixels the whole app speaks (CLAUDE.md,
/// plan Decisions) are converted into each other. The scale is always an argument, never remembered: on a mixed-DPI desktop each
/// monitor has its own, and a value kept from the last conversion would be wrong on the next one. Scale 1.0 is 96 dpi (100%).
/// </summary>
public static class DisplayUnits
{
    public const int BaseDpi = 96;

    /// <summary>The scale of a monitor: 96 dpi is 1.0 (100%), 144 dpi is 1.5 (150%).</summary>
    public static double ScaleFromDpi(int dpi) => dpi / (double)BaseDpi;

    /// <summary>Units to physical pixels, to the nearest pixel (halves away from zero, so a negative desktop coordinate mirrors a positive one).</summary>
    public static int ToPixels(double units, double scale = 1.0) =>
        (int)Math.Round(units * CheckedScale(scale), MidpointRounding.AwayFromZero);

    /// <summary>A size to physical pixels. A different name from <see cref="ToPixels(double, double)"/> on purpose: (300, 200) would otherwise bind to that one, with 200 as the scale.</summary>
    public static PixelSize SizeToPixels(double width, double height, double scale = 1.0) =>
        new(ToPixels(width, scale), ToPixels(height, scale));

    /// <summary>Physical pixels to units: what a WPF element must measure to cover <paramref name="pixels"/> exactly.</summary>
    public static double ToUnits(int pixels, double scale = 1.0) => pixels / CheckedScale(scale);

    private static double CheckedScale(double scale) =>
        scale > 0 ? scale : throw new ArgumentOutOfRangeException(nameof(scale), scale, "a display scale is above zero");
}
