using System.Windows.Media;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Editor;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.Presentation.Rendering;
using Paper.ScreenWizzard.Presentation.ViewModels.Editor;
using Paper.ScreenWizzard.UiTests.ScreenCapture;
using Paper.ScreenWizzard.UiTests.Support;
using Paper.ScreenWizzard.UseCases.Common.Models;
using Paper.ScreenWizzard.UseCases.Shell.Models;

namespace Paper.ScreenWizzard.UiTests.ImageEditor;

/// <summary>
/// The editor as a real window on the fakes (plan T13): the controls the wireframe draws, their names, Tab order and focus ring, both
/// languages and themes, the real mouse drawing where the pointer points, and the F1, F3, F6 and F7 rows as the user sees them.
/// </summary>
[TestFixture]
public sealed class EditorWindowTests : UiTestBase
{
    private static readonly ToolKind[] _toolsInVisualOrder =
    [
        ToolKind.Select,
        ToolKind.Pen,
        ToolKind.Highlighter,
        ToolKind.Line,
        ToolKind.Arrow,
        ToolKind.Rectangle,
        ToolKind.Ellipse,
        ToolKind.Text,
        ToolKind.StepNumber,
        ToolKind.Blur,
        ToolKind.Crop,
    ];

    private static PixelPoint P(int x, int y) => new(x, y);

    // ---- what the window holds ----

    [Test]
    public void Window_HasTheToolbarThePaletteTheOptionsAndTheStatusBarOfTheWireframe()
    {
        using var rig = EditorWindowRig.Open(image: EditorTestData.White(1280, 720));

        Assert.That(rig.Window.Root.AutomationId, Is.EqualTo("EditorWindow"));
        foreach (var tool in _toolsInVisualOrder)
        {
            Assert.That(rig.Window.Exists("ToolButton." + tool), Is.True, $"tool {tool}");
        }

        foreach (var id in new[] { "ImageCanvas", "CustomColorBox", "CustomColorButton", "ThicknessSlider", "FontSizeBox", "UndoButton", "RedoButton", "ZoomFitButton", "Zoom100Button", "ZoomText", "SizeText", "SaveStateText", "CopyButton", "SaveAsButton", "SaveButton" })
        {
            Assert.That(rig.Window.Exists(id), Is.True, id);
        }

        Assert.That(rig.Window.TextOf("SizeText"), Is.EqualTo("1280 × 720"), "the size in the status bar");
        Assert.That(rig.Window.TextOf("SaveStateText"), Is.EqualTo("Chưa lưu"));
        Assert.That(rig.Window.Root.Name, Is.EqualTo("Trình sửa ảnh"));
        var swatches = EditorColors.Palette.Select(p => "Swatch." + p.Key).ToList();
        Assert.That(swatches.Where(id => !rig.Window.Exists(id)), Is.Empty, "eight colours");
    }

    [Test]
    public void Window_ShowsTheDefaultsOfSpecInputs()
    {
        using var rig = EditorWindowRig.Open();

        Assert.That(rig.Window.IsSelected("ToolButton.Select"), Is.True, "Công cụ: Chọn");
        Assert.That(rig.Window.IsSelected("Swatch.Red"), Is.True, "Màu: đỏ");
        Assert.That(rig.Window.TextOf("FontSizeBox"), Is.EqualTo("18"));
        Assert.That(rig.Window.Require("ThicknessSlider").Patterns.RangeValue.Pattern.Value.Value, Is.EqualTo(4), "Độ dày: 4");
        Assert.That(rig.Window.Require("ThicknessSlider").Patterns.RangeValue.Pattern.Minimum.Value, Is.EqualTo(1));
        Assert.That(rig.Window.Require("ThicknessSlider").Patterns.RangeValue.Pattern.Maximum.Value, Is.EqualTo(20));
    }

    [TestCase(ResolvedLanguage.Vietnamese, "Chọn", "Bút vẽ tay", "Bút dạ quang", "Đường thẳng", "Mũi tên", "Khung chữ nhật", "Elip", "Chữ", "Số bước", "Làm mờ", "Cắt")]
    [TestCase(ResolvedLanguage.English, "Select", "Pen", "Highlighter", "Line", "Arrow", "Rectangle", "Ellipse", "Text", "Step number", "Blur", "Crop")]
    public void Tools_EachButtonHasItsNameInBothLanguages(ResolvedLanguage language, params string[] names)
    {
        using var rig = EditorWindowRig.Open(language);

        var actual = _toolsInVisualOrder.Select(tool => rig.Window.NameOf("ToolButton." + tool)).ToList();

        Assert.That(actual, Is.EqualTo(names));
    }

