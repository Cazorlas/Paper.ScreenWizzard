using System.Windows.Media;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.Presentation.Mvvm;
using Paper.ScreenWizzard.Presentation.ViewModels.Shell;

namespace Paper.ScreenWizzard.Presentation.ViewModels.Editor;

/// <summary>One swatch of the colour palette. The palette colours are the user's data, not the theme's, so they are brushes made here.</summary>
public sealed class PaletteColorViewModel : BindableBase
{
    /// <summary>The key of the one swatch the user's own colour occupies.</summary>
    public const string CustomKey = "Custom";

    private readonly ILocalizer _localizer;
    private readonly Func<RgbaColor> _shownColor;
    private RgbaColor _color;

    public PaletteColorViewModel(string key, RgbaColor color, ILocalizer localizer, Func<RgbaColor> shownColor)
    {
        Key = key;
        _color = color;
        _localizer = localizer;
        _shownColor = shownColor;
        Brush = MakeBrush(color);
    }

    /// <summary>"Red", "Blue"... or <see cref="CustomKey"/>; names the string resource and the AutomationId.</summary>
    public string Key { get; }

    public string AutomationId => "Swatch." + Key;

    public RgbaColor Color => _color;

    public SolidColorBrush Brush { get; private set; }

    /// <summary>What a screen reader says: the colour's name, or its hex code for the user's own colour.</summary>
    public string Name => Key == CustomKey
        ? EditorColors.ToHex(_color)
        : _localizer.GetString("Editor.Color." + Key);

    /// <summary>True when this is the colour shown as the current one (the selected shape's, else the next shape's).</summary>
    public bool IsSelected => _shownColor() == _color;

    /// <summary>The user's own colour replaces this swatch's colour; only the custom swatch does it.</summary>
    internal void SetColor(RgbaColor color)
    {
        _color = color;
        Brush = MakeBrush(color);
        RaisePropertyChanged(nameof(Color));
        RaisePropertyChanged(nameof(Brush));
        RaisePropertyChanged(nameof(Name));
        RaisePropertyChanged(nameof(IsSelected));
    }

    /// <summary>Asks the view to read the selection and the name again (after a colour or language change).</summary>
    internal void Refresh()
    {
        RaisePropertyChanged(nameof(IsSelected));
        RaisePropertyChanged(nameof(Name));
    }

    private static SolidColorBrush MakeBrush(RgbaColor color)
    {
        var brush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }
}

/// <summary>The palette's eight colours and the hex text the user's own colour is typed as.</summary>
public static class EditorColors
{
    /// <summary>Red is first because it is the default (SPEC editor, Inputs).</summary>
    public static IReadOnlyList<(string Key, RgbaColor Color)> Palette { get; } =
    [
        ("Red", new RgbaColor(229, 57, 53, 255)),
        ("Orange", new RgbaColor(251, 140, 0, 255)),
        ("Yellow", new RgbaColor(255, 235, 0, 255)),
        ("Green", new RgbaColor(67, 160, 71, 255)),
        ("Blue", new RgbaColor(30, 136, 229, 255)),
        ("Purple", new RgbaColor(142, 36, 170, 255)),
        ("Black", new RgbaColor(0, 0, 0, 255)),
        ("White", new RgbaColor(255, 255, 255, 255)),
    ];

    public static RgbaColor Default => Palette[0].Color;

    /// <summary>"#RRGGBB" for opaque colours.</summary>
    public static string ToHex(RgbaColor color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    /// <summary>Reads "#RRGGBB", "RRGGBB" or "#AARRGGBB" (also without the #); false when it is anything else.</summary>
    public static bool TryParseHex(string? text, out RgbaColor color)
    {
        color = default;
        var digits = (text ?? string.Empty).Trim().TrimStart('#');
        if (digits.Length is not (6 or 8) || !digits.All(Uri.IsHexDigit))
        {
            return false;
        }

        var offset = digits.Length == 8 ? 2 : 0;
        var alpha = digits.Length == 8 ? Convert.ToByte(digits[..2], 16) : (byte)255;
        color = new RgbaColor(
            Convert.ToByte(digits.Substring(offset, 2), 16),
            Convert.ToByte(digits.Substring(offset + 2, 2), 16),
            Convert.ToByte(digits.Substring(offset + 4, 2), 16),
            alpha);
        return true;
    }
}
