using Paper.ScreenWizzard.Domain.Shell;

namespace Paper.ScreenWizzard.App.Startup;

/// <summary>
/// The settings the running app uses, read through a <c>Func&lt;AppSettings&gt;</c> by the capture and editor flows so a change made in
/// Settings applies to the next capture at once. Held by the container (one instance), not in a static.
/// </summary>
public sealed class SettingsHolder
{
    public SettingsHolder(AppSettings initial)
    {
        Current = initial;
    }

    public AppSettings Current { get; set; }
}
