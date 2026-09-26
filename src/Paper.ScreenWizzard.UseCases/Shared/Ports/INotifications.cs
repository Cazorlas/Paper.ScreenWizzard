using Paper.ScreenWizzard.UseCases.Shared.Models;

namespace Paper.ScreenWizzard.UseCases.Shared.Ports;

/// <summary>Tells the user something: a small message that fades, or an error they must see.</summary>
public interface INotifications
{
    void ShowToast(NotificationMessage message);

    void ShowError(NotificationMessage message);
}