    [TestCase(ResolvedLanguage.Vietnamese, "Hoàn tác", "Làm lại", "Sao chép", "Lưu thành…", "Lưu")]
    [TestCase(ResolvedLanguage.English, "Undo", "Redo", "Copy", "Save as…", "Save")]
    public void Buttons_TheIconOnlyOnesHaveANameAndSoDoTheBottomOnes(ResolvedLanguage language, string undo, string redo, string copy, string saveAs, string save)
    {
        using var rig = EditorWindowRig.Open(language);

        Assert.That(rig.Window.NameOf("UndoButton"), Does.StartWith(undo));
        Assert.That(rig.Window.NameOf("RedoButton"), Does.StartWith(redo));
        Assert.That(rig.Window.NameOf("CopyButton"), Does.StartWith(copy));
        Assert.That(rig.Window.NameOf("SaveAsButton"), Is.EqualTo(saveAs));
        Assert.That(rig.Window.NameOf("SaveButton"), Does.StartWith(save));
        var unnamed = rig.Window.ButtonNames().Where(b => string.IsNullOrWhiteSpace(b.Name)).Select(b => b.AutomationId).ToList();
        Assert.That(unnamed, Is.Empty, "a button with no name is one no screen reader and no test can reach");
    }

    [Test]
    public void Layout_ToolsThenOptionsThenTheCanvasThenTheStatusBar_EachLeftToRight()
    {
        using var rig = EditorWindowRig.Open();
        var tools = _toolsInVisualOrder.Select(t => rig.Window.Require("ToolButton." + t).BoundingRectangle).ToList();
        var options = new[] { "Swatch.Red", "CustomColorBox", "CustomColorButton", "ThicknessSlider", "FontSizeBox", "UndoButton", "RedoButton", "ZoomFitButton", "Zoom100Button" }
            .Select(id => rig.Window.Require(id).BoundingRectangle)
            .ToList();
        var canvas = rig.Window.Require("ImageCanvas").BoundingRectangle;
        var bottom = new[] { "CopyButton", "SaveAsButton", "SaveButton" }.Select(id => rig.Window.Require(id).BoundingRectangle).ToList();

        Assert.That(tools.Select(r => r.Left), Is.Ordered.Ascending, "the eleven tools run left to right in ToolKind order");
        Assert.That(tools.Select(r => r.Top).Distinct().Count(), Is.EqualTo(1), "on one row (wireframe row 1)");
        Assert.That(options.Select(r => r.Left), Is.Ordered.Ascending, "colour, thickness, font, undo/redo, zoom: the wireframe's row 2");
        Assert.That(options.Min(r => r.Top), Is.GreaterThan(tools.Max(r => r.Bottom) - 1), "row 2 is under row 1");
        Assert.That(canvas.Top, Is.GreaterThan(options.Max(r => r.Bottom) - 1), "the image is under the toolbar");
        Assert.That(bottom.Min(r => r.Top), Is.GreaterThan(canvas.Top), "and the status bar under the image");
        Assert.That(bottom.Select(r => r.Left), Is.Ordered.Ascending);
        Assert.That(rig.Window.Require("SizeText").BoundingRectangle.Left, Is.LessThan(bottom[0].Left), "size and state on the left of the buttons");
    }

    [Test]
    public void Keyboard_TabThroughTheWindow_FollowsTheVisualOrder()
    {
        using var rig = EditorWindowRig.Open();
        rig.Window.Focus("ToolButton.Select");

        var reached = rig.Window.TabThrough(11);

        // A group of radio buttons is one stop (the chosen one); a disabled Undo and Redo are skipped; the canvas is a stop of its own.
        Assert.That(
            reached,
            Is.EqualTo(new[]
            {
                "Swatch.Red", "CustomColorBox", "CustomColorButton", "ThicknessSlider", "FontSizeBox",
                "ZoomFitButton", "Zoom100Button", "ImageCanvas", "CopyButton", "SaveAsButton", "SaveButton",
            }));
    }

