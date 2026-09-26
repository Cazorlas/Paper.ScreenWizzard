using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UseCases.Shared.Models;

namespace Paper.ScreenWizzard.Presentation.Shell.ViewModels;

// The small services the shell view models lean on. They are interfaces because a test replaces the folder picker and the
// prompt with a fake, and because the view model must not know how the language is swapped (a WPF resource dictionary).

/// <summary>Applies what the user picked in Settings to every window at once (SPEC shell: "chữ đổi ngay").</summary>
public interface IAppearanceService
{
    void ApplyLanguage(ResolvedLanguage language);

    void ApplyTheme(AppTheme theme);
}

/// <summary>The folder browser of the Settings window, injectable so a test does not open a real dialog.</summary>
public interface IFolderPickerService
{
    /// <summary>The chosen folder, or null when the user cancelled.</summary>
    string? PickFolder(string? initialFolder);
}

/// <summary>The questions the Settings window asks the user before it saves.</summary>
public interface ISettingsPrompts
{
    /// <summary>Asks whether to create <paramref name="folder"/> (SPEC shell F5); true for Yes, false for No.</summary>
    bool AskCreateFolder(string folder);
}
