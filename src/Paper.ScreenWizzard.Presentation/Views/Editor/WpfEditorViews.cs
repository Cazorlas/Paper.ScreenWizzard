using System.Windows;
using Paper.ScreenWizzard.Presentation.ViewModels.Capture;
using Paper.ScreenWizzard.Presentation.ViewModels.Editor;
using Paper.ScreenWizzard.UseCases.Shell.Ports;

namespace Paper.ScreenWizzard.Presentation.Views.Editor;

/// <summary>Opens the real editor windows for <see cref="EditorFlow"/>. Call it on the UI thread.</summary>
public sealed class WpfEditorViews : IEditorViews
{
    private readonly IMonitorCatalogPort? _monitors;

    /// <param name="monitors">The monitors and the pointer, so each window opens on the monitor that holds the pointer; none keeps WPF's own centring.</param>
    public WpfEditorViews(IMonitorCatalogPort? monitors = null)
    {
        _monitors = monitors;
    }

    public IViewHandle OpenEditor(EditorViewModel viewModel)
    {
        var window = new EditorWindow(viewModel, _monitors);
        window.Show();
        window.Activate();
        return new WindowHandle(window);
    }

    /// <summary>A WPF window as something the flow can watch; the flow only needs to know when it is gone, however it was closed.</summary>
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
