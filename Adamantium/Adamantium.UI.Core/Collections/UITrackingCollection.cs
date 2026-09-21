using System.Collections.Specialized;
using Adamantium.Core.Collections;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Collections;

public class UITrackingCollection<T> : TrackingCollection<T> where T : AdamantiumComponent
{
    protected override void OnSet(int index, T oldItem, T newItem)
    {
        base.OnSet(index, oldItem, newItem);
        oldItem.ComponentUpdated -= OnComponentUpdated;
        newItem.ComponentUpdated += OnComponentUpdated;
    }

    protected override void OnInsert(int index, T item)
    {
        base.OnInsert(index, item);

        item.ComponentUpdated += OnComponentUpdated;
    }

    // Unsubscribe while they are still HERE - after the clear there is nothing left to unsubscribe from.
    protected override void OnClearing(System.ArraySegment<T> items)
    {
        base.OnClearing(items);
        foreach (var component in items)
        {
            component.ComponentUpdated -= OnComponentUpdated;
        }
    }

    protected override void OnRemoveItem(int index, T item)
    {
        base.OnRemoveItem(index, item);
        item.ComponentUpdated -= OnComponentUpdated;
    }
    
    private void OnComponentUpdated(object sender, ComponentUpdatedEventArgs e)
    {
        NotifyCollectionChanged(NotifyCollectionChangedAction.Reset, null);
    }
}