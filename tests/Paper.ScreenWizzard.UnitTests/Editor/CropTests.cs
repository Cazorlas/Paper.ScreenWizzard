using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Editor;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.UnitTests.Editor.Fakes;

namespace Paper.ScreenWizzard.UnitTests.Editor;

/// <summary>SPEC editor, "Cắt": keep the chosen area, take the drawings along, undo it, and F5 when the area is nothing.</summary>
[TestFixture]
public sealed class CropTests
{
    private EditorFixture _fixture = null!;

    [SetUp]
    public void SetUp() => _fixture = new EditorFixture();

    // The Positional picture: R is x, G is y (low bytes), B is 16 * (x / 256) + (y / 256), A is 255.
    private static void AssertPixel(PixelImage image, int x, int y, byte b, byte g, byte r) =>
        Assert.That(EditorData.PixelAt(image, x, y), Is.EqualTo(new[] { b, g, r, (byte)255 }), $"pixel ({x}, {y})");

    [Test]
    public void AnImageOf1000By600CroppedFrom100_50To500_350Becomes400By300AndKeepsThosePixels()
    {
        var session = _fixture.NewSession(EditorData.Positional(1000, 600));

        var outcome = session.Crop(new PixelRect(100, 50, 400, 300));

        Assert.That(outcome.Applied, Is.True);
        Assert.That(outcome.Message, Is.Null);
        Assert.That(outcome.Area, Is.EqualTo(new PixelRect(100, 50, 400, 300)));
        var source = session.Document.Source;
        Assert.That(source.Width, Is.EqualTo(400));
        Assert.That(source.Height, Is.EqualTo(300));
        Assert.That(source.Bgra, Has.Length.EqualTo(400 * 300 * 4));
        AssertPixel(source, 0, 0, 0, 50, 100);        // source (100, 50): block 0,0; G = 50; R = 100
        AssertPixel(source, 399, 299, 17, 93, 243);   // source (499, 349): block 1,1 -> 17; G = 349 - 256; R = 499 - 256
        AssertPixel(source, 200, 100, 16, 150, 44);   // source (300, 150): block 1,0 -> 16; G = 150; R = 300 - 256
    }

    [Test]
    public void ADrawingAt150_100BecomesAt50_50AfterACropFrom100_50()
    {
        var session = _fixture.NewSession(EditorData.Positional(1000, 600));
        session.Add(EditorData.Rect(1, 150, 100, 60, 40));

        session.Crop(new PixelRect(100, 50, 400, 300));

        Assert.That(session.Document.Annotations, Has.Count.EqualTo(1));
        Assert.That(((RectangleAnnotation)session.Document.Annotations[0]).Bounds, Is.EqualTo(new PixelRect(50, 50, 60, 40)));
    }

    [Test]
    public void EveryKindOfDrawingIsTranslatedByMinusTheCropCorner()
    {
        var session = _fixture.NewSession(EditorData.Positional(1000, 600));
        session.Add(new StrokeAnnotation(EditorData.Id(1), EditorData.Red, 4, [new PixelPoint(150, 100), new PixelPoint(160, 120)], false));
        session.Add(new LineAnnotation(EditorData.Id(2), EditorData.Red, 4, new PixelPoint(150, 100), new PixelPoint(250, 200)));
        session.Add(new ArrowAnnotation(EditorData.Id(3), EditorData.Red, 4, new PixelPoint(150, 100), new PixelPoint(250, 200)));
        session.Add(new EllipseAnnotation(EditorData.Id(4), EditorData.Red, 4, new PixelRect(150, 100, 60, 40)));
        session.Add(new TextAnnotation(EditorData.Id(5), EditorData.Red, 1, new PixelPoint(150, 100), "abc", 18));
        session.Add(EditorData.Step(6, 1, 150, 100));
        session.Add(new BlurAnnotation(EditorData.Id(7), EditorData.Red, 1, new PixelRect(150, 100, 60, 40)));

        session.Crop(new PixelRect(100, 50, 400, 300));

        var doc = session.Document.Annotations;
        Assert.That(doc, Has.Count.EqualTo(7));
        Assert.That(((StrokeAnnotation)doc[0]).Points, Is.EqualTo(new[] { new PixelPoint(50, 50), new PixelPoint(60, 70) }));
        Assert.That(((LineAnnotation)doc[1]).From, Is.EqualTo(new PixelPoint(50, 50)));
        Assert.That(((LineAnnotation)doc[1]).To, Is.EqualTo(new PixelPoint(150, 150)));
        Assert.That(((ArrowAnnotation)doc[2]).From, Is.EqualTo(new PixelPoint(50, 50)));
        Assert.That(((ArrowAnnotation)doc[2]).To, Is.EqualTo(new PixelPoint(150, 150)));
        Assert.That(((EllipseAnnotation)doc[3]).Bounds, Is.EqualTo(new PixelRect(50, 50, 60, 40)));
        Assert.That(((TextAnnotation)doc[4]).Origin, Is.EqualTo(new PixelPoint(50, 50)));
        Assert.That(((StepAnnotation)doc[5]).Center, Is.EqualTo(new PixelPoint(50, 50)));
        Assert.That(((BlurAnnotation)doc[6]).Area, Is.EqualTo(new PixelRect(50, 50, 60, 40)));
    }

