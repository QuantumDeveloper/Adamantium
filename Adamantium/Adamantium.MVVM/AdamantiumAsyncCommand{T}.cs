using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Adamantium.UI.Core.Commands;

namespace Adamantium.MVVM;

/// <summary>
/// An async command that passes a typed parameter to its handler. The MVVM generator's <c>[Command]</c> on a
/// <c>Task</c> method taking a single argument (optionally plus a trailing <c>CancellationToken</c>) emits one of
/// these (<c>AdamantiumAsyncCommand&lt;T&gt;</c>). Same disable-while-running / cancellation / exception semantics
/// as <see cref="AdamantiumAsyncCommand"/>; the UI binds it through the non-generic <see cref="IAsyncCommand"/> —
/// the parameter arrives as <c>object</c> and is coerced to <typeparamref name="T"/> — while code can call the typed
/// <see cref="ExecuteAsync(T, CancellationToken)"/> directly.
/// </summary>
public sealed class AdamantiumAsyncCommand<T> : IAsyncCommand
{
    private readonly Func<T, CancellationToken, Task> _execute;
    private readonly Func<T, bool> _canExecute;
    private CancellationTokenSource _cts;
    private bool _isRunning;

    public AdamantiumAsyncCommand(Func<T, CancellationToken, Task> execute, Func<T, bool> canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (_isRunning == value) return;
            _isRunning = value;
            RaisePropertyChanged(nameof(IsRunning));
            RaiseCanExecuteChanged();   // disable-while-running: bound controls re-query CanExecute
        }
    }

    public bool CanExecute(T parameter) => !_isRunning && (_canExecute == null || _canExecute(parameter));

    // Fire-and-forget for the UI click path; real exceptions surface on the UI thread (not swallowed).
    public async void Execute(T parameter) => await ExecuteAsync(parameter);

    public async Task ExecuteAsync(T parameter, CancellationToken cancellationToken = default)
    {
        if (!CanExecute(parameter)) return;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IsRunning = true;
        try
        {
            await _execute(parameter, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected when Cancel() (or the caller's token) fires — cancellation is not an error.
        }
        finally
        {
            IsRunning = false;
            var cts = _cts;
            _cts = null;
            cts?.Dispose();
        }
    }

    public void Cancel() => _cts?.Cancel();

    bool ICommand.CanExecute(object parameter) => CanExecute(Coerce(parameter));

    void ICommand.Execute(object parameter) => Execute(Coerce(parameter));

    Task IAsyncCommand.ExecuteAsync(object parameter, CancellationToken cancellationToken) =>
        ExecuteAsync(Coerce(parameter), cancellationToken);

    public event EventHandler CanExecuteChanged;

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    public event PropertyChangedEventHandler PropertyChanged;

    private void RaisePropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private static T Coerce(object parameter) => parameter is T value ? value : default;
}
