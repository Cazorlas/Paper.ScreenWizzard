using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UseCases.Common.Models;
using Paper.ScreenWizzard.UseCases.Shell.Models;

namespace Paper.ScreenWizzard.App.ViewModels.Shell;

// The small services the shell view models lean on. They are interfaces because a test replaces the folder picker and the
// prompt with a fake, and because the view model must not know how the language is swapped (a WPF resource dictionary).

/// <summary>Turns resource keys and use-case messages into text of the language in use.</summary>
public interface ILocalizer
{
    /// <summary>The text of <paramref name="key"/>; the key itself when the language has no such key, never blank.</summary>
    string GetString(string key);

    /// <summary>The user's text for a use-case message: the key's text with its arguments put into the {0} placeholders.</summary>
    string Format(NotificationMessage message);

    /// <summary>Raised after the language was swapped, so text a view model computed earlier can be read again.</summary>
    event EventHandler? LanguageChanged;
}

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
