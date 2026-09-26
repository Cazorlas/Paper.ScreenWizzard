using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.Presentation.Shell.ViewModels;

namespace Paper.ScreenWizzard.Presentation.Shell.Views;

/// <summary>
/// A box the user fills by pressing the keys, not by typing letters: it shows the chord ("Ctrl+Shift+1") and offers it as
/// <see cref="Chord"/>. Tab, Shift+Tab, Enter and Esc are left alone so the keyboard still moves between controls, saves and
/// cancels the window (SPEC shell, "Dùng được bằng bàn phím").
/// </summary>
public class HotkeyCaptureBox : TextBox
{
    public static readonly DependencyProperty ChordProperty = DependencyProperty.Register(
        nameof(Chord),
        typeof(HotkeyChord),
        typeof(HotkeyCaptureBox),
        new FrameworkPropertyMetadata(default(HotkeyChord), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnChordChanged));

    public HotkeyCaptureBox()
    {
        IsReadOnly = true;
        IsUndoEnabled = false;
        IsReadOnlyCaretVisible = false;
    }

    public HotkeyChord Chord
    {
        get => (HotkeyChord)GetValue(ChordProperty);
        set => SetValue(ChordProperty, value);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        // With Alt held WPF reports Key.System and puts the real key in SystemKey; an IME does the same with ImeProcessedKey.
        var key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            Key.DeadCharProcessed => e.DeadCharProcessedKey,
            _ => e.Key,
        };

        if (IsModifierKey(key))
        {
            // Wait for the real key of the chord; a modifier alone is never a hotkey.
            e.Handled = true;
            return;
        }

        var modifiers = ToModifiers(Keyboard.Modifiers);
        var navigation = modifiers == HotkeyModifiers.None && key is Key.Tab or Key.Enter or Key.Escape;
        var backwardTab = modifiers == HotkeyModifiers.Shift && key == Key.Tab;
        if (navigation || backwardTab)
        {
            base.OnPreviewKeyDown(e);
            return;
        }

        Chord = new HotkeyChord(modifiers, NameOf(key));
        e.Handled = true;
    }

    // Windows delivers no key-down for PrintScreen to a program that has not registered it, only the key-up: the default chords of
    // this very app are PrintScreen ones, so the box takes the chord on the key-up of that one key.
    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        if (e.Key != Key.Snapshot)
        {
            base.OnPreviewKeyUp(e);
            return;
        }

        Chord = new HotkeyChord(ToModifiers(Keyboard.Modifiers), NameOf(e.Key));
        e.Handled = true;
    }

    private static void OnChordChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) =>
        ((HotkeyCaptureBox)sender).Text = HotkeyChordFormatter.Format((HotkeyChord)e.NewValue);

    private static bool IsModifierKey(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;

    private static HotkeyModifiers ToModifiers(ModifierKeys keys)
    {
        var modifiers = HotkeyModifiers.None;
        if (keys.HasFlag(ModifierKeys.Control))
        {
            modifiers |= HotkeyModifiers.Control;
        }

        if (keys.HasFlag(ModifierKeys.Alt))
        {
            modifiers |= HotkeyModifiers.Alt;
        }

        if (keys.HasFlag(ModifierKeys.Shift))
        {
            modifiers |= HotkeyModifiers.Shift;
        }

        if (keys.HasFlag(ModifierKeys.Windows))
        {
            modifiers |= HotkeyModifiers.Windows;
        }

        return modifiers;
    }

    // The key as the user reads it. Snapshot is WPF's second name for PrintScreen and D0..D9 are the digit row.
    private static string NameOf(Key key) => key switch
    {
        Key.Snapshot => "PrintScreen",
        >= Key.D0 and <= Key.D9 => ((int)key - (int)Key.D0).ToString(),
        _ => key.ToString(),
    };
}
