using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Presentation.Mvvm;
using Paper.ScreenWizzard.Presentation.Shell.Commands;

namespace Paper.ScreenWizzard.Presentation.Shell.ViewModels;

/// <summary>
/// The capture bar: one button per capture kind, a Settings button and a close button. It captures nothing itself; it
/// raises events and the composition root wires them to the capture and shell use cases.
/// </summary>
public sealed class CaptureBarViewModel : BindableBase
{
    public CaptureBarViewModel()
    {
        CaptureCommand = new RequestCaptureCommand(this);
        OpenSettingsCommand = new RequestSettingsFromBarCommand(this);
        CloseCommand = new RequestCloseBarCommand(this);
    }

    /// <summary>The user pressed a capture button.</summary>
    public event Action<CaptureKind>? CaptureRequested;

    public event EventHandler? SettingsRequested;

    /// <summary>The X of the bar: closes this window only, the app keeps running in the tray (SPEC shell).</summary>
    public event EventHandler? CloseRequested;

    /// <summary>CommandParameter is the <see cref="CaptureKind"/> of the button.</summary>
    public RequestCaptureCommand CaptureCommand { get; }

    public RequestSettingsFromBarCommand OpenSettingsCommand { get; }

    public RequestCloseBarCommand CloseCommand { get; }

    internal void RaiseCaptureRequested(CaptureKind kind) => CaptureRequested?.Invoke(kind);

    internal void RaiseSettingsRequested() => SettingsRequested?.Invoke(this, EventArgs.Empty);

    internal void RaiseCloseRequested() => CloseRequested?.Invoke(this, EventArgs.Empty);
}
