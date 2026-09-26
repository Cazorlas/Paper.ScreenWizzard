using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UseCases.Shared.Models;
using Paper.ScreenWizzard.UseCases.Shell.Ports;

namespace Paper.ScreenWizzard.Infrastructure.Shell;

/// <summary>
/// Global hotkeys through <c>RegisterHotKey</c>, owned by a hidden message-only window (an <see cref="HwndSource"/> with the parent
/// <c>HWND_MESSAGE</c>) on the thread that created the service - the UI thread, which pumps the messages. Every method must be called
/// on that thread: a hotkey is bound to the thread of its window.
/// <para>
/// <c>Register</c> is atomic. A second <c>RegisterHotKey</c> with the same window and id keeps the first one alongside (the Learn page), so
/// a change registers the new chord under a FRESH id and only when Windows accepts it unregisters the old id. A refused chord therefore
/// leaves the old one working (SPEC shell F4). F12 is refused before Windows is asked: the page says it is reserved for the debugger.
/// </para>
/// </summary>
public sealed class HotkeyService : IHotkeyPort, IDisposable
{
    private const int FirstId = 0x0100;
    private const int LastId = 0xBFFF; // ids 0x0000-0xBFFF are the application's (RegisterHotKey page)
    private const int VirtualKeyF12 = 0x7B;

    private readonly HwndSource _window;
    private readonly Dictionary<CaptureKind, (int Id, HotkeyChord Chord)> _registered = [];
    private int _nextId = FirstId;
    private bool _disposed;

    public HotkeyService(string windowName = "Paper.ScreenWizzard.Hotkeys")
    {
        var parameters = new HwndSourceParameters(windowName)
        {
            ParentWindow = NativeMethods.HwndMessage,
            WindowStyle = 0,
            Width = 0,
            Height = 0,
        };
        _window = new HwndSource(parameters);
        _window.AddHook(OnMessage);
    }

    public event Action<CaptureKind>? Pressed;

    public PortResult Register(CaptureKind kind, HotkeyChord chord)
    {
        var text = HotkeyRules.Format(chord);
        if (_disposed)
        {
            return PortResult.Fail($"{text} could not be registered: the hotkey service was already disposed");
        }

        if (!_window.CheckAccess())
        {
            return PortResult.Fail($"{text} could not be registered: hotkeys must be registered on the thread that created the hotkey window");
        }

        if (!TryVirtualKey(chord.Key, out var virtualKey))
        {
            return PortResult.Fail($"{text} could not be registered: '{chord.Key}' is not a key name this app knows");
        }

        if (virtualKey == VirtualKeyF12)
        {
            return PortResult.Fail($"{text} could not be registered: F12 is reserved by Windows for the debugger and should not be used as a hotkey");
        }

        // The same chord for the same kind is already the one Windows holds for us; asking again would be refused as "already registered".
        if (_registered.TryGetValue(kind, out var current) && HotkeyRules.AreSame(current.Chord, chord))
        {
            return PortResult.Ok;
        }

        var id = TakeId();
        if (!NativeMethods.RegisterHotKey(_window.Handle, id, ModifiersOf(chord.Modifiers) | NativeMethods.ModNoRepeat, (uint)virtualKey))
        {
            var error = Marshal.GetLastWin32Error();
            return PortResult.Fail($"{text} could not be registered: {new Win32Exception(error).Message} (Win32 error {error}); another program may hold it");
        }

        // Accepted: only now is the old chord of this kind let go.
        if (current.Id != 0)
        {
            NativeMethods.UnregisterHotKey(_window.Handle, current.Id);
        }

        _registered[kind] = (id, chord);
        return PortResult.Ok;
    }

    public void Unregister(CaptureKind kind)
    {
        if (_disposed || !_registered.Remove(kind, out var entry))
        {
            return;
        }

        NativeMethods.UnregisterHotKey(_window.Handle, entry.Id);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var entry in _registered.Values)
        {
            NativeMethods.UnregisterHotKey(_window.Handle, entry.Id);
        }

        _registered.Clear();
        _window.RemoveHook(OnMessage);
        _window.Dispose();
    }

    /// <summary>
    /// The virtual-key code of a key as the settings name it: "PrintScreen", a single letter or digit, or the name WPF gives the key
    /// (F1..F24, Space, Insert, Prior, OemTilde ...), which is what the Settings window writes.
    /// </summary>
    internal static bool TryVirtualKey(string name, out int virtualKey)
    {
        virtualKey = 0;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        Key key;
        if (string.Equals(name, "PrintScreen", StringComparison.OrdinalIgnoreCase))
        {
            key = Key.Snapshot;
        }
        else if (name.Length == 1 && name[0] is >= '0' and <= '9')
        {
            key = Key.D0 + (name[0] - '0');
        }
        else if (name.Length == 1 && char.IsAsciiLetter(name[0]))
        {
            key = Key.A + (char.ToUpperInvariant(name[0]) - 'A');
        }
        else if (!char.IsAsciiDigit(name[0]) && Enum.TryParse(name, true, out key) && Enum.IsDefined(key) && key != Key.None)
        {
            // A name WPF knows. A string of digits is refused above: Enum.TryParse would take "112" as the number 112.
        }
        else
        {
            return false;
        }

        virtualKey = KeyInterop.VirtualKeyFromKey(key);
        return virtualKey != 0;
    }

    private static uint ModifiersOf(HotkeyModifiers modifiers)
    {
        uint result = 0;
        if (modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            result |= NativeMethods.ModAlt;
        }

        if (modifiers.HasFlag(HotkeyModifiers.Control))
        {
            result |= NativeMethods.ModControl;
        }

        if (modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            result |= NativeMethods.ModShift;
        }

        if (modifiers.HasFlag(HotkeyModifiers.Windows))
        {
            result |= NativeMethods.ModWin;
        }

        return result;
    }

    private int TakeId()
    {
        while (true)
        {
            var id = _nextId;
            _nextId = _nextId >= LastId ? FirstId : _nextId + 1;
            if (_registered.Values.All(entry => entry.Id != id))
            {
                return id;
            }
        }
    }

    private IntPtr OnMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == NativeMethods.WmHotkey)
        {
            var id = wParam.ToInt32();
            var match = _registered.Where(pair => pair.Value.Id == id).Select(pair => (CaptureKind?)pair.Key).FirstOrDefault();
            if (match is { } kind)
            {
                // Raised after the lookup, not inside it: a handler may register another chord and change the dictionary.
                handled = true;
                Pressed?.Invoke(kind);
            }
        }

        return IntPtr.Zero;
    }
}
