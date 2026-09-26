using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Animation;
using Paper.ScreenWizzard.Presentation.Capture.ViewModels;

namespace Paper.ScreenWizzard.Presentation.Capture.Views;

/// <summary>
/// Shows the number and gives each new one a short pulse. When the user turned Windows animation off
/// (<see cref="SystemParameters.ClientAreaAnimation"/>) the number just changes.
/// </summary>
public partial class CountdownWindow : Window
{
    private static readonly Duration _pulse = new(TimeSpan.FromMilliseconds(250));

    private readonly CountdownViewModel _viewModel;

    public CountdownWindow(CountdownViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.PropertyChanged += OnViewModelChanged;
        Closed += (_, _) => viewModel.PropertyChanged -= OnViewModelChanged;

        // The window never takes focus (the user is holding a menu open in another program, and Esc there would close that menu), so
        // Esc reaches it only when the user clicked it first; a click on the number is the way to give up.
        MouseLeftButtonUp += (_, _) => viewModel.Cancel();
        MouseRightButtonUp += (_, _) => viewModel.Cancel();
        KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                viewModel.Cancel();
            }
        };
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CountdownViewModel.Text) || !SystemParameters.ClientAreaAnimation || _viewModel.SecondsLeft <= 0)
        {
            return;
        }

        var shrink = new DoubleAnimation(1.3, 1.0, _pulse);
        PulseScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, shrink);
        PulseScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, shrink);
    }
}
