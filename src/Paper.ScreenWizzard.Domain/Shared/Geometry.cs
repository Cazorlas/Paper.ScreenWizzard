namespace Paper.ScreenWizzard.Domain.Shared;
// Every coordinate in this app is a PHYSICAL pixel of the virtual desktop (CLAUDE.md). x and y may be negative when a
// monitor sits left of or above the primary one. Plain data only: the rules that use these live in UseCases.

/// <summary>A point in physical desktop pixels.</summary>
public readonly record struct PixelPoint(int X, int Y);

/// <summary>A size in physical pixels.</summary>
public readonly record struct PixelSize(int Width, int Height);

/// <summary>A rectangle in physical desktop pixels: top-left corner plus size. Width and height are never negative.</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height);

/// <summary>A colour with straight (not premultiplied) alpha.</summary>
public readonly record struct RgbaColor(byte R, byte G, byte B, byte A);

/// <summary>
/// A bitmap as bytes, so it can cross a port without a WPF or GDI type: 4 bytes per pixel in B, G, R, A order, straight
/// alpha, rows top to bottom with no padding (stride is Width * 4).
/// </summary>
public sealed record PixelImage(int Width, int Height, byte[] Bgra);
