using System.Windows;

namespace Paper.ScreenWizzard.E2eTests.Support;

/// <summary>
/// Best-effort copy of what is on the clipboard before a test overwrites it, and the way back. Formats that cannot be read or
/// written back (a delay-rendered format of another program) are lost; a test that must not lose the developer's clipboard says so
/// in the report. Run on the <see cref="StaHost"/> thread.
/// </summary>
public sealed class ClipboardBackup
{
    private readonly Dictionary<string, object> _formats = [];

    public int FormatCount => _formats.Count;

    public static ClipboardBackup Take() => StaHost.Instance.Invoke(() =>
    {
        var backup = new ClipboardBackup();
        try
        {
            var data = Clipboard.GetDataObject();
            if (data is null)
            {
                return backup;
            }

            foreach (var format in data.GetFormats(false))
            {
                try
                {
                    if (data.GetData(format, false) is { } value)
                    {
                        backup._formats[format] = value;
                    }
                }
                catch (Exception)
                {
                    // A format that cannot be read is not backed up.
                }
            }
        }
        catch (Exception)
        {
            // The clipboard was busy: nothing is backed up.
        }

        return backup;
    });

    public void Restore() => StaHost.Instance.Invoke(() =>
    {
        try
        {
            if (_formats.Count == 0)
            {
                Clipboard.Clear();
                return;
            }

            var data = new DataObject();
            foreach (var (format, value) in _formats)
            {
                try
                {
                    data.SetData(format, value);
                }
                catch (Exception)
                {
                    // A format that cannot be set again is dropped.
                }
            }

            Clipboard.SetDataObject(data, true);
        }
        catch (Exception)
        {
            // Best effort by design.
        }
    });
}
