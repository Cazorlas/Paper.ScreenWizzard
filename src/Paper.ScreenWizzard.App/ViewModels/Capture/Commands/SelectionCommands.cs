using Paper.ScreenWizzard.App.Mvvm;

namespace Paper.ScreenWizzard.App.ViewModels.Capture.Commands;

/// <summary>Esc or the right mouse button on the overlay: nothing is captured and the screen returns to normal (SPEC capture step 5).</summary>
public sealed class CancelSelectionCommand : CommandBase
{
    private readonly SelectionOverlayViewModel _owner;

    public CancelSelectionCommand(SelectionOverlayViewModel owner)
    {
        _owner = owner;
    }

    public override void Execute(object? parameter) => _owner.Cancel();
}
