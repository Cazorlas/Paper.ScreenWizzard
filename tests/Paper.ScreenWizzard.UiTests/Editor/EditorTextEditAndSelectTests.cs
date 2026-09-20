using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Editor;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.UiTests.ScreenCapture;
using Paper.ScreenWizzard.UiTests.Support;

namespace Paper.ScreenWizzard.UiTests.ImageEditor;

/// <summary>
/// Plan T27 / T28, SPEC editor "Chữ" and "Số bước": a committed text is edited again with a double-click of the Select tool; the size box
/// changes the SELECTED text or step marker, once per committed change; the thickness slider does not fill the history while it is dragged;
/// and a click asks the use case which shape is there (IEditorInteractor.HitTest) instead of deciding in the window. The window is real, the
/// mouse and keyboard are real, the session and the use case are the fakes in EditorFakes.cs.
/// </summary>
[TestFixture]
public sealed class EditorTextEditAndSelectTests : UiTestBase
{
    private const string OldText = "Ghi chú cũ";

    private static PixelPoint P(int x, int y) => new(x, y);

    // A committed text through the same door the user uses (the Text tool), then back to the Select tool, so the fake session holds a TextAnnotation.
    private static TextAnnotation AddText(EditorWindowRig rig, int x, int y, string text)
    {
        rig.Ui(() =>
        {
            rig.ViewModel.SelectTool(ToolKind.Text);
            rig.Rig.Click(x, y);
            rig.ViewModel.TextDraftText = text;
            rig.ViewModel.CommitText();
            rig.ViewModel.SelectTool(ToolKind.Select);
        });
        return rig.Rig.Session.Document.Annotations.OfType<TextAnnotation>().Last();
    }

    private static StepAnnotation AddStep(EditorWindowRig rig, int x, int y)
    {
        rig.Ui(() =>
        {
            rig.ViewModel.SelectTool(ToolKind.StepNumber);
            rig.Rig.Click(x, y);
            rig.ViewModel.SelectTool(ToolKind.Select);
        });
        return rig.Rig.Session.Document.Annotations.OfType<StepAnnotation>().Last();
    }

    // The text box is the keyboard's: put the focus in it (Ctrl+Enter and Esc are bound there).
    private static void FocusDraftBox(EditorWindowRig rig)
    {
        rig.Window.EnsureForeground();
        rig.Window.Focus("TextDraftBox");
    }

    // ---- double-click edits a committed text ----

    [Test]
    public void DoubleClick_OnACommittedText_OpensTheDraftBoxWithTheOldTextWhereTheTextIs()
    {
        using var rig = EditorWindowRig.Open(image: EditorTestData.White(400, 200));
        var text = AddText(rig, 30, 40, OldText);
        var steps = rig.Rig.Session.HistorySteps;

        rig.MouseDoubleClick(P(36, 48));

        Assert.That(rig.ViewModel.IsEditingText, Is.True, "the double-click opened the text box");
        Assert.That(rig.Window.Exists("TextDraftBox"), Is.True, "and it is on the picture");
        Assert.That(rig.Window.TextOf("TextDraftBox"), Is.EqualTo(OldText), "with the text that is there now");
        Assert.That(rig.ViewModel.TextDraftOrigin, Is.EqualTo(text.Origin), "at the place of the text");
        Assert.That(rig.Rig.Session.HistorySteps, Is.EqualTo(steps), "nothing enters the history until the edit is committed");
        Assert.That(rig.Rig.Session.Calls, Has.None.EqualTo("SetText"));
    }

    [Test]
    public void DoubleClick_EditAndCtrlEnter_ReplacesTheTextThroughSetText_InOneStep_AndUndoBringsTheOldTextBack()
    {
        using var rig = EditorWindowRig.Open(image: EditorTestData.White(400, 200));
        var text = AddText(rig, 30, 40, OldText);
        var steps = rig.Rig.Session.HistorySteps;
        rig.MouseDoubleClick(P(36, 48));
        Assert.That(rig.Window.Exists("TextDraftBox"), Is.True, "the box opened");

        rig.Window.SetText("TextDraftBox", "Đường ống 45° — mới");
        FocusDraftBox(rig);
        rig.Window.PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.RETURN);

