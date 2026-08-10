using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.RoutedEvents;

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

    /// <summary>The row the ARROWS are on while the list is open - where Enter would commit. Distinct from
    /// <see cref="ListBoxItem.IsSelected"/> on purpose: selected is the value the control HAS, highlighted is the value
    /// it WOULD take. Walking the list with the arrows must not change the former, or every step raises
    /// SelectionChanged and fires whatever is bound to it - and Escape cannot take those back.</summary>
    public static readonly AdamantiumProperty IsHighlightedProperty = AdamantiumProperty.Register(
        nameof(IsHighlighted), typeof(bool), typeof(DropDownItem),
        new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    public bool IsHighlighted
    {
        get => GetValue<bool>(IsHighlightedProperty);
        set => SetValue(IsHighlightedProperty, value);
    }

    protected override void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // base sets IsPressed/Focus/Handled (its ListBox lookup is a harmless no-op with no ListBox ancestor here).
        base.OnMouseLeftButtonDown(sender, e);
        (Owner ?? FindOwnerLogically())?.SelectFromContainer(this);
    }

    /// <summary>Enter or Space on a row that somehow holds the focus (a click focuses it) picks it - the same thing the
    /// press does. The keyboard's normal path never comes through here: the drop-down keeps the focus on its header and
    /// answers the arrows itself, because a key pressed inside the popup would never reach the control that owns it.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Key is not (Key.Enter or Key.Space))
            return;

        (Owner ?? FindOwnerLogically())?.SelectFromContainer(this);
        e.Handled = true;
    }

    // Fallback for an authored DropDownItem (no PrepareContainer): the popup child keeps a LOGICAL link to the DropDown
    // even though the visual tree is broken. The SHARED walk, which bridges template boundaries by TemplatedParent - a
    // raw LogicalParent loop dead-ends at the first template part between the row and its list.
    private DropDown FindOwnerLogically() => this.GetSelfAndLogicalAncestors().OfType<DropDown>().FirstOrDefault();
}