    [Test]
    public void ADrawingCompletelyOutsideTheCropIsDroppedAndUndoBringsItBack()
    {
        var session = _fixture.NewSession(EditorData.Positional(1000, 600));
        session.Add(EditorData.Rect(1, 150, 100, 60, 40));
        session.Add(EditorData.Rect(2, 600, 400, 50, 50));
        session.Add(EditorData.Rect(3, 10, 10, 50, 20));
        session.Add(EditorData.Rect(4, 10, 100, 50, 40));    // only left of the crop (x 10..60 against 100..500)
        session.Add(EditorData.Rect(5, 150, 5, 50, 20));     // only above the crop (y 5..25 against 50..350)

        session.Crop(new PixelRect(100, 50, 400, 300));

        Assert.That(session.Document.Annotations.Select(a => a.Id), Is.EqualTo(new[] { EditorData.Id(1) }));

        session.Undo();

        Assert.That(session.Document.Annotations.Select(a => a.Id), Is.EqualTo(new[] { EditorData.Id(1), EditorData.Id(2), EditorData.Id(3), EditorData.Id(4), EditorData.Id(5) }));
        Assert.That(((RectangleAnnotation)session.Document.Annotations[0]).Bounds, Is.EqualTo(new PixelRect(150, 100, 60, 40)));
        Assert.That(((RectangleAnnotation)session.Document.Annotations[1]).Bounds, Is.EqualTo(new PixelRect(600, 400, 50, 50)));
    }

    [Test]
    public void ADrawingCutAcrossStaysAndItsOutsidePartIsLeftToTheRendererToClip()
    {
        var session = _fixture.NewSession(EditorData.Positional(1000, 600));
        session.Add(EditorData.Rect(1, 450, 300, 100, 100));

        session.Crop(new PixelRect(100, 50, 400, 300));

        Assert.That(session.Document.Annotations, Has.Count.EqualTo(1));
        Assert.That(((RectangleAnnotation)session.Document.Annotations[0]).Bounds, Is.EqualTo(new PixelRect(350, 250, 100, 100)));
    }

    [Test]
    public void UndoBringsBackTheWholeImageAndRedoCropsAgain()
    {
        var session = _fixture.NewSession(EditorData.Positional(1000, 600));
        session.Add(EditorData.Rect(1, 150, 100, 60, 40));
        session.Crop(new PixelRect(100, 50, 400, 300));
        Assert.That(session.CanUndo, Is.True);

        var undone = session.Undo();

        Assert.That(undone, Is.True);
        Assert.That(session.Document.Source.Width, Is.EqualTo(1000));
        Assert.That(session.Document.Source.Height, Is.EqualTo(600));
        AssertPixel(session.Document.Source, 999, 599, 50, 87, 231); // (999, 599): block 3,2 -> 3 * 16 + 2 = 50; G = 599 - 512; R = 999 - 768
        Assert.That(((RectangleAnnotation)session.Document.Annotations[0]).Bounds, Is.EqualTo(new PixelRect(150, 100, 60, 40)));

        session.Redo();
        Assert.That(session.Document.Source.Width, Is.EqualTo(400));
        Assert.That(((RectangleAnnotation)session.Document.Annotations[0]).Bounds, Is.EqualTo(new PixelRect(50, 50, 60, 40)));
    }

