using System.Diagnostics;
using System.Windows.Media.Imaging;
using FlaUI.Core.WindowsAPI;
using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.E2eTests.Support;

namespace Paper.ScreenWizzard.E2eTests.Drive;

/// <summary>
/// Plan T18, flows 1, 8, 9 and 11: start-up (the bar, the settings file, the tray icon and its menu), the bar's place, a second copy, closing the
/// bar, opening an image from the tray, pasting with nothing to paste, and "Thoát" in the tray menu.
/// </summary>
[TestFixture]
public sealed class ShellDriveTests : DriveBase
{
    [Test]
    public void Start_BarAppearsAtTheSavedPlace_SettingsFileKeeps_TrayIconAndMenuExist()
    {
        NewApp();
        var bar = StartAndWaitForBar();
        var rect = AppRun.WindowRectOf(bar);
        Assert.That((rect.X, rect.Y), Is.EqualTo((40, 40)), "the capture bar is where the settings file put it");
        AppRun.Shot(bar, ShotName("capturebar", "m0"));

        Assert.That(File.Exists(App.SettingsPath), Is.True, "the settings file is still there");
        using (var json = App.ReadSettingsJson())
        {
            Assert.That(json.RootElement.GetProperty("hotkeys").GetProperty("Rectangle").GetProperty("key").GetString(), Is.EqualTo("F13"), "the file was not overwritten by defaults");
            Assert.That(json.RootElement.GetProperty("saveFolder").GetString(), Is.EqualTo(App.SaveFolder));
            Assert.That(json.RootElement.GetProperty("captureBarPosition").GetProperty("x").GetInt32(), Is.EqualTo(40));
        }

        var tray = new TrayDriver(App);
        var menu = tray.OpenMenu();
        var names = TrayDriver.ItemNames(menu);
        Assert.That(names, Is.EqualTo(new[] { "Chụp vùng chữ nhật", "Chụp vùng tự do", "Chụp cửa sổ", "Chụp toàn màn hình", "Mở ảnh…", "Thanh chụp", "Cài đặt", "Thoát" }), "the tray menu of SPEC shell step 2, read from the running exe");
        var barItem = menu.FindAllDescendants(cf => cf.ByName("Thanh chụp")).First();
        Assert.That((Convert.ToUInt32(barItem.Patterns.LegacyIAccessible.Pattern.State.Value) & 0x10) != 0, Is.True, "'Thanh chụp' is ticked while the bar is shown");
        var shot = Desk.ShotPath("drive-tray-menu");
        var mr = menu.BoundingRectangle;
        FlaUI.Core.Capturing.Capture.Rectangle(new System.Drawing.Rectangle(mr.X, mr.Y, mr.Width, mr.Height)).ToFile(shot);
        AppKeys.Type(App, menu, VirtualKeyShort.ESCAPE);
        tray.CloseOverflow();
    }

    [Test]
    public void Start_BarSavedOutsideEveryMonitor_AppearsAtTheTopRightOfThePrimary()
    {
        NewApp(s => s with { CaptureBarPosition = new PixelPoint(-5000, -5000) });
        var bar = StartAndWaitForBar();
        var rect = AppRun.WindowRectOf(bar);
        var primary = Desk.Monitors().First(m => m.IsPrimary).Bounds;
        Assert.That(rect.X >= primary.X && rect.Y >= primary.Y && rect.X + rect.Width <= primary.X + primary.Width && rect.Y + rect.Height <= primary.Y + primary.Height, Is.True, $"the bar {rect} is inside the primary monitor {primary}");
        Assert.That(primary.X + primary.Width - (rect.X + rect.Width), Is.LessThan(80), $"at the right edge of the primary monitor: {rect}");
        Assert.That(rect.Y - primary.Y, Is.LessThan(80), "at its top");
    }

