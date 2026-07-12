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

    /// <summary>Queues <paramref name="action"/> to run on the UI LOOP thread (drained at the start of the next Update),
    /// running inline when already on it. Unlike <see cref="Invoke"/> - which the dispatcher executes on the OS
    /// message-pump thread - this marshals onto the layout/render loop thread (<see cref="UIThread"/>).</summary>
    void Post(Action action);
}