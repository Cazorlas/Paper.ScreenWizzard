namespace Paper.ScreenWizzard.App;

/// <summary>
/// What the exe reads before it builds anything: two environment variables and one flag. The variables let a test (or a second profile) run
/// its own copy with its own settings folder, mutex and Run value, so it never touches the real ones; unset, they are the real defaults.
/// </summary>
/// <param name="DataRoot">The folder that holds <c>configs\settings.json</c> and <c>logs\</c>.</param>
/// <param name="InstanceName">The name of the mutex, the wake event, the Run value and the hotkey window.</param>
/// <param name="Autostart">Started by Windows at logon: only the tray icon appears, no window (SPEC shell).</param>
public sealed record StartupOptions(string DataRoot, string InstanceName, bool Autostart)
{
    public const string DataVariable = "PAPER_SCREENWIZZARD_DATA";
    public const string InstanceVariable = "PAPER_SCREENWIZZARD_INSTANCE";
    public const string DefaultInstanceName = "Paper.ScreenWizzard";

    public static StartupOptions From(string[] args, Func<string, string?> environment)
    {
        var data = environment(DataVariable);
        var instance = environment(InstanceVariable);
        return new StartupOptions(
            string.IsNullOrWhiteSpace(data)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Paper", "ScreenWizzard")
                : data,
            string.IsNullOrWhiteSpace(instance) ? DefaultInstanceName : instance,
            args.Contains(AppShell.AutostartFlag, StringComparer.OrdinalIgnoreCase));
    }
}
