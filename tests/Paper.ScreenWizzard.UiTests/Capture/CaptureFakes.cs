using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.Presentation.Capture.ViewModels;
using Paper.ScreenWizzard.Presentation.Shell.ViewModels;
using Paper.ScreenWizzard.UiTests.Shell;
using Paper.ScreenWizzard.UseCases.Capture.Models;
using Paper.ScreenWizzard.UseCases.Capture.Ports;
using Paper.ScreenWizzard.UseCases.Shared.Models;

// The namespace is not ...UiTests.Capture on purpose: FlaUI has a class called Capture, and a namespace of that name in
// Paper.ScreenWizzard.UiTests would hide it from Support/WindowSession.cs.
namespace Paper.ScreenWizzard.UiTests.ScreenCapture;

/// <summary>A list that must hold exactly one item: an empty one fails an assertion, never an exception from Single().</summary>
public static class Expect
{
    public static T Only<T>(this IEnumerable<T> items, string what = "item")
    {
        var list = items.ToList();
        NUnit.Framework.Assert.That(list, NUnit.Framework.Has.Count.EqualTo(1), $"expected exactly one {what}");
        return list[0];
    }
}

/// <summary>Plain data for the capture tests: a small virtual screen, a gradient image whose colour tells its coordinates.</summary>
public static class CaptureTestData
{
    /// <summary>A virtual screen far smaller than any real desktop, with a positive origin, so no test covers the whole screen.</summary>
    public static readonly PixelRect SmallScreen = new(100, 100, 800, 600);

    /// <summary>Notepad-like window on top of an Explorer-like one; both inside <see cref="SmallScreen"/>.</summary>
    public static readonly PixelRect NotepadFrame = new(200, 200, 300, 200);

    public static readonly PixelRect ExplorerFrame = new(350, 300, 400, 300);

    /// <summary>R grows with x, G grows with y, B is fixed: a pixel's colour says where in the image it came from.</summary>
    public static PixelImage Gradient(int width, int height)
    {
        var bgra = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = ((y * width) + x) * 4;
                bgra[i] = 128;
                bgra[i + 1] = GradientG(y, height);
                bgra[i + 2] = GradientR(x, width);
                bgra[i + 3] = 255;
            }
        }

        return new PixelImage(width, height, bgra);
    }

    public static byte GradientR(int x, int width) => (byte)(x * 255 / Math.Max(1, width - 1));

    public static byte GradientG(int y, int height) => (byte)(y * 255 / Math.Max(1, height - 1));

    public static PixelImage Solid(int width, int height, byte value = 200)
    {
        var bgra = new byte[width * height * 4];
        Array.Fill(bgra, value);
        return new PixelImage(width, height, bgra);
    }

    public static DesktopSnapshot Snapshot(PixelPoint? cursor = null) => Snapshot(SmallScreen, cursor);

    public static DesktopSnapshot Snapshot(PixelRect screen, PixelPoint? cursor = null) => new(
        screen,
        Gradient(screen.Width, screen.Height),
        [new MonitorInfo(0, screen, true, 96)],
        [
            new WindowInfo(1, "Notepad", NotepadFrame, true, false, false, false, 0),
            new WindowInfo(2, "Explorer", ExplorerFrame, true, false, false, false, 1),
        ],
        cursor ?? new PixelPoint(screen.X + 10, screen.Y + 10),
        "layout-1");

    public static CaptureOutcome Captured(PixelRect region) => new(Solid(region.Width, region.Height), region, CaptureIssue.None, null);

    public static CaptureOutcome Refused(CaptureIssue issue, NotificationMessage? message) => new(null, null, issue, message);
}

/// <summary>Text for a message is its key and its arguments, so a view model test reads what it was handed without a language file.</summary>
public sealed class FakeLocalizer : ILocalizer
{
    public event EventHandler? LanguageChanged
    {
        add { }
        remove { }
    }

    public string GetString(string key) => key;

    public string Format(NotificationMessage message) => message.Arguments.Count == 0
        ? message.Key
        : $"{message.Key}({string.Join(",", message.Arguments)})";
}

/// <summary>
/// A capture run on canned answers. Every call is recorded; the answers are what a test sets. The rules (clamping, the 3 x 3
/// minimum) are the logic lane's and are not repeated here.
/// </summary>
public sealed class FakeCaptureSession : ICaptureSession
{
    public FakeCaptureSession(CaptureKind kind, DesktopSnapshot? snapshot = null)
    {
        Kind = kind;
        Snapshot = snapshot ?? CaptureTestData.Snapshot();
    }

    public CaptureKind Kind { get; }

    public DesktopSnapshot Snapshot { get; }

    public bool IsActive { get; set; } = true;

    /// <summary>Every call in the order it came, so a test can say "checked the display, then completed".</summary>
    public List<string> Calls { get; } = [];

    public List<(PixelPoint From, PixelPoint To)> RectangleCompletions { get; } = [];

