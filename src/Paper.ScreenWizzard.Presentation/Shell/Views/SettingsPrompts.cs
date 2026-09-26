using System.Windows;
using Paper.ScreenWizzard.Presentation.Shared.ViewModels;
using Paper.ScreenWizzard.Presentation.Shared.Views;
using Paper.ScreenWizzard.Presentation.Shell.ViewModels;
using Paper.ScreenWizzard.UseCases.Shared.Models;

namespace Paper.ScreenWizzard.Presentation.Shell.Views;

/// <summary>The questions the Settings window asks, as real Yes/No dialogs shown over the window that asked.</summary>
public sealed class SettingsPrompts : ISettingsPrompts
{
    private readonly ILocalizer _localizer;

    public SettingsPrompts(ILocalizer localizer)
    {
        _localizer = localizer;
    }

    public bool AskCreateFolder(string folder)
    {
        // "The folder X does not exist." then the question, so the same text of Shell.FolderMissing serves the message and the prompt.
        var question = _localizer.Format(NotificationMessage.Of("Shell.FolderMissing", folder))
            + "\n"
            + _localizer.GetString("Settings.CreateFolderQuestion");
        var dialog = new ConfirmDialogWindow(_localizer.GetString("Prompt.Title"), question)
        {
            Owner = OwnerWindow.Find(),
        };
        return dialog.ShowDialog() == true;
    }
}
