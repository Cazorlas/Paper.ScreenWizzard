using System.Windows;
using System.Windows.Media;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.WindowsAPI;
using NUnit.Framework;
using Paper.ScreenWizzard.App.ViewModels.Capture;
using Paper.ScreenWizzard.App.Views.Capture;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Common;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UiTests.Shell;
using Paper.ScreenWizzard.UiTests.Support;
using Paper.ScreenWizzard.UseCases.Capture.Models;
using Paper.ScreenWizzard.UseCases.Common.Models;
using Paper.ScreenWizzard.UseCases.Shell.Models;

namespace Paper.ScreenWizzard.UiTests.ScreenCapture;

/// <summary>
/// The "Đã chụp" dialog as a real window (plan T9): thumbnail, size, format, five named buttons, Tab order, focus ring, both
/// themes, Esc = Bỏ, an error that leaves the dialog open (F4, F5), one dialog per capture. The interactor is a fake.
/// </summary>
[TestFixture]
public sealed class CaptureDoneWindowTests : UiTestBase
{
    private static readonly string[] _buttonsInVisualOrder =
    [
        "SaveButton",
        "SaveAsButton",
        "CopyButton",
        "EditButton",
        "DiscardButton",
    ];

    private sealed record Rig(
        CaptureDoneViewModel ViewModel,
        FakeCaptureInteractor Interactor,
        FakeFileDialogs Dialogs,
        RecordingNotifications Notes,
        WindowSession Window,
        List<string> Events);

    private static Rig Open(
        ResolvedLanguage language = ResolvedLanguage.Vietnamese,
        AppTheme theme = AppTheme.Light,
        int width = 1280,
        int height = 720,
        ImageFormat format = ImageFormat.Png,
        PixelRect? area = null)
    {
        var interactor = new FakeCaptureInteractor();
        var dialogs = new FakeFileDialogs();
        var notes = new RecordingNotifications();
        var events = new List<string>();
        CaptureDoneViewModel? viewModel = null;
        var window = WpfHost.Instance.Show(
            () =>
            {
                viewModel = new CaptureDoneViewModel(
                    interactor,
                    CaptureTestData.Gradient(width, height),
                    CaptureSettings.Default(format),
                    dialogs,
                    notes,
                    WpfHost.Instance.Language);
                viewModel.EditRequested += _ => events.Add("edit");
                return new CaptureDoneWindow(viewModel, area ?? CaptureTestData.SmallScreen);
            },
            language,
            theme);
        return new Rig(viewModel!, interactor, dialogs, notes, window, events);
    }

    [Test]
    public void Dialog_ShowsAThumbnailTheSizeAndTheFormat()
    {
        var rig = Open(width: 1280, height: 720);
        using (rig.Window)
        {
            Assert.That(rig.Window.Root.AutomationId, Is.EqualTo("CaptureDoneWindow"));
            Assert.That(rig.Window.TextOf("SizeText"), Is.EqualTo("1280 × 720"));
            Assert.That(rig.Window.TextOf("FormatText"), Is.EqualTo("PNG"));
            Assert.That(rig.Window.Exists("Thumbnail"), Is.True, "the picture that was captured, scaled down");
            Assert.That(rig.Window.Root.Name, Is.EqualTo("Đã chụp"));
        }
    }

    [Test]
    public void Dialog_ThumbnailShowsTheCapturedPicture_NotABlankBox()
    {
        var rig = Open(width: 800, height: 400);
        using (rig.Window)
        {
            // The gradient is dark on the left and bright on the right; a blank or placeholder box would not be.
            var left = rig.Window.CountPixels("Thumbnail", System.Windows.Media.Color.FromRgb(0, 0, 128), tolerance: 40);
            var right = rig.Window.CountPixels("Thumbnail", System.Windows.Media.Color.FromRgb(255, 255, 128), tolerance: 40);

            Assert.That(left, Is.GreaterThan(50), "the dark left edge of the gradient is in the thumbnail");
            Assert.That(right, Is.GreaterThan(50), "the bright right edge of the gradient is in the thumbnail");
        }
    }

    [Test]
    public void Dialog_JpgSettings_ShowJpgAsTheFormat()
    {
        var rig = Open(format: ImageFormat.Jpg);
        using (rig.Window)
        {
            Assert.That(rig.Window.TextOf("FormatText"), Is.EqualTo("JPG"));
        }
    }

