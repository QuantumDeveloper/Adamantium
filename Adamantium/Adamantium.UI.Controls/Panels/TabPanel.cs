using System;
using Adamantium.Mathematics;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Panels;

/// <summary>
/// The tab strip's items panel: a NON-virtualizing stack that sizes each tab to its OWN content. The virtualizing
/// <see cref="StackPanel"/> gives every item one uniform extent (probed from a single item) - correct for a long
/// scrolled list, wrong for tab headers of differing width (they'd all take the probe tab's width, and a reorder that
/// moved a different tab into the probe slot resized them all). Tabs are few, so realizing them all up front is fine;
/// being non-virtualizing also keeps every container alive across a drag-reorder, which the reorder animation relies on.
/// Orientation-aware: Horizontal for a top/bottom strip, Vertical for a left/right strip.
/// </summary>
public class TabPanel : Panel
{
    public static readonly AdamantiumProperty OrientationProperty = AdamantiumProperty.Register(nameof(Orientation),
        typeof(Orientation), typeof(TabPanel),
        new PropertyMetadata(Orientation.Horizontal,
            PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));

    public Orientation Orientation
    {
        get => GetValue<Orientation>(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var horizontal = Orientation == Orientation.Horizontal;
        // Unbounded main axis so each tab takes exactly its content extent; the cross axis is the strip's.
        var childConstraint = horizontal
            ? new Size(double.PositiveInfinity, availableSize.Height)
            : new Size(availableSize.Width, double.PositiveInfinity);

        double main = 0, cross = 0;
        foreach (var child in Children)
        {
            child.Measure(childConstraint);
            var size = child.DesiredSize;
            if (horizontal) { main += size.Width; cross = Math.Max(cross, size.Height); }
            else { main += size.Height; cross = Math.Max(cross, size.Width); }
        }

        return horizontal ? new Size(main, cross) : new Size(cross, main);
    }

    /// <summary>Whether a LONE tab fills the strip - the owning control's <see cref="TabControl.StretchSingleTab"/>. Read
    /// from the owner rather than set on this panel: the panel is built from an ItemsPanelTemplate, so the theme cannot
    /// address this instance, and writing it from the control's code would outrank the theme and never give it back.</summary>
    private bool StretchesTheOnlyTab =>
        Children.Count == 1 && (VisualParent as ItemsPresenter)?.Owner is TabControl { StretchSingleTab: true };

    protected override Size ArrangeOverride(Size finalSize)
    {
        var horizontal = Orientation == Orientation.Horizontal;
        // Cross = the content extent clamped to the slot (honest bounds - a strip only occupies what it stacks).
        var cross = horizontal
            ? Math.Min(finalSize.Height, DesiredSize.Height)
            : Math.Min(finalSize.Width, DesiredSize.Width);

        // ONE tab told to fill takes the whole strip. Only in ARRANGE: the strip is measured with an unbounded axis (see
        // TabStripScroller) so there is no length to fill until the slot is handed down.
        if (StretchesTheOnlyTab)
        {
            var only = Children[0];
            only.Arrange(horizontal ? new Rect(0, 0, finalSize.Width, cross) : new Rect(0, 0, cross, finalSize.Height));
            return horizontal ? new Size(finalSize.Width, cross) : new Size(cross, finalSize.Height);
        }

        double main = 0;
        foreach (var child in Children)
        {
            var size = child.DesiredSize;
            if (horizontal)
            {
                child.Arrange(new Rect(main, 0, size.Width, cross));
                main += size.Width;
            }
            else
            {
                child.Arrange(new Rect(0, main, cross, size.Height));
                main += size.Height;
            }
        }

        return horizontal ? new Size(main, cross) : new Size(cross, main);
    }
}
