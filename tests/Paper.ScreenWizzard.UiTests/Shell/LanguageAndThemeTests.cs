using System.Windows;
using System.Windows.Media;
using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UiTests.Support;
using Paper.ScreenWizzard.UseCases.Shell.Models;

namespace Paper.ScreenWizzard.UiTests.Shell;

/// <summary>
/// SPEC shell "Ngôn ngữ và giao diện" on the resource files themselves (plan T5): one shared key set in Vietnamese and English,
/// and text 4.5:1 / icons and borders 3:1 in BOTH themes, measured from the brushes and not judged by eye.
/// </summary>
[TestFixture]
public sealed class LanguageAndThemeTests : UiTestBase
{
    /// <summary>Text on its background: the ratio must reach 4.5.</summary>
    private static readonly (string Foreground, string Background)[] _textPairs =
    [
        ("Brush.Text", "Brush.Window"),
        ("Brush.Text", "Brush.Surface"),
        ("Brush.TextMuted", "Brush.Window"),
        ("Brush.TextMuted", "Brush.Surface"),
        ("Brush.Text", "Brush.ButtonBackground"),
        ("Brush.Text", "Brush.ButtonHover"),
        ("Brush.Text", "Brush.ButtonPressed"),
        ("Brush.AccentText", "Brush.Accent"),
        ("Brush.AccentText", "Brush.AccentHover"),
        ("Brush.AccentText", "Brush.AccentPressed"),
        ("Brush.Error", "Brush.Window"),
        ("Brush.Error", "Brush.Surface"),
        ("Brush.ToastText", "Brush.ToastBackground"),
    ];

    /// <summary>Icons, borders and the focus ring against what they are drawn on: the ratio must reach 3.</summary>
    private static readonly (string Foreground, string Background)[] _graphicPairs =
    [
        ("Brush.Border", "Brush.Window"),
        ("Brush.Border", "Brush.Surface"),
        ("Brush.Icon", "Brush.Window"),
        ("Brush.Icon", "Brush.Surface"),
        ("Brush.Icon", "Brush.ButtonBackground"),
        ("Brush.Focus", "Brush.Window"),
        ("Brush.Focus", "Brush.Surface"),
        ("Brush.Focus", "Brush.ButtonBackground"),
        ("Brush.Accent", "Brush.Window"),
    ];

    /// <summary>The keys the logic lane's catalogue and the three shell windows use; both languages must define every one.</summary>
    private static readonly string[] _requiredStringKeys =
    [
        "Shell.SettingsCorrupt",
        "Shell.SettingsNotSaved",
        "Shell.AutostartFailed",
        "Shell.HotkeyNeedsModifier",
        "Shell.HotkeyUsedByOtherKind",
        "Shell.HotkeyHeldByAnotherProgram",
        "Shell.HotkeyUnavailableAtStart",
        "Shell.FolderMissing",
        "Capture.Kind.Rectangle",
        "Capture.Kind.Freeform",
        "Capture.Kind.Window",
        "Capture.Kind.FullScreen",
        "Tray.CaptureRectangle",
        "Tray.CaptureFreeform",
        "Tray.CaptureWindow",
        "Tray.CaptureFullScreen",
        "Tray.OpenImage",
        "Tray.CaptureBar",
        "Tray.Settings",
        "Tray.Exit",
        "Settings.Save",
        "Settings.Cancel",
    ];

    [Test]
    public void Language_ViAndEn_ShareOneKeySetThatCoversTheShell()
    {
        var vi = ResourceFiles.Load("Strings.vi.xaml");
        var en = ResourceFiles.Load("Strings.en.xaml");
        var viKeys = vi.Keys.Cast<object>().Select(k => k.ToString()!).ToHashSet();
        var enKeys = en.Keys.Cast<object>().Select(k => k.ToString()!).ToHashSet();

        Assert.Multiple(() =>
        {
            Assert.That(viKeys.Except(enKeys), Is.Empty, "keys the English file lacks");
            Assert.That(enKeys.Except(viKeys), Is.Empty, "keys the Vietnamese file lacks");
            Assert.That(_requiredStringKeys.Except(viKeys), Is.Empty, "keys the shell needs and Strings.vi.xaml lacks");
            Assert.That(viKeys.Where(k => vi[k] is string text && string.IsNullOrWhiteSpace(text)), Is.Empty, "empty Vietnamese texts");
            Assert.That(enKeys.Where(k => en[k] is string text && string.IsNullOrWhiteSpace(text)), Is.Empty, "empty English texts");
        });
    }

