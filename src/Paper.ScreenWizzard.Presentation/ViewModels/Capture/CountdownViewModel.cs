using Paper.ScreenWizzard.Presentation.Mvvm;

namespace Paper.ScreenWizzard.Presentation.ViewModels.Capture;

/// <summary>The number the countdown window shows: 5, 4, 3, 2, 1 as the use case reports the seconds left (SPEC capture, "Độ trễ").</summary>
public sealed class CountdownViewModel : BindableBase
{
    private int _secondsLeft;

    public int SecondsLeft
    {
        get => _secondsLeft;
        set
        {
            if (SetProperty(ref _secondsLeft, value))
            {
                RaisePropertyChanged(nameof(Text));
            }
        }
    }

    /// <summary>Raised when the user asks to give up the delayed capture (a click on the number, or Esc while it has the keyboard).</summary>
    public event EventHandler? CancelRequested;

    public string Text => _secondsLeft.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public void Cancel() => CancelRequested?.Invoke(this, EventArgs.Empty);
}
