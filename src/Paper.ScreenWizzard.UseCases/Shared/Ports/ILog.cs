namespace Paper.ScreenWizzard.UseCases.Shared.Ports;

public interface ILog
{
    void Info(string message);

    void Warning(string message);

    void Error(string message, Exception? exception);
}
