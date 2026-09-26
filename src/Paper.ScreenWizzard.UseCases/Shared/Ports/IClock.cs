namespace Paper.ScreenWizzard.UseCases.Shared.Ports;

/// <summary>The local time, so a file name is decided by a test and not by the wall clock.</summary>
public interface IClock
{
    DateTime Now { get; }
}
