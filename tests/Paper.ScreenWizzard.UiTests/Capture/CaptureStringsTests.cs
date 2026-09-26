using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Paper.ScreenWizzard.UiTests.Support;
using Paper.ScreenWizzard.UseCases.Shared.Models;

namespace Paper.ScreenWizzard.UiTests.ScreenCapture;

/// <summary>
/// The words of the capture feature in both languages (plan T9): every key the use case can put in a NotificationMessage reads
/// as a sentence, in the argument order the logic lane gives, and every key the capture windows name exists. An unknown key must
/// show itself and never render blank.
/// </summary>
[TestFixture]
public sealed class CaptureStringsTests : UiTestBase
{
    /// <summary>Exactly the messages the logic lane's capture use case emits, with the arguments it puts in ({0} {1} {2} in this order).</summary>
    private static readonly NotificationMessage[] _catalogue =
    [
        NotificationMessage.Of("Capture.RegionTooSmall"),
        NotificationMessage.Of("Capture.OutlineTooSmall"),
        NotificationMessage.Of("Capture.DisplayChanged"),
        NotificationMessage.Of("Capture.ImageTooLarge"),
        NotificationMessage.Of("Capture.Failed", "the screen could not be read"),
        NotificationMessage.Of("Capture.SavedToFile", @"D:\Shots\Screenshot 2026-09-20 14.03.05.png", "1280", "720"),
        NotificationMessage.Of("Capture.CopiedToClipboard", "1280", "720"),
        NotificationMessage.Of("Capture.SaveFailed", @"D:\ReadOnly", "access is denied"),
        NotificationMessage.Of("Capture.ClipboardFailed", "the clipboard is held by another program"),
    ];

    /// <summary>
    /// The use case answers a changed display with the bare issue (ICaptureSession.CheckDisplayUnchanged returns a CaptureIssue, no
    /// message), so the key of F6 is chosen in the App from that issue and never appears in the use case source.
    /// </summary>
    private static readonly string[] _keysTheAppBuildsFromAnIssue = ["Capture.DisplayChanged"];

    /// <summary>The labels of the overlay, the countdown and the "Đã chụp" dialog (their views name these keys).</summary>
    private static readonly string[] _labelKeys =
    [
        "Capture.Overlay.Title",
        "Capture.Overlay.CancelHint",
        "Capture.Countdown.Title",
        "Capture.Done.Title",
        "Capture.Done.Save",
        "Capture.Done.SaveAs",
        "Capture.Done.Copy",
        "Capture.Done.Edit",
        "Capture.Done.Discard",
        "Capture.Done.Thumbnail",
        "Capture.Done.SaveAsBoxTitle",
    ];

    [TestCase(ResolvedLanguage.Vietnamese)]
    [TestCase(ResolvedLanguage.English)]
    public void Messages_EveryKeyTheCaptureFeatureEmits_ReadsAsASentenceWithItsArgumentsInPlace(ResolvedLanguage language)
    {
        var host = WpfHost.Instance;
        host.Invoke(() => host.Language.Apply(language));

        foreach (var message in _catalogue)
        {
            var text = host.Invoke(() => host.Language.Format(message));

            Assert.That(text, Is.Not.Empty.And.Not.EqualTo(message.Key), $"{message.Key} has no {language} text");
            Assert.That(text, Does.Not.Contain("{"), $"{message.Key} ({language}) left a placeholder unfilled");
            foreach (var argument in message.Arguments)
            {
                Assert.That(text, Does.Contain(argument), $"{message.Key} ({language}) does not show '{argument}'");
            }
        }
    }

