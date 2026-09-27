using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Adamantium.Core.Commands;

namespace Adamantium.MVVM;

/// <summary>
/// An async command wrapping a <c>Func&lt;CancellationToken, Task&gt;</c>. While it runs, <see cref="IsRunning"/>
/// is true (bindable, change-notified) and <see cref="CanExecute"/> returns false — so a bound control auto-disables
/// and re-entry is blocked. <see cref="Cancel"/> cancels the in-flight run; the method's own exceptions propagate
/// (via the awaited <see cref="ExecuteAsync"/>, or onto the UI thread from the fire-and-forget <see cref="Execute"/>),
/// while expected cancellation is swallowed. The generator's <c>[Command]</c> on a <c>Task</c> method emits one.
/// </summary>
public sealed class AdamantiumAsyncCommand : IAsyncCommand
{
    private readonly Func<CancellationToken, Task> _execute;
    private readonly Func<bool> _canExecute;
    private CancellationTokenSource _cts;
    private bool _isRunning;

    public AdamantiumAsyncCommand(Func<CancellationToken, Task> execute, Func<bool> canExecute = null)
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

    public bool CanExecute(object parameter = null) => !_isRunning && (_canExecute == null || _canExecute());

    // Fire-and-forget for the UI click path; real exceptions surface on the UI thread (not swallowed).
    public async void Execute(object parameter = null) => await ExecuteAsync(parameter);

    public async Task ExecuteAsync(object parameter = null, CancellationToken cancellationToken = default)
    {
        if (!CanExecute()) return;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IsRunning = true;
        try
        {
            await _execute(_cts.Token);
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

    public event EventHandler CanExecuteChanged;

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    public event PropertyChangedEventHandler PropertyChanged;

    private void RaisePropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
