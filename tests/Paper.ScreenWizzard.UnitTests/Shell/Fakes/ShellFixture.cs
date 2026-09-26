using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UseCases.Shell.UseCases;

namespace Paper.ScreenWizzard.UnitTests.Shell.Fakes;

/// <summary>The interactor wired to plain-data fakes.</summary>
public sealed class ShellFixture
{
    public FakeSettingsStore Store { get; } = new();

    public FakeHotkeys Hotkeys { get; } = new();

    public FakeAutostart Autostart { get; } = new();

    public FakeFileStore Files { get; } = new();

    public FakeLog Log { get; } = new();

    public ShellInteractor Create() => new(Store, Hotkeys, Autostart, Files, Log);
}

/// <summary>Numbers and shapes written out from SPEC shell, so a test never asks the code for its own expected value.</summary>
public static class ShellData
{
    public const string Pictures = @"C:\Users\Hung\Pictures";

    public static HotkeyChord Chord(HotkeyModifiers modifiers, string key) => new(modifiers, key);

    public static HotkeyChord PrintScreen => new(HotkeyModifiers.None, "PrintScreen");

    public static HotkeyChord ShiftPrintScreen => new(HotkeyModifiers.Shift, "PrintScreen");

    public static HotkeyChord AltPrintScreen => new(HotkeyModifiers.Alt, "PrintScreen");

    public static HotkeyChord CtrlPrintScreen => new(HotkeyModifiers.Control, "PrintScreen");

    public static MonitorInfo Monitor(int index, int x, int y, int width, int height, bool primary) =>
        new(index, new PixelRect(x, y, width, height), primary, 96);

    /// <summary>One 1920 x 1080 monitor at the origin.</summary>
    public static IReadOnlyList<MonitorInfo> OnePrimary { get; } = [Monitor(0, 0, 0, 1920, 1080, true)];

    /// <summary>The settings of SPEC shell "Inputs", typed by hand.</summary>
    public static AppSettings SpecDefaults() => new(
        new Dictionary<CaptureKind, HotkeyChord>
        {
            [CaptureKind.Rectangle] = PrintScreen,
            [CaptureKind.Freeform] = ShiftPrintScreen,
            [CaptureKind.Window] = AltPrintScreen,
            [CaptureKind.FullScreen] = CtrlPrintScreen,
        },
        AfterCaptureAction.ShowDialog,
        @"C:\Users\Hung\Pictures\Paper.ScreenWizzard",
        ImageFormat.Png,
        90,
        0,
        false,
        FullScreenScope.MonitorUnderCursor,
        false,
        AppLanguage.System,
        AppTheme.System,
        null);

    /// <summary>The defaults registered on a fake port, as a running app would have them.</summary>
    public static void RegisterAll(FakeHotkeys port, AppSettings settings)
    {
        foreach (var pair in settings.Hotkeys)
        {
            port.Register(pair.Key, pair.Value);
        }

        port.RegisterCalls.Clear();
    }
}
