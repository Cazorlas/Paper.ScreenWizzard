using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Paper.ScreenWizzard.App.Startup;

/// <summary>
/// The tray icon, drawn in code: a blue rounded square with four white corner marks, like a camera's viewfinder. There is no icon file to
/// ship or to lose, and it needs no design tool. Drawn at 32 x 32, which Windows scales to the tray's size.
/// </summary>
internal static class TrayIconImage
{
    public static Icon Create()
    {
        using var bitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            using var background = new SolidBrush(Color.FromArgb(255, 0, 103, 192));
            using var path = RoundedSquare(1, 1, 30, 7);
            graphics.FillPath(background, path);

            using var pen = new Pen(Color.White, 2.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            const int Near = 8;
            const int Far = 24;
            const int Arm = 6;
            graphics.DrawLines(pen, [new Point(Near, Near + Arm), new Point(Near, Near), new Point(Near + Arm, Near)]);
            graphics.DrawLines(pen, [new Point(Far - Arm, Near), new Point(Far, Near), new Point(Far, Near + Arm)]);
            graphics.DrawLines(pen, [new Point(Near, Far - Arm), new Point(Near, Far), new Point(Near + Arm, Far)]);
            graphics.DrawLines(pen, [new Point(Far - Arm, Far), new Point(Far, Far), new Point(Far, Far - Arm)]);
            using var dot = new SolidBrush(Color.White);
            graphics.FillEllipse(dot, 13, 13, 6, 6);
        }

        // GetHicon makes a GDI icon the caller must destroy; Clone copies it into a managed Icon first.
        var handle = bitmap.GetHicon();
        try
        {
            using var borrowed = Icon.FromHandle(handle);
            return (Icon)borrowed.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }

    private static GraphicsPath RoundedSquare(int x, int y, int size, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(x, y, diameter, diameter, 180, 90);
        path.AddArc(x + size - diameter, y, diameter, diameter, 270, 90);
        path.AddArc(x + size - diameter, y + size - diameter, diameter, diameter, 0, 90);
        path.AddArc(x, y + size - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
