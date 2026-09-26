using System.Windows;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Presentation.Capture.ViewModels;

namespace Paper.ScreenWizzard.Presentation.Capture.Views;

/// <summary>Opens the real windows of a capture for <see cref="CaptureFlow"/>. Call it on the UI thread.</summary>
public sealed class WpfCaptureViews : ICaptureViews
{
    public IViewHandle OpenCountdown(CountdownViewModel viewModel)
    {
        var window = new CountdownWindow(viewModel);
        window.Show();
        return new WindowHandle(window);
    }

    public IViewHandle OpenSelection(SelectionOverlayViewModel viewModel)
    {
        var window = new SelectionOverlayWindow(viewModel);
        window.Show();

        // Esc needs keyboard focus, so the overlay asks for it. Windows may refuse when another program holds the foreground; the
        // mouse (drag, right button) works either way.
        window.Activate();
        return new WindowHandle(window);
    }

    public IViewHandle OpenDone(CaptureDoneViewModel viewModel, PixelRect area)
    {
        var window = new CaptureDoneWindow(viewModel, area);
        window.Show();
        window.Activate();
        return new WindowHandle(window);
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
