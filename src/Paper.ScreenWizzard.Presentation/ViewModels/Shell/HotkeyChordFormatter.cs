using Paper.ScreenWizzard.Domain.Shell;

namespace Paper.ScreenWizzard.Presentation.ViewModels.Shell;

/// <summary>How a chord is written for the user: modifiers in the order Windows itself lists them, then the key.</summary>
public static class HotkeyChordFormatter
{
    public static string Format(HotkeyChord chord)
    {
        var parts = new List<string>(5);
        if (chord.Modifiers.HasFlag(HotkeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (chord.Modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (chord.Modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (chord.Modifiers.HasFlag(HotkeyModifiers.Windows))
        {
            parts.Add("Win");
        }

        if (!string.IsNullOrEmpty(chord.Key))
        {
            parts.Add(chord.Key);
        }

        return string.Join("+", parts);
    }
}
