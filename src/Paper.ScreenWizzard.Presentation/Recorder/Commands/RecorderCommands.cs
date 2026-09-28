using Paper.ScreenWizzard.Domain.Recorder;
using Paper.ScreenWizzard.Presentation.Mvvm;
using Paper.ScreenWizzard.Presentation.Recorder.ViewModels;

namespace Paper.ScreenWizzard.Presentation.Recorder.Commands;

/// <summary>A button of "what to record"; CommandParameter is the <see cref="RecordTargetKind"/>.</summary>
public sealed class SelectRecordTargetCommand : CommandBase
{
    private readonly RecordingBarViewModel _owner;

    public SelectRecordTargetCommand(RecordingBarViewModel owner)
    {
        _owner = owner;
    }

    public override bool CanExecute(object? parameter) => parameter is RecordTargetKind;

    public override void Execute(object? parameter)
    {
        if (parameter is RecordTargetKind kind)
        {
            _owner.Target = kind;
        }
    }
}

/// <summary>Record: hands the bar's choices to the recording flow.</summary>
public sealed class StartRecordingCommand : CommandBase
{
    private readonly RecordingBarViewModel _owner;

    public StartRecordingCommand(RecordingBarViewModel owner)
    {
        _owner = owner;
    }

    public override void Execute(object? parameter) => _owner.RaiseRecordRequested();
}

public sealed class CloseRecordingBarCommand : CommandBase
{
    private readonly RecordingBarViewModel _owner;

    public CloseRecordingBarCommand(RecordingBarViewModel owner)
    {
        _owner = owner;
    }

    public override void Execute(object? parameter) => _owner.RaiseCloseRequested();
}

public sealed class OpenRecordedVideoCommand : CommandBase
{
    private readonly RecordedViewModel _owner;

    public OpenRecordedVideoCommand(RecordedViewModel owner)
    {
        _owner = owner;
    }

    public override void Execute(object? parameter) => _owner.RaiseOpenVideo();
}

public sealed class ShowRecordedInFolderCommand : CommandBase
{
    private readonly RecordedViewModel _owner;

    public ShowRecordedInFolderCommand(RecordedViewModel owner)
    {
        _owner = owner;
    }

    public override void Execute(object? parameter) => _owner.RaiseShowInFolder();
}

public sealed class CloseRecordedCommand : CommandBase
{
    private readonly RecordedViewModel _owner;

    public CloseRecordedCommand(RecordedViewModel owner)
    {
        _owner = owner;
    }

    public override void Execute(object? parameter) => _owner.RaiseClose();
}
