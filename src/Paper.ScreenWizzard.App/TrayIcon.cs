using System.ComponentModel;
using Paper.ScreenWizzard.Presentation.Shared.ViewModels;
using Paper.ScreenWizzard.Presentation.Shell.ViewModels;
using WinForms = System.Windows.Forms;

namespace Paper.ScreenWizzard.App;

/// <summary>
/// The system tray icon (<c>System.Windows.Forms.NotifyIcon</c>: WPF has none) and its menu. It only shows what
/// <see cref="TrayMenuViewModel"/> says - the items, their shortcut texts and the tick of "Capture bar" - and calls each item's command; every
/// decision is somewhere else. The texts are read again when the language changes and the whole menu is built again when the items do
/// (a hotkey changed). Dispose hides the icon first, so no ghost icon stays in the tray after the process ends.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly WinForms.NotifyIcon _icon;
    private readonly WinForms.ContextMenuStrip _menu = new();
    private readonly TrayMenuViewModel _viewModel;
    private readonly ILocalizer _localizer;
    private readonly System.Drawing.Icon _image;
    private System.Drawing.Icon? _recordingImage;
    private readonly List<(TrayMenuItemViewModel Item, WinForms.ToolStripMenuItem Entry)> _entries = [];
    private Action? _balloonClicked;
    private bool _disposed;

    public TrayIcon(TrayMenuViewModel viewModel, ILocalizer localizer, Action showCaptureBar)
    {
        _viewModel = viewModel;
        _localizer = localizer;
        _image = TrayIconImage.Create();
        _icon = new WinForms.NotifyIcon
        {
            Icon = _image,
            Text = localizer.GetString("Tray.Tooltip"),
            ContextMenuStrip = _menu,
        };
        _icon.MouseDoubleClick += (_, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Left)
            {
                showCaptureBar();
            }
        };

        _icon.BalloonTipClicked += (_, _) => _balloonClicked?.Invoke();
        _icon.BalloonTipClosed += (_, _) => _balloonClicked = null;

        Rebuild();
        _viewModel.PropertyChanged += OnViewModelChanged;
        _localizer.LanguageChanged += OnLanguageChanged;
        _icon.Visible = true;
    }

    /// <summary>
    /// A Windows notification from the tray icon (on Windows 10 and 11 it is a toast and goes to the notification centre). A click on it
    /// runs <paramref name="clicked"/> (none: a click does nothing); one that closes unclicked forgets it.
    /// </summary>
    public void ShowNotice(string title, string text, Action? clicked)
    {
        _balloonClicked = clicked;
        _icon.ShowBalloonTip(10_000, title, text, WinForms.ToolTipIcon.Info);
    }

    /// <summary>
    /// While recording the icon carries a red dot and the tooltip says the time recorded (SPEC recorder, "What the user does" 4); null
    /// puts the usual icon and tooltip back.
    /// </summary>
    public void ShowRecording(string? tooltip)
    {
        if (_disposed)
        {
            return;
        }

        if (tooltip is null)
        {
            _icon.Icon = _image;
            _icon.Text = _localizer.GetString("Tray.Tooltip");
            DisposeRecordingImage();
            return;
        }

        _recordingImage ??= WithRecordingDot(_image);
        _icon.Icon = _recordingImage;
        _icon.Text = tooltip.Length > 120 ? tooltip[..120] : tooltip;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _viewModel.PropertyChanged -= OnViewModelChanged;
        _localizer.LanguageChanged -= OnLanguageChanged;
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
        _image.Dispose();
        DisposeRecordingImage();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TrayMenuViewModel.Items))
        {
            Rebuild();
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        _icon.Text = _localizer.GetString("Tray.Tooltip");
        Rebuild();
    }

    // Capture x4, a line, Open image / Capture bar / Settings / Kiểm bản mới (/ Tải bản mới… while a newer version is out), a line, Exit.
    private void Rebuild()
    {
        foreach (var (item, _) in _entries)
        {
            item.PropertyChanged -= OnItemChanged;
        }

        _entries.Clear();
        _menu.Items.Clear();
        var items = _viewModel.Items;
        for (var index = 0; index < items.Count; index++)
        {
            if (index == 4 || index == items.Count - 1)
            {
                _menu.Items.Add(new WinForms.ToolStripSeparator());
            }

            var item = items[index];
            var entry = new WinForms.ToolStripMenuItem(TextOf(item))
            {
                ShortcutKeyDisplayString = item.ShortcutText,
                Checked = item.IsChecked,
            };
            entry.Click += (_, _) => item.Command.Execute(null);
            item.PropertyChanged += OnItemChanged;
            _entries.Add((item, entry));
            _menu.Items.Add(entry);
        }
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is TrayMenuItemViewModel item && e.PropertyName == nameof(TrayMenuItemViewModel.IsChecked))
        {
            foreach (var (candidate, entry) in _entries)
            {
                if (ReferenceEquals(candidate, item))
                {
                    entry.Checked = item.IsChecked;
                }
            }
        }
    }

    // The app's icon with a red dot in its lower right corner, the colour of a recording light.
    private static System.Drawing.Icon WithRecordingDot(System.Drawing.Icon icon)
    {
        using var bitmap = icon.ToBitmap();
        using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
        using (var red = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255, 196, 43, 28)))
        using (var ring = new System.Drawing.Pen(System.Drawing.Color.White, Math.Max(1f, bitmap.Width / 16f)))
        {
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var size = bitmap.Width * 0.55f;
            var x = bitmap.Width - size - 0.5f;
            var y = bitmap.Height - size - 0.5f;
            graphics.FillEllipse(red, x, y, size, size);
            graphics.DrawEllipse(ring, x, y, size, size);
        }

        var handle = bitmap.GetHicon();
        using var owned = System.Drawing.Icon.FromHandle(handle);
        var copy = (System.Drawing.Icon)owned.Clone();
        DestroyIcon(handle);
        return copy;
    }

    private void DisposeRecordingImage()
    {
        _recordingImage?.Dispose();
        _recordingImage = null;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);

    // The resource text may end with "…" (Open image…), which a menu item shows as it is.
    private string TextOf(TrayMenuItemViewModel item) => _localizer.GetString(item.TextKey);
}
