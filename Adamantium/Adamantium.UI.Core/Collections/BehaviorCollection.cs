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

    protected override void OnClear(Behavior[] items)
    {
        foreach (var item in items)
        {
            item.DetachFrom(_adamantiumComponent);
        }
        base.OnClear(items);
    }
}