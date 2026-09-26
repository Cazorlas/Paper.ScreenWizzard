namespace Paper.ScreenWizzard.Domain.Shell;

/// <summary>When the app looks for a new version and where it sends the user for it (SPEC shell, "Báo bản mới").</summary>
public static class UpdateRules
{
    /// <summary>The GitHub repository whose published releases are the app's versions.</summary>
    public const string Repository = "Cazorlas/Paper.ScreenWizzard";

    /// <summary>After start, so a start at sign-in does not wait on the network and the tray is up before a notice.</summary>
    public static readonly TimeSpan FirstCheckDelay = TimeSpan.FromMinutes(1);

    /// <summary>Then once a day, for an app that stays in the tray for weeks.</summary>
    public static readonly TimeSpan CheckInterval = TimeSpan.FromDays(1);

    /// <summary>
    /// The page of that version's release. Built from the version, never taken from the answer of the network, so the app only ever
    /// opens a page of this repository's releases.
    /// </summary>
    public static string ReleasePage(AppVersion version) => $"https://github.com/{Repository}/releases/tag/v{version}";
}
