using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Editor;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.Presentation.Editor.ViewModels;
using Paper.ScreenWizzard.UiTests.ScreenCapture;
using Paper.ScreenWizzard.UiTests.Support;
using Paper.ScreenWizzard.UseCases.Shell.Models;

namespace Paper.ScreenWizzard.UiTests.ImageEditor;

/// <summary>
/// The pictures a person opens and judges against the wireframe (plan T13, T14): the editor with several shapes in both themes and languages,
/// the crop overlay, and the text being typed. A screenshot nobody looked at is not evidence, so the file name says what to look for.
/// </summary>
[TestFixture]
public sealed class EditorScreenshotTests : UiTestBase
{
    private static PixelPoint P(int x, int y) => new(x, y);

    /// <summary>Draws what a person writing a bug report would: an arrow, a frame, an ellipse, a highlight, step numbers, a note and a blur.</summary>
    private static void DrawSampleMarkup(EditorWindowRig rig)
    {
        rig.Ui(() =>
        {
            var r = rig.Rig;
            r.Draw(ToolKind.Rectangle, P(168, 84), P(732, 146));
            r.ViewModel.SetColor(EditorColors.Palette[4].Color);
            r.Draw(ToolKind.Ellipse, P(176, 292), P(300, 340));
            r.ViewModel.SetColor(EditorColors.Palette[0].Color);
            r.Draw(ToolKind.Arrow, P(560, 250), P(420, 174));
            r.ViewModel.SetColor(EditorColors.Palette[2].Color);
            r.Draw(ToolKind.Highlighter, P(260, 176), P(470, 176));
            r.ViewModel.SetColor(EditorColors.Palette[0].Color);
            r.ViewModel.SelectTool(ToolKind.StepNumber);
            r.Click(150, 100);
            r.Click(150, 178);
            r.Click(150, 316);
            r.ViewModel.SelectTool(ToolKind.Text);
            r.Click(500, 258);
            r.ViewModel.TextDraftText = "Đường ống 45° — thử nghiệm";
            r.ViewModel.CommitText();
            r.Draw(ToolKind.Blur, P(170, 356), P(420, 380));
            r.ViewModel.SelectTool(ToolKind.Select);
            r.Click(176, 316);
        });
    }

    [TestCase(ResolvedLanguage.Vietnamese, AppTheme.Light, "vi-light")]
    [TestCase(ResolvedLanguage.Vietnamese, AppTheme.Dark, "vi-dark")]
    [TestCase(ResolvedLanguage.English, AppTheme.Light, "en-light")]
    [TestCase(ResolvedLanguage.English, AppTheme.Dark, "en-dark")]
    public void Screenshot_EditorWithSeveralShapes_IsTakenForBothThemesAndLanguages(ResolvedLanguage language, AppTheme theme, string suffix)
    {
        var image = EditorWindowRig.MockScreenshot();
        using var rig = EditorWindowRig.Open(language, theme, image);
        DrawSampleMarkup(rig);

        var path = rig.Window.Screenshot($"editor-{suffix}");

        Assert.That(rig.Window.Exists("ImageCanvas"), Is.True, "open the picture: the canvas is missing");
        Assert.That(rig.Rig.Session.Document.Annotations, Has.Count.GreaterThanOrEqualTo(9), "the markup was drawn");
        Assert.That(new System.IO.FileInfo(path).Length, Is.GreaterThan(20_000), "the picture is nearly empty: the editor drew nothing");
    }

    [Test]
    public void Screenshot_CropOverlay_ShowsTheChosenRegionAndTheDimmedRest()
    {
        var image = EditorWindowRig.MockScreenshot();
        using var rig = EditorWindowRig.Open(ResolvedLanguage.Vietnamese, AppTheme.Light, image);
        rig.Ui(() =>
        {
            rig.ViewModel.SelectTool(ToolKind.Crop);
            rig.Rig.Drag(P(160, 40), P(560, 300));
        });

        var path = rig.Window.Screenshot("editor-crop-vi-light");

        Assert.That(rig.ViewModel.CropRect, Is.EqualTo(new PixelRect(160, 40, 400, 260)), "the region is on the screen until Enter");
        Assert.That(new System.IO.FileInfo(path).Length, Is.GreaterThan(20_000));
    }

