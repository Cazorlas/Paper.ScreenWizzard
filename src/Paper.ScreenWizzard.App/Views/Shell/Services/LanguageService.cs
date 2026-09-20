using System.Globalization;
using System.Windows;
using Paper.ScreenWizzard.App.ViewModels.Shell;
using Paper.ScreenWizzard.UseCases.Common.Models;
using Paper.ScreenWizzard.UseCases.Shell.Models;

namespace Paper.ScreenWizzard.App.Views.Shell.Services;

/// <summary>
/// The language of every window. The text lives in <c>Resources/Strings.vi.xaml</c> and <c>Strings.en.xaml</c>, one shared key
/// set; the views bind with {DynamicResource Key}. <see cref="Apply"/> swaps the dictionary in Application.Resources, and WPF
/// re-reads every DynamicResource, so open windows change at once with no restart (SPEC shell).
/// </summary>
public sealed class LanguageService : ILocalizer
{
    private const string AssemblyName = "Paper.ScreenWizzard";

    private ResourceDictionary? _current;

    public event EventHandler? LanguageChanged;

    public ResolvedLanguage Current { get; private set; } = ResolvedLanguage.English;

    /// <summary>Swaps the strings for <paramref name="language"/>; safe to call from any thread.</summary>
    public void Apply(ResolvedLanguage language)
    {
        OnUiThread(() =>
        {
            var code = language == ResolvedLanguage.Vietnamese ? "vi" : "en";
            var next = new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/{AssemblyName};component/Resources/Strings.{code}.xaml"),
            };

            // Add the new dictionary before removing the old one: the last merged dictionary wins a lookup, and the swap
            // never leaves a moment in which a key is missing and a label draws blank.
            var merged = Application.Current.Resources.MergedDictionaries;
            merged.Add(next);
            if (_current is not null)
            {
                merged.Remove(_current);
            }

            _current = next;
            Current = language;
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    public string GetString(string key) => OnUiThread(() => Application.Current.TryFindResource(key) as string ?? key);

    public string Format(NotificationMessage message)
    {
        var template = GetString(message.Key);
        if (template == message.Key)
        {
            // No text for this key: show the key itself so the gap is seen and fixed, never a blank message.
            return message.Key;
        }

        var arguments = new object[message.Arguments.Count];
        for (var i = 0; i < arguments.Length; i++)
        {
            arguments[i] = LocalizeArgument(message.Key, i, message.Arguments[i]);
        }

        try
        {
            return string.Format(CultureInfo.CurrentCulture, template, arguments);
        }
        catch (FormatException)
        {
            // A translation with a broken placeholder must still say something.
            return template;
        }
    }

    // Shell.HotkeyUsedByOtherKind carries the enum name of the kind ("Freeform"); the user reads the kind in the language in use.
    private string LocalizeArgument(string key, int index, string argument) =>
        key == "Shell.HotkeyUsedByOtherKind" && index == 0 ? GetString("Capture.Kind." + argument) : argument;

    private static void OnUiThread(Action action)
    {
        var dispatcher = Application.Current.Dispatcher;
        if (dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.Invoke(action);
        }
    }

    private static T OnUiThread<T>(Func<T> function)
    {
        var dispatcher = Application.Current.Dispatcher;
        return dispatcher.CheckAccess() ? function() : dispatcher.Invoke(function);
    }
}
