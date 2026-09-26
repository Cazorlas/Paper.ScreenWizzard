using Paper.ScreenWizzard.Presentation.Editor.ViewModels;
using Paper.ScreenWizzard.Presentation.Shared.ViewModels;
using Paper.ScreenWizzard.Presentation.Shared.Views;
using Paper.ScreenWizzard.UseCases.Shared.Models;

namespace Paper.ScreenWizzard.Presentation.Editor.Views;

/// <summary>The editor's two questions as real modal windows over the editor that asked, in the language in use.</summary>
public sealed class WpfEditorPrompts : IEditorPrompts
{
    private readonly ILocalizer _localizer;

    public WpfEditorPrompts(ILocalizer localizer)
    {
        _localizer = localizer;
    }

    public OverwriteChoice AskOverwriteOrCopy(string path)
    {
        var choice = Ask(
            "Editor.Prompt.OverwriteTitle",
            _localizer.Format(NotificationMessage.Of("Editor.Prompt.OverwriteQuestion", path)),
            "Editor.Prompt.Overwrite",
            "Editor.Prompt.SaveCopy",
            "Editor.Prompt.Cancel");
        return choice switch
        {
            0 => OverwriteChoice.Overwrite,
            1 => OverwriteChoice.SaveCopy,
            _ => OverwriteChoice.Cancel,
        };
    }

    public CloseChoice AskSaveDiscardCancel()
    {
        var choice = Ask(
            "Editor.Prompt.CloseTitle",
            _localizer.GetString("Editor.Prompt.CloseQuestion"),
            "Editor.Prompt.Save",
            "Editor.Prompt.Discard",
            "Editor.Prompt.Back");
        return choice switch
        {
            0 => CloseChoice.Save,
            1 => CloseChoice.Discard,
            _ => CloseChoice.Cancel,
        };
    }

    private int Ask(string titleKey, string question, string primaryKey, string secondaryKey, string cancelKey)
    {
        var dialog = new EditorPromptWindow(
            _localizer.GetString(titleKey),
            question,
            _localizer.GetString(primaryKey),
            _localizer.GetString(secondaryKey),
            _localizer.GetString(cancelKey))
        {
            Owner = OwnerWindow.Find(),
        };
        dialog.ShowDialog();
        return dialog.Choice;
    }
}
