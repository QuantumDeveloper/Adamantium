using System;
using Adamantium.Core.Commands;

namespace Adamantium.MVVM;

/// <summary>A synchronous command with a typed parameter: the UI binds it untyped through <see cref="ICommand"/>, code calls
/// <see cref="Execute(T)"/> and <see cref="CanExecute(T)"/>.</summary>
public sealed class AdamantiumCommand<T> : ICommand
{
    private readonly Action<T> _execute;
    private readonly Func<T, bool> _canExecute;

    public AdamantiumCommand(Action<T> execute, Func<T, bool> canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(T parameter) => _canExecute == null || _canExecute(parameter);

    public void Execute(T parameter)
    {
        if (CanExecute(parameter)) _execute(parameter);
    }

    bool ICommand.CanExecute(object parameter) => CanExecute(Coerce(parameter));

    void ICommand.Execute(object parameter) => Execute(Coerce(parameter));

    public event EventHandler CanExecuteChanged;

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    private static T Coerce(object parameter) => parameter is T value ? value : default;
}