    [TestCase(AppTheme.Light)]
    [TestCase(AppTheme.Dark)]
    public void Keyboard_TheFocusedControl_ShowsAVisibleRingInBothThemes(AppTheme theme)
    {
        using var rig = EditorWindowRig.Open(theme: theme);
        var ring = Contrast.ColorOf((Brush)ResourceFiles.Load($"Themes/{theme}.xaml")["Brush.Focus"]);

        foreach (var id in new[] { "ToolButton.Select", "Swatch.Red", "SaveAsButton", "SaveButton", "ZoomFitButton", "CustomColorBox", "FontSizeBox" })
        {
            rig.Window.Focus(id);

            Assert.That(rig.Window.CountPixels(id, ring), Is.GreaterThan(20), $"{id} shows the focus ring while it holds focus ({theme})");
        }

        rig.Window.Focus("SaveAsButton");
        Assert.That(rig.Window.CountPixels("CopyButton", ring), Is.Zero, "and only the focused one shows it");
    }

    // ---- choosing ----

    [Test]
    public void Tools_ClickingAToolButton_ChoosesThatToolAndOnlyIt()
    {
        using var rig = EditorWindowRig.Open();

        rig.ChooseTool(ToolKind.Rectangle);

        Assert.That(rig.ViewModel.Tool, Is.EqualTo(ToolKind.Rectangle));
        Assert.That(_toolsInVisualOrder.Where(t => rig.Window.IsSelected("ToolButton." + t)), Is.EqualTo(new[] { ToolKind.Rectangle }));
    }

    [Test]
    public void Palette_ClickingASwatch_ChoosesItAndOnlyIt()
    {
        using var rig = EditorWindowRig.Open();

        rig.Window.Click("Swatch.Blue");

        Assert.That(rig.ViewModel.Color, Is.EqualTo(EditorColors.Palette.Where(p => p.Key == "Blue").Only().Color));
        Assert.That(EditorColors.Palette.Where(p => rig.Window.IsSelected("Swatch." + p.Key)).Select(p => p.Key), Is.EqualTo(new[] { "Blue" }));
    }

    [Test]
    public void CustomColor_TypedHexAndTheAddButton_ChoosesThatColour()
    {
        using var rig = EditorWindowRig.Open();

        rig.Window.SetText("CustomColorBox", "#1E90FF");
        rig.Window.Click("CustomColorButton");

        Assert.That(rig.ViewModel.Color, Is.EqualTo(new RgbaColor(30, 144, 255, 255)));
        Assert.That(rig.Window.Exists("Swatch.Custom"), Is.True, "the user's colour appears as a swatch");
        Assert.That(rig.Window.IsSelected("Swatch.Custom"), Is.True);
    }

    [Test]
    public void Thickness_TheSliderChangesTheThicknessOfTheNextShapes()
    {
        using var rig = EditorWindowRig.Open();

        rig.Window.Require("ThicknessSlider").Patterns.RangeValue.Pattern.SetValue(9);
        rig.Window.Host.Settle();

        Assert.That(rig.ViewModel.Thickness, Is.EqualTo(9));
    }

    [Test]
    public void FontSize_TheBoxChangesTheSizeOfTheNextText()
    {
        using var rig = EditorWindowRig.Open();

        rig.Window.SetText("FontSizeBox", "30");

        Assert.That(rig.ViewModel.FontSize, Is.EqualTo(30));
    }

    // ---- drawing with the real mouse ----

    [TestCase(1.0)]
    [TestCase(4.0)]
    public void Mouse_ARectangleDrawnAtImagePixel150x100_LandsThereInTheSavedImage_AtAnyZoom(double zoom)
    {
        // 200 x 120 shows whole even at 400% (800 x 480) in the window, so the canvas rectangle is the image's.
        using var rig = EditorWindowRig.Open(image: EditorTestData.White(200, 120));
        rig.Ui(() => rig.ViewModel.SetZoom(zoom));
        rig.ChooseTool(ToolKind.Rectangle);

        rig.MouseDrag(P(150, 100), P(190, 115));

        var drawn = rig.Rig.Session.Document.Annotations.Only("rectangle");
        Assert.That(((RectangleAnnotation)drawn).Bounds, Is.EqualTo(new PixelRect(150, 100, 40, 15)), $"the pointer's image pixel at {zoom:P0}: SPEC: (150, 100) không lệch");
        var flat = rig.Ui(() => new WpfImageFlattener().Flatten(rig.Rig.Session));
        var box = EditorTestData.PaintedBoxOrFail(flat);
        Assert.That((box.MinX, box.MinY, box.MaxX, box.MaxY), Is.EqualTo((148, 98, 191, 116)), "the stroke's outer edge, to the pixel");
        Assert.That(rig.Window.TextOf("ZoomText"), Is.EqualTo($"{zoom * 100:0}%"));
    }

