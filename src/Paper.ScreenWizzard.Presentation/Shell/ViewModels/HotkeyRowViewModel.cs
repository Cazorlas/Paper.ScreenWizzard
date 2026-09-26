using Paper.ScreenWizzard.Presentation.Mvvm;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shell;

namespace Paper.ScreenWizzard.Presentation.Shell.ViewModels;

/// <summary>One row of the "Phím tắt" group: a capture kind and the chord the user is typing for it.</summary>
public sealed class HotkeyRowViewModel : BindableBase
{
    private HotkeyChord _chord;

    public HotkeyRowViewModel(CaptureKind kind, HotkeyChord chord)
    {
        Kind = kind;
        _chord = chord;
        SavedChord = chord;
    }

    public CaptureKind Kind { get; }

    /// <summary>The resource key of the kind's name, the row's label and the hotkey box's accessible name.</summary>
    public string LabelKey => "Capture.Kind." + Kind;

    /// <summary>What the box shows and what Save will try to register.</summary>
    public HotkeyChord Chord
    {
        get => _chord;
        set
        {
            if (SetProperty(ref _chord, value))
            {
                RaisePropertyChanged(nameof(DisplayText));
            }
        }
    }

    /// <summary>The chord the use case last accepted; a rejected change goes back to it (SPEC shell F4).</summary>
    public HotkeyChord SavedChord { get; internal set; }

    public string DisplayText => HotkeyChordFormatter.Format(_chord);
}
