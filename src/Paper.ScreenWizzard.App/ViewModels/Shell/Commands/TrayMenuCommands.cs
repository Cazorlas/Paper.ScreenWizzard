using Paper.ScreenWizzard.App.Mvvm;
using Paper.ScreenWizzard.Domain.Capture;

namespace Paper.ScreenWizzard.App.ViewModels.Shell.Commands;

/// <summary>A capture line of the tray menu.</summary>
public sealed class TrayCaptureCommand : CommandBase
{
    private readonly TrayMenuViewModel _owner;
    private readonly CaptureKind _kind;

    public TrayCaptureCommand(TrayMenuViewModel owner, CaptureKind kind)
    {
        _owner = owner;
        _kind = kind;
    }

    public override void Execute(object? parameter) => _owner.RaiseCaptureRequested(_kind);
}

/// <summary>"Mở ảnh…".</summary>
public sealed class TrayOpenImageCommand : CommandBase
{
    private readonly TrayMenuViewModel _owner;

    public TrayOpenImageCommand(TrayMenuViewModel owner)
    {
        _owner = owner;
    }

    public override void Execute(object? parameter) => _owner.RaiseOpenImageRequested();
}

/// <summary>"Thanh chụp": asks the owner to show or hide the bar.</summary>
public sealed class TrayToggleCaptureBarCommand : CommandBase
{
    private readonly TrayMenuViewModel _owner;

    public TrayToggleCaptureBarCommand(TrayMenuViewModel owner)
    {
        _owner = owner;
    }

    public override void Execute(object? parameter) => _owner.RaiseToggleCaptureBarRequested();
}

/// <summary>"Cài đặt".</summary>
public sealed class TraySettingsCommand : CommandBase
{
    private readonly TrayMenuViewModel _owner;

    public TraySettingsCommand(TrayMenuViewModel owner)
    {
        _owner = owner;
    }

    public override void Execute(object? parameter) => _owner.RaiseSettingsRequested();
}

/// <summary>"Thoát": the only way to end the app (SPEC shell).</summary>
public sealed class TrayExitCommand : CommandBase
{
    private readonly TrayMenuViewModel _owner;

    public TrayExitCommand(TrayMenuViewModel owner)
    {
        _owner = owner;
    }

    public override void Execute(object? parameter) => _owner.RaiseExitRequested();
}
