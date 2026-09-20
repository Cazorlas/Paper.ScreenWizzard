using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using NUnit.Framework;
using Window = System.Windows.Window;

namespace Paper.ScreenWizzard.UiTests.Support;

/// <summary>
/// One WPF window shown by <see cref="WpfHost"/>, with FlaUI attached to it by handle: find a control by AutomationId, click
/// and type as a user would, read what it shows, and take the screenshot a person then opens and judges. Generic on purpose:
/// the capture and editor tests reuse it.
/// </summary>
public sealed class WindowSession : IDisposable
{
    private static readonly TimeSpan _findTimeout = TimeSpan.FromSeconds(5);

    private readonly WpfHost _host;
    private readonly UIA3Automation _automation = new();

    static WindowSession()
    {
        // FlaUI's default 0.5 px/ms spends seconds crossing a window; 20 px/ms measured the same trip in ~190 ms.
        Mouse.MovePixelsPerMillisecond = 20;
        Mouse.MovePixelsPerStep = 10;
    }

    public WindowSession(WpfHost host, Window window, IntPtr handle)
    {
        _host = host;
        Window = window;
        Handle = handle;
        Root = _automation.FromHandle(handle);
    }

    /// <summary>The WPF window; touch it only through <see cref="WpfHost.Invoke(Action)"/>.</summary>
    public Window Window { get; }

    public IntPtr Handle { get; }

    /// <summary>The window as FlaUI sees it.</summary>
    public AutomationElement Root { get; }

    public WpfHost Host => _host;

    /// <summary>True while the WPF window has not been closed.</summary>
    public bool IsOpen => _host.Invoke(() => Application.Current.Windows.OfType<Window>().Contains(Window));

    public AutomationElement? Find(string automationId) =>
        Root.FindFirstDescendant(conditions => conditions.ByAutomationId(automationId));

    public bool Exists(string automationId) => Find(automationId) is not null;

    /// <summary>
    /// Waits for the control and fails with its name when it never comes - "'SaveButton' never appeared" is a question
    /// someone can answer, a NullReferenceException with a line number is not.
    /// </summary>
    public AutomationElement Require(string automationId)
    {
        var found = Retry.WhileNull(() => Find(automationId), _findTimeout, TimeSpan.FromMilliseconds(100)).Result;
        Assert.That(found, Is.Not.Null, $"'{automationId}' never appeared in the window");
        return found!;
    }

    /// <summary>What the control shows: the value of an edit box, else the name a screen reader would read.</summary>
    public string TextOf(string automationId)
    {
        var element = Require(automationId);
        var value = element.Patterns.Value.PatternOrDefault;
        return value is not null ? value.Value.Value ?? string.Empty : element.Name ?? string.Empty;
    }

    /// <summary>True when a check box is ticked (UIA toggle pattern).</summary>
    public bool IsToggledOn(string automationId) =>
        Require(automationId).Patterns.Toggle.Pattern.ToggleState.Value == ToggleState.On;

    /// <summary>True when a radio button is the selected one of its group (UIA selection-item pattern).</summary>
    public bool IsSelected(string automationId) =>
        Require(automationId).Patterns.SelectionItem.Pattern.IsSelected.Value;

    public bool IsEnabled(string automationId) => Require(automationId).IsEnabled;

    /// <summary>The accessible name of the control, which is what a screen reader reads for an icon-only button.</summary>
    public string NameOf(string automationId) => Require(automationId).Name ?? string.Empty;

    public void Click(string automationId)
    {
        Require(automationId).Click();
        _host.Settle();
    }

    /// <summary>Puts text in an edit box through its value pattern, as a screen-reader or voice-control user would.</summary>
    public void SetText(string automationId, string text)
    {
        Require(automationId).Patterns.Value.Pattern.SetValue(text);
        _host.Settle();
    }

    /// <summary>Real keyboard focus on the control; typing follows into it.</summary>
    public void Focus(string automationId)
    {
        EnsureForeground();
        Require(automationId).Focus();
        _host.Settle();
    }

    /// <summary>Presses and releases one key on the real keyboard. Only ever sent to this window (see <see cref="EnsureForeground"/>).</summary>
    public void Press(VirtualKeyShort key)
    {
        EnsureForeground();
        Keyboard.Type(key);
        _host.Settle();
    }

    /// <summary>Holds the keys together (Ctrl+Shift+1), then releases them.</summary>
    public void PressChord(params VirtualKeyShort[] keys)
    {
        EnsureForeground();
        Keyboard.TypeSimultaneously(keys);
        _host.Settle();
    }

    /// <summary>The AutomationId of the control that has keyboard focus now.</summary>
    public string FocusedId() => _automation.FocusedElement()?.AutomationId ?? string.Empty;

