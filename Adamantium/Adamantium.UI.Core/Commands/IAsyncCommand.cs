using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Adamantium.UI.Core.Commands;

/// <summary>
/// An async-first command. <see cref="ExecuteAsync"/> can be awaited; <see cref="IsRunning"/> (with change
/// notification, so it's bindable to a busy indicator) is true while it runs and gates re-entrancy + auto-disables
/// a bound control; <see cref="Cancel"/> cancels the in-flight run. This is the evolution over the classic void
/// <c>Execute</c> (which forced async-void and lost completion/state/cancellation).
/// </summary>
public interface IAsyncCommand : ICommand, INotifyPropertyChanged
{
    Task ExecuteAsync(object parameter = null, CancellationToken cancellationToken = default);

    bool IsRunning { get; }

    void Cancel();
}
