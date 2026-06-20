using System;
using Adamantium.UI.Core.Commands;

namespace Adamantium.MVVM;

/// <summary>
/// A synchronous command wrapping a delegate (+ optional CanExecute predicate). The MVVM generator's
/// <c>[Command]</c> on a <c>void</c> method emits a lazy property creating one of these. (Async <c>Task</c>
/// methods get <see cref="AdamantiumAsyncCommand"/> instead.)
/// </summary>
public sealed class AdamantiumCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool> _canExecute;

    public AdamantiumCommand(Action execute, Func<bool> canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object parameter = null) => _canExecute == null || _canExecute();

    public void Execute(object parameter = null)
    {
        if (CanExecute()) _execute();
    }

    public event EventHandler CanExecuteChanged;

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
