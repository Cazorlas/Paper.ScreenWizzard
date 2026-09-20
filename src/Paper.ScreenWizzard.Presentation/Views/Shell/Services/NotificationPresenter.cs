using System.Windows;
using Paper.ScreenWizzard.Presentation.ViewModels.Shell;
using Paper.ScreenWizzard.UseCases.Common.Models;
using Paper.ScreenWizzard.UseCases.Common.Ports;

namespace Paper.ScreenWizzard.Presentation.Views.Shell.Services;

/// <summary>
/// Shows what the use cases tell the user: a toast (small, fades, takes no focus) or an error dialog (stays until closed).
/// The text comes from <see cref="ILocalizer"/> in the language in use. May be called from any thread; the window is made on
/// the UI thread.
/// </summary>
public sealed class NotificationPresenter : INotificationPort
{
    private readonly ILocalizer _localizer;

    public NotificationPresenter(ILocalizer localizer)
    {
        _localizer = localizer;
    }

    /// <summary>How long a toast stays fully visible before it fades.</summary>
    public TimeSpan ToastLifetime { get; init; } = TimeSpan.FromSeconds(4);

    public void ShowToast(NotificationMessage message)
    {
        var text = _localizer.Format(message);
        OnUiThread(() => new ToastWindow(text, ToastLifetime).Show());
    }

    public void ShowError(NotificationMessage message)
    {
        var text = _localizer.Format(message);
        var title = _localizer.GetString("Notice.ErrorTitle");
        OnUiThread(() =>
        {
            var dialog = new ErrorDialogWindow(title, text);
            dialog.Show();
            dialog.Activate();
        });
    }

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
}
