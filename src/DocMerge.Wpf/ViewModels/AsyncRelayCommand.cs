using System.Windows.Input;

namespace DocMerge.Wpf.ViewModels;

/// <summary>Adapts an asynchronous operation to a non-reentrant WPF command.</summary>
public sealed class AsyncRelayCommand(
    Func<Task> execute,
    Func<bool>? canExecute = null,
    Action<Exception>? errorHandler = null) : ICommand
{
    private bool _isRunning;

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc />
    public bool CanExecute(object? parameter) => !_isRunning && (canExecute?.Invoke() ?? true);

    /// <inheritdoc />
    public async void Execute(object? parameter)
    {
        await ExecuteAsync();
    }

    /// <summary>Executes the command and completes when the operation and state cleanup finish.</summary>
    public async Task ExecuteAsync()
    {
        if (!CanExecute(parameter: null))
        {
            return;
        }

        _isRunning = true;
        NotifyCanExecuteChanged();
        try
        {
            await execute();
        }
        catch (Exception error)
        {
            errorHandler?.Invoke(error);
        }
        finally
        {
            _isRunning = false;
            NotifyCanExecuteChanged();
        }
    }

    /// <summary>Reevaluates command availability.</summary>
    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
