using Paper.ScreenWizzard.Domain.Recorder;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.UseCases.Recorder.Models;
using Paper.ScreenWizzard.UseCases.Recorder.Ports;
using Paper.ScreenWizzard.UseCases.Recorder.UseCases;
using Paper.ScreenWizzard.UseCases.Shared.Models;
using Paper.ScreenWizzard.UseCases.Shared.Ports;

namespace Paper.ScreenWizzard.UnitTests.Recorder.Fakes;

public sealed class FakeTicks : IMonotonicClock
{
    public TimeSpan Now { get; set; }
}

/// <summary>
/// The screen as a script: each step sets the clock and either hands a picture, runs an action (Pause, a sound), or reports a changed
/// display. When the script is done it says so and then has no new picture, so the test can stop the recording.
/// </summary>
public sealed class FakeScreenFrames(FakeTicks ticks) : IScreenFrames
{
    private readonly Queue<Step> _steps = new();

    public TaskCompletionSource ScriptDone { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public PortResult OpenResult { get; set; } = PortResult.Ok;

    public PixelRect? OpenedArea { get; private set; }

    public bool? OpenedWithPointer { get; private set; }

    public bool Closed { get; private set; }

    public void Frame(TimeSpan at, byte id = 1) => _steps.Enqueue(new Step(at, Picture(id), null, false));

    public void At(TimeSpan at, Action action) => _steps.Enqueue(new Step(at, null, action, false));

    public void DisplayChanges(TimeSpan at) => _steps.Enqueue(new Step(at, null, null, true));

    /// <summary>A picture every frame of <paramref name="fps"/> from <paramref name="from"/> up to and including <paramref name="to"/>.</summary>
    public void Frames(TimeSpan from, TimeSpan to, int fps, byte id = 1)
    {
        for (var at = from; at <= to; at += TimeSpan.FromTicks(TimeSpan.TicksPerSecond / fps))
        {
            Frame(at, id);
        }
    }

    public PortResult Open(PixelRect area, bool pointer)
    {
        OpenedArea = area;
        OpenedWithPointer = pointer;
        return OpenResult;
    }

    public ScreenFrame Next(TimeSpan wait)
    {
        while (_steps.TryDequeue(out var step))
        {
            ticks.Now = step.At;
            if (step.Action is { } action)
            {
                action();
                continue;
            }

            return step.DisplayChanged
                ? new ScreenFrame(null, step.At, ScreenFrameIssue.DisplayChanged, "the monitor was unplugged")
                : new ScreenFrame(step.Image, step.At, ScreenFrameIssue.None);
        }

        ScriptDone.TrySetResult();
        Thread.Sleep(1);
        return new ScreenFrame(null, ticks.Now, ScreenFrameIssue.NoNewFrame);
    }

    public void Close() => Closed = true;

    public static PixelImage Picture(byte id) => new(2, 2, [id, 0, 0, 255, id, 0, 0, 255, id, 0, 0, 255, id, 0, 0, 255]);

    private sealed record Step(TimeSpan At, PixelImage? Image, Action? Action, bool DisplayChanged);
}

public sealed class FakeSoundSources : ISoundSources
{
    public SoundStartResult? Answer { get; set; }

    public List<(bool System, bool Microphone)> Starts { get; } = [];

    public int Stops { get; private set; }

    public event Action<SoundSource, TimeSpan, float[]>? Samples;

    public event Action<SoundSource, string>? Lost;

    public SoundStartResult Start(bool systemSound, bool microphone)
    {
        Starts.Add((systemSound, microphone));
        return Answer ?? new SoundStartResult(systemSound, microphone, null, null);
    }

    public void Stop() => Stops++;

    public void Emit(SoundSource source, TimeSpan at, float value, TimeSpan length)
    {
        var frames = (int)Math.Round(length.TotalSeconds * RecorderRules.SampleRate);
        var samples = new float[frames * RecorderRules.Channels];
        Array.Fill(samples, value);
        Samples?.Invoke(source, at, samples);
    }

    public void Lose(SoundSource source) => Lost?.Invoke(source, "the device was removed");
}

public sealed class FakeVideoWriter : IVideoWriter
{
    public VideoWriterResult OpenResult { get; set; } = VideoWriterResult.Ok;

    /// <summary>The write of this picture (1 = the first) fails as a full disk would.</summary>
    public int? FailPictureNumber { get; set; }

    public (string Path, PixelSize Size, int Fps, bool WithSound)? Opened { get; private set; }

    public List<(PixelImage Image, TimeSpan At, TimeSpan Duration)> Pictures { get; } = [];

    /// <summary>Off for a long recording: only the length of each block is kept.</summary>
    public bool KeepSamples { get; set; } = true;

    public List<(float[] Samples, TimeSpan At)> Sounds { get; } = [];

    public List<(int Length, TimeSpan At)> SoundBlocks { get; } = [];

    public string? FinishedAs { get; private set; }

    public bool Abandoned { get; private set; }

    public TimeSpan VideoEnd => Pictures.Count == 0 ? TimeSpan.Zero : Pictures.Max(p => p.At + p.Duration);

    public TimeSpan SoundEnd => SoundBlocks.Count == 0
        ? TimeSpan.Zero
        : SoundBlocks.Max(s => s.At + TimeSpan.FromSeconds(s.Length / (double)RecorderRules.Channels / RecorderRules.SampleRate));

    public VideoWriterResult Open(string path, PixelSize size, int framesPerSecond, bool withSound)
    {
        Opened = (path, size, framesPerSecond, withSound);
        return OpenResult;
    }

