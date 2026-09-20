namespace Paper.ScreenWizzard.E2eTests.Support;

/// <summary>
/// Holds the Windows clipboard open from a helper thread, the way another program does while it copies: while it is held, another
/// caller's OpenClipboard fails. OpenClipboard and CloseClipboard must be called by the same thread, hence the thread.
/// </summary>
public sealed class ClipboardHolder : IDisposable
{
    private readonly ManualResetEventSlim _release = new();
    private readonly Thread _thread;

    private ClipboardHolder(Action<bool> opened)
    {
        _thread = new Thread(() =>
        {
            // The clipboard may itself be busy for a moment; try for a second before giving up.
            var open = false;
            for (var attempt = 0; attempt < 20 && !open; attempt++)
            {
                open = Native.OpenClipboard(IntPtr.Zero);
                if (!open)
                {
                    Thread.Sleep(50);
                }
            }

            opened(open);
            if (open)
            {
                _release.Wait();
                Native.CloseClipboard();
            }
        })
        {
            IsBackground = true,
            Name = "E2eTests clipboard holder",
        };
    }

    public static ClipboardHolder Hold()
    {
        var holding = new ManualResetEventSlim();
        var success = false;
        var holder = new ClipboardHolder(ok =>
        {
            success = ok;
            holding.Set();
        });
        holder._thread.Start();
        holding.Wait();
        if (!success)
        {
            holder.Dispose();
            throw new InvalidOperationException("could not open the clipboard to hold it");
        }

        return holder;
    }

    public void Dispose()
    {
        _release.Set();
        _thread.Join(TimeSpan.FromSeconds(3));
    }
}
