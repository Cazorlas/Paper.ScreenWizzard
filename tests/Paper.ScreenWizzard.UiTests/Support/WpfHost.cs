using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Paper.ScreenWizzard.Presentation.ViewModels.Shell;
using Paper.ScreenWizzard.Presentation.Views.Shell.Services;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UseCases.Shell.Models;

namespace Paper.ScreenWizzard.UiTests.Support;

/// <summary>
/// The one WPF world of the test process (skill drive-wpf): a dedicated STA thread that owns the single
/// <see cref="Application"/> and runs its dispatcher for as long as the tests run. A window is created on that thread,
/// shown, and attached to FlaUI by handle - never launched as another process, because a cross-process UIA tree goes stale.
/// </summary>
public sealed class WpfHost
{
    private static readonly Lazy<WpfHost> _instance = new(() => new WpfHost());

    private readonly Dispatcher _dispatcher;

    private WpfHost()
    {
        Dispatcher? dispatcher = null;
        LanguageService? language = null;
        ThemeService? theme = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            // Per-monitor v2 before the first window, like the real app's manifest: WPF then draws sharp on a scaled monitor
            // and the UIA rectangles equal the physical pixels a screenshot uses.
            SetProcessDpiAwarenessContext(new IntPtr(-4));
            _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            dispatcher = Dispatcher.CurrentDispatcher;
            language = new LanguageService();
            theme = new ThemeService();
            ready.Set();
            Dispatcher.Run();
        })
        {
            IsBackground = true,
            Name = "UiTests STA",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        _dispatcher = dispatcher!;
        Language = language!;
        Theme = theme!;
        Appearance = new AppearanceService(Language, Theme);
        Notifications = new NotificationPresenter(Language) { ToastLifetime = TimeSpan.FromMilliseconds(400) };
    }

    public static WpfHost Instance => _instance.Value;

    /// <summary>The real language service, shared by every window of the run.</summary>
    public LanguageService Language { get; }

    public ThemeService Theme { get; }

    public IAppearanceService Appearance { get; }

    /// <summary>The real toast and error presenter, with a short toast so a test does not wait four seconds.</summary>
    public NotificationPresenter Notifications { get; }

    public Dispatcher Dispatcher => _dispatcher;

    public void Invoke(Action action) => _dispatcher.Invoke(action);

    public T Invoke<T>(Func<T> function) => _dispatcher.Invoke(function);

    /// <summary>Waits until WPF has processed what is queued, asking the dispatcher and not the clock.</summary>
    public void Settle()
    {
        FlaUI.Core.Input.Wait.UntilInputIsProcessed();
        _dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    /// <summary>
    /// Sets the language and the theme for the next window, creates and shows it on the UI thread, and returns the session
    /// FlaUI is attached to. Every test starts from Vietnamese and Light unless it says otherwise.
    /// </summary>
    public WindowSession Show<TWindow>(
        Func<TWindow> create,
        ResolvedLanguage language = ResolvedLanguage.Vietnamese,
        AppTheme theme = AppTheme.Light)
        where TWindow : Window
    {
        Invoke(() =>
        {
            Language.Apply(language);
            Theme.Apply(theme);
        });
        var window = Invoke(() =>
        {
            var created = create();
            created.Topmost = true;
            created.Show();
            created.Activate();
            return created;
        });
        var handle = Invoke(() => new System.Windows.Interop.WindowInteropHelper(window).Handle);
        Settle();
        return new WindowSession(this, window, handle);
    }

    /// <summary>
    /// Attaches FlaUI to another window of this process (a dialog or a toast) by its AutomationId. Such a window is a popup
    /// in its own top-level window, not a descendant of the one under test, so it is found in WPF's own list and never by
    /// searching the desktop, which one busy application can time out.
    /// </summary>
    public WindowSession Attach(string automationId)
    {
        var found = FlaUI.Core.Tools.Retry.WhileNull(
            () => Invoke(() => Application.Current.Windows.OfType<Window>().FirstOrDefault(
                w => System.Windows.Automation.AutomationProperties.GetAutomationId(w) == automationId && w.IsVisible)),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMilliseconds(100)).Result;
        NUnit.Framework.Assert.That(found, NUnit.Framework.Is.Not.Null, $"no window with AutomationId '{automationId}' opened");
        var handle = Invoke(() => new System.Windows.Interop.WindowInteropHelper(found!).Handle);
        Settle();
        return new WindowSession(this, found!, handle);
    }

    /// <summary>Closes every window still open, so the next test does not find the last one's leftovers.</summary>
    public void CloseAllWindows()
    {
        Invoke(() =>
        {
            foreach (var window in Application.Current.Windows.OfType<Window>().ToList())
            {
                window.Close();
            }
        });
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr value);
}
