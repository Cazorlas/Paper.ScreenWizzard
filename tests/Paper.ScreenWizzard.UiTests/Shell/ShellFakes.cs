using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.Presentation.ViewModels.Shell;
using Paper.ScreenWizzard.UseCases.Shared.Models;
using Paper.ScreenWizzard.UseCases.Shared.Ports;
using Paper.ScreenWizzard.UseCases.Shell.Models;
using Paper.ScreenWizzard.UseCases.Shell.Ports;

namespace Paper.ScreenWizzard.UiTests.Shell;

/// <summary>The defaults of SPEC shell, "Inputs", as plain data for a view model to start from.</summary>
public static class ShellTestData
{
    public const string SaveFolder = @"C:\Users\Test\Pictures\Paper.ScreenWizzard";

    public static IReadOnlyDictionary<CaptureKind, HotkeyChord> DefaultHotkeys { get; } = new Dictionary<CaptureKind, HotkeyChord>
    {
        [CaptureKind.Rectangle] = new(HotkeyModifiers.None, "PrintScreen"),
        [CaptureKind.Freeform] = new(HotkeyModifiers.Shift, "PrintScreen"),
        [CaptureKind.Window] = new(HotkeyModifiers.Alt, "PrintScreen"),
        [CaptureKind.FullScreen] = new(HotkeyModifiers.Control, "PrintScreen"),
    };

    public static AppSettings DefaultSettings() => new(
        DefaultHotkeys,
        AfterCaptureAction.ShowDialog,
        SaveFolder,
        ImageFormat.Png,
        90,
        0,
        false,
        FullScreenScope.MonitorUnderCursor,
        false,
        AppLanguage.System,
        AppTheme.System,
        null);
}

/// <summary>
/// A fake of the shell use case with canned answers, so the view model is proven on mock data and never on the real rules
/// (those are the logic lane's, tested in the unit project). Every call is recorded for the test to read.
/// </summary>
public sealed class FakeShellInteractor : IShellInteractor
{
    public List<(CaptureKind Kind, HotkeyChord Chord)> HotkeyChanges { get; } = [];

    public List<AppSettings> Applied { get; } = [];

    public List<string> FoldersChecked { get; } = [];

    public List<string> FoldersCreated { get; } = [];

    public List<bool> AutostartChanges { get; } = [];

    /// <summary>What CheckSaveFolder answers; Exists unless a test says the folder is missing.</summary>
    public FolderCheck FolderAnswer { get; set; } = FolderCheck.Exists;

    public FolderCreateResult CreateAnswer { get; set; } = new(true, null);

    /// <summary>When set, ChangeHotkey refuses with this issue and message and leaves the settings alone.</summary>
    public (HotkeyIssue Issue, CaptureKind? Conflict, NotificationMessage Message)? RefuseHotkeyWith { get; set; }

    /// <summary>When set, Apply reports a failed write with this message (SPEC shell F2).</summary>
    public NotificationMessage? ApplyFailsWith { get; set; }

    /// <summary>When set, SetAutostart fails with this message and the switch stays as it was (SPEC shell F3).</summary>
    public NotificationMessage? AutostartFailsWith { get; set; }

    public ResolvedLanguage LanguageAnswer { get; set; } = ResolvedLanguage.Vietnamese;

    public ShellStartResult Start(ShellStartInput input) => throw new NotSupportedException("Settings never starts the shell");

    public AppSettings CreateDefaultSettings(string picturesFolder) => ShellTestData.DefaultSettings();

    public HotkeyChangeResult ChangeHotkey(AppSettings current, CaptureKind kind, HotkeyChord proposed)
    {
        HotkeyChanges.Add((kind, proposed));
        if (RefuseHotkeyWith is { } refusal)
        {
            return new HotkeyChangeResult(false, refusal.Issue, refusal.Conflict, current, refusal.Message);
        }

        var hotkeys = new Dictionary<CaptureKind, HotkeyChord>(current.Hotkeys) { [kind] = proposed };
        return new HotkeyChangeResult(true, HotkeyIssue.None, null, current with { Hotkeys = hotkeys }, null);
    }

    public SettingsApplyResult Apply(AppSettings settings)
    {
        Applied.Add(settings);
        return ApplyFailsWith is { } message
            ? new SettingsApplyResult(false, settings, message)
            : new SettingsApplyResult(true, settings, null);
    }

    public AutostartChangeResult SetAutostart(AppSettings current, bool enabled)
    {
        AutostartChanges.Add(enabled);
        return AutostartFailsWith is { } message
            ? new AutostartChangeResult(current.StartWithWindows, current, message)
            : new AutostartChangeResult(enabled, current with { StartWithWindows = enabled }, null);
    }

    public FolderCheck CheckSaveFolder(string folder)
    {
        FoldersChecked.Add(folder);
        return FolderAnswer;
    }

    public FolderCreateResult CreateSaveFolder(string folder)
    {
        FoldersCreated.Add(folder);
        return CreateAnswer;
    }

    public SecondInstanceResult OnSecondInstanceLaunched() => new(true);

    public PixelPoint PlaceCaptureBar(PixelPoint? saved, IReadOnlyList<MonitorInfo> monitors, PixelSize barSize) => default;

    public ResolvedLanguage ResolveLanguage(AppLanguage language, string systemCultureName) => language switch
    {
        AppLanguage.Vietnamese => ResolvedLanguage.Vietnamese,
        AppLanguage.English => ResolvedLanguage.English,
        _ => LanguageAnswer,
    };
}

public sealed class FakeFolderPicker : IFolderPickerService
{
    public string? Answer { get; set; }

    public List<string?> Asked { get; } = [];

    public string? PickFolder(string? initialFolder)
    {
        Asked.Add(initialFolder);
        return Answer;
    }
}

/// <summary>Used where the real prompt is not the subject; answers what the test sets.</summary>
public sealed class FakePrompts : ISettingsPrompts
{
    public bool Answer { get; set; }

    public List<string> Asked { get; } = [];

    public bool AskCreateFolder(string folder)
    {
        Asked.Add(folder);
        return Answer;
    }
}

/// <summary>An INotifications that only records, for the tests that ask "did the view model tell the user".</summary>
public sealed class RecordingNotifications : INotifications
{
    public List<NotificationMessage> Toasts { get; } = [];

    public List<NotificationMessage> Errors { get; } = [];

    public void ShowToast(NotificationMessage message) => Toasts.Add(message);

    public void ShowError(NotificationMessage message) => Errors.Add(message);
}
