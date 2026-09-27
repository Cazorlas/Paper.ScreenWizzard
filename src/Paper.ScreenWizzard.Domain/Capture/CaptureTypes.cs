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
