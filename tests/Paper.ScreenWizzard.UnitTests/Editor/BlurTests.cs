using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Editor;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.UnitTests.Editor.Fakes;

namespace Paper.ScreenWizzard.UnitTests.Editor;

/// <summary>SPEC editor, "Làm mờ": squares of 12 x 12 pixels, one colour each, that cannot be undone from the output.</summary>
[TestFixture]
public sealed class BlurTests
{
    // The Checker picture averages, over any cell with an even number of pixels, to B 50, G 100, R 20, A 150 (hand-worked
    // in EditorData.Checker). The pixels in the four channels differ from that average, so a cell left alone shows.
    private static readonly byte[] _average = [50, 100, 20, 150];

    private EditorFixture _fixture = null!;

    [SetUp]
    public void SetUp() => _fixture = new EditorFixture();

    private static BlurAnnotation Blur(int id, int x, int y, int width, int height) =>
        new(EditorData.Id(id), EditorData.Red, 1, new PixelRect(x, y, width, height));

    [Test]
    public void ABlurOf200By100EveryCellOf12By12HasOneColourCheckedPixelByPixel()
    {
        var source = EditorData.Checker(240, 140);
        var session = _fixture.NewSession(source);
        session.Add(Blur(1, 20, 10, 200, 100));

        var rendered = session.RenderBase();

        // 200 = 16 cells of 12 plus one of 8; 100 = 8 cells of 12 plus one of 4; cells are counted from the area's top-left.
        Assert.That(rendered.Width, Is.EqualTo(240));
        Assert.That(rendered.Height, Is.EqualTo(140));
        var wrong = 0;
        var firstWrong = string.Empty;
        for (var y = 10; y < 110; y++)
        {
            for (var x = 20; x < 220; x++)
            {
                if (!EditorData.PixelAt(rendered, x, y).SequenceEqual(_average))
                {
                    wrong++;
                    firstWrong = firstWrong.Length == 0 ? $"({x},{y})" : firstWrong;
                }
            }
        }

        Assert.That(wrong, Is.EqualTo(0), "pixels of the blur area that are not the cell average; first at " + firstWrong);
    }

    [Test]
    public void CellsAreCountedFromTheTopLeftOfTheAreaAndEachCellTakesTheAverageOfItsOwnPixels()
    {
        // Area (5, 3, 24, 12) on a 40 x 20 image: two cells of 12 x 12, x 5..16 and x 17..28.
        // Source R by column: 5..10 -> 0, 11..16 -> 100 (cell average 50); 17..22 -> 200, 23..28 -> 100 (average 150).
        // B, G are 0 and A is 255. Column 4 and 29 (outside the area) are 77.
        var bytes = new byte[40 * 20 * 4];
        for (var y = 0; y < 20; y++)
        {
            for (var x = 0; x < 40; x++)
            {
                var r = x switch
                {
                    >= 5 and <= 10 => 0,
                    >= 11 and <= 16 => 100,
                    >= 17 and <= 22 => 200,
                    >= 23 and <= 28 => 100,
                    _ => 77,
                };
                var at = ((y * 40) + x) * 4;
                bytes[at + 2] = (byte)r;
                bytes[at + 3] = 255;
            }
        }

        var session = _fixture.NewSession(new PixelImage(40, 20, bytes));
        session.Add(Blur(1, 5, 3, 24, 12));

        var rendered = session.RenderBase();

        for (var y = 3; y < 15; y++)
        {
            Assert.That(EditorData.PixelAt(rendered, 5, y), Is.EqualTo(new byte[] { 0, 0, 50, 255 }), $"first cell, row {y}");
            Assert.That(EditorData.PixelAt(rendered, 16, y), Is.EqualTo(new byte[] { 0, 0, 50, 255 }), $"first cell, row {y}");
            Assert.That(EditorData.PixelAt(rendered, 17, y), Is.EqualTo(new byte[] { 0, 0, 150, 255 }), $"second cell, row {y}");
            Assert.That(EditorData.PixelAt(rendered, 28, y), Is.EqualTo(new byte[] { 0, 0, 150, 255 }), $"second cell, row {y}");
        }
    }

