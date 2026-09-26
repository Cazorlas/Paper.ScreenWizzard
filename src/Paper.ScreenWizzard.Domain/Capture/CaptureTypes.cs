using Paper.ScreenWizzard.Domain.Shared;

namespace Paper.ScreenWizzard.Domain.Capture;

/// <summary>The four ways to take a screenshot (SPEC capture, "What the user does" step 4).</summary>
public enum CaptureKind
{
    Rectangle,
    Freeform,
    Window,
    FullScreen,
}

/// <summary>Which screen "full screen" takes (SPEC capture, Inputs).</summary>
public enum FullScreenScope
{
    MonitorUnderCursor,
    AllMonitors,
}

/// <summary>Where a captured image goes after the capture (SPEC capture, "Sau khi chụp").</summary>
public enum CaptureDestination
{
    Editor,
    Clipboard,
    File,
}

/// <summary>What the user set for "after capture": ask in the dialog, or go straight to somewhere (SPEC capture, Inputs).</summary>
public enum AfterCaptureAction
{
    ShowDialog,
    OpenEditor,
    CopyToClipboard,
    SaveToFile,
    ClipboardAndFile,
}

/// <summary>
/// A top-level window at the moment of the snapshot.
/// </summary>
/// <param name="Handle">The window handle as a number; the port's, never dereferenced here.</param>
/// <param name="VisibleFrame">The frame the user sees, without the invisible resize border and shadow.</param>
/// <param name="ZOrder">0 is the topmost window; larger is further back.</param>
/// <param name="IsOwnOverlay">True for this app's own selection overlay, which is never a target.</param>
/// <param name="IsDesktop">True for the desktop's own windows (Progman, WorkerW): as big as every monitor together, never a target.</param>
public sealed record WindowInfo(
    long Handle,
    string Title,
    PixelRect VisibleFrame,
    bool IsVisible,
    bool IsMinimized,
    bool IsCloaked,
    bool IsOwnOverlay,
    int ZOrder,
    bool IsDesktop = false);

/// <summary>Everything the selection needs, taken at one instant, so what the user sees stays still while they choose.</summary>
/// <param name="VirtualScreen">The bounding rectangle of all monitors.</param>
/// <param name="Image">The pixels of <paramref name="VirtualScreen"/>.</param>
/// <param name="LayoutSignature">Changes whenever monitors are added, removed or resized (SPEC capture F6).</param>
public sealed record DesktopSnapshot(
    PixelRect VirtualScreen,
    PixelImage Image,
    IReadOnlyList<MonitorInfo> Monitors,
    IReadOnlyList<WindowInfo> Windows,
    PixelPoint CursorPosition,
    string LayoutSignature);
