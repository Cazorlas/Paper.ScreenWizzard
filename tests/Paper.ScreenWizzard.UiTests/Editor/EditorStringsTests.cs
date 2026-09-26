using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Editor;
using Paper.ScreenWizzard.UiTests.Support;
using Paper.ScreenWizzard.UseCases.Shared.Models;

namespace Paper.ScreenWizzard.UiTests.ImageEditor;

/// <summary>
/// The words of the editor in both languages (plan T13): every message the editor use case can put in a NotificationMessage reads as a
/// sentence with its arguments in place, every label and tooltip the editor views name exists in Vietnamese and English, and the views
/// carry no literal text and no hard-coded colour.
/// </summary>
[TestFixture]
public sealed class EditorStringsTests : UiTestBase
{
    /// <summary>Exactly the keys of the brief, with their arguments in order ({0} {1}).</summary>
    private static readonly NotificationMessage[] _catalogue =
    [
        NotificationMessage.Of("Editor.ReadFailed", @"C:\Pics\broken.png", "the file is locked by another program"),
        NotificationMessage.Of("Editor.NotAnImage", @"C:\Docs\notes.txt"),
        NotificationMessage.Of("Editor.ImageTooLarge", @"C:\Pics\huge.png"),
        NotificationMessage.Of("Editor.ClipboardHasNoImage"),
        NotificationMessage.Of("Editor.ClipboardReadFailed", "the clipboard is held by another program"),
        NotificationMessage.Of("Editor.SaveFailed", @"D:\ReadOnly\shot.png", "access is denied"),
        NotificationMessage.Of("Editor.CopyFailed", "the clipboard is held by another program"),
        NotificationMessage.Of("Editor.CropInvalid"),
        NotificationMessage.Of("Editor.FileChangedOnDisk", @"C:\Pics\original.png"),
        NotificationMessage.Of("Editor.Saved", @"D:\Out\shot.png"),
        NotificationMessage.Of("Editor.Copied", "1280", "720"),
    ];

    /// <summary>The labels, names and tooltips of the editor windows and its two questions (the views and the prompt name these keys).</summary>
    private static readonly string[] _labelKeys =
    [
        "Editor.Title", "Editor.Canvas",
        "Editor.Tool.Select", "Editor.Tool.Select.Name", "Editor.Tool.Pen", "Editor.Tool.Pen.Name", "Editor.Tool.Highlighter", "Editor.Tool.Highlighter.Name",
        "Editor.Tool.Line", "Editor.Tool.Line.Name", "Editor.Tool.Arrow", "Editor.Tool.Arrow.Name", "Editor.Tool.Rectangle", "Editor.Tool.Rectangle.Name",
        "Editor.Tool.Ellipse", "Editor.Tool.Ellipse.Name", "Editor.Tool.Text", "Editor.Tool.Text.Name", "Editor.Tool.StepNumber", "Editor.Tool.StepNumber.Name",
        "Editor.Tool.Blur", "Editor.Tool.Blur.Name", "Editor.Tool.Crop", "Editor.Tool.Crop.Name",
        "Editor.Color.Label", "Editor.Color.Red", "Editor.Color.Orange", "Editor.Color.Yellow", "Editor.Color.Green", "Editor.Color.Blue", "Editor.Color.Purple",
        "Editor.Color.Black", "Editor.Color.White", "Editor.Color.Custom", "Editor.Color.Add", "Editor.Color.Invalid",
        "Editor.Thickness", "Editor.FontSize", "Editor.Undo", "Editor.Undo.Tip", "Editor.Redo", "Editor.Redo.Tip",
        "Editor.Zoom.Fit", "Editor.Zoom.Reset", "Editor.Zoom.Level",
        "Editor.Status.Size", "Editor.Status.Saved", "Editor.Status.Unsaved", "Editor.Hint.Crop", "Editor.Hint.Text",
        "Editor.Copy", "Editor.Copy.Tip", "Editor.SaveAs", "Editor.Save", "Editor.Save.Tip", "Editor.Text.Draft",
        "Editor.Prompt.OverwriteTitle", "Editor.Prompt.OverwriteQuestion", "Editor.Prompt.Overwrite", "Editor.Prompt.SaveCopy", "Editor.Prompt.Cancel",
        "Editor.Prompt.CloseTitle", "Editor.Prompt.CloseQuestion", "Editor.Prompt.Save", "Editor.Prompt.Discard", "Editor.Prompt.Back",
    ];

    [TestCase(ResolvedLanguage.Vietnamese)]
    [TestCase(ResolvedLanguage.English)]
    public void Messages_EveryKeyTheEditorEmits_ReadsAsASentenceWithItsArgumentsInPlace(ResolvedLanguage language)
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

