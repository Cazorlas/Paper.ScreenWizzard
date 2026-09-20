using Paper.ScreenWizzard.App.Mvvm;
using Paper.ScreenWizzard.UseCases.Common.Models;
using Paper.ScreenWizzard.UseCases.Common.Ports;
using Paper.ScreenWizzard.UseCases.Shell.Models;
using Paper.ScreenWizzard.UseCases.Shell.Ports;

namespace Paper.ScreenWizzard.App.ViewModels.Shell.Commands;

/// <summary>
/// Save (Enter). The steps run in the order the SPEC gives each its way to fail, and a refused step stops the save with the
/// window still open, so the user can fix the one thing and press Enter again:
/// folder (F5), hotkeys (F4), the settings file (F2), then "start with Windows" (F3).
/// </summary>
public sealed class SaveSettingsCommand : CommandBase
{
    private readonly SettingsViewModel _owner;
    private readonly IShellInteractor _shell;
    private readonly ISettingsPrompts _prompts;
    private readonly IAppearanceService _appearance;
    private readonly INotificationPort _notifications;
    private readonly string _systemCultureName;

    public SaveSettingsCommand(
        SettingsViewModel owner,
        IShellInteractor shell,
        ISettingsPrompts prompts,
        IAppearanceService appearance,
        INotificationPort notifications,
        string systemCultureName)
    {
        _owner = owner;
        _shell = shell;
        _prompts = prompts;
        _appearance = appearance;
        _notifications = notifications;
        _systemCultureName = systemCultureName;
    }

    public override void Execute(object? parameter)
    {
        _owner.ShowMessage(null);

        // The start-up entry goes before Apply on purpose: SetAutostart does not save, it only returns the settings with the
        // new switch, so Apply must run after it to write that value to the file (and a refusal must stop before anything
        // says the switch is on).
        if (!SaveFolderIsUsable() || !HotkeysAreAccepted() || !AutostartIsAccepted())
        {
            return;
        }

        // Settings the use case now holds (with the accepted hotkeys and switch) plus what was edited in the other groups.
        var applied = _shell.Apply(_owner.BuildCandidate());
        _owner.Current = applied.Settings;
        if (applied.Message is not null)
        {
            // Not saved (F2): the app keeps the values for this session, so the window still closes, but the user must be
            // told loudly that the change is lost on exit. A message on a saved result is only a notice.
            if (applied.Saved)
            {
                _notifications.ShowToast(applied.Message);
            }
            else
            {
                _notifications.ShowError(applied.Message);
            }
        }

        // Language and theme take effect now, on every open window, without a restart (SPEC shell).
        _appearance.ApplyLanguage(_shell.ResolveLanguage(_owner.Language, _systemCultureName));
        _appearance.ApplyTheme(_owner.Theme);

        _owner.RequestClose(true);
    }

    // F5: a folder that does not exist is created only when the user says yes; on no the old folder stays.
    private bool SaveFolderIsUsable()
    {
        var folder = _owner.SaveFolder;
        switch (_shell.CheckSaveFolder(folder))
        {
            case FolderCheck.Exists:
                return true;

            case FolderCheck.MissingCanCreate:
                if (!_prompts.AskCreateFolder(folder))
                {
                    _owner.SaveFolder = _owner.Current.SaveFolder;
                    _owner.ShowMessage(NotificationMessage.Of("Shell.FolderMissing", folder));
                    return false;
                }

                var created = _shell.CreateSaveFolder(folder);
                if (!created.Created)
                {
                    _owner.ShowMessage(NotificationMessage.Of("Shell.SettingsNotSaved", created.Detail ?? folder));
                    return false;
                }

                return true;

            default:
                _owner.ShowMessage(NotificationMessage.Of("Shell.FolderMissing", folder));
                return false;
        }
    }

    // F4: a refused chord goes back to the one that works, and the reason is shown. The others are still tried.
    private bool HotkeysAreAccepted()
    {
        var allAccepted = true;
        foreach (var row in _owner.Hotkeys)
        {
            if (row.Chord == row.SavedChord)
            {
                continue;
            }

            var result = _shell.ChangeHotkey(_owner.Current, row.Kind, row.Chord);
            if (result.Accepted)
            {
                _owner.Current = result.Settings;
                row.SavedChord = row.Chord;
                continue;
            }

            row.Chord = row.SavedChord;
            if (allAccepted)
            {
                _owner.ShowMessage(result.Message);
            }

            allAccepted = false;
        }

        return allAccepted;
    }

    // F3: when Windows will not take the start-up entry the switch returns to its old state and the reason shows.
    private bool AutostartIsAccepted()
    {
        if (_owner.StartWithWindows == _owner.Current.StartWithWindows)
        {
            return true;
        }

        var result = _shell.SetAutostart(_owner.Current, _owner.StartWithWindows);
        _owner.Current = result.Settings;
        if (result.Message is null)
        {
            return true;
        }

        _owner.StartWithWindows = result.Enabled;
        _owner.ShowMessage(result.Message);
        return false;
    }
}

/// <summary>Cancel (Esc): closes the window; nothing the user edited reaches the use case.</summary>
public sealed class CancelSettingsCommand : CommandBase
{
    private readonly SettingsViewModel _owner;

    public CancelSettingsCommand(SettingsViewModel owner)
    {
        _owner = owner;
    }

    public override void Execute(object? parameter) => _owner.RequestClose(false);
}

/// <summary>The Browse button next to the save folder.</summary>
public sealed class BrowseFolderCommand : CommandBase
{
    private readonly SettingsViewModel _owner;
    private readonly IFolderPickerService _folderPicker;

    public BrowseFolderCommand(SettingsViewModel owner, IFolderPickerService folderPicker)
    {
        _owner = owner;
        _folderPicker = folderPicker;
    }

    public override void Execute(object? parameter)
    {
        var picked = _folderPicker.PickFolder(_owner.SaveFolder);
        if (!string.IsNullOrWhiteSpace(picked))
        {
            _owner.SaveFolder = picked;
        }
    }
}
