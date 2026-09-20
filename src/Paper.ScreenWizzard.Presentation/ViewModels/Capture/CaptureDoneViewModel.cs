using System.Globalization;
using Paper.ScreenWizzard.Presentation.Mvvm;
using Paper.ScreenWizzard.Presentation.ViewModels.Capture.Commands;
using Paper.ScreenWizzard.Presentation.ViewModels.Shell;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UseCases.Capture.Models;
using Paper.ScreenWizzard.UseCases.Capture.Ports;
using Paper.ScreenWizzard.UseCases.Common.Models;
using Paper.ScreenWizzard.UseCases.Common.Ports;

namespace Paper.ScreenWizzard.Presentation.ViewModels.Capture;

/// <summary>
/// The "Đã chụp" dialog (SPEC capture, "Sau khi chụp"): one dialog holds one image, so several can be open at once. The buttons
/// ask the use case to deliver and show what came back. The dialog never loses the image on its own: a delivery that fails keeps
/// the dialog open with the reason inside it (F4, F5), and only Bỏ, Esc, the ✕ or a delivery that worked close it. Bỏ delivers
/// nothing and says nothing (F10).
/// </summary>
public sealed class CaptureDoneViewModel : BindableBase, IDisposable
{
    private readonly INotificationPort _notifications;
    private readonly ILocalizer _localizer;
    private NotificationMessage? _error;

    public CaptureDoneViewModel(
        ICaptureInteractor interactor,
        PixelImage image,
        AppSettings settings,
        IFileDialogService fileDialogs,
        INotificationPort notifications,
        ILocalizer localizer)
    {
        Image = image;
        _notifications = notifications;
        _localizer = localizer;
        SizeText = string.Create(CultureInfo.InvariantCulture, $"{image.Width} × {image.Height}");
        FormatText = settings.Format.ToString().ToUpperInvariant();
        SaveCommand = new SaveImageCommand(this, interactor, settings);
        SaveAsCommand = new SaveImageAsCommand(this, interactor, settings, fileDialogs);
        CopyCommand = new CopyImageCommand(this, interactor, settings);
        EditCommand = new EditImageCommand(this);
        DiscardCommand = new DiscardImageCommand(this);
        _localizer.LanguageChanged += OnLanguageChanged;
    }

    /// <summary>The dialog should go away; the window closes itself on it, the view model never holds the window.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>The user pressed Sửa: the editor takes this image (the editor arrives in plan group 4).</summary>
    public event Action<PixelImage>? EditRequested;

    /// <summary>The image this dialog holds. It is never replaced, so a failed delivery can always be tried again.</summary>
    public PixelImage Image { get; }

    /// <summary>"width × height".</summary>
    public string SizeText { get; }

    /// <summary>"PNG" or "JPG": what Lưu would write.</summary>
    public string FormatText { get; }

    /// <summary>The reason a delivery failed, inside the dialog, in the language in use; null when there is none (F4, F5).</summary>
    public string? ErrorText => _error is null ? null : _localizer.Format(_error);

    public bool HasError => _error is not null;

    public SaveImageCommand SaveCommand { get; }

    public SaveImageAsCommand SaveAsCommand { get; }

    public CopyImageCommand CopyCommand { get; }

    public EditImageCommand EditCommand { get; }

    public DiscardImageCommand DiscardCommand { get; }

    public void Dispose() => _localizer.LanguageChanged -= OnLanguageChanged;

    /// <summary>Shows what a delivery answered: leave on success, stay with the reason on failure.</summary>
    internal void ApplyResult(CaptureDeliveryResult result)
    {
        if (result.Delivered)
        {
            if (result.Message is not null)
            {
                _notifications.ShowToast(result.Message);
            }

            if (!result.KeepDialogOpen)
            {
                RequestClose();
            }

            return;
        }

        // Anything that did not deliver keeps the dialog, even when the use case did not ask for it: the image is only here,
        // and losing it because of a failure would be silent (SPEC F4, F5).
        ShowError(CaptureMessages.For(CaptureIssue.Failed, result.Message));
    }

    internal void ShowError(NotificationMessage? message)
    {
        _error = message;
        RaisePropertyChanged(nameof(ErrorText));
        RaisePropertyChanged(nameof(HasError));
    }

    internal void RaiseEditRequested() => EditRequested?.Invoke(Image);

    internal void RequestClose() => CloseRequested?.Invoke(this, EventArgs.Empty);

    private void OnLanguageChanged(object? sender, EventArgs e) => RaisePropertyChanged(nameof(ErrorText));
}
