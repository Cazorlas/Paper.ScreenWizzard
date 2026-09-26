using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.UseCases.Editor.Ports;
using Paper.ScreenWizzard.UseCases.Shared.Models;

namespace Paper.ScreenWizzard.UseCases.Editor.Models;

/// <summary>Why an image could not be opened (SPEC editor F2, F3, F8).</summary>
public enum EditorOpenIssue
{
    None,
    NotAnImage,
    TooLarge,
    ClipboardHasNoImage,
    ReadFailed,
}

public sealed record EditorOpenResult(IEditorSession? Session, EditorOpenIssue Issue, NotificationMessage? Message);

/// <summary>Where a dragged shape ends after the Shift constraint (SPEC editor, "Đường thẳng, mũi tên, khung, elip").</summary>
public sealed record DragShape(PixelPoint From, PixelPoint To);

/// <summary>What Ctrl+S should do (SPEC editor, "Lưu và chép").</summary>
public enum SaveAction
{
    /// <summary>The image has no file yet: open "Save as" with the suggestion.</summary>
    AskSaveAs,

    /// <summary>The image came from a file that has not been overwritten yet: ask "overwrite or save a copy".</summary>
    AskOverwriteOrCopy,

    /// <summary>The user already agreed to overwrite: write straight to the path.</summary>
    WriteDirect,

    /// <summary>F9: the file changed on disk since it was opened; offer "Save as" instead of writing over it.</summary>
    FileChangedOnDisk,
}

public sealed record SaveDecision(SaveAction Action, string Folder, string FileName, string? Path);

/// <summary>A save or copy: whether it worked and what to tell the user (SPEC editor F1).</summary>
public sealed record EditorSaveResult(bool Saved, string? Path, NotificationMessage? Message);

/// <summary>What closing the window should do (SPEC editor F7).</summary>
public enum CloseAction
{
    Close,
    AskSaveDiscardCancel,
}

/// <summary>A crop's outcome: the new size, or why nothing changed (SPEC editor F5).</summary>
public sealed record CropOutcome(bool Applied, PixelRect Area, NotificationMessage? Message);