    public List<IReadOnlyList<PixelPoint>> FreeformCompletions { get; } = [];

    public List<PixelPoint> WindowCompletions { get; } = [];

    public int FullScreenCompletions { get; private set; }

    public int CancelCount { get; private set; }

    public CaptureIssue DisplayAnswer { get; set; } = CaptureIssue.None;

    /// <summary>The answers of the next completions; when unset, a success covering the dragged region.</summary>
    public Queue<CaptureOutcome> Answers { get; } = new();

    public PixelRect PreviewRectangle(PixelPoint from, PixelPoint to)
    {
        // The session clamps to the virtual screen (SPEC capture), and the view must show what the session says, not its own guess.
        var left = Math.Max(Math.Min(from.X, to.X), Snapshot.VirtualScreen.X);
        var top = Math.Max(Math.Min(from.Y, to.Y), Snapshot.VirtualScreen.Y);
        var right = Math.Min(Math.Max(from.X, to.X), Snapshot.VirtualScreen.X + Snapshot.VirtualScreen.Width);
        var bottom = Math.Min(Math.Max(from.Y, to.Y), Snapshot.VirtualScreen.Y + Snapshot.VirtualScreen.Height);
        return new PixelRect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    public CaptureOutcome CompleteRectangle(PixelPoint from, PixelPoint to)
    {
        Calls.Add("CompleteRectangle");
        RectangleCompletions.Add((from, to));
        return Answers.Count > 0 ? Answers.Dequeue() : CaptureTestData.Captured(PreviewRectangle(from, to));
    }

    public CaptureOutcome CompleteFreeform(IReadOnlyList<PixelPoint> outline)
    {
        Calls.Add("CompleteFreeform");
        FreeformCompletions.Add(outline.ToList());
        var left = outline.Min(p => p.X);
        var top = outline.Min(p => p.Y);
        var region = new PixelRect(left, top, outline.Max(p => p.X) - left, outline.Max(p => p.Y) - top);
        return Answers.Count > 0 ? Answers.Dequeue() : CaptureTestData.Captured(region);
    }

    public WindowHit HitTestWindow(PixelPoint pointer)
    {
        var window = Snapshot.Windows.OrderBy(w => w.ZOrder).FirstOrDefault(w => Contains(w.VisibleFrame, pointer));
        return window is null
            ? new WindowHit(true, Snapshot.Monitors[0].Bounds, string.Empty, true)
            : new WindowHit(true, window.VisibleFrame, window.Title, false);
    }

    public CaptureOutcome CompleteWindow(PixelPoint pointer)
    {
        Calls.Add("CompleteWindow");
        WindowCompletions.Add(pointer);
        return Answers.Count > 0 ? Answers.Dequeue() : CaptureTestData.Captured(HitTestWindow(pointer).Frame);
    }

    public CaptureOutcome CompleteFullScreen()
    {
        Calls.Add("CompleteFullScreen");
        FullScreenCompletions++;
        return Answers.Count > 0 ? Answers.Dequeue() : CaptureTestData.Captured(Snapshot.VirtualScreen);
    }

    public CaptureIssue CheckDisplayUnchanged()
    {
        Calls.Add("CheckDisplayUnchanged");
        return DisplayAnswer;
    }

    public void Cancel()
    {
        Calls.Add("Cancel");
        CancelCount++;
        IsActive = false;
    }

    private static bool Contains(PixelRect rect, PixelPoint point) =>
        point.X >= rect.X && point.X < rect.X + rect.Width && point.Y >= rect.Y && point.Y < rect.Y + rect.Height;
}

/// <summary>The capture use case with canned answers and a record of every delivery.</summary>
public sealed class FakeCaptureInteractor : ICaptureInteractor
{
    public List<(PixelImage Image, CaptureDestination Destination)> Deliveries { get; } = [];

    public List<(PixelImage Image, string Path)> PathDeliveries { get; } = [];

    public List<CaptureRequest> Begun { get; } = [];

    /// <summary>The countdown seconds BeginAsync reports before it returns its session.</summary>
    public int[] CountdownToReport { get; set; } = [];

    /// <summary>The session BeginAsync returns; when a test needs the run to stay pending, it sets <see cref="BeginGate"/> instead.</summary>
    public ICaptureSession? SessionToReturn { get; set; }

    public CaptureIssue BeginIssue { get; set; } = CaptureIssue.None;

    /// <summary>When set, BeginAsync stays pending until the test completes this source (a countdown in progress).</summary>
    public TaskCompletionSource<CaptureBeginResult>? BeginGate { get; set; }

    /// <summary>The session BeginAsync cancels first, as the real interactor does with the run it replaces (SPEC capture F8).</summary>
    public ICaptureSession? RunningSession { get; set; }

    public AfterCapturePlan Plan { get; set; } = new(true, []);

