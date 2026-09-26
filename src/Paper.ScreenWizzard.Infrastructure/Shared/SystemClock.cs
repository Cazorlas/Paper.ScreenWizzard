using Paper.ScreenWizzard.UseCases.Shared.Ports;

namespace Paper.ScreenWizzard.Infrastructure.Shared;

/// <summary>The wall clock (local time).</summary>
public sealed class SystemClock : IClock
{
    public DateTime Now => DateTime.Now;
}
