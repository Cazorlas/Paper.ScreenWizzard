using System.IO;
using FlaUI.Core.Tools;
using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UiTests.Support;
using Paper.ScreenWizzard.UseCases.Shared.Models;
using Paper.ScreenWizzard.UseCases.Shell.Models;

namespace Paper.ScreenWizzard.UiTests.Shell;

/// <summary>
/// The toast and the error dialog of the notification port (plan T5), and the text the logic lane's message catalogue
/// becomes in both languages: every key it produces must read as a sentence, not as a key.
/// </summary>
[TestFixture]
public sealed class NotificationTests : UiTestBase
{
    /// <summary>The catalogue of SPEC shell F1 to F7 as the logic lane emits it, with the arguments it puts in.</summary>
    private static readonly NotificationMessage[] _catalogue =
    [
        NotificationMessage.Of("Shell.SettingsCorrupt"),
        NotificationMessage.Of("Shell.SettingsUnreadable", @"C:\Data\settings.json: used by another process"),
        NotificationMessage.Of("Shell.HotkeyUnsafeAtStart", "A"),
        NotificationMessage.Of("Shell.SettingsNotSaved", "the file is read-only"),
        NotificationMessage.Of("Shell.AutostartFailed", "access denied"),
        NotificationMessage.Of("Shell.HotkeyNeedsModifier", "A"),
        NotificationMessage.Of("Shell.HotkeyUsedByOtherKind", "Freeform"),
        NotificationMessage.Of("Shell.HotkeyHeldByAnotherProgram", "Ctrl+Shift+P"),
        NotificationMessage.Of("Shell.HotkeyUnavailableAtStart", "PrintScreen, F9"),
        NotificationMessage.Of("Shell.FolderMissing", @"D:\Shots"),
    ];

    [TestCase(ResolvedLanguage.Vietnamese)]
    [TestCase(ResolvedLanguage.English)]
    public void Catalogue_EveryMessageOfTheShell_ReadsAsASentenceWithItsArguments(ResolvedLanguage language)
    {
        var host = WpfHost.Instance;
        host.Invoke(() => host.Language.Apply(language));

        foreach (var message in _catalogue)
        {
            var text = host.Invoke(() => host.Language.Format(message));

            Assert.That(text, Is.Not.Empty.And.Not.EqualTo(message.Key), $"{message.Key} has no {language} text");
            Assert.That(text, Does.Not.Contain("{0}"), $"{message.Key} left its placeholder unfilled");
            if (message.Key != "Shell.HotkeyUsedByOtherKind")
            {
                foreach (var argument in message.Arguments)
                {
                    Assert.That(text, Does.Contain(argument), $"{message.Key} ({language}) does not show '{argument}'");
                }
            }
        }
    }

    [TestCase(ResolvedLanguage.Vietnamese, "Vùng tự do")]
    [TestCase(ResolvedLanguage.English, "Freeform")]
    public void Catalogue_HotkeyUsedByOtherKind_ShowsTheKindsNameInTheLanguageInUse(ResolvedLanguage language, string kindName)
    {
        var host = WpfHost.Instance;
        host.Invoke(() => host.Language.Apply(language));

        var text = host.Invoke(() => host.Language.Format(NotificationMessage.Of("Shell.HotkeyUsedByOtherKind", "Freeform")));

        Assert.That(text, Does.Contain(kindName));
    }

    [Test]
    public void TheErrorDialogHasNoTaskbarButtonBecauseTheAppLivesInTheTrayOnly()
    {
        // SPEC shell: the app never shows on the taskbar unless the editor or Settings is open; an error box is neither.
        var shown = WpfHost.Instance.Invoke(() => new Paper.ScreenWizzard.Presentation.Shell.Views.ErrorDialogWindow("title", "text").ShowInTaskbar);

        Assert.That(shown, Is.False);
    }

    [Test]
    public void Toast_ShowsTheMessageAndThenFadesAway()
    {
        var host = WpfHost.Instance;
        host.Invoke(() => host.Language.Apply(ResolvedLanguage.Vietnamese));
        var message = NotificationMessage.Of("Shell.FolderMissing", @"D:\Shots");

        host.Notifications.ShowToast(message);

        using var toast = host.Attach("ToastWindow");
        Assert.That(toast.TextOf("ToastText"), Is.EqualTo(host.Language.Format(message)));
        Assert.That(toast.TextOf("ToastText"), Does.Contain(@"D:\Shots").And.Not.StartWith("Shell."));
        toast.Screenshot("shell-toast-light");
        var gone = Retry.WhileTrue(() => toast.WindowExists("ToastWindow"), TimeSpan.FromSeconds(4), TimeSpan.FromMilliseconds(100)).Success;
        Assert.That(gone, Is.True, "a toast is a small message that fades; it must not stay on the screen");
    }

    [Test]
    public void Toast_InTheDarkTheme_IsPhotographedToo()
    {
        var host = WpfHost.Instance;
        host.Invoke(() =>
        {
            host.Language.Apply(ResolvedLanguage.English);
            host.Theme.Apply(AppTheme.Dark);
        });

        host.Notifications.ShowToast(NotificationMessage.Of("Shell.SettingsCorrupt"));

        using var toast = host.Attach("ToastWindow");
        var path = toast.Screenshot("shell-toast-dark");
        Assert.That(new FileInfo(path).Length, Is.GreaterThan(1_000));
        Assert.That(toast.TextOf("ToastText"), Does.Contain("corrupt").IgnoreCase.Or.Contain("damaged").IgnoreCase);
    }

    [TestCase(AppTheme.Light)]
    [TestCase(AppTheme.Dark)]
    public void Error_ShowsTheMessageInADialogThatStaysUntilTheUserClosesIt(AppTheme theme)
    {
        var host = WpfHost.Instance;
        host.Invoke(() =>
        {
            host.Language.Apply(ResolvedLanguage.Vietnamese);
            host.Theme.Apply(theme);
        });
        var message = NotificationMessage.Of("Shell.SettingsNotSaved", "the file is read-only");

        host.Notifications.ShowError(message);

        using var dialog = host.Attach("ErrorDialog");
        Assert.That(dialog.TextOf("ErrorText"), Is.EqualTo(host.Language.Format(message)));
        Assert.That(dialog.TextOf("ErrorText"), Does.Contain("the file is read-only"));
        dialog.Screenshot($"shell-error-{theme.ToString().ToLowerInvariant()}");
        Thread.Sleep(700);
        Assert.That(dialog.WindowExists("ErrorDialog"), Is.True, "an error stays until the user has read it");

        dialog.Click("ErrorOkButton");

        Assert.That(dialog.WaitUntilClosed(TimeSpan.FromSeconds(3)), Is.True, "OK closes the dialog");
    }
}
