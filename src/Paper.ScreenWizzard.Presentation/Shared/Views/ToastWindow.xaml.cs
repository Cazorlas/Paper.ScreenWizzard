using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Paper.ScreenWizzard.Presentation.Shared.Views;

/// <summary>
/// Shows its text, waits <c>lifetime</c>, then fades out and closes itself. When the user turned Windows animation off
/// (<see cref="SystemParameters.ClientAreaAnimation"/>) it appears and disappears without the fade.
/// </summary>
public partial class ToastWindow : Window
{
    private static readonly Duration _fadeIn = new(TimeSpan.FromMilliseconds(150));
    private static readonly Duration _fadeOut = new(TimeSpan.FromMilliseconds(250));

    private readonly DispatcherTimer _lifetime;

    public ToastWindow(string text, TimeSpan lifetime)
    {
        InitializeComponent();
        MessageText.Text = text;
        var animate = SystemParameters.ClientAreaAnimation;
        Opacity = animate ? 0 : 1;

        _lifetime = new DispatcherTimer { Interval = lifetime };
        _lifetime.Tick += (_, _) =>
        {
            _lifetime.Stop();
            if (animate)
            {
                var fade = new DoubleAnimation(0, _fadeOut);
                fade.Completed += (_, _) => Close();
                BeginAnimation(OpacityProperty, fade);
            }
            else
            {
                Close();
            }
        };

        Loaded += (_, _) =>
        {
            PlaceAtBottomRight();
            if (animate)
            {
                BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, _fadeIn));
            }

            _lifetime.Start();
        };
        Closed += (_, _) => _lifetime.Stop();
    }

    // Bottom right of the primary work area, clear of the taskbar; WPF units, so no per-monitor arithmetic is needed here.
    private void PlaceAtBottomRight()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth - 8;
        Top = area.Bottom - ActualHeight - 8;
    }
}
