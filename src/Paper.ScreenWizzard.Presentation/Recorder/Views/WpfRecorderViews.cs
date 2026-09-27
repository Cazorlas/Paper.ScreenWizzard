using System.Windows;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Presentation.Recorder.ViewModels;
using Paper.ScreenWizzard.Presentation.Shared.ViewModels;

namespace Paper.ScreenWizzard.Presentation.Recorder.Views;

/// <summary>Opens the real windows of a recording for <see cref="RecordingFlow"/>. Call it on the UI thread.</summary>
public sealed class WpfRecorderViews : IRecorderViews
{
    public PixelPoint PointerPosition() => AreaPickerWindow.Pointer();

    public Task<PixelRect?> PickRegionAsync(IReadOnlyList<MonitorInfo> monitors) => AreaPickerWindow.PickRegionAsync(monitors);

    public Task<PickedWindow?> PickWindowAsync(IReadOnlyList<MonitorInfo> monitors, Func<PixelPoint, PickedWindow?> windowAt) =>
        AreaPickerWindow.PickWindowAsync(monitors, windowAt);

    public IRecordingOutline OpenOutline(PixelRect area)
    {
        var window = new RecordingOutlineWindow(area);
        window.Show();
        return window;
    }

    public IViewHandle OpenRecorded(RecordedViewModel viewModel, PixelRect area)
    {
        var window = new RecordedWindow(viewModel, area);
        var handle = new WindowHandle(window);
        window.Show();
        window.Activate();
        return handle;
    }

    /// <summary>A WPF window as something the flow can close; closing twice, or after the user closed it, does nothing.</summary>
    private sealed class WindowHandle : IViewHandle
    {
        private readonly Window _window;
        private bool _closed;

        public WindowHandle(Window window)
        {
            _window = window;
            window.Closed += (_, _) =>
            {
                _closed = true;
                Closed?.Invoke(this, EventArgs.Empty);
            };
        }

        public event EventHandler? Closed;

        public void Close()
        {
            if (!_closed)
            {
                _window.Close();
            }
        }
    }
}
