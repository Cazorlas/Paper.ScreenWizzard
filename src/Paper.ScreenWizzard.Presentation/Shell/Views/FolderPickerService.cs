using System.Windows;
using Microsoft.Win32;
using Paper.ScreenWizzard.Presentation.Shell.ViewModels;

namespace Paper.ScreenWizzard.Presentation.Shell.Views;

/// <summary>The folder browser of the Settings window: the system's own folder dialog, shown over the active window.</summary>
public sealed class FolderPickerService : IFolderPickerService
{
    public string? PickFolder(string? initialFolder)
    {
        var dialog = new OpenFolderDialog { Multiselect = false };
        if (!string.IsNullOrWhiteSpace(initialFolder) && System.IO.Directory.Exists(initialFolder))
        {
            dialog.InitialDirectory = initialFolder;
        }

        var owner = OwnerWindow.Find();
        var chosen = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        return chosen == true ? dialog.FolderName : null;
    }
}