    [TestCase(ResolvedLanguage.Vietnamese, "Lưu", "Lưu thành…", "Sao chép", "Sửa", "Bỏ")]
    [TestCase(ResolvedLanguage.English, "Save", "Save as…", "Copy", "Edit", "Discard")]
    public void Buttons_AreFiveWithTheirNamesInVisualOrder(ResolvedLanguage language, params string[] names)
    {
        var rig = Open(language);
        using (rig.Window)
        {
            var buttons = rig.Window.ButtonNames().Where(b => b.AutomationId.EndsWith("Button", StringComparison.Ordinal)).ToList();

            Assert.That(buttons.Select(b => b.AutomationId), Is.EquivalentTo(_buttonsInVisualOrder), "exactly these five buttons");
            Assert.That(_buttonsInVisualOrder.Select(id => rig.Window.NameOf(id)), Is.EqualTo(names));
            var lefts = _buttonsInVisualOrder.Select(id => rig.Window.Require(id).BoundingRectangle.Left).ToList();
            Assert.That(lefts, Is.Ordered.Ascending, "left to right in the order the SPEC lists them");
            var tops = _buttonsInVisualOrder.Select(id => rig.Window.Require(id).BoundingRectangle.Top).Distinct().ToList();
            Assert.That(tops, Has.Count.EqualTo(1), "one row of buttons, as in the wireframe");
        }
    }

    [Test]
    public void Keyboard_TabThroughTheDialog_FollowsTheVisualOrder()
    {
        var rig = Open();
        using (rig.Window)
        {
            rig.Window.Focus(_buttonsInVisualOrder[0]);

            var reached = rig.Window.TabThrough(_buttonsInVisualOrder.Length - 1);

            Assert.That(reached, Is.EqualTo(_buttonsInVisualOrder.Skip(1).ToArray()));
        }
    }

    [Test]
    public void Keyboard_TheDialogOpensWithFocusOnSave_SoEnterKeepsTheImage()
    {
        var rig = Open();
        using (rig.Window)
        {
            rig.Window.EnsureForeground();
            rig.Window.Host.Settle();

            Assert.That(rig.Window.FocusedId(), Is.EqualTo("SaveButton"));
        }
    }

    [TestCase(AppTheme.Light)]
    [TestCase(AppTheme.Dark)]
    public void Keyboard_FocusedButton_ShowsAVisibleFocusRing(AppTheme theme)
    {
        var rig = Open(theme: theme);
        using (rig.Window)
        {
            var ring = Contrast.ColorOf((Brush)ResourceFiles.Load($"Themes/{theme}.xaml")["Brush.Focus"]);

            foreach (var id in _buttonsInVisualOrder)
            {
                rig.Window.Focus(id);
                Assert.That(rig.Window.CountPixels(id, ring), Is.GreaterThan(20), $"{id} shows the focus ring while it holds focus ({theme})");
            }

            rig.Window.Focus("SaveAsButton");
            Assert.That(rig.Window.CountPixels("CopyButton", ring), Is.Zero, "and only the focused one shows it");
        }
    }

    [Test]
    public void F10_Escape_ClosesTheDialogAndDeliversNothing()
    {
        var rig = Open();
        using (rig.Window)
        {
            rig.Window.EnsureForeground();

            rig.Window.Press(VirtualKeyShort.ESCAPE);

            Assert.That(rig.Window.WaitUntilClosed(TimeSpan.FromSeconds(3)), Is.True, "Esc is Bỏ");
            AssertNothingDelivered(rig);
        }
    }

    [Test]
    public void F10_TheDiscardButton_ClosesTheDialogAndDeliversNothing()
    {
        var rig = Open();
        using (rig.Window)
        {
            rig.Window.Click("DiscardButton");

            Assert.That(rig.Window.WaitUntilClosed(TimeSpan.FromSeconds(3)), Is.True);
            AssertNothingDelivered(rig);
        }
    }

