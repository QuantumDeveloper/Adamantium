using System.Linq;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>The quick-access bar: DOCUMENT commands kept within one click, shown in the caption through
/// <see cref="TitleBar.LeadingContent"/>. Its own control and its own list - the user reorders these, while the
/// window's commands belong to the application, and the reorder gesture must not reach across. It holds no reference
/// to a <see cref="Ribbon"/>: the two meet at a collection in the shell's view model. See docs/RIBBON_PLAN.md §7.1.</summary>
public class RibbonQuickAccess : ItemsControl
{
    /// <summary>Where the bar is shown. The application hosts an instance in BOTH slots, bound to the one collection,
    /// and each shows itself only while this names the slot it is standing in.</summary>
    public static readonly AdamantiumProperty PlacementProperty = AdamantiumProperty.Register(nameof(Placement),
        typeof(RibbonQuickAccessPlacement), typeof(RibbonQuickAccess),
        new PropertyMetadata(RibbonQuickAccessPlacement.Caption, PropertyMetadataOptions.AffectsMeasure, OnPlacementChanged));

    public RibbonQuickAccessPlacement Placement
    {
        get => GetValue<RibbonQuickAccessPlacement>(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    private static void OnPlacementChanged(AdamantiumComponent sender, AdamantiumPropertyChangedEventArgs e)
    {
        (sender as RibbonQuickAccess)?.ShowOnlyInItsOwnSlot();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ShowOnlyInItsOwnSlot();
    }

    // Which slot this instance stands in is a fact about the tree, not something the application should have to repeat:
    // a caption is a TitleBar, anything else is the ribbon's own row.
    private void ShowOnlyInItsOwnSlot()
    {
        var slot = this.GetVisualAncestors().OfType<TitleBar>().Any()
            ? RibbonQuickAccessPlacement.Caption
            : RibbonQuickAccessPlacement.BelowRibbon;

        // Not a plain write: a Local value would mask an author's own binding on Visibility.
        SetCurrentValue(VisibilityProperty, slot == Placement ? Visibility.Visible : Visibility.Collapsed);
    }
}
