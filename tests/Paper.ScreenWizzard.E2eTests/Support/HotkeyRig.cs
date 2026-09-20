using System.Collections.Concurrent;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.Infrastructure.Shell;

namespace Paper.ScreenWizzard.E2eTests.Support;

/// <summary>What the hotkey tests share: services made on the STA thread that pumps messages, chords nobody uses, real keystrokes.</summary>
public sealed class HotkeyRig : IDisposable
{
    public const HotkeyModifiers Chorded = HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift;

    public static readonly HotkeyChord F11 = new(Chorded, "F11");
    public static readonly HotkeyChord F13 = new(Chorded, "F13");
    public static readonly HotkeyChord F14 = new(Chorded, "F14");
    public static readonly HotkeyChord F15 = new(Chorded, "F15");

    private readonly List<HotkeyService> _services = [];

    public readonly record struct Outcome(bool Success, string? Detail);

    // The service is created on the STA thread that pumps messages: the hidden window of the hotkeys lives on the thread that made it.
    public HotkeyService NewService(ConcurrentQueue<CaptureKind>? pressed = null)
    {
        var service = StaHost.Instance.Invoke(() =>
        {
            var created = new HotkeyService("E2E hotkeys " + Guid.NewGuid().ToString("N"));
            if (pressed is not null)
            {
                created.Pressed += pressed.Enqueue;
            }

            return created;
        });
        _services.Add(service);
        return service;
    }

    public static Outcome Register(HotkeyService service, CaptureKind kind, HotkeyChord chord)
    {
        var result = StaHost.Instance.Invoke(() => service.Register(kind, chord));
        return new Outcome(result.Success, result.Detail);
    }

    public static bool WaitFor(ConcurrentQueue<CaptureKind> pressed, CaptureKind kind, int milliseconds = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (DateTime.UtcNow < deadline)
        {
            if (pressed.Contains(kind))
            {
                return true;
            }

            Thread.Sleep(25);
        }

        return false;
    }

    public static void PressChord(int functionKey) => KeySender.Press(Chorded, KeySender.FunctionKey(functionKey));

    public void Dispose()
    {
        foreach (var service in _services)
        {
            StaHost.Instance.Invoke(service.Dispose);
        }

        _services.Clear();
    }
}