    [TestCase(false)]
    [TestCase(true, Description = "DEFECT D2: the position is only written at Exit, and a bar closed with X is no longer there to be measured")]
    public void BarPlace_DraggedByItsBackground_IsKeptAcrossAnExit_WithTheBarOpenOrClosedWithX(bool closeTheBarFirst)
    {
        NewApp();
        var bar = StartAndWaitForBar();
        var start = AppRun.WindowRectOf(bar);

        // Grab the bar by its own surface (the padding under the buttons) and pull it.
        // The grab point is worked out from the bar's real height, not a fixed offset: at 100% the bar is ~50 physical pixels tall and a fixed
        // +46 was its bottom padding, but at 200% (this machine's laptop panel, alone on 2026-09-21) it is ~100 tall and +46 fell on the buttons.
        var grabY = start.Y + start.Height - 4;
        Desk.Drag(new PixelPoint(start.X + 120, grabY), new PixelPoint(start.X + 120 + 300, grabY + 150), 12);
        Thread.Sleep(400);
        var moved = AppRun.WindowRectOf(bar);
        Assert.That((moved.X, moved.Y), Is.EqualTo((start.X + 300, start.Y + 150)).Within(3), "the bar followed the mouse");

        if (closeTheBarFirst)
        {
            AppRun.Invoke(bar, "CloseButton");
            Assert.That(App.WaitWindowGone("CaptureBarWindow"), Is.True);
        }

        var process = App.Process!;
        new TrayDriver(App).Choose("Thoát");
        Assert.That(process.WaitForExit(8000), Is.True, "the exe ended after Thoát");
        using (var json = App.ReadSettingsJson())
        {
            var saved = json.RootElement.GetProperty("captureBarPosition");
            Assert.That((saved.GetProperty("x").GetInt32(), saved.GetProperty("y").GetInt32()), Is.EqualTo((moved.X, moved.Y)), "the settings file holds where the bar stood (SPEC shell: drag it, close, open again, it is there)");
        }

        App.Restart();
        var again = App.WaitWindow("CaptureBarWindow", 15);
        var placed = AppRun.WindowRectOf(again);
        Assert.That((placed.X, placed.Y), Is.EqualTo((moved.X, moved.Y)), "after a restart the bar is where it was dragged to");
    }

    [Test]
    public void SecondCopy_ExitsWithCodeZeroAtOnce_ShowsNoWindow_AndTheFirstShowsItsBar()
    {
        NewApp();
        var bar = StartAndWaitForBar();
        AppRun.Invoke(bar, "CloseButton");
        Assert.That(App.WaitWindowGone("CaptureBarWindow"), Is.True, "set-up: the first copy's bar is closed");

        var clock = Stopwatch.StartNew();
        var second = App.Start();
        var windowsOfTheSecond = 0;
        while (!second.HasExited && clock.Elapsed < TimeSpan.FromSeconds(8))
        {
            windowsOfTheSecond = Math.Max(windowsOfTheSecond, App.TopWindows(second).Count);
            Thread.Sleep(20);
        }

        Assert.That(second.HasExited, Is.True, "the second copy ended");
        Assert.That(clock.Elapsed.TotalSeconds, Is.LessThan(5), "within 5 seconds");
        Assert.That(second.ExitCode, Is.Zero, "with exit code 0");
        Assert.That(windowsOfTheSecond, Is.Zero, "and it never showed a window of its own");
        Assert.That(App.IsRunning, Is.True, "the first copy still runs");
        var shown = App.WaitWindow("CaptureBarWindow", 6);
        Assert.That(shown, Is.Not.Null, "the first copy showed its capture bar (SPEC shell F6)");
        Assert.That(App.Windows("ErrorDialog"), Is.Empty, "and no error message");
    }

    [Test]
    public void CloseBarWithX_TheProcessAndItsHotkeysStayAlive()
    {
        NewApp();
        var bar = StartAndWaitForBar();
        ShowProbe(Desk.Monitor(0), "closebar");
        AppRun.Invoke(bar, "CloseButton");
        Assert.That(App.WaitWindowGone("CaptureBarWindow"), Is.True);
        Thread.Sleep(500);
        Assert.That(App.IsRunning, Is.True, "the process is still running");

        AppRun.PressHotkey(CaptureKind.Rectangle);
        var overlay = App.WaitWindow("SelectionOverlay", 8);
        Assert.That(overlay, Is.Not.Null, "the hotkey still starts a capture with the bar closed");
        Desk.RightClickAt(Desk.Cursor().X, Desk.Cursor().Y);
        Assert.That(App.WaitWindowGone("SelectionOverlay"), Is.True);
        Assert.That(App.FindWindow("CaptureBarWindow"), Is.Null, "a closed bar stays closed after a capture");
    }

