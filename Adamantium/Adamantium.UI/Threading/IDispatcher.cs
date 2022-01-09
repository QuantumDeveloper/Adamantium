using System;
using System.Threading;
using System.Threading.Tasks;

namespace Adamantium.UI.Threading;

public interface IDispatcher
{
    bool IsRunning { get; }
    void Run(CancellationToken token);
    bool CheckAccess();
    void VerifyAccess();
    Thread MainThread { get; }
    Thread UIThread { get; set; }

    void Invoke(Action action, DispatcherPriority priority = DispatcherPriority.Normal);
    void Invoke(Delegate action, object args);
    Task InvokeAsync(Action action, DispatcherPriority priority = DispatcherPriority.Normal);
    Task InvokeAsync(Delegate action, object args);
}