    [TestCase(ResolvedLanguage.Vietnamese, "Capture.RegionTooSmall", "quá nhỏ")]
    [TestCase(ResolvedLanguage.Vietnamese, "Capture.OutlineTooSmall", "quá nhỏ")]
    [TestCase(ResolvedLanguage.Vietnamese, "Capture.DisplayChanged", "chụp lại")]
    [TestCase(ResolvedLanguage.Vietnamese, "Capture.ImageTooLarge", "nhỏ hơn")]
    [TestCase(ResolvedLanguage.English, "Capture.RegionTooSmall", "too small")]
    [TestCase(ResolvedLanguage.English, "Capture.OutlineTooSmall", "too small")]
    [TestCase(ResolvedLanguage.English, "Capture.DisplayChanged", "changed")]
    [TestCase(ResolvedLanguage.English, "Capture.ImageTooLarge", "too large")]
    public void Messages_TheSpecsOwnWords_AreThere(ResolvedLanguage language, string key, string fragment)
    {
        var host = WpfHost.Instance;
        host.Invoke(() => host.Language.Apply(language));

        var text = host.Invoke(() => host.Language.Format(NotificationMessage.Of(key)));

        Assert.That(text, Is.Not.EqualTo(key), "an unknown key would show itself");
        Assert.That(text, Does.Contain(fragment).IgnoreCase);
    }

    [Test]
    public void Messages_SavedToFile_ShowsThePathAndTheSizeAsWidthByHeightOrThreeSeparateNumbers()
    {
        var host = WpfHost.Instance;
        host.Invoke(() => host.Language.Apply(ResolvedLanguage.English));

        var text = host.Invoke(() => host.Language.Format(NotificationMessage.Of("Capture.SavedToFile", @"C:\Pics\a.png", "300", "200")));

        Assert.That(text, Does.Contain(@"C:\Pics\a.png"));
        Assert.That(text.IndexOf("300", StringComparison.Ordinal), Is.LessThan(text.IndexOf("200", StringComparison.Ordinal)), "width before height");
    }

    [Test]
    public void Messages_SaveFailedAndClipboardFailed_TellTheUserTheImageIsStillInTheDialog()
    {
        var host = WpfHost.Instance;
        host.Invoke(() => host.Language.Apply(ResolvedLanguage.Vietnamese));

        var save = host.Invoke(() => host.Language.Format(NotificationMessage.Of("Capture.SaveFailed", @"D:\ReadOnly", "denied")));
        var clipboard = host.Invoke(() => host.Language.Format(NotificationMessage.Of("Capture.ClipboardFailed", "locked")));

        Assert.That(save, Does.Contain("Lưu thành").Or.Contain("Sao chép"), "F4: what else the user can press");
        Assert.That(clipboard, Does.Contain("clipboard").IgnoreCase);
    }

    [TestCase(ResolvedLanguage.Vietnamese)]
    [TestCase(ResolvedLanguage.English)]
    public void Messages_AnUnknownKey_NeverRendersBlank(ResolvedLanguage language)
    {
        var host = WpfHost.Instance;
        host.Invoke(() => host.Language.Apply(language));

        var text = host.Invoke(() => host.Language.Format(NotificationMessage.Of("Capture.NoSuchMessage", "x", "y")));

        Assert.That(text, Is.EqualTo("Capture.NoSuchMessage"), "a key nobody translated shows itself so the gap is seen");
    }

