using System.Globalization;
using System.Windows.Data;

namespace Paper.ScreenWizzard.Presentation.Views.Shell;

/// <summary>
/// Lets one radio button stand for one value of a setting. ConverterParameter is the value written as text
/// ("Vietnamese", "5"): the button is checked when the setting equals it, and checking the button writes it back. Unchecking
/// writes nothing, because choosing another button does that.
/// </summary>
public sealed class ChoiceConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not true || parameter is not string text)
        {
            return Binding.DoNothing;
        }

        return targetType.IsEnum ? Enum.Parse(targetType, text) : System.Convert.ChangeType(text, targetType, CultureInfo.InvariantCulture);
    }
}
