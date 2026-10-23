namespace Paper.ScreenWizzard.Domain.Shared;

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

/// <summary>Which window the pointer is on: shared by the capture (SPEC capture, "Cửa sổ") and the recorder (SPEC recorder, "What is recorded").</summary>
public static class WindowPicking
{
    /// <summary>
    /// The topmost window under <paramref name="pointer"/> that can be a target: never a hidden, minimised or cloaked window, the app's
    /// own overlay, or the desktop itself.
    /// </summary>
    public static WindowInfo? TopmostAt(IEnumerable<WindowInfo> windows, PixelPoint pointer) =>
        windows
            .Where(w => w.IsVisible && !w.IsMinimized && !w.IsCloaked && !w.IsOwnOverlay && !w.IsDesktop && Contains(w.VisibleFrame, pointer))
            .OrderBy(w => w.ZOrder)
            .FirstOrDefault();

    private static bool Contains(PixelRect rect, PixelPoint point) =>
        point.X >= rect.X && point.X < rect.X + rect.Width && point.Y >= rect.Y && point.Y < rect.Y + rect.Height;
}
