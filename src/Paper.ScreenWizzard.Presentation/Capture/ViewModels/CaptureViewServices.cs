using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Presentation.Shared.ViewModels;

namespace Paper.ScreenWizzard.Presentation.Capture.ViewModels;

/// <summary>Opens the windows of a capture, so the flow is tested with fakes and the real windows are tested on their own.</summary>
public interface ICaptureViews
{
    IViewHandle OpenCountdown(CountdownViewModel viewModel);

    IViewHandle OpenSelection(SelectionOverlayViewModel viewModel);

    /// <param name="area">The monitor of the capture, in physical desktop pixels: the dialog centres on it.</param>
    IViewHandle OpenDone(CaptureDoneViewModel viewModel, PixelRect area);
}
