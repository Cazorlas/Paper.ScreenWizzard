using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.Presentation.Capture.ViewModels;
using Paper.ScreenWizzard.Presentation.Mvvm;
using Paper.ScreenWizzard.UseCases.Capture.Ports;

namespace Paper.ScreenWizzard.Presentation.Capture.Commands;

/// <summary>Lưu: the use case writes the image to the save folder under an automatic name (SPEC capture, "Sau khi chụp").</summary>
public sealed class SaveImageCommand : CommandBase
{
    private readonly CaptureDoneViewModel _owner;
    private readonly ICaptureInteractor _interactor;
    private readonly AppSettings _settings;

    public SaveImageCommand(CaptureDoneViewModel owner, ICaptureInteractor interactor, AppSettings settings)
    {
        _owner = owner;
        _interactor = interactor;
        _settings = settings;
    }

    public override void Execute(object? parameter)
    {
        _owner.ShowError(null);
        _owner.ApplyResult(_interactor.Deliver(_owner.Image, CaptureDestination.File, _settings));
    }
}

/// <summary>Sao chép: the use case puts the image on the clipboard.</summary>
public sealed class CopyImageCommand : CommandBase
{
    private readonly CaptureDoneViewModel _owner;
    private readonly ICaptureInteractor _interactor;
    private readonly AppSettings _settings;

    public CopyImageCommand(CaptureDoneViewModel owner, ICaptureInteractor interactor, AppSettings settings)
    {
        _owner = owner;
        _interactor = interactor;
        _settings = settings;
    }

    public override void Execute(object? parameter)
    {
        _owner.ShowError(null);
        _owner.ApplyResult(_interactor.Deliver(_owner.Image, CaptureDestination.Clipboard, _settings));
    }
}

/// <summary>
/// Lưu thành…: the box starts at the folder and name the use case suggests; cancelling the box leaves the dialog exactly as it was
/// (SPEC: quay về hộp thoại "Đã chụp", ảnh vẫn còn).
/// </summary>
public sealed class SaveImageAsCommand : CommandBase
{
    private readonly CaptureDoneViewModel _owner;
    private readonly ICaptureInteractor _interactor;
    private readonly AppSettings _settings;
    private readonly IFileDialogService _fileDialogs;

    public SaveImageAsCommand(CaptureDoneViewModel owner, ICaptureInteractor interactor, AppSettings settings, IFileDialogService fileDialogs)
    {
        _owner = owner;
        _interactor = interactor;
        _settings = settings;
        _fileDialogs = fileDialogs;
    }

    public override void Execute(object? parameter)
    {
        var suggestion = _interactor.SuggestSaveAs(_settings);
        var path = _fileDialogs.PickSavePath(suggestion.Folder, suggestion.FileName);
        if (path is null)
        {
            return;
        }

        _owner.ShowError(null);
        _owner.ApplyResult(_interactor.DeliverToPath(_owner.Image, path, _settings));
    }
}

/// <summary>Sửa: hands the image to the editor and closes the dialog. Nothing is written or copied.</summary>
public sealed class EditImageCommand : CommandBase
{
    private readonly CaptureDoneViewModel _owner;

    public EditImageCommand(CaptureDoneViewModel owner)
    {
        _owner = owner;
    }

    public override void Execute(object? parameter)
    {
        _owner.RaiseEditRequested();
        _owner.RequestClose();
    }
}

/// <summary>Bỏ (also Esc and the ✕): closes the dialog and keeps nothing (SPEC capture F10). It says nothing either: the user chose it.</summary>
public sealed class DiscardImageCommand : CommandBase
{
    private readonly CaptureDoneViewModel _owner;

    public DiscardImageCommand(CaptureDoneViewModel owner)
    {
        _owner = owner;
    }

    public override void Execute(object? parameter) => _owner.RequestClose();
}
