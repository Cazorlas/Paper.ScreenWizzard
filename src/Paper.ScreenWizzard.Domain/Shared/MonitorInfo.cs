namespace Paper.ScreenWizzard.Domain.Shared;

/// <summary>A monitor of the virtual desktop.</summary>
/// <param name="Bounds">Its rectangle in virtual-desktop pixels; X and Y may be negative.</param>
/// <param name="Dpi">The monitor's DPI; 96 is 100%.</param>
public sealed record MonitorInfo(int Index, PixelRect Bounds, bool IsPrimary, int Dpi);