    [Test]
    public void Mouse_DragMovesTheSelectedShapeAndOneUndoBringsItBack()
    {
        using var rig = EditorWindowRig.Open(image: EditorTestData.White(200, 120));
        rig.Ui(() => rig.ViewModel.SetZoom(2));
        rig.ChooseTool(ToolKind.Rectangle);
        rig.MouseDrag(P(40, 30), P(120, 80));
        rig.ChooseTool(ToolKind.Select);

        rig.MouseDrag(P(40, 55), P(60, 65));

        Assert.That(((RectangleAnnotation)rig.Rig.Session.Document.Annotations.Only("rectangle")).Bounds, Is.EqualTo(new PixelRect(60, 40, 80, 50)), "moved by (20, 10) image pixels");
        rig.Window.Click("UndoButton");
        Assert.That(((RectangleAnnotation)rig.Rig.Session.Document.Annotations.Only("rectangle")).Bounds, Is.EqualTo(new PixelRect(40, 30, 80, 50)), "one Undo = one drag");
    }

    [Test]
    public void Mouse_CtrlWheelZoomsInAndOutBetween10And800Percent()
    {
        using var rig = EditorWindowRig.Open(image: EditorTestData.White(200, 120));
        rig.Ui(() => rig.ViewModel.SetZoom(1));
        rig.Window.EnsureForeground();
        var middle = rig.ScreenPointOf(100, 60);
        Mouse.MoveTo(middle);

        using (Keyboard.Pressing(VirtualKeyShort.CONTROL))
        {
            Mouse.Scroll(2);
        }

        rig.Window.Host.Settle();
        Assert.That(rig.ViewModel.Zoom, Is.GreaterThan(1.2), "wheel up with Ctrl zooms in");
        Assert.That(rig.ViewModel.IsFitMode, Is.False);

        using (Keyboard.Pressing(VirtualKeyShort.CONTROL))
        {
            Mouse.Scroll(-40);
        }

        rig.Window.Host.Settle();
        Assert.That(rig.ViewModel.Zoom, Is.EqualTo(0.1).Within(1e-9), "and wheel down stops at 10%");
        Assert.That(rig.ZoomText, Is.EqualTo("10%"));
    }

    [Test]
    public void Zoom_FitAndOneHundredPercentButtons_SetTheZoom()
    {
        using var rig = EditorWindowRig.Open(image: EditorTestData.White(3000, 2000));

        // Whatever the monitor's scale, a fitted image is smaller than 100% and lies inside the window.
        Assert.That(rig.ViewModel.Zoom, Is.LessThan(0.8), "a large image starts fitted into the window");
        Assert.That(rig.Window.Require("ImageCanvas").BoundingRectangle.Width, Is.LessThan(rig.Window.Root.BoundingRectangle.Width));
        rig.Window.Click("Zoom100Button");
        Assert.That(rig.ZoomText, Is.EqualTo("100%"));
        rig.Window.Click("ZoomFitButton");
        Assert.That(rig.ViewModel.IsFitMode, Is.True);
        Assert.That(rig.ViewModel.Zoom, Is.LessThan(0.8));
        Assert.That(rig.Window.Require("ImageCanvas").BoundingRectangle.Width, Is.LessThan(rig.Window.Root.BoundingRectangle.Width));
    }

    [Test]
    public void Text_ClickTypeAndCtrlEnter_CommitsTheTextWithItsDiacritics()
    {
        using var rig = EditorWindowRig.Open(image: EditorTestData.White(400, 200));
        rig.ChooseTool(ToolKind.Text);
        Assert.That(rig.ViewModel.Tool, Is.EqualTo(ToolKind.Text), "the tool button took the click");

        rig.MouseClick(P(30, 40));
        Assert.That(rig.ViewModel.IsEditingText, Is.True, "the click on the picture started a text box");
        var box = rig.Window.Require("TextDraftBox");
        Assert.That(box.BoundingRectangle.Width, Is.GreaterThan(20), "a text box opened where the user clicked");
        rig.Window.SetText("TextDraftBox", "Đường ống 45° — thử nghiệm");
        rig.Window.EnsureForeground();
        rig.Window.Focus("TextDraftBox");
        rig.Window.PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.RETURN);