    /// <summary>Presses Tab <paramref name="count"/> times from where focus is and returns the AutomationId reached after each.</summary>
    public IReadOnlyList<string> TabThrough(int count)
    {
        var reached = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            Press(VirtualKeyShort.TAB);
            reached.Add(FocusedId());
        }

        return reached;
    }

    /// <summary>
    /// Makes this window the foreground window, or fails the test. Real keyboard input goes to whichever window is in front;
    /// typing into another program because Windows refused the switch would be worse than a failed test.
    /// </summary>
    public void EnsureForeground()
    {
        if (GetForegroundWindow() == Handle)
        {
            return;
        }

        // Windows refuses a foreground switch while the user is typing in another program or another process just took it, so
        // one attempt is not enough on a desktop somebody is also using (measured 2026-09-20: 1 of 3 runs of one test failed on
        // the first attempt). A few attempts, then the assert below still fails the test rather than typing elsewhere.
        for (var attempt = 0; attempt < 4 && GetForegroundWindow() != Handle; attempt++)
        {
            var foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
            var thisThread = GetCurrentThreadId();
            var attached = foregroundThread != 0 && foregroundThread != thisThread && AttachThreadInput(thisThread, foregroundThread, true);
            try
            {
                SetForegroundWindow(Handle);
            }
            finally
            {
                if (attached)
                {
                    AttachThreadInput(thisThread, foregroundThread, false);
                }
            }

            Retry.WhileFalse(() => GetForegroundWindow() == Handle, TimeSpan.FromMilliseconds(600), TimeSpan.FromMilliseconds(50));
        }

        Assert.That(GetForegroundWindow(), Is.EqualTo(Handle), "Windows would not bring the test window to the front; real keys would go to another program");
    }

    /// <summary>
    /// Attaches to another window of this process (a dialog or a toast) by its AutomationId. See <see cref="WpfHost.Attach"/>.
    /// </summary>
    public WindowSession AttachToWindow(string automationId) => _host.Attach(automationId);

    /// <summary>True when a visible window of the process carries this AutomationId.</summary>
    public bool WindowExists(string automationId) =>
        _host.Invoke(() => Application.Current.Windows.OfType<Window>().Any(
            w => System.Windows.Automation.AutomationProperties.GetAutomationId(w) == automationId && w.IsVisible));

    /// <summary>Waits until the WPF window is gone (closed by Enter, Esc, a button) and returns whether it went.</summary>
    public bool WaitUntilClosed(TimeSpan timeout) =>
        Retry.WhileTrue(() => IsOpen, timeout, TimeSpan.FromMilliseconds(50)).Success;

    /// <summary>
    /// Takes the window's picture to <c>tests/Paper.ScreenWizzard.UiTests/shots/&lt;name&gt;.png</c> (git-ignored) and returns
    /// the path. Nothing checks a picture nobody opened: open it and judge it against the plan's wireframe.
    /// </summary>
    public string Screenshot(string name)
    {
        var path = Shots.PathOf(name);
        Capture.Element(Root).ToFile(path);
        return path;
    }

    /// <summary>
    /// How many pixels of the control are within <paramref name="tolerance"/> of <paramref name="color"/> in each channel,
    /// drawn in process with WPF's own renderer, so the answer does not depend on what is in front on the screen.
    /// </summary>
    public int CountPixels(string automationId, Color color, int tolerance = 3) => _host.Invoke(() =>
    {
        var element = VisualTreeFinder.FindByAutomationId(Window, automationId)
            ?? throw new AssertionException($"'{automationId}' is not in the window's visual tree");
        var width = Math.Max(1, (int)Math.Ceiling(element.ActualWidth));
        var height = Math.Max(1, (int)Math.Ceiling(element.ActualHeight));
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);

        // Rendering the element itself would draw it at its offset inside its parent and crop it away; painting it through a
        // brush into a visual of its own size puts it at the origin.
        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen())
        {
            context.DrawRectangle(new VisualBrush(element), null, new Rect(0, 0, width, height));
        }

        bitmap.Render(drawing);
        var pixels = new byte[width * height * 4];
        bitmap.CopyPixels(pixels, width * 4, 0);
        var count = 0;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            if (pixels[i + 3] == 255
                && Math.Abs(pixels[i] - color.B) <= tolerance
                && Math.Abs(pixels[i + 1] - color.G) <= tolerance
                && Math.Abs(pixels[i + 2] - color.R) <= tolerance)
            {
                count++;
            }
        }

        return count;
    });

    /// <summary>Every button in the window with the name a screen reader reads for it.</summary>
    public IReadOnlyList<(string AutomationId, string Name)> ButtonNames() =>
        Root.FindAllDescendants(conditions => conditions.ByControlType(ControlType.Button))
            .Select(button => (button.AutomationId ?? string.Empty, button.Name ?? string.Empty))
            .ToList();

    public void Dispose()
    {
        _automation.Dispose();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint attach, uint attachTo, bool attachOrDetach);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
