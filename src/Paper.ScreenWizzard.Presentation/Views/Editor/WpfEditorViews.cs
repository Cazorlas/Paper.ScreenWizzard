using System.Windows;
using Paper.ScreenWizzard.Presentation.ViewModels.Capture;
using Paper.ScreenWizzard.Presentation.ViewModels.Editor;

namespace Paper.ScreenWizzard.Presentation.Views.Editor;

/// <summary>Opens the real editor windows for <see cref="EditorFlow"/>. Call it on the UI thread.</summary>
public sealed class WpfEditorViews : IEditorViews
{
    public IViewHandle OpenEditor(EditorViewModel viewModel)
    {
        var window = new EditorWindow(viewModel);
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