    [Test]
    public void F10_TheWindowsCloseButton_ClosesTheDialogAndDeliversNothing()
    {
        var rig = Open();
        using (rig.Window)
        {
            Assert.That(rig.Window.Exists("DiscardButton"), Is.True, "the dialog is really built before the ✕ is pressed");
            rig.Window.Root.AsWindow().Close();
            rig.Window.Host.Settle();

            Assert.That(rig.Window.WaitUntilClosed(TimeSpan.FromSeconds(3)), Is.True, "the ✕ of the title bar is Bỏ too");
            AssertNothingDelivered(rig);
        }
    }

    private static void AssertNothingDelivered(Rig rig)
    {
        Assert.That(rig.Interactor.Deliveries, Is.Empty, "no file, no clipboard");
        Assert.That(rig.Interactor.PathDeliveries, Is.Empty);
        Assert.That(rig.Events, Is.Empty, "no editor");
        Assert.That(rig.Notes.Toasts, Is.Empty, "and no message: the user chose to discard");
        Assert.That(rig.Notes.Errors, Is.Empty);
    }

    [Test]
    public void Save_Click_DeliversToAFileAndClosesTheDialogWithAToast()
    {
        var rig = Open();
        using (rig.Window)
        {
            var saved = NotificationMessage.Of("Capture.SavedToFile", @"C:\Pics\shot.png", "1280", "720");
            rig.Interactor.DeliverAnswer = _ => new CaptureDeliveryResult(true, false, @"C:\Pics\shot.png", saved);

            rig.Window.Click("SaveButton");

            Assert.That(rig.Window.WaitUntilClosed(TimeSpan.FromSeconds(3)), Is.True);
            Assert.That(rig.Interactor.Deliveries.Select(d => d.Destination), Is.EqualTo(new[] { CaptureDestination.File }));
            Assert.That(rig.Notes.Toasts, Is.EqualTo(new[] { saved }));
        }
    }

    [Test]
    public void Edit_Click_RaisesEditRequestedAndClosesTheDialog()
    {
        var rig = Open();
        using (rig.Window)
        {
            rig.Window.Click("EditButton");

            Assert.That(rig.Window.WaitUntilClosed(TimeSpan.FromSeconds(3)), Is.True);
            Assert.That(rig.Events, Is.EqualTo(new[] { "edit" }));
            Assert.That(rig.Interactor.Deliveries, Is.Empty);
        }
    }

    [Test]
    public void SaveAs_TheUserCancelsTheBox_TheDialogIsStillThere()
    {
        var rig = Open();
        using (rig.Window)
        {
            rig.Dialogs.Answer = null;

            rig.Window.Click("SaveAsButton");

            Assert.That(rig.Dialogs.Asked, Has.Count.EqualTo(1));
            Assert.That(rig.Window.IsOpen, Is.True, "cancelling the box returns to the dialog");
            Assert.That(rig.Window.Exists("Thumbnail"), Is.True, "with the image still in it");
            Assert.That(rig.Window.TextOf("SizeText"), Is.EqualTo("1280 × 720"));
            Assert.That(rig.Interactor.PathDeliveries, Is.Empty);
        }
    }

    [Test]
    public void F4_SaveFails_TheDialogStaysOpenWithTheImageAndTheErrorInsideIt()
    {
        var rig = Open();
        using (rig.Window)
        {
            var failure = NotificationMessage.Of("Capture.SaveFailed", @"D:\ReadOnly", "access denied");
            rig.Interactor.DeliverAnswer = destination => destination == CaptureDestination.File
                ? new CaptureDeliveryResult(false, true, null, failure)
                : new CaptureDeliveryResult(true, false, null, null);

            rig.Window.Click("SaveButton");

            Assert.That(rig.Window.IsOpen, Is.True, "F4: the dialog stays open");
            var text = rig.Window.TextOf("DialogError");
            Assert.That(text, Is.EqualTo(WpfHost.Instance.Invoke(() => WpfHost.Instance.Language.Format(failure))));
            Assert.That(text, Does.Contain(@"D:\ReadOnly").And.Contain("access denied").And.Not.StartWith("Capture."), "it names the folder and the reason");
            Assert.That(rig.Window.Exists("Thumbnail"), Is.True, "with the image intact");
            Assert.That(rig.Window.TextOf("SizeText"), Is.EqualTo("1280 × 720"));
            Assert.That(rig.Window.IsEnabled("CopyButton") && rig.Window.IsEnabled("EditButton") && rig.Window.IsEnabled("SaveAsButton"), Is.True, "the other buttons still work");
            Assert.That(rig.Notes.Errors, Is.Empty);
            rig.Window.Screenshot("capture-done-f4-error-light");

            rig.Window.Click("CopyButton");
            Assert.That(rig.Window.WaitUntilClosed(TimeSpan.FromSeconds(3)), Is.True, "another button can still finish the job");
            Assert.That(rig.Interactor.Deliveries.Select(d => d.Destination), Is.EqualTo(new[] { CaptureDestination.File, CaptureDestination.Clipboard }));
        }
    }

