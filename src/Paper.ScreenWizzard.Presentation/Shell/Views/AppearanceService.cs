using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.Presentation.Shell.ViewModels;
using Paper.ScreenWizzard.UseCases.Shell.Models;

namespace Paper.ScreenWizzard.Presentation.Shell.Views;

/// <summary>What Save in Settings calls: the language and the theme swapped on every open window at once.</summary>
public sealed class AppearanceService : IAppearanceService
{
    private readonly LanguageService _language;
    private readonly ThemeService _theme;

    public AppearanceService(LanguageService language, ThemeService theme)
    {
        _language = language;
        _theme = theme;
    }

    public void ApplyLanguage(ResolvedLanguage language) => _language.Apply(language);

    public void ApplyTheme(AppTheme theme) => _theme.Apply(theme);
}
