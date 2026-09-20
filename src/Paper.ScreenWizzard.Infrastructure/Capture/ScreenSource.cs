using System.Runtime.InteropServices;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.UseCases.Capture.Ports;

namespace Paper.ScreenWizzard.Infrastructure.Capture;

/// <summary>
/// Reads the pixels of the virtual desktop with GDI: <c>BitBlt</c> (SRCCOPY | CAPTUREBLT, so layered windows on top are included) from
/// the desktop's device context into a top-down 32-bit DIB section. Everything is physical pixels of the virtual screen, which is what
/// GDI answers in a per-monitor-DPI-aware process (the exe's manifest). The result is B, G, R, A with alpha 255: GDI leaves the alpha
/// byte of a screen copy at 0, and a screenshot is opaque.
/// </summary>
public sealed class ScreenSource : IScreenSourcePort
{
    private const int BytesPerPixel = 4;

    public ScreenCaptureResult Capture(PixelRect area, bool includeCursor)
    {
        if (area.Width <= 0 || area.Height <= 0)
        {
            return new ScreenCaptureResult(null, ScreenCaptureIssue.Failed);
        }

        var length = (long)area.Width * area.Height * BytesPerPixel;
        if (length > int.MaxValue)
        {
            // A managed array cannot hold it: the same "too big to hold" answer as running out of memory.
            return new ScreenCaptureResult(null, ScreenCaptureIssue.OutOfMemory);
        }

        var screen = IntPtr.Zero;
        var memory = IntPtr.Zero;
        var bitmap = IntPtr.Zero;
        var previous = IntPtr.Zero;
        try
        {
            screen = NativeMethods.GetDC(IntPtr.Zero);
            if (screen == IntPtr.Zero)
            {
                return new ScreenCaptureResult(null, ScreenCaptureIssue.Failed);
            }

            memory = NativeMethods.CreateCompatibleDC(screen);
            if (memory == IntPtr.Zero)
            {
                return new ScreenCaptureResult(null, IssueOf(Marshal.GetLastWin32Error()));
            }

            var info = new NativeMethods.BitmapInfo
            {
                HeaderSize = 40,
                Width = area.Width,
                Height = -area.Height, // negative: top-down, row 0 is the top row (BITMAPINFOHEADER page)
                Planes = 1,
                BitCount = 32,
                Compression = 0, // BI_RGB
            };
            bitmap = NativeMethods.CreateDIBSection(screen, ref info, NativeMethods.DibRgbColors, out var bits, IntPtr.Zero, 0);
            if (bitmap == IntPtr.Zero || bits == IntPtr.Zero)
            {
                return new ScreenCaptureResult(null, IssueOf(Marshal.GetLastWin32Error()));
            }

            previous = NativeMethods.SelectObject(memory, bitmap);
            if (!NativeMethods.BitBlt(memory, 0, 0, area.Width, area.Height, screen, area.X, area.Y, NativeMethods.SrcCopy | NativeMethods.CaptureBlt))
            {
                return new ScreenCaptureResult(null, IssueOf(Marshal.GetLastWin32Error()));
            }

            if (includeCursor)
            {
                DrawCursor(memory, area);
            }

            // GDI batches drawing calls; the bits are only complete once the batch is flushed (GdiFlush page).
            NativeMethods.GdiFlush();

            var bytes = new byte[length];
            Marshal.Copy(bits, bytes, 0, bytes.Length);
            for (var alpha = 3; alpha < bytes.Length; alpha += BytesPerPixel)
            {
                bytes[alpha] = 255;
            }

            return new ScreenCaptureResult(new PixelImage(area.Width, area.Height, bytes), ScreenCaptureIssue.None);
        }
        catch (OutOfMemoryException)
        {
            return new ScreenCaptureResult(null, ScreenCaptureIssue.OutOfMemory);
        }
        catch (Exception)
        {
            return new ScreenCaptureResult(null, ScreenCaptureIssue.Failed);
        }
        finally
        {
            // Every GDI object goes back, in the reverse order it was taken: a bitmap still selected into a DC cannot be deleted.
            if (memory != IntPtr.Zero && previous != IntPtr.Zero)
            {
                NativeMethods.SelectObject(memory, previous);
            }

            if (bitmap != IntPtr.Zero)
            {
                NativeMethods.DeleteObject(bitmap);
            }

            if (memory != IntPtr.Zero)
            {
                NativeMethods.DeleteDC(memory);
            }

            if (screen != IntPtr.Zero)
            {
                NativeMethods.ReleaseDC(IntPtr.Zero, screen);
            }
        }
    }

    private static ScreenCaptureIssue IssueOf(int win32Error) =>
        win32Error is NativeMethods.ErrorNotEnoughMemory or NativeMethods.ErrorOutOfMemory
            ? ScreenCaptureIssue.OutOfMemory
            : ScreenCaptureIssue.Failed;

    // The pointer is not part of a BitBlt: it is drawn on top at its position minus its hot spot. A pointer that cannot be drawn
    // (hidden, or a touch/pen session that suppresses it) leaves the picture as it is; the screenshot itself is still right.
    private static void DrawCursor(IntPtr memory, PixelRect area)
    {
        var cursor = new NativeMethods.CursorInfo { Size = Marshal.SizeOf<NativeMethods.CursorInfo>() };
        if (!NativeMethods.GetCursorInfo(ref cursor) || (cursor.Flags & NativeMethods.CursorShowing) == 0 || cursor.Cursor == IntPtr.Zero)
        {
            return;
        }

        var hotspotX = 0;
        var hotspotY = 0;
        if (NativeMethods.GetIconInfo(cursor.Cursor, out var icon))
        {
            hotspotX = (int)icon.HotspotX;
            hotspotY = (int)icon.HotspotY;

            // GetIconInfo makes copies of the bitmaps; the caller must delete them (GetIconInfo page).
            if (icon.Mask != IntPtr.Zero)
            {
                NativeMethods.DeleteObject(icon.Mask);
            }

            if (icon.Color != IntPtr.Zero)
            {
                NativeMethods.DeleteObject(icon.Color);
            }
        }

        NativeMethods.DrawIconEx(
            memory,
            cursor.ScreenPosition.X - hotspotX - area.X,
            cursor.ScreenPosition.Y - hotspotY - area.Y,
            cursor.Cursor,
            0,
            0,
            0,
            IntPtr.Zero,
            NativeMethods.DiNormal);
    }
}
