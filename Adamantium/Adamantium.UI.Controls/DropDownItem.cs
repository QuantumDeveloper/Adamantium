using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;

namespace Adamantium.UI.Controls;

/// <summary>
/// A selectable item container in a <see cref="DropDown"/>'s popup list. Reuses <see cref="ListBoxItem"/>'s chrome and
/// hover/pressed/selected state; the only difference is that pressing it selects it in the owning <see cref="DropDown"/>
/// and closes the popup (a dropdown picks exactly one value and dismisses).
/// </summary>
public class DropDownItem : ListBoxItem
{
    // The DropDown that hosts this row. Set by the owner when the container is created: the popup content lives in the
    // window's overlay layer, detached from the DropDown's VISUAL tree, so a visual-ancestor walk can't find the owner.
    internal DropDown Owner { get; set; }

    protected override void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // base sets IsPressed/Focus/Handled (its ListBox lookup is a harmless no-op with no ListBox ancestor here).
        base.OnMouseLeftButtonDown(sender, e);
        (Owner ?? FindOwnerLogically())?.SelectFromContainer(this);
    }

    // Fallback for an authored DropDownItem (no PrepareContainer): the popup child keeps a LOGICAL link to the DropDown
    // even though the visual tree is broken, so walk logical parents.
    private DropDown FindOwnerLogically()
    {
        for (IFundamentalUIComponent node = this; node != null; node = node.LogicalParent)
            if (node is DropDown dd) return dd;
        return null;
    }
}
