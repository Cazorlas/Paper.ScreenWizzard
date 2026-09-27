using System.Globalization;
using System.IO;
using Paper.ScreenWizzard.Presentation.Mvvm;
using Paper.ScreenWizzard.Presentation.Recorder.Commands;
using Paper.ScreenWizzard.Presentation.Shared.ViewModels;
using Paper.ScreenWizzard.UseCases.Recorder.Models;
using Paper.ScreenWizzard.UseCases.Shared.Models;

namespace Paper.ScreenWizzard.Presentation.Recorder.ViewModels;

/// <summary>
/// The "Recorded" window (SPEC recorder, "What the user does" 5): the saved file, its size and length, and Open video, Show in folder,
/// Close. The file is already saved, so there is nothing to throw away. A recording that stopped by itself says why (F1, F2).
/// </summary>
public sealed class RecordedViewModel : BindableBase
{
    private readonly ILocalizer _localizer;
    private readonly RecordingResult _result;
    private NotificationMessage? _problem;

    public RecordedViewModel(RecordingResult result, ILocalizer localizer)
    {
        _result = result;
        _localizer = localizer;
        FilePath = result.FilePath ?? string.Empty;
        FileName = Path.GetFileName(FilePath);
        var duration = RecordingTimeText.Format(result.Duration);
        var megabytes = (result.Bytes / 1024d / 1024d).ToString("0.#", CultureInfo.CurrentCulture);
        Details = string.Create(CultureInfo.InvariantCulture, $"{result.Size.Width} × {result.Size.Height} · {duration} · {megabytes} MB");
        OpenVideoCommand = new OpenRecordedVideoCommand(this);
        ShowInFolderCommand = new ShowRecordedInFolderCommand(this);
        CloseCommand = new CloseRecordedCommand(this);
    }

    public event EventHandler? OpenVideoRequested;

    public event EventHandler? ShowInFolderRequested;

    public event EventHandler? CloseRequested;

    public string FilePath { get; }

    public string FileName { get; }

    public string Details { get; }

    /// <summary>Why the recording stopped by itself (F1, F2); empty for a recording the user stopped.</summary>
    public string Note => _result.Message is null ? string.Empty : _localizer.Format(_result.Message);

    public bool HasNote => _result.Message is not null;

    /// <summary>The video or the folder did not open: said here, with the path.</summary>
    public string ProblemText => _problem is null ? string.Empty : _localizer.Format(_problem);

    public bool HasProblem => _problem is not null;

    public OpenRecordedVideoCommand OpenVideoCommand { get; }

    public ShowRecordedInFolderCommand ShowInFolderCommand { get; }

    public CloseRecordedCommand CloseCommand { get; }

    public void ShowProblem(NotificationMessage? problem)
    {
        _problem = problem;
        RaisePropertyChanged(nameof(ProblemText));
        RaisePropertyChanged(nameof(HasProblem));
    }

    internal void RaiseOpenVideo() => OpenVideoRequested?.Invoke(this, EventArgs.Empty);

    internal void RaiseShowInFolder() => ShowInFolderRequested?.Invoke(this, EventArgs.Empty);

    internal void RaiseClose() => CloseRequested?.Invoke(this, EventArgs.Empty);
}
