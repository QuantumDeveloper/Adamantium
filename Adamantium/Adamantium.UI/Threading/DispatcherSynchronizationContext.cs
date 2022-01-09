using System.Threading;

namespace Adamantium.UI.Threading;

public class DispatcherSynchronizationContext : SynchronizationContext
{
    private readonly Dispatcher dispatcher;
        
    public DispatcherSynchronizationContext(Dispatcher dispatcher)
    {
        this.dispatcher = dispatcher;
        SetWaitNotificationRequired();
    }
        
    public override void Send(SendOrPostCallback d, object state)
    {
        dispatcher.Invoke(d, state);
    }

    public override void Post(SendOrPostCallback d, object state)
    {
        dispatcher.InvokeAsync(d, state);
    }
}