    [Test]
    public void F5_CopyFails_TheDialogStaysOpenWithTheImageAndTheErrorInsideItself_InTheDarkTheme()
    {
        var rig = Open(ResolvedLanguage.Vietnamese, AppTheme.Dark);
        using (rig.Window)
        {
            var failure = NotificationMessage.Of("Capture.ClipboardFailed", "the clipboard is held by another program");
            rig.Interactor.DeliverAnswer = _ => new CaptureDeliveryResult(false, true, null, failure);

            rig.Window.Click("CopyButton");

            Assert.That(rig.Window.IsOpen, Is.True, "F5: the dialog stays open");
            Assert.That(rig.Window.TextOf("DialogError"), Does.Contain("the clipboard is held by another program").And.Not.StartWith("Capture."));
            Assert.That(rig.Window.Exists("Thumbnail"), Is.True);
            rig.Window.Screenshot("capture-done-f5-error-dark");
        }
    }

    [Test]
    public void Dialog_WithNoError_ShowsNoErrorText()
    {
        var rig = Open();
        using (rig.Window)
        {
            Assert.That(rig.Window.Exists("SaveButton"), Is.True, "the dialog is really built");
            Assert.That(rig.Window.Exists("DialogError"), Is.False, "nothing is shown where no message belongs");
        }
    }

    [Test]
    public void Dialog_IsCenteredOnTheMonitorOfTheCapture_AndAlwaysOnTop()
    {
        var area = new PixelRect(100, 100, 800, 600);
        var rig = Open(area: area);
        using (rig.Window)
        {
            var bounds = rig.Window.Root.BoundingRectangle;

            Assert.That(bounds.Left + (bounds.Width / 2.0), Is.EqualTo(500).Within(2), "horizontally centred on the monitor of the capture");
            Assert.That(bounds.Top + (bounds.Height / 2.0), Is.EqualTo(400).Within(2), "vertically centred on it");
            var topmost = WpfHost.Instance.Invoke(() => new CaptureDoneWindow(
                new CaptureDoneViewModel(new FakeCaptureInteractor(), CaptureTestData.Solid(10, 10), CaptureSettings.Default(), new FakeFileDialogs(), new RecordingNotifications(), new FakeLocalizer()),
                area).Topmost);
            Assert.That(topmost, Is.True, "above every other window");
        }
    }

    [Test]
    public void Dialog_TwoOpenAtOnce_EachKeepsItsOwnImageAndClosingOneLeavesTheOther()
    {
        var first = Open(width: 640, height: 480);
        var second = Open(width: 1920, height: 1080, area: new PixelRect(900, 100, 800, 600));
        using (first.Window)
        using (second.Window)
        {
            Assert.That(first.Window.TextOf("SizeText"), Is.EqualTo("640 × 480"));
            Assert.That(second.Window.TextOf("SizeText"), Is.EqualTo("1920 × 1080"));

            first.Window.Click("DiscardButton");

            Assert.That(first.Window.WaitUntilClosed(TimeSpan.FromSeconds(3)), Is.True);
            Assert.That(second.Window.IsOpen, Is.True, "the other capture's dialog is untouched");
            Assert.That(second.Window.TextOf("SizeText"), Is.EqualTo("1920 × 1080"));
            Assert.That(second.Interactor.Deliveries, Is.Empty);
        }
    }

