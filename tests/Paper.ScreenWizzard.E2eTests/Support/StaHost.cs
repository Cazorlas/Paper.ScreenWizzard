using System.Windows;
using System.Windows.Threading;

namespace Paper.ScreenWizzard.E2eTests.Support;

/// <summary>
/// The one WPF world of the test process: an STA thread that owns the single <see cref="Application"/> and pumps messages for as long
/// as the tests run. The adapters that need a message loop (the hotkeys) and the clipboard (which needs an STA thread) are created
/// and used on it; the test thread only calls <see cref="Invoke(Action)"/>.
/// </summary>
public sealed class StaHost
{
    private static readonly Lazy<StaHost> _instance = new(() => new StaHost());

    private StaHost()
    {
        Dispatcher? dispatcher = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            dispatcher = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run();
        })
        {
            IsBackground = true,
            Name = "E2eTests STA",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        Dispatcher = dispatcher!;
    }

    public static StaHost Instance => _instance.Value;

    public Dispatcher Dispatcher { get; }

    public void Invoke(Action action) => Dispatcher.Invoke(action);

    public T Invoke<T>(Func<T> function) => Dispatcher.Invoke(function);

    /// <summary>Waits until WPF has processed what is queued, asking the dispatcher and not the clock.</summary>
    public void Settle() => Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
}
