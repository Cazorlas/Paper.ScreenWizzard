using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.UseCases.Editor.Models;
using Paper.ScreenWizzard.UseCases.Shared.Models;

namespace Paper.ScreenWizzard.Presentation.Editor.ViewModels;

/// <summary>
/// Opens editor windows (SPEC editor: mỗi cửa sổ sửa giữ một ảnh, mở ảnh khác thì mở cửa sổ mới): from an image just captured, from a file,
/// from the clipboard. It decides nothing about images: the use case opens the session or refuses, and this shows the refusal (a file that
/// is not an image, one too large, an empty clipboard) and opens a window only when there is something to edit, so no blank window ever
/// appears (SPEC editor F2, F3, F8). Use it on the UI thread.
/// </summary>
public sealed class EditorFlow
{
    private readonly EditorServices _services;
    private readonly IEditorViews _views;
    private readonly List<EditorViewModel> _open = [];

    public EditorFlow(EditorServices services, IEditorViews views)
    {
        _services = services;
        _views = views;
    }

    /// <summary>How many editor windows are open now.</summary>
    public int OpenCount => _open.Count;

    /// <summary>Opens an image that was just captured (or any image with a known origin, null for none) in a new window.</summary>
    public EditorViewModel? Open(PixelImage image, string? sourcePath) => OpenSession(_services.Interactor.Open(image, sourcePath));

    /// <summary>Opens a file in a new window; null, with the reason shown, when it cannot be opened.</summary>
    public EditorViewModel? OpenFile(string path) => Finish(_services.Interactor.OpenFile(path), path);

    /// <summary>Opens the clipboard's image in a new window; null, with the reason shown, when there is none.</summary>
    public EditorViewModel? OpenFromClipboard() => Finish(_services.Interactor.OpenFromClipboard(), null);

    private EditorViewModel? Finish(EditorOpenResult result, string? path)
    {
        if (result.Session is not null)
        {
            return OpenSession(result.Session);
        }

        // The use case normally names its message; when it does not, the issue still has words, so a refusal is never a silent nothing.
        var message = result.Message ?? MessageFor(result.Issue, path);
        if (result.Issue == EditorOpenIssue.ClipboardHasNoImage)
        {
            // Nothing is wrong with the program, the clipboard just has no picture: a small message, not a box to close.
            _services.Notifications.ShowToast(message);
        }
        else
        {
            _services.Notifications.ShowError(message);
        }

        return null;
    }

    private EditorViewModel OpenSession(Paper.ScreenWizzard.UseCases.Editor.Ports.IEditorSession session)
    {
        var viewModel = new EditorViewModel(session, _services, this);
        _open.Add(viewModel);
        var handle = _views.OpenEditor(viewModel);
        handle.Closed += (_, _) =>
        {
            _open.Remove(viewModel);
            viewModel.Dispose();
        };
        return viewModel;
    }

    private static NotificationMessage MessageFor(EditorOpenIssue issue, string? path) => issue switch
    {
        EditorOpenIssue.NotAnImage => NotificationMessage.Of("Editor.NotAnImage", path ?? string.Empty),
        EditorOpenIssue.TooLarge => NotificationMessage.Of("Editor.ImageTooLarge", path ?? string.Empty),
        EditorOpenIssue.ClipboardHasNoImage => NotificationMessage.Of("Editor.ClipboardHasNoImage"),
        _ => NotificationMessage.Of("Editor.ReadFailed", path ?? string.Empty, "unknown reason"),
    };
}