    [Test]
    public void ACropDraggedPastTheEdgeIsClampedToTheImage()
    {
        var session = _fixture.NewSession(EditorData.Positional(1000, 600));

        var outcome = session.Crop(new PixelRect(900, 500, 400, 300));

        Assert.That(outcome.Applied, Is.True);
        Assert.That(outcome.Area, Is.EqualTo(new PixelRect(900, 500, 100, 100)));
        Assert.That(session.Document.Source.Width, Is.EqualTo(100));
        Assert.That(session.Document.Source.Height, Is.EqualTo(100));
        AssertPixel(session.Document.Source, 0, 0, 49, 244, 132); // source (900, 500): block 3,1 -> 49; G = 500 - 256; R = 900 - 768
    }

    [Test]
    public void ACropDraggedPastTheTopLeftIsClampedToo()
    {
        var session = _fixture.NewSession(EditorData.Positional(1000, 600));

        var outcome = session.Crop(new PixelRect(-50, -20, 200, 100));

        Assert.That(outcome.Applied, Is.True);
        Assert.That(outcome.Area, Is.EqualTo(new PixelRect(0, 0, 150, 80)));
        Assert.That(session.Document.Source.Width, Is.EqualTo(150));
        Assert.That(session.Document.Source.Height, Is.EqualTo(80));
        AssertPixel(session.Document.Source, 149, 79, 0, 79, 149);
    }

    [Test]
    public void CroppingAwayTheSelectedDrawingClearsTheSelection()
    {
        var session = _fixture.NewSession(EditorData.Positional(1000, 600));
        session.Add(EditorData.Rect(1, 600, 400, 50, 50));
        session.Select(EditorData.Id(1));
        Assert.That(session.SelectedId, Is.EqualTo(EditorData.Id(1)));

        session.Crop(new PixelRect(100, 50, 400, 300));

        Assert.That(session.Document.Source.Width, Is.EqualTo(400), "the crop happened");
        Assert.That(session.SelectedId, Is.Null);
    }

    [TestCase(10, 10, 0, 50, TestName = "F5_CropOfWidthZero")]
    [TestCase(10, 10, 50, 0, TestName = "F5_CropOfHeightZero")]
    [TestCase(1000, 10, 50, 50, TestName = "F5_CropThatOnlyTouchesTheRightEdge")]
    [TestCase(1200, 100, 50, 50, TestName = "F5_CropCompletelyOutsideTheImage")]
    [TestCase(-100, -100, 50, 50, TestName = "F5_CropCompletelyAboveAndLeftOfTheImage")]
    public void F5_ACropSmallerThanOnePixelOrOutsideTheImageChangesNothingAndSaysSo(int x, int y, int width, int height)
    {
        var session = _fixture.NewSession(EditorData.Positional(1000, 600));
        session.Add(EditorData.Rect(1, 150, 100, 60, 40));
        session.MarkSaved(@"C:\a.png");
        var before = session.Document;

        var outcome = session.Crop(new PixelRect(x, y, width, height));

        Assert.That(outcome.Applied, Is.False);
        Assert.That(outcome.Message, Is.Not.Null);
        Assert.That(outcome.Message!.Key, Is.EqualTo("Editor.CropInvalid"));
        Assert.That(outcome.Message.Arguments, Is.Empty);
        Assert.That(session.Document.Source.Width, Is.EqualTo(1000));
        Assert.That(session.Document.Source.Height, Is.EqualTo(600));
        Assert.That(session.Document.Annotations, Is.EqualTo(before.Annotations));
        Assert.That(session.IsDirty, Is.False, "no history step was made");
        Assert.That(session.CanUndo, Is.True, "only the earlier Add can be undone");
        session.Undo();
        Assert.That(session.Document.Annotations, Is.Empty, "the one Undo took back the Add, not a crop that never happened");
        Assert.That(session.Document.Source.Width, Is.EqualTo(1000));
    }
}
