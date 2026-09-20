using System.Windows.Input;

namespace Paper.ScreenWizzard.Presentation.Mvvm;

/// <summary>
/// A command is a named class, not a lambda: the workflow of a button then has a name and a place to be read and tested
/// (skill paper-wpf-style).
/// </summary>
public abstract class CommandBase : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public virtual bool CanExecute(object? parameter) => true;

    public abstract void Execute(object? parameter);

    /// <summary>Tells the bound button to ask CanExecute again, and WPF to requery every other command too.</summary>
    public void RaiseCanExecuteChanged()
    {
        OnCanExecuteChanged();
        CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>Raises the event only; the requery of the whole window is <see cref="RaiseCanExecuteChanged"/>'s job.</summary>
    protected virtual void OnCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
