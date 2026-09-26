using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UseCases.Shared.Models;

namespace Paper.ScreenWizzard.UseCases.Shell.Ports;

/// <summary>Global hotkeys. A chord another program holds is a failed Register, not an exception.</summary>
public interface IHotkeys
{
    /// <summary>
    /// Registers <paramref name="chord"/> for <paramref name="kind"/>, replacing that kind's earlier chord. It is atomic: when
    /// Windows or another program holds the new chord the result is a failure and the earlier chord stays registered
    /// (SPEC shell F4: "phím cũ vẫn hoạt động").
    /// </summary>
    PortResult Register(CaptureKind kind, HotkeyChord chord);

    void Unregister(CaptureKind kind);

    /// <summary>Raised on the UI thread when a registered hotkey is pressed.</summary>
    event Action<CaptureKind>? Pressed;
}
