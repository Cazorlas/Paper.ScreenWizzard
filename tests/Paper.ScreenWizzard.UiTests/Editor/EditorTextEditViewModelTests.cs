using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Editor;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Presentation.ViewModels.Editor;
using Paper.ScreenWizzard.UiTests.ScreenCapture;
using Paper.ScreenWizzard.UiTests.Support;

namespace Paper.ScreenWizzard.UiTests.ImageEditor;

/// <summary>
/// Plan T28 on the view model and the fake session, with no window: re-editing a committed text, the size box on a selected text or step
/// marker (one call per committed change), the thickness drag as one step, and every click going through the use case's HitTest. The window
/// tests next door prove the same flows with the real mouse and keyboard.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class EditorTextEditViewModelTests : UiTestBase
{
    private const string OldText = "Ghi chú cũ";

    private static PixelPoint P(int x, int y) => new(x, y);

    // ---- double-click on a text ----

    [Test]
    public void DoubleClick_OnAText_OpensTheBoxWithItsTextPlaceColourAndSize_AndTheTextIsLeftOutOfThePicture()
    {
        var rig = EditorRig.Create();
        rig.ViewModel.SetColor(EditorColors.Palette[4].Color);
        rig.ViewModel.FontSize = 30;
        var text = rig.AddText(40, 60, OldText);
        rig.ViewModel.SetColor(EditorColors.Palette[0].Color);
        rig.ViewModel.FontSize = 18;
        var steps = rig.Session.HistorySteps;

        rig.DoubleClick(45, 70);

        Assert.That(rig.ViewModel.IsEditingText, Is.True);
        Assert.That(rig.ViewModel.TextDraftText, Is.EqualTo(OldText));
        Assert.That(rig.ViewModel.TextDraftOrigin, Is.EqualTo(P(40, 60)));
        Assert.That(rig.ViewModel.TextDraftColor, Is.EqualTo(text.Color), "the text keeps its own colour in the box, not the colour of the next shapes");
        Assert.That(rig.ViewModel.TextDraftFontSize, Is.EqualTo(30), "and its own size");
        Assert.That(rig.ViewModel.DraftReplaces, Is.EqualTo(text.Id), "the canvas leaves the committed text out while the box shows it");
        Assert.That(rig.Session.SelectedId, Is.EqualTo(text.Id));
        Assert.That(rig.Session.HistorySteps, Is.EqualTo(steps), "opening the box is not an edit");
    }

    [Test]
    public void DoubleClick_CommitWithNewText_IsOneSetTextForThatText_AndNotAnAddText()
    {
        var rig = EditorRig.Create();
        var text = rig.AddText(40, 60, OldText);
        var steps = rig.Session.HistorySteps;
        rig.DoubleClick(45, 70);

        rig.ViewModel.TextDraftText = "Chữ mới\r\ndòng hai";
        rig.ViewModel.CommitTextCommand.Execute(null);

        Assert.That(rig.Session.TextSets, Is.EqualTo(new[] { (text.Id, "Chữ mới\ndòng hai") }), "a plain \\n, as for a new text");
        Assert.That(rig.Interactor.TextCalls, Has.Count.EqualTo(1), "only the first commit was an AddText");
        Assert.That(rig.Session.HistorySteps, Is.EqualTo(steps + 1));
        Assert.That(rig.ViewModel.IsEditingText, Is.False);
        Assert.That(rig.ViewModel.DraftReplaces, Is.Null, "the text is back in the picture");

        rig.ViewModel.UndoCommand.Execute(null);
        Assert.That(rig.Session.Document.Annotations.OfType<TextAnnotation>().Only().Text, Is.EqualTo(OldText));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(" \r\n ")]
    public void DoubleClick_EmptiedOrBlankBox_DeletesTheTextThroughSetText(string typed)
    {
        var rig = EditorRig.Create();
        var text = rig.AddText(40, 60, OldText);
        var steps = rig.Session.HistorySteps;
        rig.DoubleClick(45, 70);

        rig.ViewModel.TextDraftText = typed;
        rig.ViewModel.CommitText();

        Assert.That(rig.Session.TextSets, Is.EqualTo(new[] { (text.Id, string.Empty) }), "a blank box is an empty text");
        Assert.That(rig.Session.Document.Annotations, Is.Empty);
        Assert.That(rig.Session.HistorySteps, Is.EqualTo(steps + 1), "one step");
        Assert.That(rig.Notes.Toasts.Concat(rig.Notes.Errors), Is.Empty);
    }

    [Test]
    public void DoubleClick_CommitWithTheTextUnchanged_CallsNothingAndAddsNoStep()
    {
        var rig = EditorRig.Create();
        rig.AddText(40, 60, OldText);
        var steps = rig.Session.HistorySteps;
        rig.DoubleClick(45, 70);

        rig.ViewModel.CommitText();

        Assert.That(rig.Session.TextSets, Is.Empty);
        Assert.That(rig.Session.HistorySteps, Is.EqualTo(steps));
        Assert.That(rig.ViewModel.IsEditingText, Is.False);
    }

    [Test]
    public void DoubleClick_EscapeCommitsLikeTheTextTool_AndSoDoesAClickElsewhere()
    {
        var rig = EditorRig.Create();
        var text = rig.AddText(40, 60, OldText);
        rig.DoubleClick(45, 70);
        rig.ViewModel.TextDraftText = "Bằng Esc";

        rig.ViewModel.CancelCommand.Execute(null);

        Assert.That(rig.Session.TextSets, Is.EqualTo(new[] { (text.Id, "Bằng Esc") }), "Esc chốt chữ");

        rig.DoubleClick(45, 70);
        rig.ViewModel.TextDraftText = "Bằng chuột";
        rig.Click(280, 150);

        Assert.That(rig.Session.TextSets.Select(s => s.Text), Is.EqualTo(new[] { "Bằng Esc", "Bằng chuột" }));
        Assert.That(rig.ViewModel.IsEditingText, Is.False, "the click that commits does not open anything");
    }

    [Test]
    public void DoubleClick_UndoWhileTheBoxIsOpen_CommitsFirst_ThenUndoesThatCommit()
    {
        var rig = EditorRig.Create();
        rig.AddText(40, 60, OldText);
        rig.DoubleClick(45, 70);
        rig.ViewModel.TextDraftText = "Sửa rồi hoàn tác";

        rig.ViewModel.UndoCommand.Execute(null);

        Assert.That(rig.Session.TextSets, Has.Count.EqualTo(1), "the typed text was committed first");
        Assert.That(rig.Session.Document.Annotations.OfType<TextAnnotation>().Only().Text, Is.EqualTo(OldText), "and that step was the one undone");
        Assert.That(rig.ViewModel.IsEditingText, Is.False);
    }

    [Test]
    public void DoubleClick_OnOtherKinds_SelectsThemAndOpensNothing()
    {
        var rig = EditorRig.Create();
        rig.Draw(ToolKind.Rectangle, P(200, 30), P(280, 90));
        var frame = rig.Session.Document.Annotations.Only("rectangle");
        var step = rig.AddStep(100, 150);

        rig.DoubleClick(200, 60);
        Assert.That(rig.ViewModel.IsEditingText, Is.False, "a frame is not edited by a double-click");
        Assert.That(rig.Session.SelectedId, Is.EqualTo(frame.Id), "it is selected, as a click would");

        rig.DoubleClick(100, 150);
        Assert.That(rig.ViewModel.IsEditingText, Is.False, "nor is a step marker");
        Assert.That(rig.Session.SelectedId, Is.EqualTo(step.Id));

        rig.DoubleClick(20, 20);
        Assert.That(rig.Session.SelectedId, Is.Null, "and a double-click on nothing drops the selection");
        Assert.That(rig.Session.Calls, Has.None.EqualTo("SetText"));
    }

    [Test]
    public void DoubleClick_WithAnotherToolThanSelect_IsJustAnotherPress()
    {
        var rig = EditorRig.Create();
        var text = rig.AddText(40, 60, OldText);
        rig.ViewModel.SelectTool(ToolKind.StepNumber);

        rig.DoubleClick(45, 70);

        Assert.That(rig.ViewModel.IsEditingText, Is.False, "the Select tool edits texts, this one places markers");
        Assert.That(rig.Session.Document.Annotations.OfType<StepAnnotation>().Count(), Is.EqualTo(2), "both presses placed a marker, as before");
        Assert.That(rig.Session.Document.Annotations.OfType<TextAnnotation>().Only().Id, Is.EqualTo(text.Id));
    }

    [Test]
    public void DoubleClick_OnAnotherTextWhileABoxIsOpen_CommitsTheFirstAndOpensTheSecond()
    {
        var rig = EditorRig.Create();
        var first = rig.AddText(40, 60, OldText);
        rig.AddText(40, 120, "Chữ thứ hai");
        rig.DoubleClick(45, 70);
        rig.ViewModel.TextDraftText = "Đã sửa";

        rig.DoubleClick(45, 130);

        Assert.That(rig.Session.TextSets, Is.EqualTo(new[] { (first.Id, "Đã sửa") }), "the first press of the double-click committed the open box");
        Assert.That(rig.ViewModel.IsEditingText, Is.True, "and the second press opened the other text");
        Assert.That(rig.ViewModel.TextDraftText, Is.EqualTo("Chữ thứ hai"));
    }

    [Test]
    public void TextTool_ClickStillPlacesANewBoxWithTheNextColourAndSize()
    {
        var rig = EditorRig.Create();
        rig.ViewModel.FontSize = 26;
        rig.ViewModel.SelectTool(ToolKind.Text);

        rig.Click(40, 60);

        Assert.That(rig.ViewModel.IsEditingText, Is.True);
        Assert.That(rig.ViewModel.TextDraftFontSize, Is.EqualTo(26));
        Assert.That(rig.ViewModel.TextDraftColor, Is.EqualTo(rig.ViewModel.Color));
        Assert.That(rig.ViewModel.TextDraftText, Is.Empty);
    }

    // ---- the size box ----

    [Test]
    public void FontSize_TypedKeystrokesWhileATextIsSelected_WaitForTheCommit_ThenAreOneSetFontSize()
    {
        var rig = EditorRig.Create();
        var text = rig.AddText(40, 60, OldText);
        rig.Click(45, 70);
        var steps = rig.Session.HistorySteps;

        foreach (var typed in new[] { "1", "12", "120" })
        {
            rig.ViewModel.FontSizeText = typed;
        }

        Assert.That(rig.Session.FontSizeSets, Is.Empty, "typing changes nothing yet");
        Assert.That(rig.ViewModel.FontSizeText, Is.EqualTo("120"), "and the box is not rewritten under the typing hand");

        rig.ViewModel.CommitFontSize();

        Assert.That(rig.Session.FontSizeSets, Is.EqualTo(new[] { (text.Id, 120) }));
        Assert.That(rig.Session.HistorySteps, Is.EqualTo(steps + 1));
        Assert.That(rig.ViewModel.FontSize, Is.EqualTo(120), "the box shows the size of the selected text");

        rig.ViewModel.CommitFontSize();
        Assert.That(rig.Session.FontSizeSets, Has.Count.EqualTo(1), "committing twice is one change");
    }

    [Test]
    public void FontSize_AValueThatIsNotASize_IsNotTaken_AndTheBoxGoesBackToTheRealSizeOnCommit()
    {
        var rig = EditorRig.Create();
        var text = rig.AddText(40, 60, OldText);
        rig.Click(45, 70);

        foreach (var typed in new[] { string.Empty, "abc", "3", "300", "-5" })
        {
            rig.ViewModel.FontSizeText = typed;
            rig.ViewModel.CommitFontSize();

            Assert.That(rig.Session.FontSizeSets, Is.Empty, $"'{typed}' is not a size from 6 to 200");
            Assert.That(rig.ViewModel.FontSizeText, Is.EqualTo("18"), $"after '{typed}' the box shows the real size again");
        }

        Assert.That(rig.Session.Document.Annotations.OfType<TextAnnotation>().Only().FontSize, Is.EqualTo(text.FontSize));
    }

    [Test]
    public void FontSize_ATypedSizeEqualToTheCurrentOne_CallsNothing()
    {
        var rig = EditorRig.Create();
        rig.AddText(40, 60, OldText);
        rig.Click(45, 70);

        rig.ViewModel.FontSizeText = "18";
        rig.ViewModel.CommitFontSize();

        Assert.That(rig.Session.FontSizeSets, Is.Empty);
        Assert.That(rig.Session.HistorySteps, Is.EqualTo(1), "only the text's own step");
    }

    [Test]
    public void FontSize_EscapeInTheBoxTakesBackWhatWasTyped()
    {
        var rig = EditorRig.Create();
        rig.AddText(40, 60, OldText);
        rig.Click(45, 70);
        rig.ViewModel.FontSizeText = "44";

        rig.ViewModel.CancelCommand.Execute(null);

        Assert.That(rig.ViewModel.FontSizeText, Is.EqualTo("18"));
        Assert.That(rig.Session.SelectedId, Is.Not.Null, "the first Esc only undid the typing; the text is still selected");
        rig.ViewModel.CommitFontSize();
        Assert.That(rig.Session.FontSizeSets, Is.Empty);
    }

    [Test]
    public void FontSize_OnAStepMarker_ChangesThatMarkerAndTheNextStepsKeepTheirOwnSize()
    {
        var rig = EditorRig.Create();
        var first = rig.AddStep(60, 60);
        var second = rig.AddStep(160, 60);
        rig.Click(60, 60);

        rig.ViewModel.FontSizeText = "40";
        rig.ViewModel.CommitFontSize();

        Assert.That(rig.Session.FontSizeSets, Is.EqualTo(new[] { (first.Id, 40) }));
        var steps = rig.Session.Document.Annotations.OfType<StepAnnotation>().ToList();
        Assert.That(steps.Select(s => (s.Id, s.FontSize)), Is.EqualTo(new[] { (first.Id, 40), (second.Id, 18) }));

        rig.ViewModel.SelectTool(ToolKind.StepNumber);
        rig.Click(260, 60);
        Assert.That(rig.Session.Document.Annotations.OfType<StepAnnotation>().Last().FontSize, Is.EqualTo(18), "the next marker has the next size, not the selected one's");
    }

    [Test]
    public void FontSize_WhenSelectingAnotherText_TheBoxShowsThatTextsSize_AndDeselectingBringsTheNextSizeBack()
    {
        var rig = EditorRig.Create();
        rig.ViewModel.FontSize = 24;
        rig.AddText(40, 60, "Nhỏ");
        rig.ViewModel.FontSize = 32;
        rig.AddText(40, 140, "Lớn");
        rig.ViewModel.FontSize = 18;

        rig.Click(45, 70);
        Assert.That(rig.ViewModel.FontSizeText, Is.EqualTo("24"), "the first text is 24");
        rig.Click(45, 150);
        Assert.That(rig.ViewModel.FontSizeText, Is.EqualTo("32"), "the second one is 32");
        rig.Click(280, 20);
        Assert.That(rig.ViewModel.FontSizeText, Is.EqualTo("18"), "nothing selected: the size of the next text");
        Assert.That(rig.ViewModel.FontSize, Is.EqualTo(18));
    }

    [Test]
    public void FontSize_ATypedSizeIsAppliedToTheTextThatWasSelectedWhenTypingBegan_EvenIfTheNextClickSelectsAnother()
    {
        var rig = EditorRig.Create();
        var first = rig.AddText(40, 60, "Một");
        rig.AddText(40, 140, "Hai");
        rig.Click(45, 70);
        rig.ViewModel.FontSizeText = "50";

        rig.Click(45, 150);

        Assert.That(rig.Session.FontSizeSets, Is.EqualTo(new[] { (first.Id, 50) }), "committed by the click, before the selection moved");
    }

    [Test]
    public void FontSize_WithNothingSelectedOrAFrameSelected_IsTheNextSize_AndNoDrawingIsTouched()
    {
        var rig = EditorRig.Create();
        rig.Draw(ToolKind.Rectangle, P(50, 50), P(150, 120));
        rig.ViewModel.SelectTool(ToolKind.Select);

        rig.ViewModel.FontSizeText = "30";
        Assert.That(rig.ViewModel.FontSize, Is.EqualTo(30), "nothing selected");

        rig.Click(50, 85);
        Assert.That(rig.Session.SelectedId, Is.Not.Null, "the frame is selected");
        rig.ViewModel.FontSizeText = "44";
        rig.ViewModel.CommitFontSize();

        Assert.That(rig.ViewModel.FontSize, Is.EqualTo(44), "a frame has no font: the box keeps meaning the next size");
        Assert.That(rig.Session.FontSizeSets, Is.Empty);
        rig.ViewModel.SelectTool(ToolKind.StepNumber);
        rig.Click(200, 150);
        Assert.That(rig.Session.Document.Annotations.OfType<StepAnnotation>().Only().FontSize, Is.EqualTo(44));
    }

    [Test]
    public void FontSize_ThePropertyOnASelectedText_IsOneCall_AndTheSessionClampsAndIgnoresTheSameSize()
    {
        var rig = EditorRig.Create();
        var text = rig.AddText(40, 60, OldText);
        rig.Click(45, 70);

        rig.ViewModel.FontSize = 18;
        Assert.That(rig.Session.FontSizeSets, Is.Empty, "the same size asks nothing");

        rig.ViewModel.FontSize = 500;
        Assert.That(rig.Session.FontSizeSets, Is.EqualTo(new[] { (text.Id, 200) }), "clamped to 200 before it is asked");
        Assert.That(rig.Session.Document.Annotations.OfType<TextAnnotation>().Only().FontSize, Is.EqualTo(200));
    }

    // ---- the thickness slider ----

    [Test]
    public void Thickness_ADragOfTheThumbOnASelectedShape_IsOneSetThickness_AndTheShapeIsOnlyPreviewedMeanwhile()
    {
        var rig = EditorRig.Create();
        rig.Draw(ToolKind.Rectangle, P(50, 50), P(150, 120));
        rig.ViewModel.SelectTool(ToolKind.Select);
        rig.Click(50, 85);
        var shape = rig.Session.Document.Annotations.Only("rectangle");
        var steps = rig.Session.HistorySteps;

        rig.ViewModel.BeginThicknessChange();
        for (var value = 5; value <= 12; value++)
        {
            rig.ViewModel.Thickness = value;
        }

        Assert.That(rig.Session.Calls, Has.None.EqualTo("SetThickness"), "nothing reaches the history while the thumb is held");
        Assert.That(rig.ViewModel.Thickness, Is.EqualTo(12), "but the slider and the number show the drag");
        Assert.That(rig.ViewModel.Draft?.Thickness, Is.EqualTo(12), "and the canvas previews the shape with it");
        Assert.That(rig.ViewModel.DraftReplaces, Is.EqualTo(shape.Id));

        rig.ViewModel.EndThicknessChange();

        Assert.That(rig.Session.Calls.Count(c => c == "SetThickness"), Is.EqualTo(1));
        Assert.That(rig.Session.Document.Annotations.Only("rectangle").Thickness, Is.EqualTo(12));
        Assert.That(rig.Session.HistorySteps, Is.EqualTo(steps + 1));
        Assert.That(rig.ViewModel.Draft, Is.Null, "the preview is gone; the real shape is drawn");
        Assert.That(rig.ViewModel.DraftReplaces, Is.Null);
    }

    [Test]
    public void Thickness_ADragThatEndsWhereItBegan_AddsNoStep()
    {
        var rig = EditorRig.Create();
        rig.Draw(ToolKind.Rectangle, P(50, 50), P(150, 120));
        rig.ViewModel.SelectTool(ToolKind.Select);
        rig.Click(50, 85);
        var steps = rig.Session.HistorySteps;

        rig.ViewModel.BeginThicknessChange();
        rig.ViewModel.Thickness = 9;
        rig.ViewModel.Thickness = 4;
        rig.ViewModel.EndThicknessChange();

        Assert.That(rig.Session.Calls, Has.None.EqualTo("SetThickness"));
        Assert.That(rig.Session.HistorySteps, Is.EqualTo(steps));
    }

    [Test]
    public void Thickness_ADragWithNothingSelected_SetsTheNextShapesAtOnce_AndNoStep()
    {
        var rig = EditorRig.Create();

        rig.ViewModel.BeginThicknessChange();
        rig.ViewModel.Thickness = 9;
        Assert.That(rig.ViewModel.Thickness, Is.EqualTo(9));
        rig.ViewModel.EndThicknessChange();
        rig.Draw(ToolKind.Line, P(10, 10), P(100, 10));

        Assert.That(rig.Session.Document.Annotations.OfType<LineAnnotation>().Only().Thickness, Is.EqualTo(9));
        Assert.That(rig.Session.Calls, Has.None.EqualTo("SetThickness"));
    }

    [Test]
    public void Thickness_AValueSetOutsideADrag_ChangesTheSelectedShapeAtOnce_LikeAKeyPressOnTheSlider()
    {
        var rig = EditorRig.Create();
        rig.Draw(ToolKind.Rectangle, P(50, 50), P(150, 120));
        rig.ViewModel.SelectTool(ToolKind.Select);
        rig.Click(50, 85);

        rig.ViewModel.Thickness = 5;
        rig.ViewModel.Thickness = 6;

        Assert.That(rig.Session.Calls.Count(c => c == "SetThickness"), Is.EqualTo(2), "each discrete change is a step, as before");
        rig.ViewModel.EndThicknessChange();
        Assert.That(rig.Session.Calls.Count(c => c == "SetThickness"), Is.EqualTo(2), "an End with no Begin does nothing");
    }

    // ---- selecting goes through the use case ----

    [Test]
    public void Select_ClickInsideAFrame_SelectsNothing_AndOnTheTextOrTheMarkerItSelectsThem()
    {
        var rig = EditorRig.Create();
        rig.Draw(ToolKind.Rectangle, P(150, 30), P(280, 150));
        var text = rig.AddText(20, 40, OldText);
        var step = rig.AddStep(60, 120);

        rig.Click(215, 90);
        Assert.That(rig.Session.SelectedId, Is.Null, "the middle of a frame is not the frame: the use case says only its outline is hit");

        rig.Click(30, 50);
        Assert.That(rig.Session.SelectedId, Is.EqualTo(text.Id), "inside the box of a text");

        rig.Click(60, 120);
        Assert.That(rig.Session.SelectedId, Is.EqualTo(step.Id), "inside a step marker");
        Assert.That(rig.Interactor.HitTests, Has.Count.EqualTo(3), "each click asked once");
    }
}
