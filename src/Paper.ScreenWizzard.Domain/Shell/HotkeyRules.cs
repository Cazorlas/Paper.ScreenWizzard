using System.Text;

namespace Paper.ScreenWizzard.Domain.Shell;

/// <summary>The pure rules for a hotkey (SPEC shell, "Phím tắt").</summary>
public static class HotkeyRules
{
    /// <summary>
    /// A letter or digit needs Control, Alt or Windows, because Shift alone only types a capital. PrintScreen and F1..F12
    /// may stand alone. Any other key with no modifier would steal ordinary typing or navigation.
    /// </summary>
    public static bool IsSafe(HotkeyChord chord)
    {
        if (string.IsNullOrWhiteSpace(chord.Key))
        {
            return false;
        }

        var strong = HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Windows;
        if ((chord.Modifiers & strong) != HotkeyModifiers.None)
        {
            return true;
        }

        return IsStandaloneKey(chord.Key);
    }

    /// <summary>True when both chords are the same key with the same modifiers; the key name is not case sensitive.</summary>
    public static bool AreSame(HotkeyChord first, HotkeyChord second) =>
        first.Modifiers == second.Modifiers && string.Equals(first.Key, second.Key, StringComparison.OrdinalIgnoreCase);

    /// <summary>The chord as the user reads it: "Ctrl+Alt+Shift+Win+Key", for example "Ctrl+Shift+A" or "PrintScreen".</summary>
    public static string Format(HotkeyChord chord)
    {
        var text = new StringBuilder();
        if (chord.Modifiers.HasFlag(HotkeyModifiers.Control))
        {
            text.Append("Ctrl+");
        }

        if (chord.Modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            text.Append("Alt+");
        }

        if (chord.Modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            text.Append("Shift+");
        }

        if (chord.Modifiers.HasFlag(HotkeyModifiers.Windows))
        {
            text.Append("Win+");
        }

        return text.Append(chord.Key).ToString();
    }

    private static bool IsStandaloneKey(string key)
    {
        if (string.Equals(key, "PrintScreen", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // F1 .. F12
        return key.Length is 2 or 3
            && (key[0] == 'F' || key[0] == 'f')
            && int.TryParse(key.AsSpan(1), out var number)
            && number is >= 1 and <= 12
            && key.AsSpan(1).IndexOfAnyExceptInRange('0', '9') < 0;
    }
}
