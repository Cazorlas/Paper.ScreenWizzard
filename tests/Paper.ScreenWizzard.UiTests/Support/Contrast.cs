using System.Windows;
using System.Windows.Media;

namespace Paper.ScreenWizzard.UiTests.Support;

/// <summary>WCAG 2 contrast: 4.5:1 for text, 3:1 for icons and borders (skill paper-wpf-style).</summary>
public static class Contrast
{
    public const double MinimumForText = 4.5;

    public const double MinimumForIconOrBorder = 3.0;

    /// <summary>The ratio (L1 + 0.05) / (L2 + 0.05), lighter colour first, from 1 (none) to 21 (black on white).</summary>
    public static double Ratio(Color first, Color second)
    {
        var a = Luminance(first);
        var b = Luminance(second);
        var lighter = Math.Max(a, b);
        var darker = Math.Min(a, b);
        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <summary>The ratio of two solid brushes; a brush that is not a solid colour cannot be measured, so it fails loudly.</summary>
    public static double Ratio(Brush first, Brush second) => Ratio(ColorOf(first), ColorOf(second));

    public static Color ColorOf(Brush brush) =>
        brush is SolidColorBrush solid
            ? solid.Color
            : throw new InvalidOperationException($"{brush.GetType().Name} is not a SolidColorBrush, so its contrast cannot be measured");

    /// <summary>The brush stored under <paramref name="key"/> in a resource dictionary, or null when the key is missing.</summary>
    public static Brush? BrushIn(ResourceDictionary dictionary, string key) =>
        dictionary.Contains(key) ? dictionary[key] as Brush : null;

    // Relative luminance as WCAG defines it: sRGB channels made linear, then weighted by how bright the eye sees each.
    private static double Luminance(Color color)
    {
        static double Linear(byte channel)
        {
            var c = channel / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Linear(color.R)) + (0.7152 * Linear(color.G)) + (0.0722 * Linear(color.B));
    }
}
