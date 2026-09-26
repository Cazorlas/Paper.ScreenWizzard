namespace Paper.ScreenWizzard.Presentation.Shared.ViewModels;

/// <summary>The "Save as" box of the "Đã chụp" dialog, injectable so a test never opens a real one.</summary>
public interface IFileDialogService
{
    /// <summary>The path the user chose, or null when the user cancelled the box.</summary>
    string? PickSavePath(string initialFolder, string suggestedFileName);
}

/// <summary>A window the flow opened and may close again.</summary>
public interface IViewHandle
{
    /// <summary>Raised once the window is gone, whoever closed it (the flow, a button, the title bar's ✕).</summary>
    event EventHandler? Closed;

    /// <summary>Closes the window; nothing happens when it is already closed.</summary>
    void Close();
}
