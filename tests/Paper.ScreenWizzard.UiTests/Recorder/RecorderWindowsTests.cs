using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Recorder;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.Presentation.Recorder.ViewModels;
using Paper.ScreenWizzard.Presentation.Recorder.Views;
using Paper.ScreenWizzard.UiTests.Capture;
using Paper.ScreenWizzard.UiTests.Support;
using Paper.ScreenWizzard.UseCases.Recorder.Models;
using Paper.ScreenWizzard.UseCases.Shared.Models;

namespace Paper.ScreenWizzard.UiTests.Recorder;

/// <summary>
/// SPEC recorder, "What the user does" 2 to 5 and F7, on the real windows with mock data: the recording bar and the "Recorded" window.
/// The recording itself is the logic lane's and the adapters' (unit and E2E tests).
/// </summary>
[TestFixture]
public sealed class RecorderWindowsTests : UiTestBase
{
    private static readonly MonitorInfo One = new(0, new PixelRect(0, 0, 1920, 1080), true, 96);
    private static readonly MonitorInfo Two = new(1, new PixelRect(1920, 0, 2560, 1440), false, 96);

    private static RecorderSettings Settings() => RecorderRules.Defaults(@"C:\Users\An\Videos");

    private static (RecordingBarViewModel ViewModel, WindowSession Session) OpenBar(params MonitorInfo[] monitors)
    {
        RecordingBarViewModel? viewModel = null;
        var session = WpfHost.Instance.Show(
            () =>
            {
                viewModel = new RecordingBarViewModel(Settings(), monitors.Length == 0 ? [One] : monitors, new FakeLocalizer());
                return new RecordingBarWindow(viewModel);
            },
            ResolvedLanguage.Vietnamese,
            AppTheme.Light);
        return (viewModel!, session);
    }

    [Test]
    public void TheBar_HasTheFourChoices_TheThreeSwitches_AndRecord()
    {
        var (_, session) = OpenBar();
        using (session)
        {
            foreach (var id in new[] { "Target.Monitor", "Target.Region", "Target.Window", "Target.Desktop", "SystemSoundSwitch", "MicrophoneSwitch", "PointerSwitch", "RecordButton", "CloseButton" })
            {
                Assert.That(session.Exists(id), Is.True, id);
            }

            Assert.That(session.IsSelected("Target.Monitor"), Is.True, "SPEC recorder Inputs: the monitor under the pointer");
            Assert.That(session.IsToggledOn("SystemSoundSwitch"), Is.True, "system sound on");
            Assert.That(session.IsToggledOn("MicrophoneSwitch"), Is.False, "microphone off");
            Assert.That(session.IsToggledOn("PointerSwitch"), Is.True, "pointer on");
        }
    }

    [Test]
    public void TheMonitorMenu_IsThereOnlyWithSeveralMonitors()
    {
        var (_, single) = OpenBar(One);
        using (single)
        {
            Assert.That(single.Find("MonitorChoice")?.IsOffscreen ?? true, Is.True, "one monitor: nothing to choose");
        }

        var (viewModel, both) = OpenBar(One, Two);
        using (both)
        {
            Assert.That(both.Exists("MonitorChoice"), Is.True);
            Assert.That(viewModel.Monitors.Select(m => m.Label), Is.EqualTo(new[] { "Recorder.MonitorPrimary", "Recorder.MonitorOther" }));
        }
    }

    [Test]
    public void Record_HandsTheChoicesOfTheBar()
    {
        var (viewModel, session) = OpenBar();
        using (session)
        {
            RecorderSettings? asked = null;
            WpfHost.Instance.Invoke(() => viewModel.RecordRequested += choices => asked = choices);

            session.Click("Target.Region");
            session.Click("MicrophoneSwitch");
            session.Click("RecordButton");

            Assert.That(asked, Is.Not.Null);
            Assert.That(asked!.Target, Is.EqualTo(RecordTargetKind.Region));
            Assert.That(asked.Microphone, Is.True);
            Assert.That(asked.SystemSound, Is.True);
        }
    }

    [Test]
    public void F7_ARegionTooSmall_IsSaidOnTheBar()
    {
        var (viewModel, session) = OpenBar();
        using (session)
        {
            WpfHost.Instance.Invoke(() => viewModel.ShowMessage(NotificationMessage.Of("Recorder.AreaTooSmall")));

            Assert.That(session.TextOf("RecordingBarMessage"), Is.EqualTo("Recorder.AreaTooSmall"));
        }
    }

    [Test]
    public void TheRecordedWindow_ShowsTheFile_AndItsThreeButtonsAsk()
    {
        RecordedViewModel? viewModel = null;
        var result = new RecordingResult(RecordingEnd.Stopped, @"C:\Users\An\Videos\Paper.ScreenWizzard\Recording 2026-09-27 14.03.05.mp4", new PixelSize(1920, 1080), TimeSpan.FromSeconds(83), null, 42L * 1024 * 1024);
        var session = WpfHost.Instance.Show(
            () =>
            {
                viewModel = new RecordedViewModel(result, new FakeLocalizer());
                return new RecordedWindow(viewModel, new PixelRect(0, 0, 1920, 1080));
            },
            ResolvedLanguage.Vietnamese,
            AppTheme.Light);
        using (session)
        {
            var asked = new List<string>();
            WpfHost.Instance.Invoke(() =>
            {
                viewModel!.OpenVideoRequested += (_, _) => asked.Add("open");
                viewModel.ShowInFolderRequested += (_, _) => asked.Add("folder");
                viewModel.CloseRequested += (_, _) => asked.Add("close");
            });

            Assert.That(session.TextOf("RecordedFileName"), Is.EqualTo("Recording 2026-09-27 14.03.05.mp4"));
            Assert.That(session.TextOf("RecordedDetails"), Does.StartWith("1920 × 1080 · 01:23 · 42"));
            session.Click("OpenVideoButton");
            session.Click("ShowInFolderButton");
            session.Click("CloseRecordedButton");

            Assert.That(asked, Is.EqualTo(new[] { "open", "folder", "close" }));
        }
    }
}
