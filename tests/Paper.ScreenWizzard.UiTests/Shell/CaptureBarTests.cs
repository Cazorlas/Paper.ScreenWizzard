using System.IO;
using FlaUI.Core.Input;
using NUnit.Framework;
using Paper.ScreenWizzard.App.ViewModels.Shell;
using Paper.ScreenWizzard.App.Views.Shell;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UiTests.Support;
using Paper.ScreenWizzard.UseCases.Shell.Models;

namespace Paper.ScreenWizzard.UiTests.Shell;

/// <summary>The capture bar on mock data (plan T5): the wireframe's one button per capture kind, a Settings button, keyboard and names.</summary>
[TestFixture]
public sealed class CaptureBarTests : UiTestBase
{
    private static readonly string[] _visibleOrder =
    [
        "CaptureButton.Rectangle",
        "CaptureButton.Freeform",
        "CaptureButton.Window",
        "CaptureButton.FullScreen",
        "SettingsButton",
        "CloseButton",
    ];

    private static (CaptureBarViewModel ViewModel, WindowSession Session) Open(
        ResolvedLanguage language = ResolvedLanguage.Vietnamese,
        AppTheme theme = AppTheme.Light)
    {
        CaptureBarViewModel? viewModel = null;
        var session = WpfHost.Instance.Show(
            () =>
            {
                viewModel = new CaptureBarViewModel();
                return new CaptureBarWindow(viewModel);
            },
            language,
            theme);
        return (viewModel!, session);
    }

    [Test]
    public void Keyboard_TabThroughTheBar_FollowsTheVisibleOrder()
    {
        var (_, session) = Open();
        using (session)
        {
            session.Focus(_visibleOrder[0]);

            var reached = session.TabThrough(_visibleOrder.Length - 1);

            Assert.That(reached, Is.EqualTo(_visibleOrder.Skip(1).ToArray()));
        }
    }

    [Test]
    public void Keyboard_EveryIconOnlyButtonOfTheBar_HasAName()
    {
        var (_, session) = Open();
        using (session)
        {
            var buttons = session.ButtonNames();

            Assert.That(buttons.Select(b => b.AutomationId), Is.EquivalentTo(_visibleOrder), "the bar should hold exactly these buttons");
            Assert.That(buttons.Where(b => string.IsNullOrWhiteSpace(b.Name)).Select(b => b.AutomationId), Is.Empty, "buttons with no accessible name");
            Assert.That(session.NameOf("SettingsButton"), Is.EqualTo("Cài đặt"));
            Assert.That(session.NameOf("CloseButton"), Is.EqualTo("Đóng"));
        }
    }

    [TestCase(AppTheme.Light)]
    [TestCase(AppTheme.Dark)]
    public void Keyboard_FocusedBarButton_ShowsAVisibleFocusRing(AppTheme theme)
    {
        var (_, session) = Open(theme: theme);
        using (session)
        {
            var focusBrush = ResourceFiles.Load($"Themes/{theme}.xaml")["Brush.Focus"];
            Assert.That(focusBrush, Is.InstanceOf<System.Windows.Media.SolidColorBrush>(), $"Brush.Focus is missing from the {theme} theme");
            var ring = ((System.Windows.Media.SolidColorBrush)focusBrush).Color;

            session.Focus("CaptureButton.Window");
            session.Focus("CaptureButton.Freeform");

            Assert.That(session.CountPixels("CaptureButton.Window", ring), Is.Zero);
            Assert.That(session.CountPixels("CaptureButton.Freeform", ring), Is.GreaterThan(20));
        }
    }

    [TestCase("CaptureButton.Rectangle", CaptureKind.Rectangle)]
    [TestCase("CaptureButton.Freeform", CaptureKind.Freeform)]
    [TestCase("CaptureButton.Window", CaptureKind.Window)]
    [TestCase("CaptureButton.FullScreen", CaptureKind.FullScreen)]
    public void Buttons_EachCaptureKind_RaisesCaptureRequestedWithThatKind(string buttonId, CaptureKind expected)
    {
        var (viewModel, session) = Open();
        using (session)
        {
            var requested = new List<CaptureKind>();
            WpfHost.Instance.Invoke(() => viewModel.CaptureRequested += requested.Add);

            session.Click(buttonId);

            Assert.That(requested, Is.EqualTo(new[] { expected }), "one click asks for one capture of that kind, and the bar captures nothing itself");
        }
    }

    [Test]
    public void Buttons_Settings_RaisesSettingsRequested()
    {
        var (viewModel, session) = Open();
        using (session)
        {
            var raised = 0;
            WpfHost.Instance.Invoke(() => viewModel.SettingsRequested += (_, _) => raised++);

            session.Click("SettingsButton");

            Assert.That(raised, Is.EqualTo(1));
        }
    }

    [Test]
    public void Buttons_Close_ClosesTheBarWindowOnly()
    {
        var (viewModel, session) = Open();
        using (session)
        {
            var raised = 0;
            WpfHost.Instance.Invoke(() => viewModel.CloseRequested += (_, _) => raised++);

            session.Click("CloseButton");

            Assert.That(session.WaitUntilClosed(TimeSpan.FromSeconds(3)), Is.True, "X closes the bar");
            Assert.That(raised, Is.EqualTo(1));
            Assert.That(System.Windows.Application.Current, Is.Not.Null, "closing the bar must not end the application (SPEC shell)");
        }
    }