    [TestCase(ResolvedLanguage.Vietnamese, AppTheme.Light, "vi-light")]
    [TestCase(ResolvedLanguage.Vietnamese, AppTheme.Dark, "vi-dark")]
    [TestCase(ResolvedLanguage.English, AppTheme.Light, "en-light")]
    [TestCase(ResolvedLanguage.English, AppTheme.Dark, "en-dark")]
    public void Screenshot_Dialog_IsTakenForBothThemesAndLanguages(ResolvedLanguage language, AppTheme theme, string suffix)
    {
        var rig = Open(language, theme);
        using (rig.Window)
        {
            rig.Window.Focus("SaveAsButton");
            var path = rig.Window.Screenshot($"capture-done-{suffix}");

            Assert.That(rig.Window.Exists("SaveButton"), Is.True, "open the picture: the buttons are missing");
            Assert.That(new System.IO.FileInfo(path).Length, Is.GreaterThan(3_000), "the picture is nearly empty: the dialog drew nothing");
        }
    }

    [TestCase(AppTheme.Light)]
    [TestCase(AppTheme.Dark)]
    public void Contrast_TheDialogsOwnTextButtonsAndBorders_ReachTheRatiosInBothThemes(AppTheme theme)
    {
        var rig = Open(theme: theme);
        using (rig.Window)
        {
            rig.Interactor.DeliverAnswer = _ => new CaptureDeliveryResult(false, true, null, NotificationMessage.Of("Capture.SaveFailed", @"D:\ReadOnly", "access denied"));
            rig.Window.Click("SaveButton");

            var problems = WpfHost.Instance.Invoke(() =>
            {
                var found = new List<string>();
                var window = rig.Window.Window;
                var windowBackground = (SolidColorBrush)window.Background;

                void Text(string id)
                {
                    var text = VisualTreeFinder.FindByAutomationId(window, id) as System.Windows.Controls.TextBlock;
                    if (text?.Foreground is not SolidColorBrush foreground)
                    {
                        found.Add($"{id}: no solid text brush to measure");
                        return;
                    }

                    var ratio = Contrast.Ratio(foreground, windowBackground);
                    if (ratio < Contrast.MinimumForText)
                    {
                        found.Add($"{id}: {ratio:0.00}:1 on the window, needs {Contrast.MinimumForText}:1");
                    }
                }

                Text("SizeText");
                Text("FormatText");
                Text("DialogError");

                foreach (var id in _buttonsInVisualOrder)
                {
                    var button = VisualTreeFinder.FindByAutomationId(window, id) as System.Windows.Controls.Button;
                    if (button?.Foreground is not SolidColorBrush foreground || button.Background is not SolidColorBrush background)
                    {
                        found.Add($"{id}: no solid brushes to measure");
                        continue;
                    }

                    var ratio = Contrast.Ratio(foreground, background);
                    if (ratio < Contrast.MinimumForText)
                    {
                        found.Add($"{id}: label {ratio:0.00}:1 on its button, needs {Contrast.MinimumForText}:1");
                    }

                    if (button.BorderBrush is SolidColorBrush border && border.Color.A > 0 && Contrast.Ratio(border, windowBackground) < Contrast.MinimumForIconOrBorder)
                    {
                        found.Add($"{id}: border is under {Contrast.MinimumForIconOrBorder}:1 on the window");
                    }
                }

                var frame = VisualTreeFinder.FindByAutomationId(window, "ThumbnailFrame") as System.Windows.Controls.Border;
                if (frame?.BorderBrush is not SolidColorBrush frameBrush)
                {
                    found.Add("ThumbnailFrame: no solid border brush to measure");
                }
                else if (Contrast.Ratio(frameBrush, windowBackground) < Contrast.MinimumForIconOrBorder)
                {
                    found.Add($"ThumbnailFrame: border is under {Contrast.MinimumForIconOrBorder}:1 on the window");
                }

                return found;
            });

            Assert.That(problems, Is.Empty, $"{theme} theme:\n{string.Join('\n', problems)}");
        }
    }

    [Test]
    public void Language_SwitchWhileOpen_RenamesTheButtonsAtOnce()
    {
        var rig = Open(ResolvedLanguage.Vietnamese);
        using (rig.Window)
        {
            Assert.That(rig.Window.NameOf("SaveButton"), Is.EqualTo("Lưu"));

            rig.Window.Host.Invoke(() => rig.Window.Host.Language.Apply(ResolvedLanguage.English));
            rig.Window.Host.Settle();

            Assert.That(rig.Window.NameOf("SaveButton"), Is.EqualTo("Save"));
            Assert.That(rig.Window.NameOf("DiscardButton"), Is.EqualTo("Discard"));
        }
    }
}
