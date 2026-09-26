using Paper.ScreenWizzard.UseCases.Shared.Models;

namespace Paper.ScreenWizzard.Presentation.Shared.ViewModels;

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
