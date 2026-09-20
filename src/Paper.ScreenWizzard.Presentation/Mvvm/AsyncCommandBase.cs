namespace Paper.ScreenWizzard.Presentation.Mvvm;

/// <summary>
/// A command whose work awaits. While it runs it cannot be started again, so a double click never runs the workflow
/// twice; subclasses restore their own state (IsBusy) in a finally block.
/// </summary>
public abstract class AsyncCommandBase : CommandBase
{
    private bool _isExecuting;

    public bool IsExecuting => _isExecuting;

    public override bool CanExecute(object? parameter) => !_isExecuting && CanExecuteCore(parameter);

    public override async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _isExecuting = true;
        RaiseCanExecuteChanged();
        try
        {
            await ExecuteAsync(parameter);
        }
        catch (Exception exception)
        {
            HandleException(exception);
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    protected virtual bool CanExecuteCore(object? parameter) => true;

    protected abstract Task ExecuteAsync(object? parameter);

    /// <summary>
    /// Runs on the UI thread when the work threw. The default rethrows so the application's unhandled-exception handler
    /// reports it: an async void command must never swallow an exception (SPEC: nothing fails silently).
    /// </summary>
    protected virtual void HandleException(Exception exception) =>
        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
}
