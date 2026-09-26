using Paper.ScreenWizzard.UseCases.Shell.Ports;

namespace Paper.ScreenWizzard.Infrastructure.Shell;

/// <summary>
/// One copy per Windows session: a named mutex <c>Local\&lt;name&gt;</c> says who is first (<c>Local\</c> is the logon session, so two
/// users on one PC can each run a copy), and a named auto-reset event <c>Local\&lt;name&gt;.wake</c> is how a second launch tells the first
/// one. <see cref="SecondInstanceLaunched"/> is raised on a background thread; the app marshals it to the UI thread. The mutex is created
/// NOT owned, so no thread has to release it: it lives as long as this object (and the process) does. The name may not contain a
/// backslash (the Mutex page), so one is replaced.
/// </summary>
public sealed class SingleInstance : ISingleInstance, IDisposable
{
    private readonly string _mutexName;
    private readonly string _wakeName;
    private readonly ManualResetEventSlim _stop = new();
    private Mutex? _mutex;
    private EventWaitHandle? _wake;
    private Thread? _waiter;
    private bool _disposed;

    public SingleInstance(string name = "Paper.ScreenWizzard")
    {
        var safe = name.Replace('\\', '_');
        _mutexName = @"Local\" + safe;
        _wakeName = @"Local\" + safe + ".wake";
    }

    public event Action? SecondInstanceLaunched;

    public bool TryBecomeFirstInstance()
    {
        if (_disposed || _mutex is not null)
        {
            return _mutex is not null;
        }

        try
        {
            var mutex = new Mutex(false, _mutexName, out var createdNew);
            if (!createdNew)
            {
                mutex.Dispose();
                return false;
            }

            _mutex = mutex;
        }
        catch (UnauthorizedAccessException)
        {
            // The name is taken by an object this user may not open: another copy, as far as this session can tell.
            return false;
        }

        _wake = new EventWaitHandle(false, EventResetMode.AutoReset, _wakeName);
        _waiter = new Thread(WaitForSecondLaunch) { IsBackground = true, Name = "Paper.ScreenWizzard second launch" };
        _waiter.Start();
        return true;
    }

    public void NotifyFirstInstance()
    {
        try
        {
            // Opens the event the first copy made, or makes it when the first copy is still starting; either way it is signalled.
            using var wake = new EventWaitHandle(false, EventResetMode.AutoReset, _wakeName);
            wake.Set();
        }
        catch (Exception)
        {
            // Nobody to tell: the second copy exits either way, and there is nothing useful it could do about it.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stop.Set();
        _waiter?.Join(TimeSpan.FromSeconds(2));
        _wake?.Dispose();
        _mutex?.Dispose();
        _stop.Dispose();
    }

    private void WaitForSecondLaunch()
    {
        var wake = _wake;
        if (wake is null)
        {
            return;
        }

        var handles = new WaitHandle[] { wake, _stop.WaitHandle };
        while (WaitHandle.WaitAny(handles) == 0)
        {
            try
            {
                SecondInstanceLaunched?.Invoke();
            }
            catch (Exception)
            {
                // A handler that throws must not end the wait: the next launch still has to be heard.
            }
        }
    }
}