    [Test]
    public void TrayOpenImage_ANonImageGivesAMessageAndNoWindow_AnImageOpensInTheEditor()
    {
        NewApp();
        StartAndWaitForBar();
        var tray = new TrayDriver(App);

        var notes = Path.Combine(App.ScratchPath, "notes.txt");
        File.WriteAllText(notes, "not an image at all");
        tray.Choose("Mở ảnh…");
        var open = FileDialogs.Wait(App);
        AppRun.Shot(open, ShotName("open-box", "m0"));
        FileDialogs.AcceptWithEnter(App, open, notes);
        Assert.That(FileDialogs.WaitGone(App), Is.True);
        var error = App.WaitWindow("ErrorDialog", 8);
        var text = AppRun.TextOf(error, "ErrorText");
        Assert.That(text, Does.Contain("notes.txt").And.Contain("không đọc được"), "the message names the file and says it cannot be read");
        AppRun.Shot(error, ShotName("open-nonimage-error", "m0"));
        Assert.That(App.FindWindow("EditorWindow"), Is.Null, "no editor window (SPEC editor F2)");
        AppRun.Invoke(error, "ErrorOkButton");

        var png = Path.Combine(App.ScratchPath, "real.png");
        WritePng(png, 120, 80);
        tray.Choose("Mở ảnh…");
        open = FileDialogs.Wait(App);
        FileDialogs.AcceptWithEnter(App, open, png);
        var editor = App.WaitWindow("EditorWindow", 10);
        Assert.That(AppRun.TextOf(editor, "SizeText"), Is.EqualTo("120 × 80"), "the opened file's size in the editor");
        Assert.That(App.SavedFiles(), Is.Empty);
    }

    [Test]
    public void Editor_PasteWithAnEmptyClipboard_SaysSoAndKeepsTheImage()
    {
        ProtectClipboard();
        NewApp();
        StartAndWaitForBar();
        var png = Path.Combine(App.ScratchPath, "paste.png");
        WritePng(png, 90, 60);
        var tray = new TrayDriver(App);
        tray.Choose("Mở ảnh…");
        FileDialogs.AcceptWithEnter(App, FileDialogs.Wait(App), png);
        var editor = App.WaitWindow("EditorWindow", 10);
        Thread.Sleep(600);

        Desk.ClearClipboard();
        AppKeys.Chord(App, editor, VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_V);
        var notice = Flaui_WaitAny();
        Assert.That(notice, Does.Contain("Clipboard không có ảnh"), "the message when the clipboard has no image (SPEC editor F3)");
        Assert.That(AppRun.TextOf(editor, "SizeText"), Is.EqualTo("90 × 60"), "the image being edited did not change");
        Assert.That(App.Windows("EditorWindow"), Has.Count.EqualTo(1), "and no other editor window opened");

        string Flaui_WaitAny()
        {
            var end = DateTime.UtcNow.AddSeconds(6);
            while (DateTime.UtcNow < end)
            {
                if (App.FindWindow("ErrorDialog") is { } dialog)
                {
                    return AppRun.TextOf(dialog, "ErrorText");
                }

                if (App.FindWindow("ToastWindow") is { } toast)
                {
                    return AppRun.TextOf(toast, "ToastText");
                }

                Thread.Sleep(100);
            }

            return "(no message window appeared; the exe's windows: " + App.Describe() + ")";
        }
    }

    [Test]
    public void TrayExit_EndsTheProcess_AndTheIconIsGone()
    {
        NewApp();
        StartAndWaitForBar();
        var tray = new TrayDriver(App);
        Assert.That(tray.FindIcon(), Is.Not.Null, "set-up: the icon is in the tray");
        tray.CloseOverflow();

        var process = App.Process!;
        tray.Choose("Thoát");
        Assert.That(process.WaitForExit(8000), Is.True, "the exe ended after Thoát");
        Assert.That(process.ExitCode, Is.Zero, "with exit code 0");

        Thread.Sleep(800);
        var after = new TrayDriver(App).FindIcon(2) is not null;
        new TrayDriver(App).CloseOverflow();
        Assert.That(after, Is.False, "no tray icon named Paper.ScreenWizzard is left after Thoát");
    }

    private static void WritePng(string path, int width, int height)
    {
        StaHost.Instance.Invoke(() =>
        {
            var pixels = Pixels.Pattern(width, height);
            var source = BitmapSource.Create(width, height, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, pixels.Bgra, width * 4);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var stream = File.Create(path);
            encoder.Save(stream);
        });
    }
}
