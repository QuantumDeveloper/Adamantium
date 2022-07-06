using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI;

public class ObservableUIComponent : UIComponent, IObservableComponent
{
    private readonly Dictionary<RoutedEvent, List<EventSubscription>> eventHandlers =
        new Dictionary<RoutedEvent, List<EventSubscription>>();

    public IObservableComponent ObservableParent => ((IUIComponent)this).VisualParent as IObservableComponent;

    public void AddHandler(RoutedEvent routedEvent, Delegate handler, bool handledEventsToo = false)
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
                HandledEeventToo = handledEventsToo,
            };
            subscriptions.Add(sub);
        }

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
                var handlersList = eventHandlers[e.RoutedEvent];
                foreach (var handler in handlersList)
                {
                    if (!e.Handled || handler.HandledEeventToo)
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