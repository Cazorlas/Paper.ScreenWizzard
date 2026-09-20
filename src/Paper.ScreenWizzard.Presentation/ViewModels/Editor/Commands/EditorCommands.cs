using Paper.ScreenWizzard.Domain.Editor;
using Paper.ScreenWizzard.Presentation.Mvvm;

namespace Paper.ScreenWizzard.Presentation.ViewModels.Editor.Commands;

// One named class per button or shortcut of the editor (skill paper-wpf-style: a command is a class, so the workflow of a button has a
// name). Each holds its window's view model and asks it; the view model keeps the state, the flow of a save or a close is in its methods.

/// <summary>A tool button: the parameter is the <see cref="ToolKind"/> to choose.</summary>
public sealed class SelectToolCommand : CommandBase
{
    private readonly EditorViewModel _owner;

    public SelectToolCommand(EditorViewModel owner)
    {
        _owner = owner;
    }

    public override void Execute(object? parameter)
    {
        if (parameter is ToolKind tool)
        {
            _owner.SelectTool(tool);
        }
    }
}

/// <summary>A palette swatch: the parameter is the swatch. It recolours the selected shape, or sets the colour of the next ones.</summary>
public sealed class SetColorCommand : CommandBase
{
    private readonly EditorViewModel _owner;

    public SetColorCommand(EditorViewModel owner)
    {
        _owner = owner;
    }

    public override void Execute(object? parameter)
    {
        if (parameter is PaletteColorViewModel swatch)
        {
            _owner.SetColor(swatch.Color);
        }
    }
}

/// <summary>The "+" beside the hex box (and Enter in it): reads the typed code.</summary>
public sealed class ApplyCustomColorCommand : CommandBase
{
    private readonly EditorViewModel _owner;

    public ApplyCustomColorCommand(EditorViewModel owner)
    {
        _owner = owner;
    }

    public override void Execute(object? parameter) => _owner.ApplyCustomColor();
}

/// <summary>Ctrl+Z and the ↶ button. Disabled, so the button greys out and the shortcut does nothing, when there is no step to undo (SPEC editor F6).</summary>
public sealed class UndoCommand : CommandBase
{
    private readonly EditorViewModel _owner;

    public UndoCommand(EditorViewModel owner)
    {
        _owner = owner;
    }

    public override bool CanExecute(object? parameter) => _owner.Session.CanUndo;

    public override void Execute(object? parameter)
    {
        if (CanExecute(parameter))
        {
            _owner.Undo();
        }
    }
}

/// <summary>Ctrl+Y and the ↷ button; disabled when there is nothing to redo (SPEC editor F6).</summary>
public sealed class RedoCommand : CommandBase
{
    private readonly EditorViewModel _owner;

    public RedoCommand(EditorViewModel owner)
    {
        _owner = owner;
    }

    public override bool CanExecute(object? parameter) => _owner.Session.CanRedo;

    public override void Execute(object? parameter)
    {
        if (CanExecute(parameter))
        {
            _owner.Redo();
        }
    }
}

/// <summary>The Delete key: removes the selected shape. Disabled with nothing selected, and while text is typed (Delete belongs to the text box then).</summary>
public sealed class DeleteSelectionCommand : CommandBase
{
    private readonly EditorViewModel _owner;

    public DeleteSelectionCommand(EditorViewModel owner)
    {
        _owner = owner;
    }

    public override bool CanExecute(object? parameter) => _owner.Session.SelectedId is not null && !_owner.IsEditingText;

    public override void Execute(object? parameter)
    {
        if (CanExecute(parameter))
        {
            _owner.DeleteSelection();
        }
    }
}

/// <summary>Enter: applies the crop region (SPEC editor, "Cắt"). Does nothing when no region is chosen.</summary>
public sealed class ConfirmCommand : CommandBase
{
    private readonly EditorViewModel _owner;

    public ConfirmCommand(EditorViewModel owner)
    {
        _owner = owner;
    }

    public override void Execute(object? parameter) => _owner.Confirm();
}

/// <summary>Esc: finishes the text being typed, else drops the crop region, else drops the selection.</summary>
public sealed class CancelCommand : CommandBase
{
    private readonly EditorViewModel _owner;

    public CancelCommand(EditorViewModel owner)
    {
        _owner = owner;
    }

    public override void Execute(object? parameter) => _owner.Cancel();
}

/// <summary>Ctrl+Enter and Esc in the text box, or a click elsewhere: commits the text (an empty one commits nothing, SPEC editor F4).</summary>
public sealed class CommitTextCommand : CommandBase
{
    private readonly EditorViewModel _owner;

    public CommitTextCommand(EditorViewModel owner)
    {
        _owner = owner;
    }

    public override void Execute(object? parameter) => _owner.CommitText();
}

/// <summary>"Vừa khung": fits the image to the window again.</summary>
public sealed class ZoomFitCommand : CommandBase
{
    private readonly EditorViewModel _owner;

    public ZoomFitCommand(EditorViewModel owner)
    {
        _owner = owner;
    }

    public override void Execute(object? parameter) => _owner.FitToViewport();
}

/// <summary>"100%": one image pixel per screen pixel.</summary>
public sealed class ZoomActualSizeCommand : CommandBase
{
    private readonly EditorViewModel _owner;

    public ZoomActualSizeCommand(EditorViewModel owner)
    {
        _owner = owner;
    }

    public override void Execute(object? parameter) => _owner.SetZoom(1.0);
}

/// <summary>Ctrl+S and the Lưu button: the use case decides whether to ask, overwrite or refuse; this runs what it decided.</summary>
public sealed class SaveEditedImageCommand : CommandBase
{
    private readonly EditorViewModel _owner;

    public SaveEditedImageCommand(EditorViewModel owner)
    {
        _owner = owner;
    }

    public override void Execute(object? parameter) => _owner.Save();
}

/// <summary>The Lưu thành… button: always the "Save as" box.</summary>
public sealed class SaveEditedImageAsCommand : CommandBase
{
    private readonly EditorViewModel _owner;

    public SaveEditedImageAsCommand(EditorViewModel owner)
    {
        _owner = owner;
    }

    public override void Execute(object? parameter) => _owner.SaveAs();
}

/// <summary>Ctrl+C and the Sao chép button: the flattened image, at its own size, onto the clipboard.</summary>
public sealed class CopyEditedImageCommand : CommandBase
{
    private readonly EditorViewModel _owner;

    public CopyEditedImageCommand(EditorViewModel owner)
    {
        _owner = owner;
    }

    public override void Execute(object? parameter) => _owner.Copy();
}

/// <summary>Ctrl+V: opens the clipboard's image in a new editor window (SPEC editor F3 when there is none).</summary>
public sealed class PasteImageCommand : CommandBase
{
    private readonly EditorViewModel _owner;

    public PasteImageCommand(EditorViewModel owner)
    {
        _owner = owner;
    }

    public override void Execute(object? parameter) => _owner.Paste();
}

/// <summary>A file dropped on the window: the parameter is its path. Opens it in a new editor window (SPEC editor F2 when it is not an image).</summary>
public sealed class OpenFileCommand : CommandBase
{
    private readonly EditorViewModel _owner;

    public OpenFileCommand(EditorViewModel owner)
    {
        _owner = owner;
    }

    public override void Execute(object? parameter)
    {
        if (parameter is string path && path.Length > 0)
        {
            _owner.OpenFile(path);
        }
    }
}
