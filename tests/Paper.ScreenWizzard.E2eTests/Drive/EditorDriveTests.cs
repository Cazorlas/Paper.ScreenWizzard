using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.WindowsAPI;
using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.E2eTests.Support;

namespace Paper.ScreenWizzard.E2eTests.Drive;

/// <summary>
/// Plan T18, flow 7: the editor opened from the dialog, drawn on with the real mouse (an arrow and a step number), undone and redone with the keys,
/// saved with Ctrl+S through the native box, and closed with unsaved edits (Cancel, then Discard). Every number is read from the running exe: the
/// pixels of the canvas as drawn, the file it wrote, the buttons' enabled state.
/// </summary>
[TestFixture]
public sealed class EditorDriveTests : DriveBase
{
    private static bool IsRed(RgbaColor c) => c.A > 200 && c.R > 170 && c.G < 100 && c.B < 100;

    private static int CountRed(PixelImage image, int x0 = 0, int y0 = 0, int x1 = int.MaxValue, int y1 = int.MaxValue)
    {
        var count = 0;
        for (var y = Math.Max(0, y0); y < Math.Min(image.Height, y1); y++)
        {
            for (var x = Math.Max(0, x0); x < Math.Min(image.Width, x1); x++)
            {
                if (IsRed(Desk.At(image, x, y)))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static PixelImage PictureOf(AutomationElement element)
    {
        var r = element.BoundingRectangle;
        var path = Path.Combine(Path.GetTempPath(), "e2e-canvas-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            Capture.Rectangle(new System.Drawing.Rectangle(r.X, r.Y, r.Width, r.Height)).ToFile(path);
            return Desk.ReadImage(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void Tool(AutomationElement editor, string toolId) => AppRun.Require(editor, toolId).Patterns.SelectionItem.Pattern.Select();

    [TestCase(0)]
    [TestCase(1)]
    public void Editor_ArrowAndStepNumber_UndoRedo_SaveWritesThemIntoTheFile_AndCloseAsksSaveDiscardCancel(int monitorIndex)
    {
        var monitor = Desk.Monitor(monitorIndex);
        NewApp();
        StartAndWaitForBar();
        var probe = ShowProbe(monitor, "editor");

        // The captured part is white only: 300 x 200 physical pixels below the blocks, so the drawing is the only red in the picture.
        var dialog = CaptureToDialog(probe, 30, 200, 300, 200);
        probe.Close();
        Probe = null;
        AppRun.Invoke(dialog, "EditButton");
        var editor = App.WaitWindow("EditorWindow", 10);
        Thread.Sleep(800);
        AppKeys.BringToFront(App, editor);
        Assert.That(AppRun.TextOf(editor, "SizeText"), Is.EqualTo("300 × 200"));
        Assert.That(AppRun.TextOf(editor, "SaveStateText"), Is.EqualTo("Chưa lưu"), "a fresh capture is unsaved");

        var canvas = AppRun.Require(editor, "ImageCanvas");
        var box = AppRun.BoundsOf(canvas);
        var k = box.Width / 300.0;
        PixelPoint At(int x, int y) => new((int)Math.Round(box.X + (x * k)), (int)Math.Round(box.Y + (y * k)));

        var before = CountRed(PictureOf(canvas));
        Assert.That(before, Is.Zero, "the picture has no red before drawing");

        // An arrow from (40,150) to (240,170), then a step number at (60,60), both by mouse.
        Tool(editor, "ToolButton.Arrow");
        Desk.Drag(At(40, 150), At(240, 170), 12);
        var afterArrow = CountRed(PictureOf(canvas));
        Assert.That(afterArrow, Is.GreaterThan(200), "the arrow drew red pixels on the canvas");
        Tool(editor, "ToolButton.StepNumber");
        var stepAt = At(60, 60);
        Desk.ClickAt(stepAt.X, stepAt.Y);
        Thread.Sleep(300);
        var afterStep = CountRed(PictureOf(canvas));
        Assert.That(afterStep, Is.GreaterThan(afterArrow + 100), "the step number added a red disc");
        AppRun.Shot(editor, ShotName("editor-drawn", $"m{monitorIndex}"));

        // Undo the step, redo it.
        Assert.That(AppRun.Require(editor, "UndoButton").IsEnabled, Is.True, "Undo is available after drawing");
        AppKeys.Chord(App, editor, VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
        Thread.Sleep(300);
        var afterUndo = CountRed(PictureOf(canvas));
        Assert.That(afterUndo, Is.EqualTo(afterArrow).Within(5), "Ctrl+Z removed the step number and left the arrow");
        Assert.That(AppRun.Require(editor, "RedoButton").IsEnabled, Is.True, "Redo is available after Undo");
        AppKeys.Chord(App, editor, VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y);
        Thread.Sleep(300);
        Assert.That(CountRed(PictureOf(canvas)), Is.EqualTo(afterStep).Within(5), "Ctrl+Y brought the step number back");

        // Ctrl+S: a fresh capture has no name, so the native "Save as" box opens; type a path.
        AppKeys.Chord(App, editor, VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
        var box2 = FileDialogs.Wait(App);
        var target = Path.Combine(App.ScratchPath, "edited.png");
        FileDialogs.Accept(box2, target);
        Assert.That(FileDialogs.WaitGone(App), Is.True);
        Thread.Sleep(500);
        Assert.That(File.Exists(target), Is.True, "Ctrl+S then a typed path wrote the file");
        Assert.That(AppRun.TextOf(editor, "SaveStateText"), Is.EqualTo("Đã lưu"), "the status says saved");

        var image = Desk.ReadImage(target);
        Assert.That((image.Width, image.Height), Is.EqualTo((300, 200)), "the saved file has the size of the image, not of the view");
        var arrowMid = CountRed(image, 110, 148, 170, 172);
        var step = CountRed(image, 40, 40, 80, 80);
        Assert.That(arrowMid, Is.GreaterThan(20), "the arrow's shaft is in the file around (140,160)");
        Assert.That(step, Is.GreaterThan(150), "the step number's disc is in the file around (60,60)");
        Assert.That(CountRed(image, 0, 0, 300, 30), Is.Zero, "nothing red where nothing was drawn");

        // Closing with an unsaved edit asks; Back keeps the window, Discard closes it and the file is untouched.
        var savedBytes = File.ReadAllBytes(target);
        Tool(editor, "ToolButton.Rectangle");
        Desk.Drag(At(200, 30), At(280, 90), 8);
        Assert.That(AppRun.TextOf(editor, "SaveStateText"), Is.EqualTo("Chưa lưu"), "another drawing makes it unsaved again");
        editor.Patterns.Window.Pattern.Close();
        var prompt = App.WaitWindow("EditorPromptDialog", 6);
        AppRun.Shot(prompt, ShotName("editor-close-prompt", $"m{monitorIndex}"));
        AppRun.Invoke(prompt, "PromptCancelButton");
        Assert.That(App.WaitWindowGone("EditorPromptDialog"), Is.True);
        Assert.That(App.FindWindow("EditorWindow"), Is.Not.Null, "Quay lại keeps the editor open");

        editor.Patterns.Window.Pattern.Close();
        prompt = App.WaitWindow("EditorPromptDialog", 6);
        AppRun.Invoke(prompt, "PromptSecondaryButton");
        Assert.That(App.WaitWindowGone("EditorWindow"), Is.True, "Bỏ closes the editor");
        Assert.That(File.ReadAllBytes(target), Is.EqualTo(savedBytes), "discarding left the saved file as it was");
        Assert.That(App.IsRunning, Is.True);
    }
}