    [Test]
    public void PixelsOutsideEveryBlurAreaAreIdenticalToTheSourceAndTheSourceIsNeverChanged()
    {
        var source = EditorData.Positional(60, 40);
        var before = source.Bgra.ToArray();
        var session = _fixture.NewSession(source);
        session.Add(Blur(1, 10, 10, 24, 12));

        var rendered = session.RenderBase();

        Assert.That(source.Bgra, Is.EqualTo(before), "the source array is never modified");
        Assert.That(rendered.Bgra, Is.Not.SameAs(source.Bgra));
        Assert.That(session.Document.Source.Bgra, Is.EqualTo(before));
        var changedOutside = 0;
        var changedInside = 0;
        for (var y = 0; y < 40; y++)
        {
            for (var x = 0; x < 60; x++)
            {
                var same = EditorData.PixelAt(rendered, x, y).SequenceEqual(EditorData.PixelAt(source, x, y));
                var inside = x is >= 10 and < 34 && y is >= 10 and < 22;
                if (!inside && !same)
                {
                    changedOutside++;
                }

                if (inside && !same)
                {
                    changedInside++;
                }
            }
        }

        Assert.That(changedOutside, Is.EqualTo(0));
        Assert.That(changedInside, Is.GreaterThan(0), "the Positional picture differs from pixel to pixel, so a pixelated area must differ");
    }

    [Test]
    public void ABlurCannotBeReadBackFromTheOutputBecauseAllPixelsOfACellAreEqual()
    {
        var session = _fixture.NewSession(EditorData.Positional(60, 40));
        session.Add(Blur(1, 0, 0, 12, 12));

        var rendered = session.RenderBase();

        var distinct = new HashSet<string>();
        for (var y = 0; y < 12; y++)
        {
            for (var x = 0; x < 12; x++)
            {
                distinct.Add(string.Join(",", EditorData.PixelAt(rendered, x, y)));
            }
        }

        Assert.That(distinct, Has.Count.EqualTo(1), "144 different source pixels became one colour");
    }

    [Test]
    public void AnAreaThatEndsOutsideTheImageIsClippedToTheImage()
    {
        var session = _fixture.NewSession(EditorData.Checker(30, 14));
        session.Add(Blur(1, 24, 0, 20, 14));

        var rendered = session.RenderBase();

        Assert.That(rendered.Width, Is.EqualTo(30));
        Assert.That(EditorData.PixelAt(rendered, 24, 0), Is.EqualTo(_average));
        Assert.That(EditorData.PixelAt(rendered, 29, 13), Is.EqualTo(_average));
        Assert.That(EditorData.PixelAt(rendered, 23, 0), Is.EqualTo(EditorData.PixelAt(EditorData.Checker(30, 14), 23, 0)));
    }

    [Test]
    public void AnUnsavedBlurCanBeMovedAndDeletingItBringsTheOriginalPixelsBack()
    {
        var source = EditorData.Checker(60, 30);
        var session = _fixture.NewSession(source);
        session.Add(Blur(1, 0, 0, 12, 12));
        Assert.That(EditorData.PixelAt(session.RenderBase(), 3, 3), Is.EqualTo(_average));

        session.Move(EditorData.Id(1), 24, 12);
        var moved = session.RenderBase();
        Assert.That(EditorData.PixelAt(moved, 3, 3), Is.EqualTo(EditorData.PixelAt(source, 3, 3)), "the old place shows the picture again");
        Assert.That(EditorData.PixelAt(moved, 27, 15), Is.EqualTo(_average), "the new place is pixelated");

        session.Delete(EditorData.Id(1));

        Assert.That(session.RenderBase().Bgra, Is.EqualTo(source.Bgra), "deleting the blur shows the original everywhere");
    }

    [Test]
    public void WithoutAnyBlurTheBaseIsThePictureUnchanged()
    {
        var source = EditorData.Positional(30, 20);
        var session = _fixture.NewSession(source);
        session.Add(EditorData.Rect(1, 0, 0, 10, 10));
        Assert.That(session.Document.Annotations, Has.Count.EqualTo(1));

        var rendered = session.RenderBase();

        Assert.That(rendered.Width, Is.EqualTo(30));
        Assert.That(rendered.Height, Is.EqualTo(20));
        Assert.That(rendered.Bgra, Is.EqualTo(source.Bgra));
    }
}
