using System;
using Adamantium.Core.Commands;

namespace Adamantium.MVVM;

/// <summary>
/// A synchronous command that passes a typed parameter to its handler. The MVVM generator's <c>[Command]</c> on a
/// <c>void</c> method taking a single argument (e.g. <c>void Delete(Guid id)</c>) emits one of these
/// (<c>AdamantiumCommand&lt;Guid&gt;</c>). The UI binds it through the non-generic <see cref="ICommand"/> — the
/// parameter arrives as <c>object</c> and is coerced to <typeparamref name="T"/> — while code can call the typed
/// <see cref="Execute(T)"/>/<see cref="CanExecute(T)"/> directly. There is deliberately NO generic command interface;
/// type safety lives here, in the class, and the UI boundary stays untyped (as it must for any XAML binding).
/// </summary>
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
