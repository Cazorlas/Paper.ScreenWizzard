namespace Paper.ScreenWizzard.UseCases.Shared.Ports;

public interface ILogPort
{
    void Info(string message);

    void Warning(string message);

    void Error(string message, Exception? exception);
}