    [TestCase(ResolvedLanguage.Vietnamese, "Editor.NotAnImage", "không đọc được")]
    [TestCase(ResolvedLanguage.Vietnamese, "Editor.ClipboardHasNoImage", "clipboard không có ảnh")]
    [TestCase(ResolvedLanguage.Vietnamese, "Editor.CropInvalid", "vùng cắt không hợp lệ")]
    [TestCase(ResolvedLanguage.Vietnamese, "Editor.ImageTooLarge", "quá lớn")]
    [TestCase(ResolvedLanguage.Vietnamese, "Editor.FileChangedOnDisk", "Lưu thành")]
    [TestCase(ResolvedLanguage.English, "Editor.NotAnImage", "could not be read")]
    [TestCase(ResolvedLanguage.English, "Editor.ClipboardHasNoImage", "no image")]
    [TestCase(ResolvedLanguage.English, "Editor.CropInvalid", "not valid")]
    [TestCase(ResolvedLanguage.English, "Editor.ImageTooLarge", "too large")]
    [TestCase(ResolvedLanguage.English, "Editor.FileChangedOnDisk", "Save as")]
    public void Messages_TheSpecsOwnWords_AreThere(ResolvedLanguage language, string key, string fragment)
    {
        var host = WpfHost.Instance;
        host.Invoke(() => host.Language.Apply(language));

        var text = host.Invoke(() => host.Language.Format(NotificationMessage.Of(key, @"C:\Pics\a.png")));

        Assert.That(text, Is.Not.EqualTo(key), "an unknown key would show itself");
        Assert.That(text, Does.Contain(fragment).IgnoreCase);
    }

    [Test]
    public void Messages_SaveFailedAndFileChanged_NameThePathAndTheReasonAndWhatToDoNext()
    {
        var host = WpfHost.Instance;
        host.Invoke(() => host.Language.Apply(ResolvedLanguage.Vietnamese));

        var failed = host.Invoke(() => host.Language.Format(NotificationMessage.Of("Editor.SaveFailed", @"D:\ReadOnly\a.png", "access is denied")));

        Assert.That(failed, Does.Contain(@"D:\ReadOnly\a.png").And.Contain("access is denied"), "SPEC F1: nêu đường dẫn và lý do");
        Assert.That(failed, Does.Contain("vẫn").IgnoreCase.Or.Contain("giữ").IgnoreCase, "and that the edits are still there");
    }

    [Test]
    public void Messages_Copied_ShowsWidthBeforeHeight()
    {
        var host = WpfHost.Instance;
        host.Invoke(() => host.Language.Apply(ResolvedLanguage.English));

        var text = host.Invoke(() => host.Language.Format(NotificationMessage.Of("Editor.Copied", "1280", "720")));

        Assert.That(text.IndexOf("1280", StringComparison.Ordinal), Is.LessThan(text.IndexOf("720", StringComparison.Ordinal)));
    }

    [Test]
    public void Keys_ViAndEn_BothDefineEveryEditorKeyAndTheTwoSetsAreTheSame()
    {
        var vi = ResourceFiles.Load("Strings.vi.xaml");
        var en = ResourceFiles.Load("Strings.en.xaml");
        var needed = _catalogue.Select(m => m.Key).Concat(_labelKeys).ToList();
        var editorKeysVi = vi.Keys.OfType<string>().Where(k => k.StartsWith("Editor.", StringComparison.Ordinal)).ToHashSet();
        var editorKeysEn = en.Keys.OfType<string>().Where(k => k.StartsWith("Editor.", StringComparison.Ordinal)).ToHashSet();

        Assert.Multiple(() =>
        {
            Assert.That(needed.Where(key => !vi.Contains(key)), Is.Empty, "editor keys missing from Strings.vi.xaml");
            Assert.That(needed.Where(key => !en.Contains(key)), Is.Empty, "editor keys missing from Strings.en.xaml");
            Assert.That(editorKeysVi.Except(editorKeysEn), Is.Empty, "editor keys only in Vietnamese");
            Assert.That(editorKeysEn.Except(editorKeysVi), Is.Empty, "editor keys only in English");
            Assert.That(
                needed.Where(key => vi.Contains(key) && en.Contains(key) && vi[key] is string v && en[key] is string e && v == e && !IsSameInBothLanguages(key)),
                Is.Empty,
                "an editor text that is identical in both languages was probably not translated");
        });
    }

