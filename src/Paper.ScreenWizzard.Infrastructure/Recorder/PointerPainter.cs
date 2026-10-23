using System.Runtime.InteropServices;
using Paper.ScreenWizzard.Domain.Shared;

namespace Paper.ScreenWizzard.Infrastructure.Recorder;

/// <summary>
/// Draws the cursor Windows shows now onto a BGRA picture of a desktop rectangle, as Windows draws it (<c>DrawIconEx</c>), so every
/// kind of cursor looks right: arrows, text beams that invert what is under them, the big accessibility cursors. The piece of picture
/// under the cursor goes into a small GDI bitmap, the cursor is drawn on it, and it comes back. Positions are physical pixels.
/// </summary>
internal static class PointerPainter
{
    private const int Box = 256;

    public static void Paint(byte[] picture, PixelRect area)
    {
        var cursor = new NativeMethods.CursorInfo { Size = Marshal.SizeOf<NativeMethods.CursorInfo>() };
        if (!NativeMethods.GetCursorInfo(ref cursor) || (cursor.Flags & NativeMethods.CursorShowing) == 0 || cursor.Cursor == IntPtr.Zero)
        {
            return;
        }

        var hotX = 0;
        var hotY = 0;
        if (NativeMethods.GetIconInfo(cursor.Cursor, out var icon))
        {
            hotX = (int)icon.HotspotX;
            hotY = (int)icon.HotspotY;

            // GetIconInfo makes copies of the bitmaps; the caller deletes them (GetIconInfo page).
            if (icon.Mask != IntPtr.Zero)
            {
                NativeMethods.DeleteObject(icon.Mask);
            }

            if (icon.Color != IntPtr.Zero)
            {
                NativeMethods.DeleteObject(icon.Color);
            }
        }

        var left = cursor.ScreenPosition.X - hotX - area.X;
        var top = cursor.ScreenPosition.Y - hotY - area.Y;
        if (left >= area.Width || top >= area.Height || left + Box <= 0 || top + Box <= 0)
        {
            return;
        }

        var screen = NativeMethods.GetDC(IntPtr.Zero);
        var memory = NativeMethods.CreateCompatibleDC(screen);
        var header = new NativeMethods.BitmapInfo
        {
            HeaderSize = 40,
            Width = Box,
            Height = -Box, // top-down
            Planes = 1,
            BitCount = 32,
        };
        var bitmap = NativeMethods.CreateDIBSection(memory, ref header, NativeMethods.DibRgbColors, out var bits, IntPtr.Zero, 0);
        var old = NativeMethods.SelectObject(memory, bitmap);
        try
        {
            Transfer(picture, area.Width, area.Height, bits, left, top, toBitmap: true);
            NativeMethods.DrawIconEx(memory, 0, 0, cursor.Cursor, 0, 0, 0, IntPtr.Zero, NativeMethods.DiNormal);
            NativeMethods.GdiFlush();
            Transfer(picture, area.Width, area.Height, bits, left, top, toBitmap: false);
        }
        finally
        {
            NativeMethods.SelectObject(memory, old);
            NativeMethods.DeleteObject(bitmap);
            NativeMethods.DeleteDC(memory);
            NativeMethods.ReleaseDC(IntPtr.Zero, screen);
        }
    }

    // Copies the part of the box that lies on the picture, one row at a time; on the way back the pixels are made opaque.
    private static void Transfer(byte[] picture, int width, int height, IntPtr bits, int left, int top, bool toBitmap)
    {
        var fromX = Math.Max(0, -left);
        var toX = Math.Min(Box, width - left);
        if (toX <= fromX)
        {
            return;
        }

        var row = new byte[(toX - fromX) * 4];
        for (var y = Math.Max(0, -top); y < Box && top + y < height; y++)
        {
            var pictureOffset = (((top + y) * width) + left + fromX) * 4;
            var bitmapOffset = ((y * Box) + fromX) * 4;
            if (toBitmap)
            {
                Marshal.Copy(picture, pictureOffset, bits + bitmapOffset, row.Length);
            }
            else
            {
                Marshal.Copy(bits + bitmapOffset, row, 0, row.Length);
                for (var i = 3; i < row.Length; i += 4)
                {
                    row[i] = 255;
                }

                Buffer.BlockCopy(row, 0, picture, pictureOffset, row.Length);
            }
        }
    }
}
