using Paper.ScreenWizzard.Domain.Shared;

namespace Paper.ScreenWizzard.Presentation.Capture.ViewModels;

/// <summary>The "Save as" box of the "Đã chụp" dialog, injectable so a test never opens a real one.</summary>
public interface IFileDialogService
{
    /// <summary>The path the user chose, or null when the user cancelled the box.</summary>
    string? PickSavePath(string initialFolder, string suggestedFileName);
}

/// <summary>A window the flow opened and may close again.</summary>
public interface IViewHandle
{
    /// <summary>Raised once the window is gone, whoever closed it (the flow, a button, the title bar's ✕).</summary>
    event EventHandler? Closed;

    /// <summary>Closes the window; nothing happens when it is already closed.</summary>
    void Close();
}

/// <summary>Opens the windows of a capture, so the flow is tested with fakes and the real windows are tested on their own.</summary>
public interface ICaptureViews
{
    IViewHandle OpenCountdown(CountdownViewModel viewModel);

    IViewHandle OpenSelection(SelectionOverlayViewModel viewModel);

    /// <param name="area">The monitor of the capture, in physical desktop pixels: the dialog centres on it.</param>
    IViewHandle OpenDone(CaptureDoneViewModel viewModel, PixelRect area);
}
