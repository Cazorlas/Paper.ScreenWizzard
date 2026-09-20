using FlaUI.Core.WindowsAPI;
using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.E2eTests.Support;

namespace Paper.ScreenWizzard.E2eTests.Drive;

/// <summary>
/// Plan T18, flow 3: what each button of the "Đã chụp" dialog really does in the exe: Sao chép, Bỏ (button, Esc, the title bar's close), Lưu thành…
/// (the native box, cancelled and then answered) and Sửa (the editor opens inside the monitor that holds the pointer).
/// </summary>
[TestFixture]
public sealed class DoneDialogTests : DriveBase
{
    private const string Sentinel = "e2e-sentinel-clipboard-text";

    [TestCase(0)]
    [TestCase(1)]
    public void Copy_ClipboardGetsTheSamePixels_NoFileIsWritten_AndTheDialogCloses(int monitorIndex)
    {
        var monitor = Desk.Monitor(monitorIndex);
        ProtectClipboard();
        Desk.SetClipboardText(Sentinel);
        NewApp();
        StartAndWaitForBar();
        var probe = ShowProbe(monitor, "copy");
        var dialog = CaptureToDialog(probe);
        var (from, _) = DragInside(probe, 10, 20, 300, 200);
        var inside = BlocksInside(probe, from, 300, 200);
        Assert.That(Desk.ClipboardText(), Is.EqualTo(Sentinel), "before a choice the clipboard has not changed");

        AppRun.Invoke(dialog, "CopyButton");
        Assert.That(App.WaitWindowGone("CaptureDoneWindow"), Is.True, "Sao chép closes the dialog");
        Thread.Sleep(300);

        var image = Desk.ClipboardImage();
        Assert.That(image, Is.Not.Null, "the clipboard holds an image");
        Assert.That((image!.Width, image.Height), Is.EqualTo((300, 200)), "the clipboard image has the size the dialog showed");
        RectangleCaptureTests.AssertBlockPixels(image, inside, monitorIndex);
        RectangleCaptureTests.AssertOnlyTestWindowColours(image);
        Assert.That(App.SavedFiles(), Is.Empty, "Sao chép writes no file");
        Assert.That(App.FindWindow("EditorWindow"), Is.Null, "no editor opened");
    }

    [TestCase("button")]
    [TestCase("escape")]
    [TestCase("titlebar")]
    public void Discard_KeepsNothing_NoFile_ClipboardUnchanged_NoEditor(string how)
    {
        ProtectClipboard();
        Desk.SetClipboardText(Sentinel);
        NewApp();
        StartAndWaitForBar();
        var probe = ShowProbe(Desk.Monitor(0), "discard-" + how);
        var dialog = CaptureToDialog(probe);

        switch (how)
        {
            case "button":
                AppRun.Invoke(dialog, "DiscardButton");
                break;
            case "escape":
                AppKeys.Type(App, dialog, VirtualKeyShort.ESCAPE);
                break;
            default:
                dialog.Patterns.Window.Pattern.Close();
                break;
        }

        Assert.That(App.WaitWindowGone("CaptureDoneWindow"), Is.True, $"Bỏ ({how}) closes the dialog");
        Thread.Sleep(400);
        Assert.That(App.SavedFiles(), Is.Empty, "no file");
        Assert.That(Desk.ClipboardText(), Is.EqualTo(Sentinel), "the clipboard text is still the sentinel");
        Assert.That(Desk.ClipboardImage(), Is.Null, "no image was put on the clipboard");
        Assert.That(App.FindWindow("EditorWindow"), Is.Null, "no editor");
        Assert.That(App.IsRunning, Is.True, "the app keeps running");
    }

    [Test]
    public void SaveAs_CancellingTheBoxReturnsToTheDialog_ThenAPathWritesThatFile()
    {
        NewApp();
        StartAndWaitForBar();
        var probe = ShowProbe(Desk.Monitor(0), "saveas");
        var dialog = CaptureToDialog(probe);

        AppRun.Invoke(dialog, "SaveAsButton");
        var box = FileDialogs.Wait(App);
        var suggested = FileDialogs.FileNameShown(box);
        Assert.That(AppRun.IsScreenshotName(suggested), Is.True, $"the box is prefilled with a name by the naming rule, it shows '{suggested}'");
        AppRun.Shot(box, ShotName("saveas-box", "m0"));
        FileDialogs.Cancel(box);
        Assert.That(FileDialogs.WaitGone(App), Is.True, "the native box closed");
        Assert.That(App.FindWindow("CaptureDoneWindow"), Is.Not.Null, "cancelling the box returns to the dialog, image still there");
        Assert.That(App.SavedFiles(), Is.Empty, "nothing was written");

        var chosen = Path.Combine(App.SaveFolder, "chosen name.png");
        AppRun.Invoke(dialog, "SaveAsButton");
        box = FileDialogs.Wait(App);
        FileDialogs.Accept(box, chosen);
        Assert.That(FileDialogs.WaitGone(App), Is.True, "the native box closed after Save");
        Assert.That(App.WaitWindowGone("CaptureDoneWindow"), Is.True, "the dialog closes after a save");
        Assert.That(File.Exists(chosen), Is.True, "the file at the typed path exists; the folder holds [" + string.Join(", ", App.SavedFiles()) + "]");
        var image = Desk.ReadImage(chosen);
        Assert.That((image.Width, image.Height), Is.EqualTo((300, 200)));
    }

    [TestCase(0)]
    [TestCase(1)]
    public void Edit_OpensTheEditorWithTheImage_InsideTheMonitorHoldingThePointer(int monitorIndex)
    {
        var monitor = Desk.Monitor(monitorIndex);
        NewApp();
        StartAndWaitForBar();
        var probe = ShowProbe(monitor, "edit");
        var dialog = CaptureToDialog(probe);
        var pointer = Desk.Cursor();
        Assert.That(pointer.X >= monitor.Bounds.X && pointer.X < monitor.Bounds.X + monitor.Bounds.Width && pointer.Y >= monitor.Bounds.Y && pointer.Y < monitor.Bounds.Y + monitor.Bounds.Height, Is.True, "set-up: the pointer is on monitor " + monitorIndex);

        AppRun.Invoke(dialog, "EditButton");
        var editor = App.WaitWindow("EditorWindow", 10);
        Assert.That(App.WaitWindowGone("CaptureDoneWindow"), Is.True, "Sửa closes the dialog");
        Thread.Sleep(600);
        Assert.That(AppRun.TextOf(editor, "SizeText"), Is.EqualTo("300 × 200"), "the editor shows the image that was captured");

        Native.GetWindowRect((IntPtr)editor.Properties.NativeWindowHandle.Value, out var rect);
        var bounds = monitor.Bounds;
        Assert.That(
            rect.Left >= bounds.X && rect.Top >= bounds.Y && rect.Right <= bounds.X + bounds.Width && rect.Bottom <= bounds.Y + bounds.Height,
            Is.True,
            $"the editor window ({rect.Left},{rect.Top})-({rect.Right},{rect.Bottom}) lies inside monitor {monitorIndex} {bounds} ({monitor.Dpi} dpi)");
        AppRun.Shot(editor, ShotName("editor-opened", $"m{monitorIndex}"));
        Assert.That(App.SavedFiles(), Is.Empty, "opening the editor writes no file");
    }
}
