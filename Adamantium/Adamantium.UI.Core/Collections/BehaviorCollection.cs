using Adamantium.Core.Collections;
using Adamantium.UI.Core.Behaviors;

namespace Adamantium.UI.Core.Collections;

public class BehaviorCollection : TrackingCollection<Behavior>
{
    private IAdamantiumComponent _adamantiumComponent;
    public BehaviorCollection(IAdamantiumComponent adamantiumComponent)
    {
        _adamantiumComponent = adamantiumComponent;
    }
    protected override void OnInsert(int index, Behavior item)
    {
        item.AttachTo(_adamantiumComponent);
        base.OnInsert(index, item);
    }

    protected override void OnRemoveItem(int index, Behavior item)
    {
        item.DetachFrom(_adamantiumComponent);
        base.OnRemoveItem(index, item);
    }

    protected override void OnSet(int index, Behavior oldItem, Behavior newItem)
    {
        oldItem?.DetachFrom(_adamantiumComponent);
        newItem?.AttachTo(_adamantiumComponent);
        base.OnSet(index, oldItem, newItem);
    }

    // Detach them while they are still HERE - after the clear they are gone and nothing can be detached from anything.
    protected override void OnClearing(System.ArraySegment<Behavior> items)
    {
        foreach (var behavior in items)
        {
            behavior.DetachFrom(_adamantiumComponent);
        }
        base.OnClearing(items);
    }
}