    public Func<CaptureDestination, CaptureDeliveryResult> DeliverAnswer { get; set; } =
        _ => new CaptureDeliveryResult(true, false, null, null);

    public SaveAsSuggestion SaveAsAnswer { get; set; } = new(@"C:\Users\Test\Pictures\Paper.ScreenWizzard", "Screenshot 2026-09-20 14.03.05.png");

    public CaptureDeliveryResult PathAnswer { get; set; } = new(true, false, @"D:\Chosen\shot.png", null);

    public List<(string Folder, string FileName)> SaveAsAsked { get; } = [];

    /// <summary>Runs first thing in BeginAsync: the moment the real use case would go on to take the snapshot when there is no delay.</summary>
    public Action? BeforeBegin { get; set; }

    /// <summary>The token of the latest BeginAsync: a test cancels the run through the window and reads it here.</summary>
    public CancellationToken LastToken { get; private set; }

    public Task<CaptureBeginResult> BeginAsync(CaptureRequest request, IProgress<int>? countdown, CancellationToken cancellationToken)
    {
        BeforeBegin?.Invoke();
        LastToken = cancellationToken;
        Begun.Add(request);
        RunningSession?.Cancel();
        foreach (var second in CountdownToReport)
        {
            countdown?.Report(second);
        }

        if (BeginGate is { } gate)
        {
            BeginGate = null;
            return gate.Task;
        }

        return Task.FromResult(new CaptureBeginResult(SessionToReturn, BeginIssue));
    }

    public CaptureRequest RequestFor(CaptureKind kind, AppSettings settings) => new(kind, settings.DelaySeconds, settings.IncludeCursor, settings.FullScreenScope);

    public AfterCapturePlan PlanAfterCapture(AppSettings settings) => Plan;

    public CaptureDeliveryResult Deliver(PixelImage image, CaptureDestination destination, AppSettings settings)
    {
        Deliveries.Add((image, destination));
        return DeliverAnswer(destination);
    }

    public SaveAsSuggestion SuggestSaveAs(AppSettings settings) => SaveAsAnswer;

    public CaptureDeliveryResult DeliverToPath(PixelImage image, string path, AppSettings settings)
    {
        PathDeliveries.Add((image, path));
        return PathAnswer;
    }
}

public sealed class FakeFileDialogs : IFileDialogService
{
    /// <summary>The path the "user" picks; null is the user cancelling the box.</summary>
    public string? Answer { get; set; }

    public List<(string Folder, string FileName)> Asked { get; } = [];

    public string? PickSavePath(string initialFolder, string suggestedFileName)
    {
        Asked.Add((initialFolder, suggestedFileName));
        return Answer;
    }
}

/// <summary>Windows the flow "opened", recorded instead of shown.</summary>
public sealed class FakeCaptureViews : ICaptureViews
{
    public List<FakeHandle<CountdownViewModel>> Countdowns { get; } = [];

    public List<FakeHandle<SelectionOverlayViewModel>> Selections { get; } = [];

    public List<FakeHandle<CaptureDoneViewModel>> Dones { get; } = [];

    public List<PixelRect> DoneAreas { get; } = [];

    public int OpenSelectionCount => Selections.Count(s => !s.IsClosed);

    /// <summary>Every number the countdown window showed, the first one included, in order.</summary>
    public List<int> CountdownValues { get; } = [];

    public IViewHandle OpenCountdown(CountdownViewModel viewModel)
    {
        CountdownValues.Add(viewModel.SecondsLeft);
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CountdownViewModel.SecondsLeft))
            {
                CountdownValues.Add(viewModel.SecondsLeft);
            }
        };
        return Track(Countdowns, viewModel);
    }

    public IViewHandle OpenSelection(SelectionOverlayViewModel viewModel) => Track(Selections, viewModel);

    public IViewHandle OpenDone(CaptureDoneViewModel viewModel, PixelRect area)
    {
        DoneAreas.Add(area);
        return Track(Dones, viewModel);
    }

    private static FakeHandle<T> Track<T>(List<FakeHandle<T>> list, T viewModel)
    {
        var handle = new FakeHandle<T>(viewModel);
        list.Add(handle);
        return handle;
    }
}

public sealed class FakeHandle<T> : IViewHandle
{
    public FakeHandle(T viewModel)
    {
        ViewModel = viewModel;
    }

    public T ViewModel { get; }

    public bool IsClosed { get; private set; }

    public event EventHandler? Closed;

    public void Close()
    {
        if (IsClosed)
        {
            return;
        }

        IsClosed = true;
        Closed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>The settings the capture tests start from: the defaults of SPEC shell, "Inputs", with the format a test may change.</summary>
public static class CaptureSettings
{
    public static AppSettings Default(Paper.ScreenWizzard.Domain.Shared.ImageFormat format = Paper.ScreenWizzard.Domain.Shared.ImageFormat.Png) =>
        ShellTestData.DefaultSettings() with { Format = format };
}