        Assert.That(rig.Rig.Session.TextSets, Is.EqualTo(new[] { (text.Id, "Đường ống 45° — mới") }), "one SetText, for THAT annotation");
        Assert.That(rig.Rig.Session.Document.Annotations.OfType<TextAnnotation>().Only("text").Text, Is.EqualTo("Đường ống 45° — mới"));
        Assert.That(rig.Rig.Session.Document.Annotations, Has.Count.EqualTo(1), "the text was replaced, not added to");
        Assert.That(rig.Rig.Interactor.TextCalls, Has.Count.EqualTo(1), "only the first commit went through AddText");
        Assert.That(rig.Rig.Session.HistorySteps, Is.EqualTo(steps + 1), "one history step");
        Assert.That(rig.Window.Exists("TextDraftBox"), Is.False, "the box is gone");

        rig.Window.Click("UndoButton");
        Assert.That(rig.Rig.Session.Document.Annotations.OfType<TextAnnotation>().Only("text").Text, Is.EqualTo(OldText), "one Undo brings the old text back");
    }

    [Test]
    public void DoubleClick_EmptiedBox_DeletesTheText_InOneStep_AndUndoBringsItBack()
    {
        using var rig = EditorWindowRig.Open(image: EditorTestData.White(400, 200));
        var text = AddText(rig, 30, 40, OldText);
        var steps = rig.Rig.Session.HistorySteps;
        rig.MouseDoubleClick(P(36, 48));
        Assert.That(rig.Window.Exists("TextDraftBox"), Is.True, "the box opened");

        rig.Window.SetText("TextDraftBox", string.Empty);
        FocusDraftBox(rig);
        rig.Window.PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.RETURN);

        Assert.That(rig.Rig.Session.TextSets, Is.EqualTo(new[] { (text.Id, string.Empty) }), "an emptied box is SetText with an empty text");
        Assert.That(rig.Rig.Session.Document.Annotations, Is.Empty, "the text is gone");
        Assert.That(rig.Rig.Session.HistorySteps, Is.EqualTo(steps + 1), "one history step");
        Assert.That(rig.Rig.Notes.Toasts.Concat(rig.Rig.Notes.Errors), Is.Empty, "and nothing is said, as for F4");

        rig.Window.Click("UndoButton");
        Assert.That(rig.Rig.Session.Document.Annotations.OfType<TextAnnotation>().Only("text").Text, Is.EqualTo(OldText));
    }

    [Test]
    public void DoubleClick_Escape_CommitsTheEditLikeTheTextToolDoes()
    {
        using var rig = EditorWindowRig.Open(image: EditorTestData.White(400, 200));
        var text = AddText(rig, 30, 40, OldText);
        rig.MouseDoubleClick(P(36, 48));
        Assert.That(rig.Window.Exists("TextDraftBox"), Is.True, "the box opened");

        rig.Window.SetText("TextDraftBox", "Sửa bằng Esc");
        FocusDraftBox(rig);
        rig.Window.Press(VirtualKeyShort.ESCAPE);

        Assert.That(rig.Rig.Session.TextSets, Is.EqualTo(new[] { (text.Id, "Sửa bằng Esc") }), "SPEC: Esc chốt chữ, for an edit as for a new text");
        Assert.That(rig.ViewModel.IsEditingText, Is.False);
    }

    [Test]
    public void DoubleClick_ClickElsewhereWhileEditing_CommitsOnce()
    {
        using var rig = EditorWindowRig.Open(image: EditorTestData.White(400, 200));
        var text = AddText(rig, 30, 40, OldText);
        rig.MouseDoubleClick(P(36, 48));
        Assert.That(rig.Window.Exists("TextDraftBox"), Is.True, "the box opened");
        rig.Window.SetText("TextDraftBox", "Bấm ra ngoài");

        rig.MouseClick(P(300, 150));

        Assert.That(rig.Rig.Session.TextSets, Is.EqualTo(new[] { (text.Id, "Bấm ra ngoài") }), "the click committed it, and only once");
        Assert.That(rig.Window.Exists("TextDraftBox"), Is.False);
    }

    [Test]
    public void DoubleClick_ThenEscapeWithoutTyping_ChangesNothingAndAddsNoStep()
    {
        using var rig = EditorWindowRig.Open(image: EditorTestData.White(400, 200));
        AddText(rig, 30, 40, OldText);
        var steps = rig.Rig.Session.HistorySteps;
        rig.MouseDoubleClick(P(36, 48));
        Assert.That(rig.Window.Exists("TextDraftBox"), Is.True, "the box opened");

        FocusDraftBox(rig);
        rig.Window.Press(VirtualKeyShort.ESCAPE);

        Assert.That(rig.Rig.Session.HistorySteps, Is.EqualTo(steps), "a text left as it was is not an edit");
        Assert.That(rig.Rig.Session.Document.Annotations.OfType<TextAnnotation>().Only("text").Text, Is.EqualTo(OldText));
    }

    [Test]
    public void DoubleClick_OnAShape_OpensNoTextBox()
    {
        using var rig = EditorWindowRig.Open(image: EditorTestData.White(400, 200));
        rig.Ui(() => rig.Rig.Draw(ToolKind.Rectangle, P(50, 50), P(200, 120)));
        rig.ChooseTool(ToolKind.Select);

        rig.MouseDoubleClick(P(50, 85));

        Assert.That(rig.ViewModel.IsEditingText, Is.False, "only a text is edited by a double-click");
        Assert.That(rig.Window.Exists("TextDraftBox"), Is.False);
        Assert.That(rig.Rig.Session.Calls, Has.None.EqualTo("SetText"));
    }

    // ---- the size box changes the selected text or step marker ----

    [Test]
    public void FontSize_TypedWhileATextIsSelected_ChangesThatTextOnEnter_AndNotTheNextOnes()
    {
        using var rig = EditorWindowRig.Open(image: EditorTestData.White(400, 200));
        var text = AddText(rig, 30, 40, OldText);
        rig.MouseClick(P(36, 48));
        Assert.That(rig.Rig.Session.SelectedId, Is.EqualTo(text.Id), "the text is selected");
        var steps = rig.Rig.Session.HistorySteps;

        rig.Window.SetText("FontSizeBox", "30");
        rig.Window.Focus("FontSizeBox");
        rig.Window.Press(VirtualKeyShort.RETURN);

        Assert.That(rig.Rig.Session.FontSizeSets, Is.EqualTo(new[] { (text.Id, 30) }), "SetFontSize for the selected text");
        Assert.That(rig.Rig.Session.Document.Annotations.OfType<TextAnnotation>().Only("text").FontSize, Is.EqualTo(30));
        Assert.That(rig.Rig.Session.HistorySteps, Is.EqualTo(steps + 1), "one history step");

        rig.Window.Click("UndoButton");
        Assert.That(rig.Rig.Session.Document.Annotations.OfType<TextAnnotation>().Only("text").FontSize, Is.EqualTo(18), "and Undo puts the size back");
    }

    [Test]
    public void FontSize_TypedKeystrokes_AreOneSetFontSizeWhenCommitted_NotOnePerKey()
    {
        using var rig = EditorWindowRig.Open(image: EditorTestData.White(400, 200));
        var text = AddText(rig, 30, 40, OldText);
        rig.MouseClick(P(36, 48));
        rig.Window.Focus("FontSizeBox");
        rig.Window.PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);

        Keyboard.Type("120");
        rig.Window.Host.Settle();
        Assert.That(rig.Rig.Session.FontSizeSets, Is.Empty, "typing changes nothing in the history yet");

        rig.Window.Press(VirtualKeyShort.RETURN);

        Assert.That(rig.Rig.Session.FontSizeSets, Is.EqualTo(new[] { (text.Id, 120) }), "one call, with the size that was typed, not 12 and then 120");
    }

    [Test]
    public void FontSize_TypedWhileAStepMarkerIsSelected_ChangesThatMarkerOnly()
    {
        using var rig = EditorWindowRig.Open(image: EditorTestData.White(400, 200));
        var first = AddStep(rig, 60, 60);
        var second = AddStep(rig, 160, 60);
        rig.MouseClick(P(60, 60));
        Assert.That(rig.Rig.Session.SelectedId, Is.EqualTo(first.Id), "the first marker is selected");

        rig.Window.SetText("FontSizeBox", "40");
        rig.Window.Focus("FontSizeBox");
        rig.Window.Press(VirtualKeyShort.RETURN);

        var steps = rig.Rig.Session.Document.Annotations.OfType<StepAnnotation>().ToList();
        Assert.That(rig.Rig.Session.FontSizeSets, Is.EqualTo(new[] { (first.Id, 40) }));
        Assert.That(steps.Select(s => s.FontSize), Is.EqualTo(new[] { 40, 18 }), "the selected marker grew and the other did not");
        Assert.That(steps.Select(s => s.Id), Is.EqualTo(new[] { first.Id, second.Id }));
    }

    [Test]
    public void FontSize_LeavingTheBoxWithTheMouse_AlsoCommitsTheTypedSize()
    {
        using var rig = EditorWindowRig.Open(image: EditorTestData.White(400, 200));
        var text = AddText(rig, 30, 40, OldText);
        rig.MouseClick(P(36, 48));
        rig.Window.SetText("FontSizeBox", "26");
        rig.Window.Focus("FontSizeBox");

        rig.Window.Click("Zoom100Button");

        Assert.That(rig.Rig.Session.FontSizeSets, Is.EqualTo(new[] { (text.Id, 26) }), "focus left the box: the size is committed");
    }

    [Test]
    public void FontSize_WithNothingSelected_OnlySetsTheSizeOfTheNextTextAndCallsNoSession()
    {
        using var rig = EditorWindowRig.Open(image: EditorTestData.White(400, 200));
        AddText(rig, 30, 40, OldText);
        Assert.That(rig.Rig.Session.SelectedId, Is.Null, "nothing is selected");

        rig.Window.SetText("FontSizeBox", "30");
        rig.Window.Focus("FontSizeBox");
        rig.Window.Press(VirtualKeyShort.RETURN);

        Assert.That(rig.Rig.Session.FontSizeSets, Is.Empty, "no drawing was touched");
        Assert.That(rig.ViewModel.FontSize, Is.EqualTo(30), "the next text or step marker will have this size");
        Assert.That(rig.Rig.Session.Document.Annotations.OfType<TextAnnotation>().Only("text").FontSize, Is.EqualTo(18));
    }

    // ---- the thickness slider ----

    [Test]
    public void Thickness_DraggingTheSliderOnASelectedShape_IsOneHistoryStepNotOnePerNotch()
    {
        using var rig = EditorWindowRig.Open(image: EditorTestData.White(400, 200));
        rig.Ui(() =>
        {
            rig.Rig.Draw(ToolKind.Rectangle, P(50, 50), P(200, 120));
            rig.ViewModel.SelectTool(ToolKind.Select);
            rig.Rig.Click(50, 85);
        });
        Assert.That(rig.Rig.Session.SelectedId, Is.Not.Null, "the rectangle is selected");
        var steps = rig.Rig.Session.HistorySteps;
        var before = rig.Rig.Session.Document.Annotations.Only("rectangle").Thickness;

        rig.DragThicknessThumb(60);

        var after = rig.Rig.Session.Document.Annotations.Only("rectangle").Thickness;
        Assert.That(after, Is.GreaterThan(before), "the drag moved the slider over several notches");
        Assert.That(rig.Rig.Session.Calls.Count(c => c == "SetThickness"), Is.EqualTo(1), "one SetThickness for the whole drag");
        Assert.That(rig.Rig.Session.HistorySteps, Is.EqualTo(steps + 1), "one history step for the whole drag");

        rig.Window.Click("UndoButton");
        Assert.That(rig.Rig.Session.Document.Annotations.Only("rectangle").Thickness, Is.EqualTo(before), "and one Undo puts the thickness back");
    }

    // ---- a click asks the use case what is there ----

    [Test]
    public void Select_AClickAsksTheUseCaseWhichShapeIsThere_WithAToleranceInImagePixels()
    {
        var rig = EditorRig.Create();
        rig.Draw(ToolKind.Rectangle, P(50, 50), P(150, 120));
        rig.ViewModel.SelectTool(ToolKind.Select);

        rig.Click(280, 20);

        Assert.That(rig.Interactor.HitTests, Has.Count.EqualTo(1), "the window asked IEditorInteractor.HitTest instead of computing the hit itself");
        Assert.That(rig.Interactor.HitTests[0].Point, Is.EqualTo(P(280, 20)), "with the image pixel that was clicked");
        Assert.That(rig.Interactor.HitTests[0].Tolerance, Is.GreaterThan(0), "and a tolerance");
    }

    [Test]
    public void Select_TheToleranceIsAFixedNumberOfScreenPixels_SoItGrowsInImagePixelsWhenTheImageIsShrunk()
    {
        var rig = EditorRig.Create();
        rig.ViewModel.SelectTool(ToolKind.Select);

        rig.ViewModel.SetZoom(1);
        rig.Click(10, 10);
        rig.ViewModel.SetZoom(0.5);
        rig.Click(10, 10);

        Assert.That(rig.Interactor.HitTests, Has.Count.EqualTo(2), "one question per click");
        var (atFull, atHalf) = (rig.Interactor.HitTests[0].Tolerance, rig.Interactor.HitTests[1].Tolerance);
        Assert.That(atFull, Is.InRange(2, 10), "a few pixels at 100%");
        Assert.That(atHalf, Is.EqualTo(atFull * 2).Within(1), "twice as many image pixels at 50%: the same distance on the screen");
    }

    [Test]
    public void Select_WhatTheUseCaseAnswersIsWhatGetsSelected_EvenWhereTheWindowWouldHaveSaidNothing()
    {
        var rig = EditorRig.Create();
        rig.Draw(ToolKind.Rectangle, P(50, 50), P(150, 120));
        var shape = rig.Session.Document.Annotations.Only("rectangle");
        rig.ViewModel.SelectTool(ToolKind.Select);
        rig.Interactor.HitTestOverride = (_, _, _) => shape.Id;

        rig.Click(280, 20);

        Assert.That(rig.Session.SelectedId, Is.EqualTo(shape.Id), "far from the rectangle, but the use case said it is the one");
    }

    [Test]
    public void Select_WhenTheUseCaseAnswersNothing_NothingIsSelected_EvenOnTheEdgeOfAShape()
    {
        var rig = EditorRig.Create();
        rig.Draw(ToolKind.Rectangle, P(50, 50), P(150, 120));
        rig.ViewModel.SelectTool(ToolKind.Select);
        rig.Interactor.HitTestOverride = (_, _, _) => null;

        rig.Click(50, 85);

        Assert.That(rig.Session.SelectedId, Is.Null, "the window does not second-guess the use case");
    }

    [Test]
    public void Select_TheDragThatFollowsMovesWhatTheUseCaseSaidWasHit()
    {
        var rig = EditorRig.Create();
        rig.Draw(ToolKind.Rectangle, P(50, 50), P(150, 120));
        var shape = rig.Session.Document.Annotations.Only("rectangle");
        rig.ViewModel.SelectTool(ToolKind.Select);
        rig.Interactor.HitTestOverride = (_, _, _) => shape.Id;

        rig.Drag(P(280, 20), P(290, 30));

        Assert.That(rig.Session.Calls, Does.Contain("Move 10,10"), "pressed away from the shape, but the use case chose it, so the drag moves it");
    }
}
