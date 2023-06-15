using System;
using System.Collections.Generic;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI;

public interface IObservableComponent: IUIComponent
{
    IObservableComponent ObservableParent { get; }
    public void AddHandler(RoutedEvent routedEvent, Delegate handler, bool handledEventsToo = false);

    void RemoveHandler(RoutedEvent routedEvent, Delegate handler);

    public void RaiseEvent(RoutedEventArgs e);

    IEnumerable<IObservableComponent> GetBubbleEventRoute();

    IEnumerable<IObservableComponent> GetTunnelEventRoute();
}