    [Test]
    public void Bar_StaysOnTopAndOffTheTaskbar()
    {
        var (_, session) = Open();
        using (session)
        {
            // The harness sets Topmost on every window it shows, so ask a bar the harness did not touch.
            var (topmost, inTaskbar) = WpfHost.Instance.Invoke(() =>
            {
                var bare = new CaptureBarWindow(new CaptureBarViewModel());
                return (bare.Topmost, bare.ShowInTaskbar);
            });

            Assert.That(topmost, Is.True, "the capture bar always floats above other windows");
            Assert.That(inTaskbar, Is.False, "the app lives in the tray, not the taskbar");
        }
    }

    [Test]
    public void Bar_DraggedByItsBackground_MovesTheWindow()
    {
        var (_, session) = Open();
        using (session)
        {
            // No EnsureForeground here: the drag is mouse only and the bar is topmost, so it needs no keyboard focus. The bar is also
            // deliberately not activated when shown, which is why Windows refused it the foreground about half the time.
            Assert.That(
                WpfHost.Instance.Invoke(() => session.Window.WindowStyle),
                Is.EqualTo(System.Windows.WindowStyle.None),
                "the bar has no title bar, so dragging it is the bar's own job and the margin below is client area");
            var before = WpfHost.Instance.Invoke(() => (session.Window.Left, session.Window.Top));
            var frame = session.Root.BoundingRectangle;
            // The bar has a few pixels of padding around its buttons: that margin is "anywhere but a button".
            var grab = new System.Drawing.Point(frame.Left + 3, frame.Top + 3);

            Mouse.MoveTo(grab);
            Mouse.Down(MouseButton.Left);
            Mouse.MoveTo(new System.Drawing.Point(grab.X + 150, grab.Y + 80));
            Mouse.Up(MouseButton.Left);
            WpfHost.Instance.Settle();

            var after = WpfHost.Instance.Invoke(() => (session.Window.Left, session.Window.Top));
            Assert.That(after.Left, Is.GreaterThan(before.Left + 40), "dragging the bar's margin must move the window right");
            Assert.That(after.Top, Is.GreaterThan(before.Top + 20), "and down");
        }
    }

    [Test]
    public void Language_SwitchWhileOpen_RenamesTheButtonsAtOnce()
    {
        var (_, session) = Open(ResolvedLanguage.Vietnamese);
        using (session)
        {
            Assert.That(session.NameOf("CaptureButton.Rectangle"), Is.EqualTo("Vùng chữ nhật"));

            session.Host.Invoke(() => session.Host.Language.Apply(ResolvedLanguage.English));
            session.Host.Settle();

            Assert.That(session.NameOf("CaptureButton.Rectangle"), Is.EqualTo("Rectangle"));
            Assert.That(session.NameOf("SettingsButton"), Is.EqualTo("Settings"));
        }
    }

    [Test]
    public void Theme_SwitchWhileOpen_RecoloursTheWindow()
    {
        var (_, session) = Open(theme: AppTheme.Light);
        using (session)
        {
            var light = ResourceFiles.Load("Themes/Light.xaml")["Brush.Window"];
            var dark = ResourceFiles.Load("Themes/Dark.xaml")["Brush.Window"];
            Assert.That(light, Is.InstanceOf<System.Windows.Media.SolidColorBrush>(), "Brush.Window is missing from the Light theme");
            Assert.That(dark, Is.InstanceOf<System.Windows.Media.SolidColorBrush>(), "Brush.Window is missing from the Dark theme");
            var lightColour = ((System.Windows.Media.SolidColorBrush)light).Color;
            var darkColour = ((System.Windows.Media.SolidColorBrush)dark).Color;

            System.Windows.Media.Color BarBackground() => session.Host.Invoke(() => ((System.Windows.Media.SolidColorBrush)session.Window.Background).Color);

            Assert.That(BarBackground(), Is.EqualTo(lightColour));
            session.Host.Invoke(() => session.Host.Theme.Apply(AppTheme.Dark));
            session.Host.Settle();
            Assert.That(BarBackground(), Is.EqualTo(darkColour), "the open window must take the dark colours without being reopened");
        }
    }

    [TestCase(ResolvedLanguage.Vietnamese, AppTheme.Light, "vi-light")]
    [TestCase(ResolvedLanguage.Vietnamese, AppTheme.Dark, "vi-dark")]
    [TestCase(ResolvedLanguage.English, AppTheme.Light, "en-light")]
    [TestCase(ResolvedLanguage.English, AppTheme.Dark, "en-dark")]
    public void Screenshot_CaptureBar_IsTakenForBothThemesAndLanguages(ResolvedLanguage language, AppTheme theme, string suffix)
    {
        var (_, session) = Open(language, theme);
        using (session)
        {
            var path = session.Screenshot($"shell-capturebar-{suffix}");

            Assert.That(session.Exists("CaptureButton.Rectangle"), Is.True, "open the picture: the capture buttons are missing");
            Assert.That(new FileInfo(path).Length, Is.GreaterThan(1_500), "the picture is nearly empty: the bar drew nothing");
        }
    }
}
