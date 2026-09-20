using Paper.ScreenWizzard.Presentation.Mvvm;
using Paper.ScreenWizzard.Domain.Capture;

namespace Paper.ScreenWizzard.Presentation.ViewModels.Shell.Commands;

/// <summary>A capture button of the bar: tells the bar's owner which kind the user asked for.</summary>
public sealed class RequestCaptureCommand : CommandBase
{
    private readonly CaptureBarViewModel _owner;

    public RequestCaptureCommand(CaptureBarViewModel owner)
    {
        _owner = owner;
    }

    public override bool CanExecute(object? parameter) => parameter is CaptureKind;

    public override void Execute(object? parameter)
    {
        if (parameter is CaptureKind kind)
        {
            _owner.RaiseCaptureRequested(kind);
        }
    }
}

/// <summary>The gear button of the bar.</summary>
public sealed class RequestSettingsFromBarCommand : CommandBase
{
    private readonly CaptureBarViewModel _owner;

    public RequestSettingsFromBarCommand(CaptureBarViewModel owner)
    {
        _owner = owner;
    }

    public override void Execute(object? parameter) => _owner.RaiseSettingsRequested();
}

/// <summary>The X of the bar.</summary>
public sealed class RequestCloseBarCommand : CommandBase
{
    private readonly CaptureBarViewModel _owner;

    public RequestCloseBarCommand(CaptureBarViewModel owner)
    {
        _owner = owner;
    }

    public override void Execute(object? parameter) => _owner.RaiseCloseRequested();
}