    [Test]
    public void Keys_ViAndEn_BothDefineEveryCaptureKeyAndNoOtherCaptureKeyIsOnlyInOne()
    {
        var vi = ResourceFiles.Load("Strings.vi.xaml");
        var en = ResourceFiles.Load("Strings.en.xaml");
        var needed = _catalogue.Select(m => m.Key).Concat(_labelKeys).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(needed.Where(key => !vi.Contains(key)), Is.Empty, "capture keys missing from Strings.vi.xaml");
            Assert.That(needed.Where(key => !en.Contains(key)), Is.Empty, "capture keys missing from Strings.en.xaml");
            Assert.That(
                needed.Where(key => vi[key] is string v && en[key] is string e && v == e && !IsSameInBothLanguages(key)),
                Is.Empty,
                "a capture text that is identical in both languages was probably not translated");
        });
    }

    [Test]
    public void Keys_EveryKeyTheUseCaseSourceNames_HasViAndEnText()
    {
        var vi = ResourceFiles.Load("Strings.vi.xaml");
        var en = ResourceFiles.Load("Strings.en.xaml");
        var useCases = Path.Combine(RepositoryRoot(), "src", "Paper.ScreenWizzard.UseCases");
        var keys = Directory.EnumerateFiles(useCases, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .SelectMany(path => Regex.Matches(File.ReadAllText(path), "\"(Capture\\.[A-Za-z]+)\"").Select(match => match.Groups[1].Value))
            .Distinct()
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(keys.Where(key => !vi.Contains(key)), Is.Empty, "keys the capture use case emits and Strings.vi.xaml lacks");
            Assert.That(keys.Where(key => !en.Contains(key)), Is.Empty, "keys the capture use case emits and Strings.en.xaml lacks");
            Assert.That(
                _catalogue.Select(m => m.Key).Except(keys).Except(_keysTheAppBuildsFromAnIssue).Where(_ => keys.Count > 0),
                Is.Empty,
                "keys of this catalogue the use case source no longer names (the two lists drifted)");
        });
    }

    [Test]
    public void Keys_EveryDynamicResourceOfTheCaptureViews_IsDefined()
    {
        var vi = ResourceFiles.Load("Strings.vi.xaml");
        var en = ResourceFiles.Load("Strings.en.xaml");
        var light = ResourceFiles.Load("Themes/Light.xaml");
        var dark = ResourceFiles.Load("Themes/Dark.xaml");
        var views = Path.Combine(RepositoryRoot(), "src", "Paper.ScreenWizzard.Presentation", "Capture", "Views");
        var used = Directory.EnumerateFiles(views, "*.xaml", SearchOption.AllDirectories)
            .SelectMany(path => Regex.Matches(File.ReadAllText(path), @"\{DynamicResource\s+([A-Za-z0-9_.]+)\}").Select(match => match.Groups[1].Value))
            .Distinct()
            .ToList();

        Assert.That(used, Is.Not.Empty, "the capture views name no resources at all: are they built?");
        Assert.Multiple(() =>
        {
            Assert.That(used.Where(key => key.StartsWith("Brush.", StringComparison.Ordinal) && !light.Contains(key)), Is.Empty, "brushes missing from Light.xaml");
            Assert.That(used.Where(key => key.StartsWith("Brush.", StringComparison.Ordinal) && !dark.Contains(key)), Is.Empty, "brushes missing from Dark.xaml");
            Assert.That(used.Where(key => !key.StartsWith("Brush.", StringComparison.Ordinal) && !vi.Contains(key)), Is.Empty, "texts missing from Strings.vi.xaml");
            Assert.That(used.Where(key => !key.StartsWith("Brush.", StringComparison.Ordinal) && !en.Contains(key)), Is.Empty, "texts missing from Strings.en.xaml");
        });
    }

    [Test]
    public void Keys_TheCaptureViewsCarryNoHardCodedColourAndNoLiteralText()
    {
        var views = Path.Combine(RepositoryRoot(), "src", "Paper.ScreenWizzard.Presentation", "Capture", "Views");
        var files = Directory.EnumerateFiles(views, "*.xaml", SearchOption.AllDirectories).ToList();
        var problems = new List<string>();

        foreach (var file in files)
        {
            var xaml = File.ReadAllText(file);
            problems.AddRange(Regex.Matches(xaml, "#[0-9A-Fa-f]{6,8}\\b").Select(match => $"{Path.GetFileName(file)}: hard-coded colour {match.Value}"));
            problems.AddRange(Regex.Matches(xaml, "(?<![\\w.])(Text|Content|Title)=\"(?!\\{)[^\"]*\\p{L}[^\"]*\"").Select(match => $"{Path.GetFileName(file)}: literal text {match.Value}"));
        }

        Assert.That(files, Is.Not.Empty, "the capture views are not built yet");
        Assert.That(problems, Is.Empty, string.Join('\n', problems));
    }

    // "PNG" is a format name, not a sentence, so it reads the same in both languages.
    private static bool IsSameInBothLanguages(string key) => key.EndsWith(".Format", StringComparison.Ordinal);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Paper.ScreenWizzard.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new AssertionException("Paper.ScreenWizzard.slnx not found above the test output");
    }
}
