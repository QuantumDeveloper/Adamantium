using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.Base;

public class ObservableUIComponent : UIComponent, IObservableComponent
{
    private readonly Dictionary<RoutedEvent, List<EventSubscription>> eventHandlers =
        new Dictionary<RoutedEvent, List<EventSubscription>>();

    // How many handlers this element carries, across all events. Maintained by Add/RemoveHandler under the same lock the
    // dictionary is written with, but READ without one - which is the whole point: nearly every element in a big scene has
    // none at all, and answering "is anyone listening" must not cost a lock per element per raise.
    private volatile int handlerCount;

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
            handlerCount++;
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

        // Nothing on the route can hear this: the walk would take a lock and ask a dictionary at every level, and change
        // nothing observable at any of them. Source/OriginalSource are assigned first so an args object the caller keeps
        // still reads the same as it always did.
        e.Source ??= this;
        e.OriginalSource ??= this;

        if (!WouldBeHeard(e.RoutedEvent)) return;

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
            if (eventHandlers.TryGetValue(routedEvent, out var list))
            {
                handlerCount -= list.RemoveAll(x => x.Handler == handler);
            }
        }
    }

    /// <summary>Would raising <paramref name="routedEvent"/> from here reach ANYONE - a class handler, a handler on this
    /// element, or one on an ancestor it would bubble through?
    /// <para>Allocation-free and lock-free in the common case: a per-element handler count answers most levels with a
    /// field read. The caller uses it to decide whether to build the event args at all - and at 4K a resize storm raising
    /// SizeChanged that nothing listens to was a third of the drag, entirely in args nobody read.</para></summary>
    protected bool WouldBeHeard(RoutedEvent routedEvent)
    {
        if (routedEvent == null) return false;
        if (routedEvent.HasClassHandlers) return true;   // a class handler hears it wherever it is raised

        if (routedEvent.RoutingStrategy == RoutingStrategy.Direct) return HasOwnHandler(routedEvent);

        // Bubble and Tunnel travel the SAME set of elements, in opposite directions - so for "is anyone on it", one walk
        // answers both.
        for (var element = (IObservableComponent)this; element != null; element = element.ObservableParent)
        {
            if (element is ObservableUIComponent component && component.HasOwnHandler(routedEvent)) return true;
        }

        return false;
    }

    private bool HasOwnHandler(RoutedEvent routedEvent)
    {
        if (handlerCount == 0) return false;

        lock (eventHandlers)
        {
            return eventHandlers.TryGetValue(routedEvent, out var list) && list.Count > 0;
        }
    }
}