using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.Presentation.ViewModels.Capture;
using Paper.ScreenWizzard.Presentation.ViewModels.Shell;
using Paper.ScreenWizzard.UseCases.Common.Ports;
using Paper.ScreenWizzard.UseCases.Editor.Ports;

namespace Paper.ScreenWizzard.Presentation.ViewModels.Editor;

// The small services the editor view models lean on. They are interfaces because a test replaces the prompts and the flattener
// with fakes, and because a view model must not open a WPF window by itself.

/// <summary>What the user chose when asked about overwriting the file the image came from (SPEC editor, "Lưu và chép").</summary>
public enum OverwriteChoice
{
    Overwrite,
    SaveCopy,
    Cancel,
}

/// <summary>What the user chose when closing a window that still has unsaved edits (SPEC editor F7).</summary>
public enum CloseChoice
{
    Save,
    Discard,
    Cancel,
}

/// <summary>The two questions the editor asks. Injectable so a test answers them and a real window shows them.</summary>
public interface IEditorPrompts
{
    /// <summary>"Overwrite the original file or save a new one?" once per image; <paramref name="path"/> is the original.</summary>
    OverwriteChoice AskOverwriteOrCopy(string path);

    /// <summary>"Save, discard or go back?" for a window closed with unsaved edits.</summary>
    CloseChoice AskSaveDiscardCancel();
}

/// <summary>Turns the session into the one bitmap that is saved or copied: the image with every drawing on it, at its own size.</summary>
public interface IEditorFlattener
{
    /// <summary>Same size as the session's image, whatever zoom the window shows; never a picture of the window.</summary>
    PixelImage Flatten(IEditorSession session);
}

/// <summary>Opens the window of an editor, so <see cref="EditorFlow"/> is tested with fakes and the real window on its own.</summary>
public interface IEditorViews
{
    IViewHandle OpenEditor(EditorViewModel viewModel);
}

/// <summary>Everything an editor view model needs from outside, in one place so the flow can hand it on to each new window.</summary>
public sealed record EditorServices(
    IEditorInteractor Interactor,
    INotificationPort Notifications,
    IFileDialogService FileDialogs,
    IEditorPrompts Prompts,
    IEditorFlattener Flattener,
    ILocalizer Localizer,
    Func<AppSettings> Settings);