    [Test]
    public void Keys_TheEditorUseCaseSourceNamesOnlyKeysThatHaveText()
    {
        var vi = ResourceFiles.Load("Strings.vi.xaml");
        var en = ResourceFiles.Load("Strings.en.xaml");
        var useCases = Path.Combine(RepositoryRoot(), "src", "Paper.ScreenWizzard.UseCases", "Editor");
        var keys = Directory.EnumerateFiles(useCases, "*.cs", SearchOption.AllDirectories)
            .SelectMany(path => Regex.Matches(File.ReadAllText(path), "\"(Editor\\.[A-Za-z]+)\"").Select(match => match.Groups[1].Value))
            .Distinct()
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(keys.Where(key => !vi.Contains(key)), Is.Empty, "keys the editor use case emits and Strings.vi.xaml lacks");
            Assert.That(keys.Where(key => !en.Contains(key)), Is.Empty, "keys the editor use case emits and Strings.en.xaml lacks");
            Assert.That(keys.Except(_catalogue.Select(m => m.Key)), Is.Empty, "a key the use case emits that this catalogue does not know (the two lists drifted)");
        });
    }

    [Test]
    public void Keys_EveryDynamicResourceOfTheEditorViews_IsDefined()
    {
        var vi = ResourceFiles.Load("Strings.vi.xaml");
        var en = ResourceFiles.Load("Strings.en.xaml");
        var light = ResourceFiles.Load("Themes/Light.xaml");
        var dark = ResourceFiles.Load("Themes/Dark.xaml");
        var used = EditorXamlFiles()
            .SelectMany(path => Regex.Matches(File.ReadAllText(path), @"\{DynamicResource\s+([A-Za-z0-9_.]+)\}").Select(match => match.Groups[1].Value))
            .Distinct()
            .ToList();

        Assert.That(used, Is.Not.Empty, "the editor views name no resources at all: are they built?");
        Assert.Multiple(() =>
        {
            Assert.That(used.Where(key => key.StartsWith("Brush.", StringComparison.Ordinal) && !light.Contains(key)), Is.Empty, "brushes missing from Light.xaml");
            Assert.That(used.Where(key => key.StartsWith("Brush.", StringComparison.Ordinal) && !dark.Contains(key)), Is.Empty, "brushes missing from Dark.xaml");
            Assert.That(used.Where(key => !key.StartsWith("Brush.", StringComparison.Ordinal) && !vi.Contains(key)), Is.Empty, "texts missing from Strings.vi.xaml");
            Assert.That(used.Where(key => !key.StartsWith("Brush.", StringComparison.Ordinal) && !en.Contains(key)), Is.Empty, "texts missing from Strings.en.xaml");
        });
    }

    [Test]
    public void Keys_TheEditorViewsCarryNoHardCodedColourAndNoLiteralText()
    {
        var files = EditorXamlFiles().ToList();
        var problems = new List<string>();

        foreach (var file in files)
        {
            var xaml = File.ReadAllText(file);
            problems.AddRange(Regex.Matches(xaml, "#[0-9A-Fa-f]{6,8}\\b").Select(match => $"{Path.GetFileName(file)}: hard-coded colour {match.Value}"));
            problems.AddRange(Regex.Matches(xaml, "(?<![\\w.])(Text|Content|Title|ToolTip|Header)=\"(?!\\{)[^\"]*\\p{L}[^\"]*\"").Select(match => $"{Path.GetFileName(file)}: literal text {match.Value}"));
            problems.AddRange(Regex.Matches(xaml, "AutomationProperties\\.Name=\"(?!\\{)[^\"]*\\p{L}[^\"]*\"").Select(match => $"{Path.GetFileName(file)}: literal name {match.Value}"));
            problems.AddRange(Regex.Matches(xaml, "(?<![\\w.])(Foreground|Background|Fill|Stroke|BorderBrush)=\"(?!\\{)(?!Transparent)[^\"]+\"").Select(match => $"{Path.GetFileName(file)}: brush not from the theme {match.Value}"));
        }

        Assert.That(files, Is.Not.Empty, "the editor views are not built yet");
        Assert.That(problems, Is.Empty, string.Join('\n', problems));
    }

    [Test]
    public void Keys_TheEditorCodeBehindAndViewModelsNameNoStringResourceThatIsMissing()
    {
        var vi = ResourceFiles.Load("Strings.vi.xaml");
        var root = Path.Combine(RepositoryRoot(), "src", "Paper.ScreenWizzard.Presentation");
        var files = new[] { "Views", "ViewModels", "Commands" }
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(root, "Editor", folder), "*.cs", SearchOption.AllDirectories))
            .ToList();
        var keys = files
            .SelectMany(path => Regex.Matches(File.ReadAllText(path), "\"(Editor\\.[A-Za-z0-9.]+)\"").Select(match => match.Groups[1].Value))
            .Where(key => !key.EndsWith('.'))
            .Distinct()
            .ToList();

        // A key ending in a dot is the start of one that is completed at run time ("Editor.Color." + the colour's name): those names are
        // checked by the label list above.
        Assert.That(files, Is.Not.Empty);
        Assert.That(keys.Where(key => !vi.Contains(key)), Is.Empty, "string keys the editor code asks for that Strings.vi.xaml lacks");
    }

    private static IEnumerable<string> EditorXamlFiles() =>
        Directory.EnumerateFiles(Path.Combine(RepositoryRoot(), "src", "Paper.ScreenWizzard.Presentation", "Editor", "Views"), "*.xaml", SearchOption.AllDirectories);

    // A number and a percentage are the same in both languages.
    private static bool IsSameInBothLanguages(string key) => key is "Editor.Tool.StepNumber";

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
