using System.Globalization;
using System.Text;
using Paper.ScreenWizzard.UseCases.Shared.Ports;

namespace Paper.ScreenWizzard.Infrastructure.Common;

/// <summary>
/// Appends one line per message to <c>&lt;dataRoot&gt;\logs\yyyy-MM-dd.log</c>. A log must never be the reason the app fails, so
/// every error here is swallowed.
/// </summary>
public sealed class FileLogger : ILog
{
    private readonly string _logFolder;
    private readonly object _lock = new();

    public FileLogger(string dataRoot)
    {
        _logFolder = Path.Combine(dataRoot, "logs");
    }

    public void Info(string message) => Write("INFO", message, null);

    public void Warning(string message) => Write("WARN", message, null);

    public void Error(string message, Exception? exception) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        try
        {
            var now = DateTime.Now;
            var text = new StringBuilder()
                .Append(now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
                .Append(' ').Append(level).Append(' ').Append(message);
            if (exception is not null)
            {
                text.AppendLine().Append(exception);
            }

            text.AppendLine();
            lock (_lock)
            {
                Directory.CreateDirectory(_logFolder);
                var file = Path.Combine(_logFolder, now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".log");
                File.AppendAllText(file, text.ToString(), Encoding.UTF8);
            }
        }
        catch (Exception)
        {
            // Deliberately nothing: see the summary.
        }
    }
}
