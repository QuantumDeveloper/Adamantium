namespace Adamantium.UI.Core.Dispatcher;

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