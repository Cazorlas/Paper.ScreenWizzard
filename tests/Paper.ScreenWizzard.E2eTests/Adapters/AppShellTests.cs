using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Paper.ScreenWizzard.App;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.E2eTests.Support;
using Paper.ScreenWizzard.Infrastructure.Shell;
using Paper.ScreenWizzard.Presentation.Shared.Views;
using Paper.ScreenWizzard.Presentation.Shell.Views;
using static Paper.ScreenWizzard.E2eTests.Support.HotkeyRig;

namespace Paper.ScreenWizzard.E2eTests.Adapters;

/// <summary>
/// <c>AppShell</c> started inside the test process, under a throw-away data folder, instance name and hotkey chords (Ctrl+Alt+Shift+F13..F16
/// written into a seeded settings file), so it shares nothing with a running copy and never asks Windows for PrintScreen. It is started as
/// Windows would at logon (<c>--autostart</c>): no window, only the tray. It proves the wiring the exe depends on; driving the exe itself as a
/// user is plan task T18.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class AppShellTests
{
    [Test]
    public void AutostartStart_ShowsNoWindow_HoldsItsHotkeys_AndASecondLaunchBringsTheBar()
    {
        using var scratch = new ScratchFolder();
        var instance = "Paper.ScreenWizzard.E2E." + Guid.NewGuid().ToString("N");
        var seeded = StoresTests.EveryFieldChanged() with
        {
            Hotkeys = new Dictionary<CaptureKind, HotkeyChord>
            {
                [CaptureKind.Rectangle] = F13,
                [CaptureKind.Freeform] = F14,
                [CaptureKind.Window] = F15,
                [CaptureKind.FullScreen] = new(Chorded, "F16"),
            },
            SaveFolder = Path.Combine(scratch.Path, "shots"),
            StartWithWindows = false,
            Language = AppLanguage.English,
            Theme = AppTheme.Light,
            CaptureBarPosition = null,
        };
        Assert.That(new SettingsStore(scratch.Path).Save(seeded).Success, Is.True, "set-up: the seeded settings file");
        using var rig = new HotkeyRig();
        var exits = 0;

        var opened = StaHost.Instance.Invoke(() =>
        {
            var provider = CompositionRoot.Build(new StartupOptions(scratch.Path, instance, Autostart: true), () => exits++);
            try
            {
                var shell = provider.GetRequiredService<AppShell>();
                Assert.That(shell.Start(), Is.True, "the first copy runs");

                // No window at logon: the capture bar is not shown (SPEC shell), only the tray icon exists.
                Assert.That(Application.Current.Windows.OfType<CaptureBarWindow>().Count(w => w.IsVisible), Is.Zero);

                // The four hotkeys of the settings file are held by this copy: nobody else can take them.
                var other = rig.NewService();
                foreach (var (kind, chord) in seeded.Hotkeys)
                {
                    Assert.That(other.Register(kind, chord).Success, Is.False, $"{kind} {chord} should be held by the app");
                }

                // A second launch (another SingleInstance object under the same name) is refused and wakes this copy.
                using var second = new SingleInstance(instance);
                Assert.That(second.TryBecomeFirstInstance(), Is.False, "the mutex is held by the running copy");
                second.NotifyFirstInstance();
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (DateTime.UtcNow < deadline && !Application.Current.Windows.OfType<CaptureBarWindow>().Any(w => w.IsVisible))
                {
                    // Pump the dispatcher so the BeginInvoke of the second-launch handler runs, as the message loop does in the exe.
                    Application.Current.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
                    Thread.Sleep(50);
                }

                var bar = Application.Current.Windows.OfType<CaptureBarWindow>().SingleOrDefault(w => w.IsVisible);
                Assert.That(bar, Is.Not.Null, "a second launch shows the capture bar of the first copy");
                Native.GetWindowRect(new System.Windows.Interop.WindowInteropHelper(bar!).Handle, out var rect);
                var desktop = new Paper.ScreenWizzard.Infrastructure.Shared.MonitorCatalog().GetMonitors();
                Assert.That(
                    desktop.Any(m => rect.Left >= m.Bounds.X && rect.Top >= m.Bounds.Y && rect.Right <= m.Bounds.X + m.Bounds.Width && rect.Bottom <= m.Bounds.Y + m.Bounds.Height),
                    Is.True,
                    $"the bar ({rect.Left},{rect.Top})-({rect.Right},{rect.Bottom}) sits inside a monitor");

                shell.Exit();
                Assert.That(exits, Is.EqualTo(1), "Exit asks the application to shut down");
                bar!.Close();
                return true;
            }
            finally
            {
                // What OnExit does: the tray icon, the hotkeys and the mutex go with the container.
                provider.Dispose();
            }
        });

        Assert.That(opened, Is.True);

        // After the container is gone the chords and the name are free again.
        var again = rig.NewService();
        Assert.That(Register(again, CaptureKind.Rectangle, F13).Success, Is.True, "Exit released the hotkeys");
        using var next = new SingleInstance(instance);
        Assert.That(next.TryBecomeFirstInstance(), Is.True, "Exit released the mutex");
    }

    [Test]
    public void AHotkeyAnotherProgramHolds_IsToldByAToastAtStart_NotByAnErrorBoxThatMustBeClosed()
    {
        // Seen by the owner on 2026-09-21: a box "These hotkeys could not be registered" that had to be closed at every start
        // (Windows at logon, the installer's "run now") because Alt+PrintScreen and Ctrl+PrintScreen are held on that PC. The
        // hotkey still does not work, the user is still told which one and why, but nothing is left on the screen to dismiss.
        using var scratch = new ScratchFolder();
        var instance = "Paper.ScreenWizzard.E2E." + Guid.NewGuid().ToString("N");
        var seeded = StoresTests.EveryFieldChanged() with
        {
            Hotkeys = new Dictionary<CaptureKind, HotkeyChord>
            {
                [CaptureKind.Rectangle] = F13,
                [CaptureKind.Freeform] = F14,
                [CaptureKind.Window] = F15,
                [CaptureKind.FullScreen] = new(Chorded, "F16"),
            },
            SaveFolder = Path.Combine(scratch.Path, "shots"),
            StartWithWindows = false,
            Language = AppLanguage.English,
            Theme = AppTheme.Light,
            CaptureBarPosition = null,
        };
        Assert.That(new SettingsStore(scratch.Path).Save(seeded).Success, Is.True, "set-up: the seeded settings file");
        using var rig = new HotkeyRig();
        var holder = rig.NewService();
        Assert.That(Register(holder, CaptureKind.Freeform, F14).Success, Is.True, "set-up: another program holds F14");

        var (errorBoxes, toasts) = StaHost.Instance.Invoke(() =>
        {
            var provider = CompositionRoot.Build(new StartupOptions(scratch.Path, instance, Autostart: true), () => { });
            try
            {
                var shell = provider.GetRequiredService<AppShell>();
                Assert.That(shell.Start(), Is.True, "the app runs although one hotkey could not be registered");
                Application.Current.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
                var found = (
                    Application.Current.Windows.OfType<ErrorDialogWindow>().Count(),
                    Application.Current.Windows.OfType<ToastWindow>().Count());
                foreach (var window in Application.Current.Windows.OfType<Window>().Where(w => w is ErrorDialogWindow or ToastWindow).ToList())
                {
                    window.Close();
                }

                return found;
            }
            finally
            {
                provider.Dispose();
            }
        });

        Assert.That(errorBoxes, Is.Zero, "no error box to close");
        Assert.That(toasts, Is.EqualTo(1), "one toast names the hotkey");
    }
}
