using System.Diagnostics;

namespace Paper.ScreenWizzard.E2eTests.Support;

/// <summary>
/// The drive tests click the real notification area and send real keys. If the owner's own copy of the app is running, its tray icon and
/// windows are on the same desktop and a click meant for the test's copy can land on it, so the drive tests refuse to start then.
/// </summary>
public static class HarnessGuard
{
    private const string ProcessName = "Paper.ScreenWizzard";

    /// <summary>The processes of the app that this test run did not start (<paramref name="ownedIds"/> are the ones it did).</summary>
    public static IReadOnlyList<Process> OtherCopies(IEnumerable<int> ownedIds)
    {
        var owned = ownedIds.ToHashSet();
        return Process.GetProcessesByName(ProcessName).Where(p => !owned.Contains(p.Id)).ToList();
    }

    /// <summary>The message a drive test stops with when another copy runs, or null when the desktop is clear.</summary>
    public static string? Refusal(IEnumerable<int> ownedIds)
    {
        var others = OtherCopies(ownedIds);
        return others.Count == 0
            ? null
            : $"A copy of {ProcessName} that this test run did not start is running (process {string.Join(", ", others.Select(p => p.Id))}). "
              + "Its tray icon and windows share this desktop with the drive tests, so a click could land on it. Close it and run the tests again.";
    }
}
