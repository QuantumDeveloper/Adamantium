using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.Base;

public class ObservableUIComponent : UIComponent, IObservableComponent
{
    private readonly Dictionary<RoutedEvent, List<EventSubscription>> eventHandlers =
        new Dictionary<RoutedEvent, List<EventSubscription>>();

    public IObservableComponent ObservableParent => ((IUIComponent)this).VisualParent as IObservableComponent;

    public void AddHandler(RoutedEvent routedEvent, Delegate handler, bool handledEventsToo = false)
    {
        ArgumentNullException.ThrowIfNull(routedEvent);

        ArgumentNullException.ThrowIfNull(handler);

        lock (eventHandlers)
        {
            List<EventSubscription> subscriptions = null;
            if (!eventHandlers.ContainsKey(routedEvent))
            {
                subscriptions = new List<EventSubscription>();
                eventHandlers.Add(routedEvent, subscriptions);
            }
            else
            {
                subscriptions = eventHandlers[routedEvent];
            }
            var sub = new EventSubscription
            {
                Handler = handler,
                HandledEventsToo = handledEventsToo,
            };
            subscriptions.Add(sub);
        }
    }

    public void AddHandler(RoutedEvent routedEvent, RoutedEventHandler handler, bool handledEventsToo = false)
    {
        AddHandler(routedEvent, (Delegate)handler, handledEventsToo);
    }

    public void RaiseEvent(RoutedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(e.RoutedEvent);

        e.Source ??= this;
        e.OriginalSource ??= this;

        if (e.RoutedEvent != null)
        {
            if (e.RoutedEvent.RoutingStrategy == RoutingStrategy.Direct)
            {
                RaiseDirectEvent(e);
            }
            else if (e.RoutedEvent.RoutingStrategy == RoutingStrategy.Bubble)
            {
                RaiseBubbleEvent(e);
            }

            else if (e.RoutedEvent.RoutingStrategy == RoutingStrategy.Tunnel)
            {
                RaiseTunnelEvent(e);
            }
        }
    }

    private void RaiseDirectEvent(RoutedEventArgs e)
    {
        if (e == null)
        {
            throw new ArgumentNullException(nameof(e));
        }

        e.RoutedEvent.InvokeClassHandlers(this, e);

        lock (eventHandlers)
        {
            if (eventHandlers.ContainsKey(e.RoutedEvent))
            {
                // Snapshot: a handler can add/remove handlers for this same event while it runs (e.g. an Unloaded
                // handler that unsubscribes itself during a template teardown) - iterating the live list would throw
                // "Collection was modified". The lock is reentrant, so the mutation itself is safe.
                var handlersList = eventHandlers[e.RoutedEvent];
                foreach (var handler in handlersList.ToArray())
                {
                    if (!e.Handled || handler.HandledEventsToo)
                    {
                        handler.Handler.DynamicInvoke(this, e);
                    }
                }
            }
        }
    }

    private void RaiseBubbleEvent(RoutedEventArgs e)
    {
        if (e == null)
        {
            throw new ArgumentNullException(nameof(e));
        }

        foreach (var uiComponent in GetBubbleEventRoute())
        {
            var element = (ObservableUIComponent)uiComponent;
            e.Source = element;
            element.RaiseDirectEvent(e);
        }
    }

    private void RaiseTunnelEvent(RoutedEventArgs e)
    {
        if (e == null)
        {
            throw new ArgumentNullException(nameof(e));
        }

        foreach (var uiComponent in GetTunnelEventRoute())
        {
            var element = (ObservableUIComponent)uiComponent;
            e.Source = element;
            element.RaiseDirectEvent(e);
        }
    }

    public IEnumerable<IObservableComponent> GetBubbleEventRoute()
    {
        var element = (IObservableComponent)this;
        while (element != null)
        {
            yield return element;
            element = element.ObservableParent;
        }
    }

    public IEnumerable<IObservableComponent> GetTunnelEventRoute()
    {
        return GetBubbleEventRoute().Reverse();
    }

    public void RemoveHandler(RoutedEvent routedEvent, Delegate handler)
    {
        if (routedEvent == null)
        {
            throw new ArgumentNullException(nameof(routedEvent));
        }

        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        lock (eventHandlers)
        {
            if (eventHandlers.ContainsKey(routedEvent))
            {
                var list = eventHandlers[routedEvent];
                list.RemoveAll(x => x.Handler == handler);
            }
        }
    }
}