using System.Windows;
using Microsoft.Win32;
using Paper.ScreenWizzard.Presentation.Shared.ViewModels;

namespace Paper.ScreenWizzard.Presentation.Shared.Views;

/// <summary>The real "Save as" box: starts in the folder and with the name the use case suggests, and adds the extension the user omits.</summary>
public sealed class FileDialogService : IFileDialogService
{
    private readonly ILocalizer _localizer;

    public FileDialogService(ILocalizer localizer)
    {
        _localizer = localizer;
    }

    public string? PickSavePath(string initialFolder, string suggestedFileName)
    {
        var extension = System.IO.Path.GetExtension(suggestedFileName);
        var dialog = new SaveFileDialog
        {
            Title = _localizer.GetString("Capture.Done.SaveAsBoxTitle"),
            InitialDirectory = initialFolder,
            FileName = suggestedFileName,
            Filter = "PNG (*.png)|*.png|JPG (*.jpg)|*.jpg",
            DefaultExt = extension,
            FilterIndex = string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) ? 2 : 1,
            AddExtension = true,
            OverwritePrompt = true,
        };

        // Over the dialog the user is in, so the box does not open behind an always-on-top window.
        var owner = OwnerWindow.Find();
        var chosen = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        return chosen == true ? dialog.FileName : null;
    }
}
