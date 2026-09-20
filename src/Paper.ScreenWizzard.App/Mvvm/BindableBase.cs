using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Paper.ScreenWizzard.App.Mvvm;

/// <summary>
/// INotifyPropertyChanged in one place. Kept in this app on purpose: PaperLibrary is a project inside the Revit
/// solution, and this repository never links another solution's source (CLAUDE.md).
/// </summary>
public abstract class BindableBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Sets the field and raises the change only when the value really differs.</summary>
    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        RaisePropertyChanged(propertyName);
        return true;
    }

    protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