    public VideoWriterResult WriteVideo(PixelImage frame, TimeSpan at, TimeSpan duration)
    {
        if (Pictures.Count + 1 == FailPictureNumber)
        {
            return new VideoWriterResult(VideoWriterIssue.Disk, "There is not enough space on the disk.");
        }

        Pictures.Add((frame, at, duration));
        return VideoWriterResult.Ok;
    }

    public VideoWriterResult WriteSound(float[] samples, TimeSpan at)
    {
        SoundBlocks.Add((samples.Length, at));
        if (KeepSamples)
        {
            Sounds.Add((samples, at));
        }

        return VideoWriterResult.Ok;
    }

    public VideoWriterResult Finish(string finalPath)
    {
        FinishedAs = finalPath;
        return new VideoWriterResult(VideoWriterIssue.None, null, 42 * 1024 * 1024);
    }

    public void Abandon() => Abandoned = true;
}

public sealed class FakeWindowPresence : IWindowPresence
{
    public bool Open { get; set; } = true;

    public bool IsOpen(long windowHandle) => Open;
}

public sealed class FakeWindowCatalog : IWindowCatalog
{
    public List<WindowInfo> Windows { get; } = [];

    public IReadOnlyList<WindowInfo> GetWindows() => Windows;
}

public sealed class FakeLauncher : IFileLauncher
{
    public PortResult Result { get; set; } = PortResult.Ok;

    public List<string> Opened { get; } = [];

    public List<string> Shown { get; } = [];

    public PortResult Open(string path)
    {
        Opened.Add(path);
        return Result;
    }

    public PortResult ShowInFolder(string path)
    {
        Shown.Add(path);
        return Result;
    }
}

/// <summary>A countdown second that passes at once, or never (until cancelled) when <see cref="Hold"/> is set.</summary>
public sealed class FakeCountdown : IDelay
{
    public bool Hold { get; set; }

    public int Seconds { get; private set; }

    public TaskCompletionSource Waiting { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
    {
        Seconds++;
        if (!Hold)
        {
            return Task.CompletedTask;
        }

        Waiting.TrySetResult();
        return Task.Delay(Timeout.Infinite, cancellationToken);
    }
}

public sealed class FakeWallClock : IClock
{
    public DateTime Now { get; set; } = new(2026, 9, 27, 14, 3, 5);
}

public sealed class FakeRecorderFiles : IFileStore
{
    public HashSet<string> Directories { get; } = [];

    public HashSet<string> Files { get; } = [];

    public PortResult CreateResult { get; set; } = PortResult.Ok;

    public bool DirectoryExists(string path) => Directories.Contains(path);

    public PortResult CreateDirectory(string path)
    {
        if (CreateResult.Success)
        {
            Directories.Add(path);
        }

        return CreateResult;
    }

    public bool FileExists(string path) => Files.Contains(path);

    public PortResult WriteAllBytes(string path, byte[] bytes) => throw new NotSupportedException("the recorder writes through the video writer");

    public PortBytesResult ReadAllBytes(string path) => throw new NotSupportedException();

    public DateTime? GetLastWriteTimeUtc(string path) => null;
}

public sealed class FakeRecorderLog : ILog
{
    public List<string> Lines { get; } = [];

    public void Info(string message) => Lines.Add("I " + message);

    public void Warning(string message) => Lines.Add("W " + message);

    public void Error(string message, Exception? exception) => Lines.Add("E " + message + " " + exception?.Message);
}

/// <summary>A recorder on fakes, and the request of SPEC recorder's examples: 1920 × 1080, 30 fps, no countdown, system sound only.</summary>
public sealed class RecorderRig
{
    public const string Folder = @"C:\Users\An\Videos\Paper.ScreenWizzard";

    public RecorderRig()
    {
        Frames = new FakeScreenFrames(Ticks);
        Recorder = new RecorderInteractor(Frames, Sounds, Writer, Ticks, Windows, Catalog, Launcher, Countdown, Wall, Files, Log);
        Recorder.Notice += Notices.Add;
        Files.Directories.Add(Folder);
    }

    public FakeTicks Ticks { get; } = new();

    public FakeScreenFrames Frames { get; }

    public FakeSoundSources Sounds { get; } = new();

    public FakeVideoWriter Writer { get; } = new();

    public FakeWindowPresence Windows { get; } = new();

    public FakeCountdown Countdown { get; } = new();

    public FakeWindowCatalog Catalog { get; } = new();

    public FakeLauncher Launcher { get; } = new();

    public FakeWallClock Wall { get; } = new();

    public FakeRecorderFiles Files { get; } = new();

    public FakeRecorderLog Log { get; } = new();

    public List<NotificationMessage> Notices { get; } = [];

    public RecorderInteractor Recorder { get; }

    public static RecorderSettings Settings(bool system = true, bool microphone = false, int countdown = 0, int fps = 30) =>
        new(RecordTargetKind.Monitor, null, system, microphone, true, countdown, fps, Folder);

    public static RecordRequest Request(RecorderSettings? settings = null, long? window = null) =>
        new(new PixelRect(0, 0, 1920, 1080), window, settings ?? Settings());

    /// <summary>Starts, plays the script to its end, and stops.</summary>
    public async Task<(RecordStartResult Start, RecordingResult? Result)> RunAsync(RecordRequest? request = null)
    {
        // Listened to before the start: a recording that stops by itself (F1, F2) may end before the script does.
        var finished = new TaskCompletionSource<RecordingResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        Recorder.Finished += result => finished.TrySetResult(result);
        var start = await Recorder.StartAsync(request ?? Request(), null, CancellationToken.None);
        if (!start.Started)
        {
            return (start, null);
        }

        var ended = await Task.WhenAny(Frames.ScriptDone.Task, finished.Task).WaitAsync(TimeSpan.FromSeconds(30));
        var result = ended == finished.Task ? await finished.Task : await Recorder.StopAsync();
        return (start, result);
    }
}
