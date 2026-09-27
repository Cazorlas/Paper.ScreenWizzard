using Paper.ScreenWizzard.Domain.Recorder;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.UseCases.Recorder.Models;
using Paper.ScreenWizzard.UseCases.Recorder.Ports;
using Paper.ScreenWizzard.UseCases.Shared.Models;
using Paper.ScreenWizzard.UseCases.Shared.Ports;

namespace Paper.ScreenWizzard.UseCases.Recorder.UseCases;

/// <summary>
/// Everything the recorder decides (SPEC recorder). One recording at a time. After the countdown a loop on its own thread pulls
/// pictures, places them on the video's clock (<see cref="RecordingTimeline"/>, <see cref="FrameClock"/>), mixes the sound that
/// arrived meanwhile (<see cref="AudioMixer"/>) and writes both; it is the only thread that writes. The sound sources and the user's
/// Pause and Stop reach it through the lock.
/// </summary>
public sealed class RecorderInteractor : IRecorderInteractor
{
    /// <summary>How often the recorded window is looked for (F1).</summary>
    private static readonly TimeSpan WindowCheckEvery = TimeSpan.FromMilliseconds(500);

    private readonly IScreenFrames _frames;
    private readonly ISoundSources _sounds;
    private readonly IVideoWriter _writer;
    private readonly IMonotonicClock _ticks;
    private readonly IWindowPresence _windows;
    private readonly IWindowCatalog _catalog;
    private readonly IFileLauncher _launcher;
    private readonly IDelay _delay;
    private readonly IClock _clock;
    private readonly IFileStore _files;
    private readonly ILog _log;
    private readonly object _lock = new();

    private RecorderState _state = RecorderState.Idle;
    private CancellationTokenSource? _countdown;
    private Run? _run;

    public RecorderInteractor(
        IScreenFrames frames,
        ISoundSources sounds,
        IVideoWriter writer,
        IMonotonicClock ticks,
        IWindowPresence windows,
        IWindowCatalog catalog,
        IFileLauncher launcher,
        IDelay delay,
        IClock clock,
        IFileStore files,
        ILog log)
    {
        _frames = frames;
        _sounds = sounds;
        _writer = writer;
        _ticks = ticks;
        _windows = windows;
        _catalog = catalog;
        _launcher = launcher;
        _delay = delay;
        _clock = clock;
        _files = files;
        _log = log;
        _sounds.Samples += OnSamples;
        _sounds.Lost += OnSoundLost;
    }

    public event Action<RecordingResult>? Finished;

    public event Action<NotificationMessage>? Notice;

    public RecorderState State
    {
        get
        {
            lock (_lock)
            {
                return _state;
            }
        }
    }

    public TimeSpan Elapsed
    {
        get
        {
            lock (_lock)
            {
                return _run?.Timeline.Elapsed(_ticks.Now) ?? TimeSpan.Zero;
            }
        }
    }

    public IReadOnlyList<WindowInfo> ListWindows() => _catalog.GetWindows();

    public WindowInfo? WindowAt(IReadOnlyList<WindowInfo> windows, PixelPoint pointer) => WindowPicking.TopmostAt(windows, pointer);

    public NotificationMessage? OpenVideo(string path) => Launched(_launcher.Open(path), path);

    public NotificationMessage? ShowInFolder(string path) => Launched(_launcher.ShowInFolder(path), path);

    public RecordAreaResult ResolveArea(RecordTargetKind kind, int? monitorIndex, PixelRect? picked, IReadOnlyList<MonitorInfo> monitors, PixelPoint pointer) =>
        RecordArea.Resolve(kind, monitorIndex, picked, monitors, pointer);

