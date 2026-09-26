using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Editor;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Presentation.Rendering;
using Paper.ScreenWizzard.Presentation.ViewModels.Editor;
using Paper.ScreenWizzard.UiTests.Support;

namespace Paper.ScreenWizzard.UiTests.ImageEditor;

/// <summary>
/// The pixels the editor writes (plan T13): the image with every drawing on it, at the image's own size. These read the flattened bitmap
/// itself, so what is proven is the file's content and not the look of the window. Nothing here depends on a zoom: the flattener does not
/// take one.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class EditorRenderingTests : UiTestBase
{
    private const string VietnameseText = "Đường ống 45° — thử nghiệm";

    private static readonly WpfImageFlattener _flattener = new();

    private static PixelPoint P(int x, int y) => new(x, y);

    private static PixelImage Flatten(PixelImage image, params Annotation[] annotations) =>
        _flattener.Flatten(EditorTestData.SessionWith(image, annotations));

    [Test]
    public void Flatten_NoDrawings_IsTheImageItself()
    {
        var image = EditorTestData.Solid(120, 80, 30, 60, 90);

        var flat = Flatten(image);

        Assert.That((flat.Width, flat.Height), Is.EqualTo((120, 80)));
        Assert.That(flat.Bgra, Is.EqualTo(image.Bgra), "no drawing, no change");
    }

    [Test]
    public void Flatten_HasTheImagesOwnSize_WhateverIsDrawn()
    {
        var flat = Flatten(
            EditorTestData.White(640, 360),
            EditorTestData.Rectangle(EditorTestData.PureRed, 6, new PixelRect(-50, -50, 900, 500)),
            EditorTestData.Arrow(EditorTestData.PureBlue, 4, P(10, 10), P(1000, 700)));

        Assert.That((flat.Width, flat.Height), Is.EqualTo((640, 360)), "drawings that leave the image are clipped, not the image grown");
        Assert.That(flat.Bgra.Length, Is.EqualTo(640 * 360 * 4));
        Assert.That(EditorTestData.PaintedBoxOrFail(flat), Is.Not.EqualTo(default((int, int, int, int))), "and they were drawn");
    }

    [Test]
    public void Flatten_DrawingsPartlyOutsideTheImage_AreClippedAndTheInsidePartIsDrawn()
    {
        var flat = Flatten(
            EditorTestData.White(200, 100),
            EditorTestData.Rectangle(EditorTestData.PureRed, 4, new PixelRect(150, 50, 200, 200)));

        Assert.That(EditorTestData.PixelIs(flat, 150, 70, EditorTestData.PureRed), Is.True, "the left edge is inside the image");
        Assert.That(EditorTestData.PixelIs(flat, 190, 50, EditorTestData.PureRed), Is.True, "and so is the top edge as far as the image goes");
        Assert.That(EditorTestData.PixelIs(flat, 100, 20, EditorTestData.WhiteColor, 2), Is.True, "far from the shape: untouched");
    }

    [Test]
    public void Highlighter_KeepsTheBlackTextUnderItBlack_AndTintsTheWhiteAroundIt()
    {
        var image = EditorTestData.White(420, 100);
        var text = EditorTestData.Text(EditorTestData.Black, P(20, 20), "Highlight me 123", 32);
        var before = Flatten(image, text);
        var stroke = EditorTestData.Stroke(EditorTestData.PureYellow, 20, highlighter: true, P(10, 40), P(410, 40));

        var after = Flatten(image, text, stroke);

        var blackBefore = new List<(int X, int Y)>();
        for (var y = 0; y < before.Height; y++)
        {
            for (var x = 0; x < before.Width; x++)
            {
                var pixel = EditorTestData.At(before, x, y);
                if (pixel.R <= 10 && pixel.G <= 10 && pixel.B <= 10)
                {
                    blackBefore.Add((x, y));
                }
            }
        }

        Assert.That(blackBefore, Has.Count.GreaterThan(30), "the text really has black pixels to check");
        var turned = blackBefore.Where(p => !EditorTestData.PixelIs(after, p.X, p.Y, EditorTestData.Black, 12)).ToList();
        Assert.That(turned, Is.Empty, $"SPEC: điểm ảnh của chữ vẫn đen sau khi ghép ({turned.Count} of {blackBefore.Count} changed)");

        var around = EditorTestData.At(after, 380, 40);
        Assert.That(around.R, Is.GreaterThan(240), "SPEC: nền quanh chữ ngả vàng - red stays");
        Assert.That(around.G, Is.GreaterThan(240), "green stays");
        Assert.That(around.B, Is.InRange(100, 200), "blue drops: about 40% of the way to yellow, the stroke's transparency");
        Assert.That(EditorTestData.PixelIs(after, 380, 95, EditorTestData.WhiteColor, 2), Is.True, "outside the stroke stays white");
    }

    [Test]
    public void Highlighter_OverAGreyPixel_DarkensItByTheHighlightColourAndDoesNotReplaceIt()
    {
        var image = EditorTestData.Solid(100, 40, 128, 128, 128);
        var stroke = EditorTestData.Stroke(EditorTestData.PureYellow, 10, highlighter: true, P(0, 20), P(100, 20));

        var flat = Flatten(image, stroke);

        var pixel = EditorTestData.At(flat, 50, 20);
        Assert.That(pixel.R, Is.InRange(120, 136), "a highlighter of yellow keeps red and green as they were");
        Assert.That(pixel.B, Is.LessThan(100), "and takes blue away from the grey");
    }

    [Test]
    public void Arrow_HasItsTipAtTheEndPoint()
    {
        var flat = Flatten(EditorTestData.White(400, 300), EditorTestData.Arrow(EditorTestData.PureRed, 4, P(100, 100), P(300, 200)));

        var box = EditorTestData.PaintedBoxOrFail(flat);

        Assert.That(box.MaxX, Is.InRange(298, 301), "SPEC: mũi tên nhọn ở (300, 200): nothing is painted past x = 300");
        Assert.That(box.MaxY, Is.InRange(198, 201), "nor past y = 200");
        Assert.That(EditorTestData.PixelIs(flat, 295, 197, EditorTestData.PureRed), Is.True, "the head is solid just behind the tip");
        Assert.That(EditorTestData.PixelIs(flat, 105, 102, EditorTestData.PureRed), Is.True, "the shaft starts at the first point");
        Assert.That(box.MinX, Is.InRange(97, 101), "and does not stick out behind it");
    }

    [Test]
    public void Arrow_DraggedTheOtherWay_HasItsTipAtTheOtherEnd()
    {
        var flat = Flatten(EditorTestData.White(400, 300), EditorTestData.Arrow(EditorTestData.PureRed, 4, P(300, 200), P(100, 100)));

        var box = EditorTestData.PaintedBoxOrFail(flat);

        Assert.That(box.MinX, Is.InRange(98, 102), "the tip is at (100, 100) now");
        Assert.That(box.MinY, Is.InRange(98, 102));
        Assert.That(box.MaxX, Is.InRange(298, 302), "and the shaft ends flat at (300, 200)");
    }

    [Test]
    public void Text_VietnameseWithDegreeSignAndEmDash_DrawsEveryGlyph()
    {
        var image = EditorTestData.White(640, 90);
        var withMarks = Flatten(image, EditorTestData.Text(EditorTestData.Black, P(10, 14), VietnameseText, 40));
        var withoutMarks = Flatten(image, EditorTestData.Text(EditorTestData.Black, P(10, 14), "Duong ong 45° — thu nghiem", 40));

        var different = EditorTestData.CountDifferent(withMarks, withoutMarks, 60);

        Assert.That(EditorTestData.PaintedBox(withMarks), Is.Not.Null, "the text drew something");
        Assert.That(different, Is.GreaterThan(150), "the glyph area differs from the same text without diacritics: Đ ư ờ ố ử ệ are drawn as themselves");

        var a = EditorTestData.PaintedBoxOrFail(withMarks);
        var b = EditorTestData.PaintedBoxOrFail(withoutMarks);
        Assert.That(a.MaxX - a.MinX, Is.EqualTo(b.MaxX - b.MinX).Within(30), "the same length: no missing-glyph boxes or gaps");
        Assert.That(a.MinY, Is.LessThanOrEqualTo(b.MinY), "the marks reach at least as high");

        SavePicture("editor-text-vietnamese", withMarks);
    }

    [Test]
    public void Text_TheDegreeSignAndTheEmDash_HaveTheirOwnShapes()
    {
        var image = EditorTestData.White(200, 80);
        var dash = EditorTestData.PaintedBoxOrFail(Flatten(image, EditorTestData.Text(EditorTestData.Black, P(20, 10), "—", 40)));
        var enDash = EditorTestData.PaintedBoxOrFail(Flatten(image, EditorTestData.Text(EditorTestData.Black, P(20, 10), "–", 40)));
        var degree = EditorTestData.PaintedBoxOrFail(Flatten(image, EditorTestData.Text(EditorTestData.Black, P(20, 10), "°", 40)));

        Assert.That(dash.MaxX - dash.MinX, Is.GreaterThan(enDash.MaxX - enDash.MinX + 5), "an em dash is longer than an en dash");
        Assert.That(dash.MaxX - dash.MinX, Is.GreaterThanOrEqualTo(30), "and about as wide as the font size");
        Assert.That(dash.MaxY - dash.MinY, Is.LessThanOrEqualTo(6), "and thin");
        Assert.That(degree.MaxY - degree.MinY, Is.InRange(4, 20), "the degree sign is a small ring");
        Assert.That(degree.MaxY, Is.LessThan(40), "in the upper half of the line");
    }

    [Test]
    public void Text_MultipleLines_AreDrawnOneUnderTheOther()
    {
        var one = EditorTestData.PaintedBoxOrFail(Flatten(EditorTestData.White(300, 200), EditorTestData.Text(EditorTestData.Black, P(10, 10), "Dòng một", 24)));
        var two = EditorTestData.PaintedBoxOrFail(Flatten(EditorTestData.White(300, 200), EditorTestData.Text(EditorTestData.Black, P(10, 10), "Dòng một\nDòng hai", 24)));

        Assert.That(two.MaxY, Is.GreaterThan(one.MaxY + 15), "the second line is below the first");
        Assert.That(two.MinY, Is.EqualTo(one.MinY).Within(2), "and the first stayed where it was");
    }

    [Test]
    public void StepNumber_IsAFilledCircleWithItsNumberInIt()
    {
        var step = new StepAnnotation(Guid.NewGuid(), EditorTestData.PureRed, 4, P(100, 80), 7, 24);

        var flat = Flatten(EditorTestData.White(200, 160), step);

        Assert.That(EditorTestData.PixelIs(flat, 100 - 18, 80, EditorTestData.PureRed), Is.True, "the disc is filled in the colour (left of the digit)");
        var box = EditorTestData.PaintedBoxOrFail(flat);
        Assert.That((box.MinX + box.MaxX) / 2.0, Is.EqualTo(100).Within(2), "centred on the point");
        Assert.That((box.MinY + box.MaxY) / 2.0, Is.EqualTo(80).Within(2));
        var digitPixels = 0;
        for (var y = 70; y < 90; y++)
        {
            for (var x = 92; x < 108; x++)
            {
                var pixel = EditorTestData.At(flat, x, y);
                if (pixel.R > 200 && pixel.G > 200 && pixel.B > 200)
                {
                    digitPixels++;
                }
            }
        }

        Assert.That(digitPixels, Is.GreaterThan(10), "the number is drawn in a contrasting colour on the disc");
    }

    [Test]
    public void Blur_IsNotDrawnOverTheBase_BecauseTheBaseAlreadyHasItsMosaic()
    {
        var image = EditorTestData.Solid(100, 100, 10, 200, 90);

        var flat = Flatten(
            image,
            new BlurAnnotation(Guid.NewGuid(), EditorTestData.PureRed, 4, new PixelRect(20, 20, 50, 50)),
            EditorTestData.Rectangle(EditorTestData.PureBlue, 4, new PixelRect(80, 80, 15, 15)));

        Assert.That(EditorTestData.PixelIs(flat, 80, 85, EditorTestData.PureBlue), Is.True, "a real shape beside the blur is drawn");
        Assert.That(EditorTestData.PixelIs(flat, 20, 45, new RgbaColor(10, 200, 90, 255), 2), Is.True, "but the blur region has no outline or tint of its own");
        Assert.That(EditorTestData.PixelIs(flat, 45, 45, new RgbaColor(10, 200, 90, 255), 2), Is.True, "the flattener trusts RenderBase for the mosaic");
    }

    [Test]
    public void Shape_DrawnAt150x100_IsExactlyThereInTheOutput()
    {
        var flat = Flatten(EditorTestData.White(400, 300), EditorTestData.Rectangle(EditorTestData.PureRed, 4, new PixelRect(150, 100, 100, 80)));

        // A 4 px stroke centred on x = 150 covers the columns 148..151, on y = 100 the rows 98..101.
        Assert.That(EditorTestData.PixelIs(flat, 148, 140, EditorTestData.PureRed), Is.True, "left edge, first column");
        Assert.That(EditorTestData.PixelIs(flat, 151, 140, EditorTestData.PureRed), Is.True, "left edge, last column");
        Assert.That(EditorTestData.PixelIs(flat, 147, 140, EditorTestData.WhiteColor, 2), Is.True, "one column further out is untouched");
        Assert.That(EditorTestData.PixelIs(flat, 152, 140, EditorTestData.WhiteColor, 2), Is.True, "and one further in");
        Assert.That(EditorTestData.PixelIs(flat, 200, 98, EditorTestData.PureRed), Is.True, "top edge, first row");
        Assert.That(EditorTestData.PixelIs(flat, 200, 101, EditorTestData.PureRed), Is.True, "top edge, last row");
        Assert.That(EditorTestData.PixelIs(flat, 200, 97, EditorTestData.WhiteColor, 2), Is.True);
        Assert.That(EditorTestData.PixelIs(flat, 200, 102, EditorTestData.WhiteColor, 2), Is.True);
        var box = EditorTestData.PaintedBoxOrFail(flat);
        Assert.That((box.MinX, box.MinY, box.MaxX, box.MaxY), Is.EqualTo((148, 98, 251, 181)), "the whole outline, to the pixel");
    }

    [TestCase(1.0)]
    [TestCase(4.0)]
    public void Shape_DrawnWithThePointerAtAnyZoom_LandsAtTheSameOutputPixels(double zoom)
    {
        var rig = EditorRig.Create(EditorTestData.White(400, 300));
        rig.ViewModel.SetViewport(800, 600, 1.0);
        rig.ViewModel.SetZoom(zoom);
        rig.ViewModel.SelectTool(ToolKind.Rectangle);

        // The mouse points at the middle of the image pixels (150, 100) and (250, 180); what the window sees is in view units.
        var from = EditorCoordinates.ImageToView(150.5, 100.5, zoom, 1.0);
        var to = EditorCoordinates.ImageToView(250.5, 180.5, zoom, 1.0);
        rig.Drag(
            EditorCoordinates.ViewToImage(from.X, from.Y, zoom, 1.0),
            EditorCoordinates.ViewToImage(to.X, to.Y, zoom, 1.0));
        var flat = new WpfImageFlattener().Flatten(rig.Session);

        var box = EditorTestData.PaintedBoxOrFail(flat);
        Assert.That((box.MinX, box.MinY, box.MaxX, box.MaxY), Is.EqualTo((148, 98, 251, 181)), $"at {zoom:P0}");
    }

    [Test]
    public void Shapes_DraggedInEitherDirection_RenderTheSamePixels()
    {
        foreach (var tool in new[] { ToolKind.Rectangle, ToolKind.Ellipse, ToolKind.Line })
        {
            var forward = EditorRig.Create(EditorTestData.White(300, 200));
            var backward = EditorRig.Create(EditorTestData.White(300, 200));
            forward.Draw(tool, P(40, 30), P(220, 150));
            backward.Draw(tool, P(220, 150), P(40, 30));

            var a = new WpfImageFlattener().Flatten(forward.Session);
            var b = new WpfImageFlattener().Flatten(backward.Session);

            Assert.That(EditorTestData.PaintedBox(a), Is.Not.Null, $"{tool} drew something");
            Assert.That(EditorTestData.CountDifferent(a, b, 8), Is.Zero, $"{tool}: the same picture whichever corner was pressed first");
        }
    }

    [Test]
    public void Order_LaterDrawingsAreOnTopOfEarlierOnes()
    {
        var blue = EditorTestData.Rectangle(EditorTestData.PureBlue, 30, new PixelRect(50, 50, 100, 100));
        var red = EditorTestData.Rectangle(EditorTestData.PureRed, 30, new PixelRect(50, 50, 100, 100));

        var redOnTop = Flatten(EditorTestData.White(200, 200), blue, red);
        var blueOnTop = Flatten(EditorTestData.White(200, 200), red, blue);

        Assert.That(EditorTestData.PixelIs(redOnTop, 50, 100, EditorTestData.PureRed), Is.True);
        Assert.That(EditorTestData.PixelIs(blueOnTop, 50, 100, EditorTestData.PureBlue), Is.True);
    }

    [Test]
    public void Highlighter_DrawnBeforeAShape_LeavesTheShapeOnTop()
    {
        var stroke = EditorTestData.Stroke(EditorTestData.PureYellow, 20, highlighter: true, P(10, 50), P(190, 50));
        var block = EditorTestData.Rectangle(EditorTestData.PureBlue, 40, new PixelRect(60, 30, 80, 40));

        var flat = Flatten(EditorTestData.White(200, 100), stroke, block);

        Assert.That(EditorTestData.PixelIs(flat, 60, 50, EditorTestData.PureBlue), Is.True, "a shape drawn after the highlighter is not tinted by it");
        Assert.That(EditorTestData.At(flat, 20, 50).B, Is.LessThan(200), "and the highlighter is there beside it");
    }

    private static void SavePicture(string name, PixelImage image)
    {
        var bitmap = BitmapSource.Create(image.Width, image.Height, 96, 96, PixelFormats.Bgra32, null, image.Bgra, image.Width * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Shots.PathOf(name));
        encoder.Save(stream);
    }
}
