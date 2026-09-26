namespace Paper.ScreenWizzard.UseCases.Shell.Ports;

/// <summary>Keeps two copies of the app from running at once.</summary>
public interface ISingleInstance
{
    /// <summary>True for the first copy; false when another copy already runs.</summary>
    bool TryBecomeFirstInstance();

    /// <summary>Tells the first copy that somebody launched the app again.</summary>
    void NotifyFirstInstance();

    event Action? SecondInstanceLaunched;
}
