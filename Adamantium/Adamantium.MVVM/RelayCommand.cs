using System;
using Adamantium.UI.Core.Commands;

namespace Adamantium.MVVM;

/// <summary>
/// A simple <see cref="ICommand"/> wrapping a delegate (+ an optional CanExecute predicate). The MVVM generator's
/// <c>[Command]</c> emits a lazy property creating one of these around the attributed method. Implements the
/// engine's parameterless <see cref="ICommand"/> (CanExecuteChanged / parameters arrive in a later phase).
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool> _canExecute;

    public RelayCommand(Action execute, Func<bool> canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute() => _canExecute?.Invoke() ?? true;

    public void Execute()
    {
        if (CanExecute()) _execute();
    }
}
