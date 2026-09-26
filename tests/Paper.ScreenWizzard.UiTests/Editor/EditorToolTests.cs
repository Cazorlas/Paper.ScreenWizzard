using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Editor;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Presentation.ViewModels.Editor;
using Paper.ScreenWizzard.UiTests.ScreenCapture;
using Paper.ScreenWizzard.UiTests.Support;
using Paper.ScreenWizzard.UseCases.Editor.Models;
using Paper.ScreenWizzard.UseCases.Shared.Models;

namespace Paper.ScreenWizzard.UiTests.ImageEditor;

/// <summary>
/// The tools of the editor on a view model and a fake session (plan T13): what a drag, a click and the colour, thickness and zoom
/// controls do, in image pixels, with no window. The rules behind them (Shift, history, numbering) are the fakes' canned answers here and
/// the logic lane's real code in the unit project; this proves the view model asks and shows them.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class EditorToolTests : UiTestBase
{
    private static PixelPoint P(int x, int y) => new(x, y);

    [Test]
    public void Defaults_AreTheOnesInSpecInputs()
    {
        var rig = EditorRig.Create(EditorTestData.White(1280, 720));

        Assert.That(rig.ViewModel.Tool, Is.EqualTo(ToolKind.Select), "Công cụ: Chọn");
        Assert.That(rig.ViewModel.Color, Is.EqualTo(EditorColors.Default), "Màu: đỏ");
        Assert.That(rig.ViewModel.Color.R, Is.GreaterThan(200), "red: a strong red channel");
        Assert.That(rig.ViewModel.Color.G, Is.LessThan(80), "and little green");
        Assert.That(rig.ViewModel.Thickness, Is.EqualTo(4), "Độ dày nét: 4");
        Assert.That(rig.ViewModel.FontSize, Is.EqualTo(18), "Cỡ chữ: 18");
        Assert.That(rig.ViewModel.SizeText, Is.EqualTo("1280 × 720"));
        Assert.That(rig.ViewModel.IsFitMode, Is.True, "Mức phóng: vừa khung");
    }

    [Test]
    public void Palette_HasEightColoursAndTheDefaultIsTheSelectedOne()
    {
        var rig = EditorRig.Create();

        Assert.That(rig.ViewModel.Palette, Has.Count.EqualTo(8));
        Assert.That(rig.ViewModel.Palette.Select(p => p.Color).Distinct().Count(), Is.EqualTo(8), "eight different colours");
        Assert.That(rig.ViewModel.Palette.Where(p => p.IsSelected).Select(p => p.Key), Is.EqualTo(new[] { "Red" }));
        Assert.That(rig.ViewModel.Palette.Select(p => p.AutomationId), Is.All.StartsWith("Swatch."));
    }

    [Test]
    public void Tools_EveryKindOfTheEnumCanBeChosen()
    {
        var rig = EditorRig.Create();

        foreach (var tool in Enum.GetValues<ToolKind>())
        {
            rig.ViewModel.SelectToolCommand.Execute(tool);

            Assert.That(rig.ViewModel.Tool, Is.EqualTo(tool));
        }

        Assert.That(Enum.GetValues<ToolKind>(), Has.Length.EqualTo(11), "the eleven tools of the toolbar");
    }

    [TestCase(ToolKind.Rectangle)]
    [TestCase(ToolKind.Ellipse)]
    [TestCase(ToolKind.Blur)]
    public void Drag_BoxTool_AddsOneShapeWithTheDraggedBoundsInOneHistoryStep(ToolKind tool)
    {
        var rig = EditorRig.Create();

        rig.Draw(tool, P(50, 40), P(200, 150));

        var shape = rig.Session.Document.Annotations.Only();
        Assert.That(BoundsOf(shape), Is.EqualTo(new PixelRect(50, 40, 150, 110)));
        Assert.That(rig.Session.HistorySteps, Is.EqualTo(1), "one drag, one step");
        Assert.That(shape.Color, Is.EqualTo(rig.ViewModel.Color));
        Assert.That(shape.Thickness, Is.EqualTo(4));
        Assert.That(shape, tool switch
        {
            ToolKind.Rectangle => Is.TypeOf<RectangleAnnotation>(),
            ToolKind.Ellipse => Is.TypeOf<EllipseAnnotation>(),
            _ => Is.TypeOf<BlurAnnotation>(),
        });
    }

    [TestCase(ToolKind.Rectangle)]
    [TestCase(ToolKind.Ellipse)]
    [TestCase(ToolKind.Blur)]
    public void Drag_BoxTool_InEitherDirection_GivesTheSameShape(ToolKind tool)
    {
        var corners = new[] { (P(50, 40), P(200, 150)), (P(200, 40), P(50, 150)), (P(50, 150), P(200, 40)), (P(200, 150), P(50, 40)) };
        var seen = new HashSet<PixelRect>();

        foreach (var (from, to) in corners)
        {
            var rig = EditorRig.Create();
            rig.Draw(tool, from, to);
            seen.Add(BoundsOf(rig.Session.Document.Annotations.Only()));
        }

        Assert.That(seen, Is.EquivalentTo(new[] { new PixelRect(50, 40, 150, 110) }), "all four drag directions give one rectangle");
    }

    [Test]
    public void Drag_Line_KeepsTheTwoEndsWhicheverEndWasPressedFirst()
    {
        var forward = EditorRig.Create();
        var backward = EditorRig.Create();

        forward.Draw(ToolKind.Line, P(20, 30), P(180, 90));
        backward.Draw(ToolKind.Line, P(180, 90), P(20, 30));

        var a = (LineAnnotation)forward.Session.Document.Annotations.Only();
        var b = (LineAnnotation)backward.Session.Document.Annotations.Only();
        Assert.That(new[] { a.From, a.To }, Is.EquivalentTo(new[] { b.From, b.To }), "the same segment");
    }

    [Test]
    public void Drag_Arrow_HasItsHeadAtTheEndOfTheDrag()
    {
        var rig = EditorRig.Create();

        rig.Draw(ToolKind.Arrow, P(100, 100), P(300, 200));

        var arrow = (ArrowAnnotation)rig.Session.Document.Annotations.Only();
        Assert.That(arrow.From, Is.EqualTo(P(100, 100)));
        Assert.That(arrow.To, Is.EqualTo(P(300, 200)), "SPEC: mũi tên nhọn ở (300, 200)");
    }

    [Test]
    public void Drag_UsesWhatTheUseCaseAnswersForShift_AndForwardsTheKeyState()
    {
        var rig = EditorRig.Create();
        rig.Interactor.ConstrainAnswer = (_, start, _, _) => new DragShape(start, new PixelPoint(start.X + 90, start.Y + 90));
        rig.ViewModel.SelectTool(ToolKind.Rectangle);

        rig.Drag(P(10, 10), P(120, 60), shift: true);

        Assert.That(rig.Interactor.Constrained, Is.Not.Empty, "the use case was asked to apply Shift");
        var call = rig.Interactor.Constrained.Last();
        Assert.That((call.Tool, call.Start, call.Current, call.Shift), Is.EqualTo((ToolKind.Rectangle, P(10, 10), P(120, 60), true)));
        Assert.That(((RectangleAnnotation)rig.Session.Document.Annotations.Only()).Bounds, Is.EqualTo(new PixelRect(10, 10, 90, 90)), "a square: the answer, not the raw drag");
    }

    [Test]
    public void Drag_ShowsAPreviewWhileTheMouseIsDown_AndNoShapeYet()
    {
        var rig = EditorRig.Create();
        rig.ViewModel.SelectTool(ToolKind.Rectangle);

        rig.ViewModel.PointerDown(P(10, 10), false);
        rig.ViewModel.PointerMoved(P(80, 60), false);

        Assert.That(rig.ViewModel.Draft, Is.TypeOf<RectangleAnnotation>(), "the canvas draws this while dragging");
        Assert.That(rig.Session.Document.Annotations, Is.Empty, "and nothing is in the history until the mouse is released");

        rig.ViewModel.PointerUp(P(80, 60), false);

        Assert.That(rig.ViewModel.Draft, Is.Null);
        Assert.That(rig.Session.Document.Annotations, Has.Count.EqualTo(1));
    }

    [Test]
    public void Drag_BoxToolWithoutMoving_AddsNothing()
    {
        var rig = EditorRig.Create();
        rig.ViewModel.SelectTool(ToolKind.Rectangle);

        rig.Click(50, 50);

        Assert.That(rig.Session.Document.Annotations, Is.Empty, "a click is not a rectangle");
        Assert.That(rig.Session.CanUndo, Is.False);

        rig.Drag(P(50, 50), P(90, 90));
        Assert.That(rig.Session.Document.Annotations, Has.Count.EqualTo(1), "while a drag is (the tool works at all)");
    }

    [Test]
    public void Pen_CollectsThePointsOfTheStrokeInOneStep()
    {
        var rig = EditorRig.Create();
        rig.ViewModel.SelectTool(ToolKind.Pen);

        rig.Drag(P(10, 10), P(60, 30), false, P(20, 15), P(40, 25));

        var stroke = (StrokeAnnotation)rig.Session.Document.Annotations.Only();
        Assert.That(stroke.Points, Is.EqualTo(new[] { P(10, 10), P(20, 15), P(40, 25), P(60, 30) }));
        Assert.That(stroke.IsHighlighter, Is.False);
        Assert.That(rig.Session.HistorySteps, Is.EqualTo(1));
    }

    [Test]
    public void Highlighter_IsAStrokeThatSaysSo()
    {
        var rig = EditorRig.Create();
        rig.ViewModel.SelectTool(ToolKind.Highlighter);

        rig.Drag(P(10, 10), P(200, 10), false, P(100, 10));

        var stroke = (StrokeAnnotation)rig.Session.Document.Annotations.Only();
        Assert.That(stroke.IsHighlighter, Is.True);
    }

    [Test]
    public void Text_ClickPlacesABox_AndCommitAddsTheTypedTextThroughTheUseCase()
    {
        var rig = EditorRig.Create();
        rig.ViewModel.SelectTool(ToolKind.Text);

        rig.Click(40, 60);

        Assert.That(rig.ViewModel.IsEditingText, Is.True, "a text box opened where the user clicked");
        Assert.That(rig.ViewModel.TextDraftOrigin, Is.EqualTo(P(40, 60)));
        Assert.That(rig.Session.Document.Annotations, Is.Empty, "nothing exists until the text is committed");

        rig.ViewModel.TextDraftText = "Đường ống 45° — thử nghiệm\r\nDòng hai";
        rig.ViewModel.CommitTextCommand.Execute(null);

        var call = rig.Interactor.TextCalls.Only();
        Assert.That(call.Origin, Is.EqualTo(P(40, 60)));
        Assert.That(call.Text, Is.EqualTo("Đường ống 45° — thử nghiệm\nDòng hai"), "Enter is a newline, and it is a plain \\n");
        Assert.That(call.FontSize, Is.EqualTo(18));
        Assert.That(call.Color, Is.EqualTo(rig.ViewModel.Color));
        Assert.That(rig.ViewModel.IsEditingText, Is.False);
        Assert.That(rig.Session.Document.Annotations.Only(), Is.TypeOf<TextAnnotation>());
    }

    [Test]
    public void F4_TextCommittedEmpty_CreatesNothingAndSaysNothing()
    {
        var rig = EditorRig.Create();
        rig.ViewModel.SelectTool(ToolKind.Text);
        rig.Click(40, 60);
        Assert.That(rig.ViewModel.IsEditingText, Is.True, "the box opened");

        rig.ViewModel.TextDraftText = string.Empty;
        rig.ViewModel.CommitTextCommand.Execute(null);

        Assert.That(rig.Session.Document.Annotations, Is.Empty, "no text");
        Assert.That(rig.ViewModel.IsEditingText, Is.False, "the empty box just goes away");
        Assert.That(rig.Notes.Toasts, Is.Empty, "and no message: dropping an empty box is the user's intent");
        Assert.That(rig.Notes.Errors, Is.Empty);
    }

    [Test]
    public void Text_ClickElsewhereWhileTyping_CommitsWhatWasTypedAndStartsNoNewBox()
    {
        var rig = EditorRig.Create();
        rig.ViewModel.SelectTool(ToolKind.Text);
        rig.Click(40, 60);
        rig.ViewModel.TextDraftText = "Ghi chú";

        rig.Click(200, 150);

        Assert.That(rig.Session.Document.Annotations.OfType<TextAnnotation>().Only().Text, Is.EqualTo("Ghi chú"));
        Assert.That(rig.ViewModel.IsEditingText, Is.False, "the click that commits does not open the next box");
    }

    [Test]
    public void Text_EscapeCancelCommand_CommitsTheTextToo()
    {
        var rig = EditorRig.Create();
        rig.ViewModel.SelectTool(ToolKind.Text);
        rig.Click(40, 60);
        rig.ViewModel.TextDraftText = "Xong";

        rig.ViewModel.CancelCommand.Execute(null);

        Assert.That(rig.Session.Document.Annotations.OfType<TextAnnotation>().Only().Text, Is.EqualTo("Xong"), "SPEC: Esc chốt chữ");
    }

    [Test]
    public void StepNumber_Click_PlacesTheNumberTheSessionSaysIsNext()
    {
        var rig = EditorRig.Create();
        rig.ViewModel.SelectTool(ToolKind.StepNumber);

        rig.Click(30, 30);
        rig.Click(60, 30);
        rig.Click(90, 30);

        var steps = rig.Session.Document.Annotations.OfType<StepAnnotation>().ToList();
        Assert.That(steps.Select(s => s.Number), Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(steps.Select(s => s.Center), Is.EqualTo(new[] { P(30, 30), P(60, 30), P(90, 30) }));
        Assert.That(steps.All(s => s.FontSize == 18), Is.True);

        rig.Session.Delete(steps[1].Id);
        rig.Click(120, 30);

        Assert.That(rig.Session.Document.Annotations.OfType<StepAnnotation>().Select(s => s.Number), Is.EqualTo(new[] { 1, 3, 4 }), "SPEC: xoá số 2 → các số còn lại giữ nguyên, kế tiếp là 4");
    }

    [Test]
    public void Crop_DragThenConfirm_KeepsTheChosenRegion()
    {
        var rig = EditorRig.Create(EditorTestData.White(1000, 600));
        rig.ViewModel.SelectTool(ToolKind.Crop);

        rig.Drag(P(100, 50), P(500, 350));

        Assert.That(rig.ViewModel.CropRect, Is.EqualTo(new PixelRect(100, 50, 400, 300)), "the overlay shows the region until Enter");
        Assert.That(rig.Session.Document.Source.Width, Is.EqualTo(1000), "nothing is cut yet");

        rig.ViewModel.ConfirmCommand.Execute(null);

        Assert.That(rig.Session.Document.Source.Width, Is.EqualTo(400));
        Assert.That(rig.Session.Document.Source.Height, Is.EqualTo(300));
        Assert.That(rig.ViewModel.SizeText, Is.EqualTo("400 × 300"), "the status bar follows the crop");
        Assert.That(rig.ViewModel.CropRect, Is.Null, "the overlay is gone");
        Assert.That(rig.Notes.Toasts, Is.Empty);
    }

    [Test]
    public void F5_CropOutsideTheImage_ShowsTheMessageAndKeepsTheImage()
    {
        var rig = EditorRig.Create(EditorTestData.White(1000, 600));
        rig.ViewModel.SelectTool(ToolKind.Crop);

        rig.Drag(P(1200, 700), P(1500, 900));
        rig.ViewModel.ConfirmCommand.Execute(null);

        Assert.That(rig.Session.Document.Source.Width, Is.EqualTo(1000), "the image is as it was");
        Assert.That(rig.Session.CanUndo, Is.False, "and nothing entered the history");
        var shown = rig.Notes.Toasts.Concat(rig.Notes.Errors).Select(m => m.Key).ToList();
        Assert.That(shown, Is.EqualTo(new[] { "Editor.CropInvalid" }), "the user is told the region is not valid");
    }

    [Test]
    public void Crop_EscapeDropsTheRegionWithoutCutting()
    {
        var rig = EditorRig.Create(EditorTestData.White(1000, 600));
        rig.ViewModel.SelectTool(ToolKind.Crop);
        rig.Drag(P(100, 50), P(500, 350));
        Assert.That(rig.ViewModel.CropRect, Is.Not.Null, "there is a region to drop");

        rig.ViewModel.CancelCommand.Execute(null);

        Assert.That(rig.ViewModel.CropRect, Is.Null);
        Assert.That(rig.Session.Document.Source.Width, Is.EqualTo(1000));
    }

    [Test]
    public void Select_ClickOnAShape_SelectsTheTopmostOne_AndClickOnNothingClearsIt()
    {
        var rig = EditorRig.Create();
        rig.Draw(ToolKind.Rectangle, P(50, 50), P(150, 120));
        rig.Draw(ToolKind.Rectangle, P(100, 80), P(220, 160));
        Assert.That(rig.Session.Document.Annotations, Has.Count.EqualTo(2), "two rectangles to choose from");
        var top = rig.Session.Document.Annotations[1];
        rig.ViewModel.SelectTool(ToolKind.Select);

        // (100, 120) is where the first rectangle's bottom edge crosses the second one's left edge: both outlines are hit.
        rig.Click(100, 120);
        Assert.That(rig.Session.SelectedId, Is.EqualTo(top.Id), "both outlines are under the pointer: the newer one is on top");

        rig.Click(280, 20);
        Assert.That(rig.Session.SelectedId, Is.Null, "an empty spot deselects");
    }

    [Test]
    public void Select_DragMovesTheShapeByTheWholeDelta_AsOneHistoryStep()
    {
        var rig = EditorRig.Create();
        rig.Draw(ToolKind.Rectangle, P(50, 50), P(150, 120));
        var steps = rig.Session.HistorySteps;
        rig.ViewModel.SelectTool(ToolKind.Select);

        // Press on the left edge, move in many small steps, release: one Move, not one per mouse move.
        rig.Drag(P(50, 80), P(80, 95), false, P(55, 82), P(60, 85), P(70, 90));

        var moves = rig.Session.Calls.Where(c => c.StartsWith("Move", StringComparison.Ordinal)).ToList();
        Assert.That(moves, Is.EqualTo(new[] { "Move 30,15" }));
        Assert.That(rig.Session.HistorySteps, Is.EqualTo(steps + 1), "SPEC: mỗi thao tác một bước");
        Assert.That(((RectangleAnnotation)rig.Session.Document.Annotations.Only()).Bounds, Is.EqualTo(new PixelRect(80, 65, 100, 70)));
    }

    [Test]
    public void Select_DragThatEndsWhereItStarted_AddsNoStep()
    {
        var rig = EditorRig.Create();
        rig.Draw(ToolKind.Rectangle, P(50, 50), P(150, 120));
        Assert.That(rig.Session.Document.Annotations, Has.Count.EqualTo(1), "there is a shape to (not) move");
        var steps = rig.Session.HistorySteps;
        rig.ViewModel.SelectTool(ToolKind.Select);

        rig.Drag(P(50, 80), P(50, 80), false, P(60, 90));
        Assert.That(rig.Session.SelectedId, Is.Not.Null, "the press did select it");

        Assert.That(rig.Session.HistorySteps, Is.EqualTo(steps));
        Assert.That(rig.Session.Calls, Has.None.StartWith("Move"));
    }

    [Test]
    public void Select_WhileDragging_ThePreviewShowsTheShapeMovedAndTheSessionIsUntouched()
    {
        var rig = EditorRig.Create();
        rig.Draw(ToolKind.Rectangle, P(50, 50), P(150, 120));
        rig.ViewModel.SelectTool(ToolKind.Select);

        rig.ViewModel.PointerDown(P(50, 80), false);
        rig.ViewModel.PointerMoved(P(90, 100), false);

        Assert.That(rig.Session.Calls, Has.None.StartWith("Move"), "the history is not touched until release");
        Assert.That(rig.ViewModel.Draft, Is.TypeOf<RectangleAnnotation>().With.Property("Bounds").EqualTo(new PixelRect(90, 70, 100, 70)), "the canvas draws the moved copy");

        rig.ViewModel.PointerUp(P(90, 100), false);
    }

    [Test]
    public void Delete_RemovesTheSelectedShape_AndIsDisabledWithNoSelection()
    {
        var rig = EditorRig.Create();
        rig.Draw(ToolKind.Rectangle, P(50, 50), P(150, 120));
        Assert.That(rig.ViewModel.DeleteCommand.CanExecute(null), Is.False, "nothing is selected");

        rig.ViewModel.SelectTool(ToolKind.Select);
        rig.Click(50, 80);
        Assert.That(rig.ViewModel.DeleteCommand.CanExecute(null), Is.True);
        rig.ViewModel.DeleteCommand.Execute(null);

        Assert.That(rig.Session.Document.Annotations, Is.Empty);
        Assert.That(rig.ViewModel.DeleteCommand.CanExecute(null), Is.False);
    }

    [Test]
    public void Tool_SwitchingAwayFromSelect_DropsTheSelection_SoColoursSetTheNextShapes()
    {
        var rig = EditorRig.Create();
        rig.Draw(ToolKind.Rectangle, P(50, 50), P(150, 120));
        rig.ViewModel.SelectTool(ToolKind.Select);
        rig.Click(50, 80);
        Assert.That(rig.Session.SelectedId, Is.Not.Null);

        rig.ViewModel.SelectTool(ToolKind.Pen);

        Assert.That(rig.Session.SelectedId, Is.Null);
    }

    [Test]
    public void Color_WhileAShapeIsSelected_RecoloursThatShapeAndNotTheNextOnes()
    {
        var rig = EditorRig.Create();
        rig.Draw(ToolKind.Rectangle, P(50, 50), P(150, 120));
        rig.ViewModel.SelectTool(ToolKind.Select);
        rig.Click(50, 80);
        var next = rig.ViewModel.Color;
        var blue = rig.ViewModel.Palette.Where(p => p.Key == "Blue").Only("Blue swatch");

        rig.ViewModel.SetColorCommand.Execute(blue);

        Assert.That(rig.Session.Document.Annotations.Only().Color, Is.EqualTo(blue.Color));
        Assert.That(rig.ViewModel.Color, Is.EqualTo(next), "the colour of the next shapes did not change");
        Assert.That(rig.ViewModel.ShownColor, Is.EqualTo(blue.Color), "but the palette shows the selected shape's colour");
        Assert.That(rig.Session.HistorySteps, Is.EqualTo(2), "drawing and recolouring: two steps");
    }

    [Test]
    public void Color_WithNothingSelected_SetsTheColourOfTheNextShapes()
    {
        var rig = EditorRig.Create();
        var green = rig.ViewModel.Palette.Where(p => p.Key == "Green").Only("Green swatch");

        rig.ViewModel.SetColorCommand.Execute(green);
        rig.Draw(ToolKind.Rectangle, P(50, 50), P(150, 120));

        Assert.That(rig.ViewModel.Color, Is.EqualTo(green.Color));
        Assert.That(rig.Session.Document.Annotations.Only().Color, Is.EqualTo(green.Color));
        Assert.That(rig.Session.HistorySteps, Is.EqualTo(1), "choosing a colour is not a history step");
        Assert.That(rig.ViewModel.Palette.Where(p => p.IsSelected).Select(p => p.Key), Is.EqualTo(new[] { "Green" }));
    }

    [Test]
    public void Thickness_WhileAShapeIsSelected_ChangesThatShape_ElseTheNextOnes()
    {
        var rig = EditorRig.Create();
        rig.Draw(ToolKind.Rectangle, P(50, 50), P(150, 120));
        rig.ViewModel.SelectTool(ToolKind.Select);
        rig.Click(50, 80);

        rig.ViewModel.Thickness = 9;

        Assert.That(rig.Session.Document.Annotations.Only().Thickness, Is.EqualTo(9));

        rig.ViewModel.SelectTool(ToolKind.Line);
        rig.ViewModel.Thickness = 12;
        rig.Drag(P(10, 10), P(100, 10));

        Assert.That(rig.Session.Document.Annotations.OfType<LineAnnotation>().Only().Thickness, Is.EqualTo(12));
        Assert.That(rig.Session.Document.Annotations.OfType<RectangleAnnotation>().Only().Thickness, Is.EqualTo(9), "the rectangle kept its own");
    }

    [TestCase(0, 1)]
    [TestCase(-5, 1)]
    [TestCase(25, 20)]
    [TestCase(7, 7)]
    public void Thickness_StaysBetween1And20(int asked, int expected)
    {
        var rig = EditorRig.Create();

        rig.ViewModel.Thickness = asked;

        Assert.That(rig.ViewModel.Thickness, Is.EqualTo(expected));
    }

    [TestCase("#1E90FF", true, 30, 144, 255)]
    [TestCase("1e90ff", true, 30, 144, 255)]
    [TestCase("  #00FF00 ", true, 0, 255, 0)]
    [TestCase("#12345", false, 0, 0, 0)]
    [TestCase("#GGGGGG", false, 0, 0, 0)]
    [TestCase("", false, 0, 0, 0)]
    public void CustomColor_HexText_IsAppliedWhenItIsAColour_AndFlaggedWhenItIsNot(string text, bool valid, int r, int g, int b)
    {
        var rig = EditorRig.Create();
        var before = rig.ViewModel.Color;

        rig.ViewModel.CustomColorText = text;
        rig.ViewModel.ApplyCustomColorCommand.Execute(null);

        if (valid)
        {
            Assert.That(rig.ViewModel.Color, Is.EqualTo(new RgbaColor((byte)r, (byte)g, (byte)b, 255)));
            Assert.That(rig.ViewModel.HasCustomColorError, Is.False);
            Assert.That(rig.ViewModel.Palette.Any(p => p.IsSelected), Is.True, "the user's colour shows as a swatch");
        }
        else
        {
            Assert.That(rig.ViewModel.Color, Is.EqualTo(before), "an invalid code changes nothing");
            Assert.That(rig.ViewModel.HasCustomColorError, Is.True);
        }
    }

    [Test]
    public void FontSize_IsUsedByTextAndStepNumbers()
    {
        var rig = EditorRig.Create();
        rig.ViewModel.FontSize = 32;

        rig.ViewModel.SelectTool(ToolKind.StepNumber);
        rig.Click(30, 30);

        Assert.That(rig.Session.Document.Annotations.OfType<StepAnnotation>().Only().FontSize, Is.EqualTo(32));
    }

    [Test]
    public void History_UndoAndRedoCommands_FollowTheSession()
    {
        var rig = EditorRig.Create();
        rig.Draw(ToolKind.Rectangle, P(10, 10), P(60, 60));
        rig.Draw(ToolKind.Rectangle, P(70, 70), P(120, 120));
        rig.Draw(ToolKind.Rectangle, P(130, 130), P(180, 180));

        rig.ViewModel.UndoCommand.Execute(null);
        rig.ViewModel.UndoCommand.Execute(null);
        Assert.That(rig.Session.Document.Annotations, Has.Count.EqualTo(1), "SPEC: ba hình, Undo hai lần → còn một hình");

        rig.ViewModel.RedoCommand.Execute(null);
        Assert.That(rig.Session.Document.Annotations, Has.Count.EqualTo(2), "SPEC: Redo một lần → hai hình");
    }

    [Test]
    public void F6_UndoAndRedoAreDisabledWhenThereIsNothingToDo_AndTheShortcutDoesNothing()
    {
        var rig = EditorRig.Create();
        Assert.That(rig.ViewModel.UndoCommand.CanExecute(null), Is.False, "a fresh image has no step to undo");
        Assert.That(rig.ViewModel.RedoCommand.CanExecute(null), Is.False);

        rig.ViewModel.UndoCommand.Execute(null);
        rig.ViewModel.RedoCommand.Execute(null);

        Assert.That(rig.Session.Document.Annotations, Is.Empty, "the shortcut did nothing");
        Assert.That(rig.Notes.Toasts.Concat(rig.Notes.Errors), Is.Empty, "and reported nothing");

        rig.Draw(ToolKind.Rectangle, P(10, 10), P(60, 60));
        Assert.That(rig.ViewModel.UndoCommand.CanExecute(null), Is.True, "a step to undo now");
        Assert.That(rig.ViewModel.RedoCommand.CanExecute(null), Is.False);

        rig.ViewModel.UndoCommand.Execute(null);
        Assert.That(rig.ViewModel.UndoCommand.CanExecute(null), Is.False, "back at the start");
        Assert.That(rig.ViewModel.RedoCommand.CanExecute(null), Is.True);
    }

    [Test]
    public void F6_TheButtonsRaiseCanExecuteChanged_SoTheBoundButtonsGreyOutAtOnce()
    {
        var rig = EditorRig.Create();
        var raised = 0;
        rig.ViewModel.UndoCommand.CanExecuteChanged += (_, _) => raised++;

        rig.Draw(ToolKind.Rectangle, P(10, 10), P(60, 60));

        Assert.That(raised, Is.GreaterThan(0), "a bound button only re-reads CanExecute when told to");
    }

    [Test]
    public void Zoom_IsClampedBetween10And800Percent_AndTheTextShowsIt()
    {
        var rig = EditorRig.Create();

        rig.ViewModel.SetZoom(0.05);
        Assert.That(rig.ViewModel.Zoom, Is.EqualTo(0.1).Within(1e-9));
        Assert.That(rig.ViewModel.ZoomText, Is.EqualTo("10%"));

        rig.ViewModel.SetZoom(20);
        Assert.That(rig.ViewModel.Zoom, Is.EqualTo(8.0).Within(1e-9));
        Assert.That(rig.ViewModel.ZoomText, Is.EqualTo("800%"));

        rig.ViewModel.Zoom100Command.Execute(null);
        Assert.That(rig.ViewModel.Zoom, Is.EqualTo(1.0).Within(1e-9));
        Assert.That(rig.ViewModel.ZoomText, Is.EqualTo("100%"));
        Assert.That(rig.ViewModel.IsFitMode, Is.False, "a chosen zoom is not fit-to-frame any more");
    }

    [Test]
    public void Zoom_ByAFactor_ScalesTheCurrentZoomAndStopsAtTheLimits()
    {
        var rig = EditorRig.Create();
        rig.ViewModel.SetZoom(1);

        rig.ViewModel.ZoomBy(2);
        Assert.That(rig.ViewModel.Zoom, Is.EqualTo(2.0).Within(1e-9));

        rig.ViewModel.ZoomBy(100);
        Assert.That(rig.ViewModel.Zoom, Is.EqualTo(8.0).Within(1e-9));

        rig.ViewModel.ZoomBy(0.0001);
        Assert.That(rig.ViewModel.Zoom, Is.EqualTo(0.1).Within(1e-9));
    }

    [Test]
    public void Zoom_FitShrinksALargeImageToTheViewport_AndNeverEnlargesASmallOne()
    {
        var large = EditorRig.Create(EditorTestData.White(1600, 1200));
        large.ViewModel.SetViewport(800, 600, 1.0);

        Assert.That(large.ViewModel.IsFitMode, Is.True, "fit is where it starts");
        Assert.That(large.ViewModel.Zoom, Is.LessThan(0.5).And.GreaterThan(0.4), "1600 x 1200 into about 800 x 600");

        var small = EditorRig.Create(EditorTestData.White(200, 100));
        small.ViewModel.SetViewport(800, 600, 1.0);

        Assert.That(small.ViewModel.Zoom, Is.EqualTo(1.0).Within(1e-9), "a small image is shown at 100%, not stretched");
    }

    [Test]
    public void Zoom_UndoingACropInFitMode_FitsTheRestoredBiggerImageAgainAndRedoFitsTheSmallerOne()
    {
        // A 3000 x 2000 image is fitted to about 0.25; cutting it to 400 x 300 shows it at 100%; Ctrl+Z brings the big image back,
        // and it must be fitted again, not shown at 100% behind scrollbars.
        var rig = EditorRig.Create(EditorTestData.White(3000, 2000));
        rig.ViewModel.SetViewport(800, 600, 1.0);
        var fitted = rig.ViewModel.Zoom;
        rig.ViewModel.SelectTool(ToolKind.Crop);
        rig.Drag(P(100, 100), P(500, 400));
        rig.ViewModel.ConfirmCommand.Execute(null);
        Assert.That(rig.ViewModel.Zoom, Is.EqualTo(1.0).Within(1e-9), "the small cut is shown at its own size");

        rig.ViewModel.UndoCommand.Execute(null);

        Assert.That(rig.ViewModel.Zoom, Is.EqualTo(fitted).Within(1e-9), "the restored 3000 x 2000 image is fitted again");

        rig.ViewModel.RedoCommand.Execute(null);

        Assert.That(rig.ViewModel.Zoom, Is.EqualTo(1.0).Within(1e-9), "and the redone small cut is back at 100%");
    }

    [Test]
    public void Zoom_WhileInFitMode_FollowsTheViewportUntilTheUserZooms()
    {
        var rig = EditorRig.Create(EditorTestData.White(1600, 1200));
        rig.ViewModel.SetViewport(800, 600, 1.0);
        var first = rig.ViewModel.Zoom;

        rig.ViewModel.SetViewport(400, 300, 1.0);
        Assert.That(rig.ViewModel.Zoom, Is.LessThan(first), "a smaller window fits the image smaller");

        rig.ViewModel.SetZoom(2);
        rig.ViewModel.SetViewport(1200, 900, 1.0);
        Assert.That(rig.ViewModel.Zoom, Is.EqualTo(2.0).Within(1e-9), "once the user chose a zoom, resizing leaves it alone");

        rig.ViewModel.ZoomFitCommand.Execute(null);
        Assert.That(rig.ViewModel.IsFitMode, Is.True, "Vừa khung goes back to fitting");
    }

    [Test]
    public void Zoom_ViewScaleIsZoomOverTheMonitorScale()
    {
        var rig = EditorRig.Create();
        rig.ViewModel.SetViewport(800, 600, 1.5);

        rig.ViewModel.SetZoom(1);

        Assert.That(rig.ViewModel.ViewScale, Is.EqualTo(1.0 / 1.5).Within(1e-9), "100% is one image pixel per physical pixel");
    }

    [Test]
    public void Status_ShowsUnsavedForANewImageAndForEditsAndSavedAfterASave()
    {
        var fresh = EditorRig.Create();
        Assert.That(fresh.ViewModel.SaveStateText, Is.EqualTo("Editor.Status.Unsaved"), "a capture that was never saved");

        var opened = EditorRig.Create(sourcePath: @"C:\Pics\a.png");
        Assert.That(opened.ViewModel.SaveStateText, Is.EqualTo("Editor.Status.Saved"), "a file nobody changed");

        opened.Draw(ToolKind.Rectangle, P(10, 10), P(60, 60));
        Assert.That(opened.ViewModel.SaveStateText, Is.EqualTo("Editor.Status.Unsaved"), "changed since it was saved");
    }

    private static PixelRect BoundsOf(Annotation annotation) => annotation switch
    {
        RectangleAnnotation r => r.Bounds,
        EllipseAnnotation e => e.Bounds,
        BlurAnnotation b => b.Area,
        _ => throw new AssertionException($"{annotation.GetType().Name} has no bounds"),
    };
}
