using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Editor;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Presentation.Editor.Rendering;
using Paper.ScreenWizzard.Presentation.Editor.ViewModels;
using Paper.ScreenWizzard.UiTests.ScreenCapture;
using Paper.ScreenWizzard.UiTests.Shell;
using Paper.ScreenWizzard.UiTests.Support;
using Paper.ScreenWizzard.UseCases.Editor.UseCases;

namespace Paper.ScreenWizzard.UiTests.ImageEditor;

/// <summary>
/// The view model and the flattener on the REAL <see cref="EditorSession"/> (the logic lane's history, numbering, mosaic and crop), with
/// only the interactor and the ports faked. The mock tests prove the window against canned answers; this proves the two halves of the seam
/// agree on what "one step", "the base image with its mosaic" and "the cropped size" mean. Nothing here opens a window.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class EditorWithRealSessionTests : UiTestBase
{
    private static PixelPoint P(int x, int y) => new(x, y);

    private static (EditorViewModel ViewModel, EditorSession Session) Open(PixelImage image)
    {
        var services = new EditorServices(
            new FakeEditorInteractor(),
            new RecordingNotifications(),
            new FakeFileDialogs(),
            new FakeEditorPrompts(),
            new WpfImageFlattener(),
            new FakeLocalizer(),
            () => ShellTestData.DefaultSettings());
        var flow = new EditorFlow(services, new FakeEditorViews());
        var session = new EditorSession(image, null);
        return (new EditorViewModel(session, services, flow), session);
    }

    [Test]
    public void History_EveryDrawingIsOneStep_AndUndoRedoAreEnabledExactlyWhenTheSessionSaysSo()
    {
        var (viewModel, session) = Open(EditorTestData.White(300, 200));
        Assert.That(viewModel.UndoCommand.CanExecute(null), Is.False, "nothing drawn, nothing to undo");

        viewModel.SelectTool(ToolKind.Rectangle);
        viewModel.PointerDown(P(20, 20), false);
        viewModel.PointerMoved(P(120, 90), false);
        viewModel.PointerUp(P(120, 90), false);
        viewModel.SelectTool(ToolKind.StepNumber);
        viewModel.PointerDown(P(150, 60), false);
        viewModel.PointerUp(P(150, 60), false);
        viewModel.PointerDown(P(180, 60), false);
        viewModel.PointerUp(P(180, 60), false);

        Assert.That(session.Document.Annotations, Has.Count.EqualTo(3));
        Assert.That(session.Document.Annotations.OfType<StepAnnotation>().Select(s => s.Number), Is.EqualTo(new[] { 1, 2 }), "the session numbers the steps");
        Assert.That(viewModel.UndoCommand.CanExecute(null), Is.True);

        viewModel.UndoCommand.Execute(null);
        viewModel.UndoCommand.Execute(null);
        Assert.That(session.Document.Annotations, Has.Count.EqualTo(1), "SPEC: ba hình, Undo hai lần → còn một hình");
        Assert.That(viewModel.RedoCommand.CanExecute(null), Is.True);

        viewModel.RedoCommand.Execute(null);
        Assert.That(session.Document.Annotations, Has.Count.EqualTo(2), "SPEC: Redo một lần → hai hình");
    }

    [Test]
    public void Select_DragMovesTheShapeInOneRealHistoryStep_AndUndoPutsItBack()
    {
        var (viewModel, session) = Open(EditorTestData.White(300, 200));
        viewModel.SelectTool(ToolKind.Rectangle);
        viewModel.PointerDown(P(50, 50), false);
        viewModel.PointerMoved(P(150, 120), false);
        viewModel.PointerUp(P(150, 120), false);
        viewModel.SelectTool(ToolKind.Select);

        viewModel.PointerDown(P(50, 80), false);
        viewModel.PointerMoved(P(60, 85), false);
        viewModel.PointerMoved(P(70, 90), false);
        viewModel.PointerUp(P(80, 95), false);

        Assert.That(((RectangleAnnotation)session.Document.Annotations.Single()).Bounds, Is.EqualTo(new PixelRect(80, 65, 100, 70)));
        viewModel.UndoCommand.Execute(null);
        Assert.That(((RectangleAnnotation)session.Document.Annotations.Single()).Bounds, Is.EqualTo(new PixelRect(50, 50, 100, 70)), "one Undo undoes the whole drag, and the shape is back where it was drawn");
    }

    [Test]
    public void Blur_TheFlattenedImageHasTheSessionsMosaicAndNoOutlineOfItsOwn()
    {
        var gradient = CaptureTestData.Gradient(200, 120);
        var (viewModel, session) = Open(gradient);
        viewModel.SelectTool(ToolKind.Blur);
        viewModel.PointerDown(P(24, 24), false);
        viewModel.PointerMoved(P(96, 72), false);
        viewModel.PointerUp(P(96, 72), false);

        var flat = new WpfImageFlattener().Flatten(session);

        Assert.That(flat.Bgra, Is.EqualTo(session.RenderBase().Bgra), "with only a blur region, the output is exactly the base with its mosaic");
        Assert.That(flat.Bgra, Is.Not.EqualTo(gradient.Bgra), "and the blur changed something: a gradient is not a mosaic");
        var first = EditorTestData.At(flat, 24, 24);
        var sameTile = Enumerable.Range(0, 12).SelectMany(dy => Enumerable.Range(0, 12).Select(dx => EditorTestData.At(flat, 24 + dx, 24 + dy))).Distinct().ToList();
        Assert.That(sameTile, Is.EqualTo(new[] { first }), "SPEC: ô 12 × 12, mỗi ô một màu duy nhất");
    }

    [Test]
    public void Crop_KeepsTheChosenRegionAndTheStatusBarAndTheFlattenedImageFollow()
    {
        var (viewModel, session) = Open(EditorTestData.White(1000, 600));
        viewModel.SelectTool(ToolKind.Rectangle);
        viewModel.PointerDown(P(150, 100), false);
        viewModel.PointerMoved(P(250, 180), false);
        viewModel.PointerUp(P(250, 180), false);
        viewModel.SelectTool(ToolKind.Crop);
        viewModel.PointerDown(P(100, 50), false);
        viewModel.PointerMoved(P(500, 350), false);
        viewModel.PointerUp(P(500, 350), false);

        viewModel.ConfirmCommand.Execute(null);

        Assert.That((session.Document.Source.Width, session.Document.Source.Height), Is.EqualTo((400, 300)), "SPEC: 400 × 300");
        Assert.That(viewModel.SizeText, Is.EqualTo("400 × 300"));
        Assert.That(((RectangleAnnotation)session.Document.Annotations.Single()).Bounds.X, Is.EqualTo(50), "SPEC: hình ở (150, 100) sau cắt nằm ở (50, 50)");
        var flat = new WpfImageFlattener().Flatten(session);
        Assert.That((flat.Width, flat.Height), Is.EqualTo((400, 300)));
        Assert.That(EditorTestData.PixelIs(flat, 50, 90, EditorColors.Default), Is.True, "the rectangle's left edge, moved with the crop");
        viewModel.UndoCommand.Execute(null);
        Assert.That(session.Document.Source.Width, Is.EqualTo(1000), "Undo brings the whole image back");
    }
}