    public async Task<RecordStartResult> StartAsync(RecordRequest request, IProgress<int>? countdown, CancellationToken cancellationToken)
    {
        var area = request.Area;
        if (area.Width < RecorderRules.MinimumSide || area.Height < RecorderRules.MinimumSide || area.Width % 2 != 0 || area.Height % 2 != 0)
        {
            return Refused(RecordStartIssue.AreaUnusable, NotificationMessage.Of("Recorder.AreaTooSmall"));
        }

        CancellationTokenSource stopCountdown;
        lock (_lock)
        {
            if (_state != RecorderState.Idle)
            {
                return Refused(RecordStartIssue.Busy, null);
            }

            _state = RecorderState.CountingDown;
            _countdown = stopCountdown = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        try
        {
            for (var left = request.Settings.CountdownSeconds; left > 0; left--)
            {
                countdown?.Report(left);
                await _delay.DelayAsync(TimeSpan.FromSeconds(1), stopCountdown.Token);
            }

            stopCountdown.Token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException)
        {
            // Stopped during the countdown: nothing recorded, no file (SPEC recorder, "Pause and stop").
            SetIdle();
            return Refused(RecordStartIssue.Cancelled, null);
        }
        finally
        {
            lock (_lock)
            {
                _countdown = null;
            }

            stopCountdown.Dispose();
        }

        return Begin(request);
    }

    public void Pause()
    {
        lock (_lock)
        {
            if (_state == RecorderState.Recording && _run is { } run)
            {
                run.Timeline.Pause(_ticks.Now);
                _state = RecorderState.Paused;
            }
        }
    }

    public void Resume()
    {
        lock (_lock)
        {
            if (_state == RecorderState.Paused && _run is { } run)
            {
                run.Timeline.Resume(_ticks.Now);
                _state = RecorderState.Recording;
            }
        }
    }

    public async Task<RecordingResult?> StopAsync()
    {
        Run? run;
        lock (_lock)
        {
            if (_state == RecorderState.CountingDown)
            {
                _countdown?.Cancel();
                return null;
            }

            run = _run;
            if (run is null)
            {
                return null;
            }

            run.StopAsked = true;
        }

        return await run.Completion.Task;
    }

    private RecordStartResult Begin(RecordRequest request)
    {
        var settings = request.Settings;
        var folder = settings.VideoFolder;
        if (!_files.DirectoryExists(folder))
        {
            var made = _files.CreateDirectory(folder);
            if (!made.Success)
            {
                _log.Warning($"The video folder {folder} could not be made: {made.Detail}");
                SetIdle();
                return Refused(RecordStartIssue.FolderNotWritable, NotificationMessage.Of("Recorder.FolderNotWritable", folder, made.Detail ?? string.Empty));
            }
        }

        var name = RecordingFileName.For(_clock.Now, candidate => _files.FileExists(Path.Combine(folder, candidate)));
        var finalPath = Path.Combine(folder, name);
        var partPath = finalPath + RecordingFileName.PartSuffix;
        var size = new PixelSize(request.Area.Width, request.Area.Height);

        var opened = _frames.Open(request.Area, settings.Pointer);
        if (!opened.Success)
        {
            _log.Warning($"The screen could not be captured: {opened.Detail}");
            SetIdle();
            return Refused(RecordStartIssue.CannotRecord, NotificationMessage.Of("Recorder.CannotRecord", opened.Detail ?? string.Empty));
        }

        var notices = new List<NotificationMessage>();
        var sound = settings.SystemSound || settings.Microphone
            ? _sounds.Start(settings.SystemSound, settings.Microphone)
            : new SoundStartResult(false, false, null, null);
        if (settings.SystemSound && !sound.SystemSound)
        {
            _log.Warning($"System sound is not recorded: {sound.SystemDetail}");
            notices.Add(NotificationMessage.Of("Recorder.SystemSoundMissing", sound.SystemDetail ?? string.Empty));
        }

        if (settings.Microphone && !sound.Microphone)
        {
            _log.Warning($"The microphone is not recorded: {sound.MicrophoneDetail}");
            notices.Add(NotificationMessage.Of("Recorder.MicrophoneMissing", sound.MicrophoneDetail ?? string.Empty));
        }

        var sources = new List<SoundSource>();
        if (sound.SystemSound)
        {
            sources.Add(SoundSource.System);
        }

        if (sound.Microphone)
        {
            sources.Add(SoundSource.Microphone);
        }

        var writer = _writer.Open(partPath, size, settings.FramesPerSecond, sources.Count > 0);
        if (!writer.Success)
        {
            _log.Warning($"The video could not be started at {partPath}: {writer.Issue} {writer.Detail}");
            _frames.Close();
            if (sources.Count > 0)
            {
                _sounds.Stop();
            }

            SetIdle();
            return writer.Issue == VideoWriterIssue.Encoder
                ? Refused(RecordStartIssue.CannotRecord, NotificationMessage.Of("Recorder.CannotRecord", writer.Detail ?? string.Empty))
                : Refused(RecordStartIssue.FolderNotWritable, NotificationMessage.Of("Recorder.FolderNotWritable", folder, writer.Detail ?? string.Empty));
        }

        var run = new Run(request, finalPath, size, new FrameClock(settings.FramesPerSecond), new AudioMixer(sources), sources.Count > 0);
        lock (_lock)
        {
            run.Timeline.Start(_ticks.Now);
            run.NextWindowCheck = _ticks.Now + WindowCheckEvery;
            _run = run;
            _state = RecorderState.Recording;
        }

        _log.Info($"Recording {size.Width} x {size.Height} at {settings.FramesPerSecond} fps to {finalPath}; sound: {string.Join(", ", sources)}");
        _ = Task.Run(() => Loop(run));
        return new RecordStartResult(RecordStartIssue.None, finalPath, notices, null);
    }

    private void Loop(Run run)
    {
        RecordingResult result;
        try
        {
            result = Record(run);
        }
        catch (Exception exception)
        {
            // Nothing may leave a half-written file behind, whatever went wrong (F3).
            _log.Error("The recording failed", exception);
            _writer.Abandon();
            CloseSources(run);
            result = new RecordingResult(RecordingEnd.WriteFailed, null, run.Size, TimeSpan.Zero, NotificationMessage.Of("Recorder.WriteFailed", run.Request.Settings.VideoFolder, exception.Message));
        }

        lock (_lock)
        {
            _run = null;
            _state = RecorderState.Idle;
        }

        Finished?.Invoke(result);
        run.Completion.TrySetResult(result);
    }

    private RecordingResult Record(Run run)
    {
        var wait = run.Clock.SlotLength;
        RecordingEnd? end = null;
        string? detail = null;
        while (end is null)
        {
            lock (_lock)
            {
                if (run.StopAsked)
                {
                    end = RecordingEnd.Stopped;
                    break;
                }
            }

            var frame = _frames.Next(wait);
            if (frame.Issue == ScreenFrameIssue.DisplayChanged)
            {
                end = RecordingEnd.DisplayChanged;
                detail = frame.Detail;
                break;
            }

            if (!WindowStillThere(run))
            {
                end = RecordingEnd.WindowClosed;
                break;
            }

            var written = Write(run, frame);
            if (!written.Success)
            {
                end = RecordingEnd.WriteFailed;
                detail = written.Detail;
            }
        }

        CloseSources(run);
        if (end == RecordingEnd.WriteFailed)
        {
            _writer.Abandon();
            _log.Warning($"Writing the video failed: {detail}");
            return new RecordingResult(RecordingEnd.WriteFailed, null, run.Size, TimeSpan.Zero, NotificationMessage.Of("Recorder.WriteFailed", run.Request.Settings.VideoFolder, detail ?? string.Empty));
        }

        TimeSpan duration;
        lock (_lock)
        {
            _state = RecorderState.Finishing;
            duration = run.Clock.End;
        }

        // The sound ends with the picture, so they stay in time to the last frame (SPEC recorder, "Sound").
        if (run.WithSound)
        {
            float[] rest;
            TimeSpan at;
            lock (_lock)
            {
                at = run.Mixer.WrittenTime;
                rest = run.Mixer.Flush(duration);
            }

            if (rest.Length > 0)
            {
                var tail = _writer.WriteSound(rest, at);
                if (!tail.Success)
                {
                    _writer.Abandon();
                    return new RecordingResult(RecordingEnd.WriteFailed, null, run.Size, TimeSpan.Zero, NotificationMessage.Of("Recorder.WriteFailed", run.Request.Settings.VideoFolder, tail.Detail ?? string.Empty));
                }
            }
        }

        var finished = _writer.Finish(run.FinalPath);
        if (!finished.Success)
        {
            _writer.Abandon();
            _log.Warning($"The video could not be completed: {finished.Detail}");
            return new RecordingResult(RecordingEnd.WriteFailed, null, run.Size, TimeSpan.Zero, NotificationMessage.Of("Recorder.WriteFailed", run.Request.Settings.VideoFolder, finished.Detail ?? string.Empty));
        }

        var message = end switch
        {
            RecordingEnd.WindowClosed => NotificationMessage.Of("Recorder.WindowClosed"),
            RecordingEnd.DisplayChanged => NotificationMessage.Of("Recorder.DisplayChanged", detail ?? string.Empty),
            _ => null,
        };
        _log.Info($"Recorded {duration} to {run.FinalPath} ({end})");
        return new RecordingResult(end ?? RecordingEnd.Stopped, run.FinalPath, run.Size, duration, message, finished.Bytes);
    }

    // One step of the loop: the picture (or, when the screen did not change, the previous one) goes into every frame slot up to now,
    // then the sound that every source has reached.
    private VideoWriterResult Write(Run run, ScreenFrame frame)
    {
        FrameSlots slots;
        float[] sound;
        TimeSpan soundAt;
        if (frame.Image is null && run.Previous is null)
        {
            // Nothing to show yet: the video starts with the first picture.
            return VideoWriterResult.Ok;
        }

        lock (_lock)
        {
            var media = run.Timeline.MediaTime(frame.Image is null ? _ticks.Now : frame.At);
            if (media is not { } time)
            {
                return VideoWriterResult.Ok;
            }

            slots = run.Clock.Place(time);
            soundAt = run.Mixer.WrittenTime;
            sound = run.WithSound ? run.Mixer.Drain(time) : [];
        }

        if (slots.Count > 0)
        {
            var fresh = frame.Image ?? run.Previous;
            for (var slot = slots.First; slot <= slots.Last; slot++)
            {
                // A gap repeats what was on screen before it; the very first picture fills the slots before it.
                var picture = slot < slots.Last ? run.Previous ?? fresh : fresh;
                if (picture is null)
                {
                    continue;
                }

                var result = _writer.WriteVideo(picture, run.Clock.TimeOf(slot), run.Clock.SlotLength);
                if (!result.Success)
                {
                    return result;
                }
            }

            if (frame.Image is not null)
            {
                run.Previous = frame.Image;
            }
        }

        return sound.Length > 0 ? _writer.WriteSound(sound, soundAt) : VideoWriterResult.Ok;
    }

    private bool WindowStillThere(Run run)
    {
        if (run.Request.WindowHandle is not { } handle)
        {
            return true;
        }

        var now = _ticks.Now;
        if (now < run.NextWindowCheck)
        {
            return true;
        }

        run.NextWindowCheck = now + WindowCheckEvery;
        return _windows.IsOpen(handle);
    }

    private void OnSamples(SoundSource source, TimeSpan at, float[] samples)
    {
        lock (_lock)
        {
            if (_run is { } run && run.Timeline.MediaTime(at) is { } time)
            {
                run.Mixer.Push(source, time, samples);
            }
        }
    }

    private void OnSoundLost(SoundSource source, string detail)
    {
        lock (_lock)
        {
            if (_run is not { } run)
            {
                return;
            }

            // A lost source no longer holds the other back; the recording goes on without it (F4, F5).
            run.Mixer.Remove(source);
        }

        _log.Warning($"{source} sound was lost while recording: {detail}");
        Notice?.Invoke(NotificationMessage.Of(source == SoundSource.Microphone ? "Recorder.MicrophoneLost" : "Recorder.SystemSoundLost", detail));
    }

    private void CloseSources(Run run)
    {
        if (run.WithSound)
        {
            _sounds.Stop();
        }

        _frames.Close();
    }

    private NotificationMessage? Launched(PortResult result, string path)
    {
        if (result.Success)
        {
            return null;
        }

        _log.Warning($"{path} could not be opened: {result.Detail}");
        return NotificationMessage.Of("Recorder.CannotOpen", path, result.Detail ?? string.Empty);
    }

    private void SetIdle()
    {
        lock (_lock)
        {
            _state = RecorderState.Idle;
        }
    }

    private static RecordStartResult Refused(RecordStartIssue issue, NotificationMessage? message) => new(issue, null, [], message);

    private sealed class Run(RecordRequest request, string finalPath, PixelSize size, FrameClock clock, AudioMixer mixer, bool withSound)
    {
        public RecordRequest Request { get; } = request;

        public string FinalPath { get; } = finalPath;

        public PixelSize Size { get; } = size;

        public FrameClock Clock { get; } = clock;

        public AudioMixer Mixer { get; } = mixer;

        public bool WithSound { get; } = withSound;

        public RecordingTimeline Timeline { get; } = new();

        public TaskCompletionSource<RecordingResult> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool StopAsked { get; set; }

        public TimeSpan NextWindowCheck { get; set; }

        public PixelImage? Previous { get; set; }
    }
}
