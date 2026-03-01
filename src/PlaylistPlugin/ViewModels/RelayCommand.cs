using System.Windows.Input;

namespace PlaylistPlugin.ViewModels;

/// <summary>
/// Basic command wrapper for parameterless actions.
/// </summary>
internal sealed class RelayCommand : ICommand
{
    private readonly Action _execute;

    /// <summary>
    /// Initializes a new instance of the <see cref="RelayCommand"/> class.
    /// </summary>
    /// <param name="execute">Action to execute when the command runs.</param>
    public RelayCommand(Action execute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        _execute = execute;
    }

    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    /// <inheritdoc/>
    public bool CanExecute(object? parameter) => true;

    /// <inheritdoc/>
    public void Execute(object? parameter) => _execute();
}

/// <summary>
/// Basic command wrapper for actions that accept a single parameter.
/// </summary>
/// <typeparam name="T">Expected command parameter type.</typeparam>
internal sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;

    /// <summary>
    /// Initializes a new instance of the <see cref="RelayCommand{T}"/> class.
    /// </summary>
    /// <param name="execute">Action to execute when the command runs.</param>
    public RelayCommand(Action<T?> execute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        _execute = execute;
    }

    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    /// <inheritdoc/>
    public bool CanExecute(object? parameter) => true;

    /// <inheritdoc/>
    public void Execute(object? parameter) => _execute(parameter is T typed ? typed : default);
}