    [Test]
    public void Language_Apply_SwapsTextImmediatelyAndRaisesTheEvent()
    {
        var host = WpfHost.Instance;
        var raised = 0;
        host.Invoke(() =>
        {
            host.Language.Apply(ResolvedLanguage.Vietnamese);
            host.Language.LanguageChanged += (_, _) => raised++;
        });
        Assert.That(host.Invoke(() => host.Language.GetString("Settings.Save")), Is.EqualTo("Lưu"));

        host.Invoke(() => host.Language.Apply(ResolvedLanguage.English));

        Assert.That(host.Invoke(() => host.Language.GetString("Settings.Save")), Is.EqualTo("Save"));
        Assert.That(raised, Is.EqualTo(1), "view models that hold computed text need to hear that the language changed");
    }

    [Test]
    public void Language_UnknownKey_ShowsTheKeyInsteadOfBlank()
    {
        var host = WpfHost.Instance;

        var text = host.Invoke(() => host.Language.GetString("No.Such.Key"));

        Assert.That(host.Invoke(() => host.Language.GetString("Settings.Save")), Is.Not.EqualTo("Settings.Save"), "a key that exists must give its text");
        Assert.That(text, Is.EqualTo("No.Such.Key"), "a missing key must stay visible so it gets fixed, never blank");
    }

    [TestCase(AppTheme.Light)]
    [TestCase(AppTheme.Dark)]
    public void Contrast_TextReaches4Point5AndIconsAndBordersReach3(AppTheme theme)
    {
        var dictionary = ResourceFiles.Load($"Themes/{theme}.xaml");
        var problems = new List<string>();

        Check(dictionary, _textPairs, Contrast.MinimumForText, problems);
        Check(dictionary, _graphicPairs, Contrast.MinimumForIconOrBorder, problems);

        Assert.That(problems, Is.Empty, $"{theme} theme:\n{string.Join('\n', problems)}");
    }

    [Test]
    public void Themes_LightAndDark_DefineTheSameBrushesAndTheyDiffer()
    {
        var light = ResourceFiles.Load("Themes/Light.xaml");
        var dark = ResourceFiles.Load("Themes/Dark.xaml");
        var required = _textPairs.Concat(_graphicPairs).SelectMany(pair => new[] { pair.Foreground, pair.Background }).Distinct().ToList();

        Assert.Multiple(() =>
        {
            Assert.That(required.Where(key => !light.Contains(key)), Is.Empty, "brushes missing from Light.xaml");
            Assert.That(required.Where(key => !dark.Contains(key)), Is.Empty, "brushes missing from Dark.xaml");
            Assert.That(light.Keys.Cast<object>().Select(k => k.ToString()), Is.EquivalentTo(dark.Keys.Cast<object>().Select(k => k.ToString())), "the two themes must define one key set");
            Assert.That(Contrast.ColorOf(light["Brush.Window"] as Brush ?? Brushes.Transparent), Is.Not.EqualTo(Contrast.ColorOf(dark["Brush.Window"] as Brush ?? Brushes.Transparent)), "dark must look different from light");
        });
    }

    [Test]
    public void Theme_Apply_PutsTheChosenBrushesInTheApplication()
    {
        var host = WpfHost.Instance;
        var light = ResourceFiles.Load("Themes/Light.xaml")["Brush.Window"] as SolidColorBrush;
        var dark = ResourceFiles.Load("Themes/Dark.xaml")["Brush.Window"] as SolidColorBrush;
        Assert.That(light, Is.Not.Null, "Brush.Window is missing from the Light theme");
        Assert.That(dark, Is.Not.Null, "Brush.Window is missing from the Dark theme");

        host.Invoke(() => host.Theme.Apply(AppTheme.Dark));
        var afterDark = host.Invoke(() => ((SolidColorBrush)Application.Current.FindResource("Brush.Window")).Color);
        host.Invoke(() => host.Theme.Apply(AppTheme.Light));
        var afterLight = host.Invoke(() => ((SolidColorBrush)Application.Current.FindResource("Brush.Window")).Color);

        Assert.That(afterDark, Is.EqualTo(dark!.Color));
        Assert.That(afterLight, Is.EqualTo(light!.Color));
    }

    private static void Check(
        ResourceDictionary dictionary,
        IEnumerable<(string Foreground, string Background)> pairs,
        double minimum,
        List<string> problems)
    {
        foreach (var (foreground, background) in pairs)
        {
            var front = Contrast.BrushIn(dictionary, foreground);
            var back = Contrast.BrushIn(dictionary, background);
            if (front is null || back is null)
            {
                problems.Add($"{foreground} on {background}: brush missing");
                continue;
            }

            var ratio = Contrast.Ratio(front, back);
            if (ratio < minimum)
            {
                problems.Add($"{foreground} on {background}: {ratio:0.00}:1, needs {minimum}:1");
            }
        }
    }
}