        var text = rig.Rig.Session.Document.Annotations.OfType<TextAnnotation>().Only("text");
        Assert.That(text.Text, Is.EqualTo("Đường ống 45° — thử nghiệm"));
        Assert.That(text.Origin, Is.EqualTo(P(30, 40)));
        Assert.That(rig.Window.Exists("TextDraftBox"), Is.False, "the box is gone once the text is committed");
    }

    // ---- keyboard shortcuts ----

    [Test]
    public void Keyboard_CtrlZAndCtrlY_UndoAndRedoAndDeleteRemovesTheSelection()
    {
        using var rig = EditorWindowRig.Open();
        rig.Ui(() => rig.Rig.Draw(ToolKind.Rectangle, P(50, 50), P(150, 120)));
        rig.Window.EnsureForeground();
        rig.Window.Focus("ImageCanvas");

        rig.Window.PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
        Assert.That(rig.Rig.Session.Document.Annotations, Is.Empty, "Ctrl+Z");

        rig.Window.PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y);
        Assert.That(rig.Rig.Session.Document.Annotations, Has.Count.EqualTo(1), "Ctrl+Y");

        rig.Ui(() =>
        {
            rig.ViewModel.SelectTool(ToolKind.Select);
            rig.Rig.Click(50, 80);
        });
        rig.Window.Press(VirtualKeyShort.DELETE);
        Assert.That(rig.Rig.Session.Document.Annotations, Is.Empty, "Delete removes the selected shape");
    }

    [Test]
    public void Keyboard_CtrlSAndCtrlC_StartTheSaveAndTheCopy()
    {
        using var rig = EditorWindowRig.Open();
        rig.Ui(() => rig.Rig.Draw(ToolKind.Rectangle, P(50, 50), P(150, 120)));
        rig.Rig.Dialogs.Answer = @"D:\Out\k.png";
        rig.Window.EnsureForeground();
        rig.Window.Focus("ImageCanvas");

        rig.Window.PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
        rig.Window.PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_C);

        Assert.That(rig.Rig.Interactor.Saves.Select(s => s.Path), Is.EqualTo(new[] { @"D:\Out\k.png" }), "Ctrl+S");
        Assert.That(rig.Rig.Interactor.Copies, Has.Count.EqualTo(1), "Ctrl+C");
    }

    [Test]
    public void Keyboard_EnterAppliesTheCropAndEscapeDropsIt()
    {
        using var rig = EditorWindowRig.Open(image: EditorTestData.White(400, 300));
        rig.ChooseTool(ToolKind.Crop);
        rig.MouseDrag(P(50, 40), P(250, 190));
        rig.Window.EnsureForeground();
        rig.Window.Focus("ImageCanvas");

        rig.Window.Press(VirtualKeyShort.ESCAPE);
        Assert.That(rig.ViewModel.CropRect, Is.Null, "Esc drops the region");
        Assert.That(rig.Rig.Session.Document.Source.Width, Is.EqualTo(400));

        rig.MouseDrag(P(50, 40), P(250, 190));
        rig.Window.Focus("ImageCanvas");
        rig.Window.Press(VirtualKeyShort.RETURN);
        Assert.That(rig.Rig.Session.Document.Source.Width, Is.EqualTo(200), "Enter cuts");
        Assert.That(rig.Window.TextOf("SizeText"), Is.EqualTo("200 × 150"));
    }

    [Test]
    public void Keyboard_EnterInTheSizeBoxAppliesTheSizeAndDoesNotAlsoCutTheImage()
    {
        // Enter is the window's key for "cut the region"; typed in the size box it only means "apply this size".
        using var rig = EditorWindowRig.Open(image: EditorTestData.White(400, 300));
        rig.ChooseTool(ToolKind.Crop);
        rig.MouseDrag(P(50, 40), P(250, 190));
        rig.Window.EnsureForeground();
        rig.Window.Focus("FontSizeBox");

        rig.Window.Press(VirtualKeyShort.RETURN);

        Assert.That(rig.Rig.Session.Document.Source.Width, Is.EqualTo(400), "the image is not cut");
        Assert.That(rig.ViewModel.CropRect, Is.Not.Null, "the region is still there to be confirmed from the picture");
    }

    [Test]
    public void Text_ClickingElsewhereOnThePictureWhileTheBoxIsOpenOnlyCommitsAndOpensNoSecondBox()
    {
        // SPEC editor: a click elsewhere finishes the text, it does not also start another box. The window's own focus step
        // (the picture takes the keyboard) used to commit first, so the click then looked like a click with no box open.
        using var rig = EditorWindowRig.Open(image: EditorTestData.White(400, 200));
        rig.ChooseTool(ToolKind.Text);
        rig.MouseClick(P(30, 40));
        Assert.That(rig.ViewModel.IsEditingText, Is.True);
        rig.Window.SetText("TextDraftBox", "Van");

        rig.MouseClick(P(250, 150));

        Assert.That(rig.ViewModel.IsEditingText, Is.False, "no second box is open");
        Assert.That(rig.Rig.Session.Document.Annotations.OfType<TextAnnotation>().Select(t => t.Text), Is.EqualTo(new[] { "Van" }));
        Assert.That(rig.Window.Exists("TextDraftBox"), Is.False);
    }

    // ---- F rows ----

    [Test]
    public void F6_UndoAndRedoButtons_AreDimmedWhenThereIsNothingToDo()
    {
        using var rig = EditorWindowRig.Open();
        Assert.That(rig.Window.IsEnabled("UndoButton"), Is.False, "a fresh image: nothing to undo");
        Assert.That(rig.Window.IsEnabled("RedoButton"), Is.False, "and nothing to redo");

        rig.ChooseTool(ToolKind.Rectangle);
        rig.MouseDrag(P(50, 50), P(150, 120));
        Assert.That(rig.Window.IsEnabled("UndoButton"), Is.True, "a step now");
        Assert.That(rig.Window.IsEnabled("RedoButton"), Is.False);

        rig.Window.Click("UndoButton");
        Assert.That(rig.Window.IsEnabled("UndoButton"), Is.False, "back at the start");
        Assert.That(rig.Window.IsEnabled("RedoButton"), Is.True);

        rig.Window.Click("RedoButton");
        Assert.That(rig.Window.IsEnabled("RedoButton"), Is.False);
        Assert.That(rig.Rig.Session.Document.Annotations, Has.Count.EqualTo(1));
        Assert.That(rig.Rig.Notes.Toasts.Concat(rig.Rig.Notes.Errors), Is.Empty, "no message either way");
    }

    [Test]
    public void F1_ASaveThatFails_KeepsTheWindowOpenAndEveryEdit()
    {
        using var rig = EditorWindowRig.Open();
        rig.Ui(() => rig.Rig.Draw(ToolKind.Rectangle, P(50, 50), P(150, 120)));
        rig.Rig.Dialogs.Answer = @"D:\ReadOnly\a.png";
        rig.Rig.Interactor.SaveFailsWith = NotificationMessage.Of("Editor.SaveFailed", @"D:\ReadOnly\a.png", "access is denied");

        rig.Window.Click("SaveButton");

        var error = rig.Rig.Notes.Errors.Only("error message");
        Assert.That(error.Arguments, Is.EqualTo(new[] { @"D:\ReadOnly\a.png", "access is denied" }), "the path and the reason");
        Assert.That(rig.Window.IsOpen, Is.True, "the window did not close");
        Assert.That(rig.Rig.Session.Document.Annotations, Has.Count.EqualTo(1), "the edits are still there");
        Assert.That(rig.Window.TextOf("SaveStateText"), Is.EqualTo("Chưa lưu"));
        Assert.That(rig.Window.IsEnabled("SaveButton"), Is.True, "and Save can be tried again");
    }

    [Test]
    public void F3_PasteWithNoImageOnTheClipboard_ShowsTheMessageAndChangesNothing()
    {
        using var rig = EditorWindowRig.Open();
        rig.Ui(() => rig.Rig.Draw(ToolKind.Rectangle, P(50, 50), P(150, 120)));
        rig.Rig.Interactor.ClipboardAnswer = () => new UseCases.Editor.Models.EditorOpenResult(
            null,
            UseCases.Editor.Models.EditorOpenIssue.ClipboardHasNoImage,
            NotificationMessage.Of("Editor.ClipboardHasNoImage"));
        rig.Window.EnsureForeground();
        rig.Window.Focus("ImageCanvas");

        rig.Window.PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_V);

        var shown = rig.Rig.Notes.Toasts.Concat(rig.Rig.Notes.Errors).Select(m => m.Key).ToList();
        Assert.That(shown, Is.EqualTo(new[] { "Editor.ClipboardHasNoImage" }));
        Assert.That(rig.Rig.Session.Document.Annotations, Has.Count.EqualTo(1), "the image being edited is as it was");
        Assert.That(rig.Rig.Views.Opened, Is.Empty, "no second window");
    }

    [Test]
    public void F7_ClosingWithUnsavedEdits_AsksAndOnlyDiscardClosesTheWindow()
    {
        using var rig = EditorWindowRig.Open(realPrompts: true);
        rig.Ui(() => rig.Rig.Draw(ToolKind.Rectangle, P(50, 50), P(150, 120)));

        rig.RequestClose();
        var prompt = rig.Window.AttachToWindow("EditorPromptDialog");
        Assert.That(prompt.TextOf("PromptText"), Does.Contain("chưa lưu"), "the question is about the unsaved changes");
        prompt.Screenshot("editor-prompt-close-vi-light");
        prompt.Click("PromptCancelButton");

        Assert.That(rig.Window.IsOpen, Is.True, "Quay lại keeps the window");
        Assert.That(rig.Rig.Session.Document.Annotations, Has.Count.EqualTo(1), "and the edits");
        Assert.That(rig.Window.WindowExists("EditorPromptDialog"), Is.False, "the question went away");

        rig.RequestClose();
        rig.Window.AttachToWindow("EditorPromptDialog").Click("PromptSecondaryButton");

        Assert.That(rig.Window.WaitUntilClosed(TimeSpan.FromSeconds(3)), Is.True, "Bỏ closes it");
        Assert.That(rig.Rig.Interactor.Saves, Is.Empty, "without writing anything");
    }

    [Test]
    public void F7_SaveInTheCloseQuestion_ClosesOnlyWhenTheSaveWorked()
    {
        using var rig = EditorWindowRig.Open(realPrompts: true);
        rig.Ui(() => rig.Rig.Draw(ToolKind.Rectangle, P(50, 50), P(150, 120)));
        rig.Rig.Dialogs.Answer = @"D:\ReadOnly\a.png";
        rig.Rig.Interactor.SaveFailsWith = NotificationMessage.Of("Editor.SaveFailed", @"D:\ReadOnly\a.png", "access is denied");

        rig.RequestClose();
        rig.Window.AttachToWindow("EditorPromptDialog").Click("PromptPrimaryButton");

        Assert.That(rig.Window.IsOpen, Is.True, "the save failed, so the window stays");
        Assert.That(rig.Rig.Notes.Errors.Select(m => m.Key), Is.EqualTo(new[] { "Editor.SaveFailed" }));

        rig.Rig.Interactor.SaveFailsWith = null;
        rig.RequestClose();
        rig.Window.AttachToWindow("EditorPromptDialog").Click("PromptPrimaryButton");

        Assert.That(rig.Window.WaitUntilClosed(TimeSpan.FromSeconds(3)), Is.True, "saved, so it closes");
        Assert.That(rig.Rig.Interactor.Saves, Has.Count.EqualTo(2));
    }

    [Test]
    public void F7_TheTitleBarsCloseButton_AsksToo()
    {
        using var rig = EditorWindowRig.Open(realPrompts: true);
        rig.Ui(() => rig.Rig.Draw(ToolKind.Rectangle, P(50, 50), P(150, 120)));

        // From another thread: UI Automation talking to the window from the window's own thread would wait on itself.
        _ = Task.Run(() =>
        {
            try
            {
                rig.Window.Root.AsWindow().Close();
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // The window went away while UI Automation was still talking to it: nothing more to close.
            }
        });
        var prompt = rig.Window.AttachToWindow("EditorPromptDialog");
        prompt.Click("PromptCancelButton");

        Assert.That(rig.Window.IsOpen, Is.True);
    }

    [Test]
    public void Close_WithNothingToLose_ClosesWithoutAsking()
    {
        using var rig = EditorWindowRig.Open(realPrompts: true, sourcePath: @"C:\Pics\a.png");
        Assert.That(rig.Window.Exists("ImageCanvas"), Is.True, "the editor is really built before it is closed");

        rig.RequestClose();

        Assert.That(rig.Window.WaitUntilClosed(TimeSpan.FromSeconds(3)), Is.True);
        Assert.That(rig.Window.WindowExists("EditorPromptDialog"), Is.False);
    }

    [Test]
    public void Prompt_OverwriteQuestion_HasThreeNamedButtonsAndEscapeCancels()
    {
        using var rig = EditorWindowRig.Open(realPrompts: true, sourcePath: @"C:\Pics\original.png");
        rig.Ui(() => rig.Rig.Draw(ToolKind.Rectangle, P(50, 50), P(150, 120)));

        rig.Window.Host.Dispatcher.BeginInvoke(new Action(() => rig.ViewModel.SaveCommand.Execute(null)));
        var prompt = rig.Window.AttachToWindow("EditorPromptDialog");

        Assert.That(prompt.TextOf("PromptText"), Does.Contain(@"C:\Pics\original.png"), "names the original file");
        Assert.That(new[] { "PromptPrimaryButton", "PromptSecondaryButton", "PromptCancelButton" }.Select(id => prompt.NameOf(id)), Is.EqualTo(new[] { "Ghi đè", "Lưu bản mới", "Huỷ" }));
        prompt.Screenshot("editor-prompt-overwrite-vi-light");
        prompt.Click("PromptCancelButton");

        Assert.That(rig.Rig.Interactor.Saves, Is.Empty);
        Assert.That(rig.Rig.Dialogs.Asked, Is.Empty);
    }

    [Test]
    public void Drop_TheWindowAcceptsFilesDroppedOnIt()
    {
        using var rig = EditorWindowRig.Open();

        var allows = rig.Ui(() => rig.Window.Window.AllowDrop);

        Assert.That(allows, Is.True, "SPEC: kéo file vào cửa sổ");
    }

    // ---- both languages and themes ----

    [Test]
    public void Language_SwitchWhileOpen_RenamesTheControlsAtOnce()
    {
        using var rig = EditorWindowRig.Open(ResolvedLanguage.Vietnamese);
        Assert.That(rig.Window.NameOf("ToolButton.Pen"), Is.EqualTo("Bút vẽ tay"));

        rig.Window.Host.Invoke(() => rig.Window.Host.Language.Apply(ResolvedLanguage.English));
        rig.Window.Host.Settle();

        Assert.That(rig.Window.NameOf("ToolButton.Pen"), Is.EqualTo("Pen"));
        Assert.That(rig.Window.TextOf("SaveStateText"), Is.EqualTo("Unsaved"), "and the status text");
        Assert.That(rig.Window.NameOf("Swatch.Red"), Is.EqualTo("Red"));
    }

    [TestCase(AppTheme.Light)]
    [TestCase(AppTheme.Dark)]
    public void Contrast_TextIsAt4Point5AndIconsAndBordersAt3_InBothThemes(AppTheme theme)
    {
        using var rig = EditorWindowRig.Open(theme: theme);
        rig.Ui(() => rig.Rig.Draw(ToolKind.Rectangle, P(50, 50), P(150, 120)));
        var problems = new List<string>();

        void Text(string id)
        {
            var ratio = rig.DominantContrast(id);
            if (ratio < Contrast.MinimumForText)
            {
                problems.Add($"{id}: text {ratio:0.00}:1, needs {Contrast.MinimumForText}:1");
            }
        }

        void Icon(string id)
        {
            var ratio = rig.DominantContrast(id);
            if (ratio < Contrast.MinimumForIconOrBorder)
            {
                problems.Add($"{id}: icon {ratio:0.00}:1, needs {Contrast.MinimumForIconOrBorder}:1");
            }
        }

        foreach (var tool in _toolsInVisualOrder)
        {
            Text("ToolButton." + tool);
        }

        foreach (var id in new[] { "SizeText", "SaveStateText", "ZoomText", "CopyButton", "SaveAsButton", "SaveButton", "ZoomFitButton", "Zoom100Button" })
        {
            Text(id);
        }

        Icon("UndoButton");
        Icon("ThicknessSlider");
        foreach (var color in EditorColors.Palette)
        {
            var border = rig.Ui(() =>
            {
                var swatch = VisualTreeFinder.FindByAutomationId(rig.Window.Window, "Swatch." + color.Key) as System.Windows.Controls.Control;
                return swatch?.BorderBrush as SolidColorBrush;
            });
            var windowBackground = rig.Ui(() => (SolidColorBrush)rig.Window.Window.Background);
            if (border is null)
            {
                problems.Add($"Swatch.{color.Key}: no solid border brush to measure");
            }
            else if (Contrast.Ratio(border, windowBackground) < Contrast.MinimumForIconOrBorder)
            {
                problems.Add($"Swatch.{color.Key}: its border is under {Contrast.MinimumForIconOrBorder}:1 on the window, so the pale colours vanish");
            }
        }

        Assert.That(problems, Is.Empty, $"{theme} theme:\n{string.Join('\n', problems)}");
    }
}
