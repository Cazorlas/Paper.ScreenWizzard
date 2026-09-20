using System.ComponentModel;
using System.Runtime.InteropServices;
using Paper.ScreenWizzard.Domain.Shell;

namespace Paper.ScreenWizzard.E2eTests.Support;

/// <summary>
/// Presses a chord with the real keyboard input queue (SendInput). It refuses PrintScreen: a real PrintScreen can open the Snipping
/// Tool of Windows or fire the real app's own hotkey on the developer's machine, so the tests use other keys (plan, Decisions, group 5).
/// </summary>
public static class KeySender
{
    private const ushort VkSnapshot = 0x2C;

    /// <summary>Virtual-key code of a function key F1..F24 (VK_F1 is 0x70).</summary>
    public static ushort FunctionKey(int number) => (ushort)(0x70 + number - 1);

    public static void Press(HotkeyModifiers modifiers, ushort key)
    {
        if (key == VkSnapshot)
        {
            throw new InvalidOperationException("PrintScreen is never sent by a test");
        }

        var down = new List<ushort>();
        if (modifiers.HasFlag(HotkeyModifiers.Control))
        {
            down.Add(Native.VkControl);
        }

        if (modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            down.Add(Native.VkMenu);
        }

        if (modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            down.Add(Native.VkShift);
        }

        down.Add(key);

        var inputs = new List<Native.KeyboardInput>();
        foreach (var code in down)
        {
            inputs.Add(Key(code, up: false));
        }

        foreach (var code in Enumerable.Reverse(down))
        {
            inputs.Add(Key(code, up: true));
        }

        var size = Marshal.SizeOf<Native.KeyboardInput>();
        if (size != 40)
        {
            throw new InvalidOperationException($"INPUT should be 40 bytes on 64-bit Windows, the test struct is {size}");
        }

        var sent = Native.SendInput((uint)inputs.Count, inputs.ToArray(), size);
        if (sent != inputs.Count)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"SendInput inserted {sent} of {inputs.Count} events (blocked by another thread, or UIPI)");
        }
    }

    private static Native.KeyboardInput Key(ushort code, bool up) => new()
    {
        Type = Native.InputKeyboard,
        VirtualKey = code,
        Flags = up ? Native.KeyEventKeyUp : 0,
    };
}
