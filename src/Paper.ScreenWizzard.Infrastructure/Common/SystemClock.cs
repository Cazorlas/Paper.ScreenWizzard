using Paper.ScreenWizzard.UseCases.Common.Ports;

namespace Paper.ScreenWizzard.Infrastructure.Common;

/// <summary>The wall clock (local time).</summary>
public sealed class SystemClock : IClockPort
{
    public DateTime Now => DateTime.Now;
}