    [Test]
    public void Screenshot_ZoomedIn_ShowsEnlargedPixelsAndScrollBarsInTheDarkTheme()
    {
        var image = EditorWindowRig.MockScreenshot();
        using var rig = EditorWindowRig.Open(ResolvedLanguage.Vietnamese, AppTheme.Dark, image);
        DrawSampleMarkup(rig);
        rig.Ui(() => rig.ViewModel.SetZoom(3));

        var path = rig.Window.Screenshot("editor-zoom300-vi-dark");

        Assert.That(rig.ZoomText, Is.EqualTo("300%"));
        Assert.That(new System.IO.FileInfo(path).Length, Is.GreaterThan(20_000));
    }

    [Test]
    public void Screenshot_TextBeingTyped_ShowsABoxOnTheImage()
    {
        var image = EditorWindowRig.MockScreenshot();
        using var rig = EditorWindowRig.Open(ResolvedLanguage.Vietnamese, AppTheme.Dark, image);
        rig.ChooseTool(ToolKind.Text);
        rig.MouseClick(P(420, 330));
        rig.Window.SetText("TextDraftBox", "Đường ống 45° — thử nghiệm");

        var path = rig.Window.Screenshot("editor-text-draft-vi-dark");

        Assert.That(rig.ViewModel.IsEditingText, Is.True);
        Assert.That(rig.Window.Exists("TextDraftBox"), Is.True, "the text box is on the canvas");
        Assert.That(new System.IO.FileInfo(path).Length, Is.GreaterThan(20_000));
    }

    [TestCase(AppTheme.Light, "light")]
    [TestCase(AppTheme.Dark, "dark")]
    public void Screenshot_TextBeingEditedAfterADoubleClick_ShowsTheBoxWithTheOldTextInPlace(AppTheme theme, string suffix)
    {
        var image = EditorWindowRig.MockScreenshot();
        using var rig = EditorWindowRig.Open(ResolvedLanguage.Vietnamese, theme, image);
        DrawSampleMarkup(rig);

        rig.MouseDoubleClick(P(520, 266));
        var path = rig.Window.Screenshot($"editor-text-edit-vi-{suffix}");

        Assert.That(rig.ViewModel.IsEditingText, Is.True, "the double-click opened the box on the note");
        Assert.That(rig.Window.TextOf("TextDraftBox"), Is.EqualTo("Đường ống 45° — thử nghiệm"));
        Assert.That(new System.IO.FileInfo(path).Length, Is.GreaterThan(20_000));
    }

    [Test]
    public void Screenshot_TextEditedAndItsSizeChanged_ShowsTheNewTextBigger()
    {
        var image = EditorWindowRig.MockScreenshot();
        using var rig = EditorWindowRig.Open(ResolvedLanguage.Vietnamese, AppTheme.Light, image);
        DrawSampleMarkup(rig);
        rig.MouseDoubleClick(P(520, 266));
        rig.Window.SetText("TextDraftBox", "Ống thoát — đã sửa");
        rig.Window.EnsureForeground();
        rig.Window.Focus("TextDraftBox");
        rig.Window.PressChord(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL, FlaUI.Core.WindowsAPI.VirtualKeyShort.RETURN);
        rig.Window.SetText("FontSizeBox", "28");
        rig.Window.Focus("FontSizeBox");
        rig.Window.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.RETURN);

        var path = rig.Window.Screenshot("editor-text-edited-vi-light");

        var text = rig.Rig.Session.Document.Annotations.OfType<TextAnnotation>().Only("text");
        Assert.That((text.Text, text.FontSize), Is.EqualTo(("Ống thoát — đã sửa", 28)));
        Assert.That(new System.IO.FileInfo(path).Length, Is.GreaterThan(20_000));
    }
}
