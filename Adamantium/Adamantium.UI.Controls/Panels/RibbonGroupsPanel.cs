using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;

namespace Adamantium.UI.Controls.Panels;

/// <summary>The items host of a <see cref="RibbonTab"/>: groups in a row, each at its own width. Not a StackPanel -
/// that virtualizes, and would give every group one probed width.</summary>
public class RibbonGroupsPanel : Panel
{
    /// <summary>A ROW: Up/Down must not become "the next group" through the generic order-based walk.</summary>
    public override IUIComponent Navigate(IUIComponent from, FocusNavigationDirection direction)
    {
        if (IsArrow(direction) && IsVertical(direction)) return null;

        return base.Navigate(from, direction);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = 0, height = 0;
        foreach (var child in Children)
        {
            if (child.Visibility != Visibility.Visible) continue;

            // Unbounded: what a group GETS is the arrange slot, and the variant is chosen from that.
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            width += child.DesiredSize.Width;
            height = Math.Max(height, child.DesiredSize.Height);
        }

        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // From the SLOT, never the measure constraint - a parent may measure unbounded just to learn our natural size.
        ChooseVariants(finalSize.Width);

        double x = 0;
        foreach (var child in Children)
        {
            if (child.Visibility != Visibility.Visible) continue;

            // The chosen variant may have just changed the sizes underneath it.
            child.Measure(new Size(double.PositiveInfinity, finalSize.Height));

            var width = WidthOf(child);
            // Full band height, so every caption sits on one line.
            child.Arrange(new Rect(x, 0, width, finalSize.Height));
            x += width;
        }

        return new Size(x, finalSize.Height);
    }

    // --- Choosing which variant each group is drawn at ---------------------------------------------------------------

    /// <summary>Growing back gets a stricter test than shrinking, so a boundary width settles instead of flipping.
    /// Untested insurance so far: the tests pass with it at zero.</summary>
    private const double GrowBackMargin = 16;

    // The width the current choice was made for.
    private double _decidedFor;

    private void ChooseVariants(double available)
    {
        var groups = new List<RibbonGroup>();
        foreach (var child in Children)
        {
            if (child is RibbonGroup group && group.Visibility == Visibility.Visible) groups.Add(group);
        }

        if (groups.Count == 0) return;

        // Sizes everywhere first; only then is a group given up wholesale.
        while (Total(groups) > available && Lower(groups, collapsing: false)) { }
        while (Total(groups) > available && Lower(groups, collapsing: true)) { }

        // Only when the tab got WIDER: collapsing frees a lot at once, and re-solving would spend it on the neighbours -
        // narrowing by a pixel made the groups on the left spring back to full size.
        if (available > _decidedFor)
        {
            while (Raise(groups, available - GrowBackMargin)) { }
        }

        _decidedFor = available;
    }

    private static double Total(List<RibbonGroup> groups)
    {
        double total = 0;
        foreach (var group in groups) total += WidthOf(group);
        return total;
    }

    // Lowest ShrinkPriority, and among equals the one furthest RIGHT. The same order serves collapsing.
    private static bool Lower(List<RibbonGroup> groups, bool collapsing)
    {
        RibbonGroup pick = null;
        for (var i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            if (!CanLower(group, collapsing)) continue;
            if (pick == null || group.ShrinkPriority <= pick.ShrinkPriority) pick = group;
        }

        if (pick == null) return false;

        pick.ApplyVariant(collapsing ? pick.Variants.Count - 1 : pick.CurrentVariant + 1);
        return true;
    }

    // The mirror: sacrificed last, restored first, and only if the result clears the margin.
    private static bool Raise(List<RibbonGroup> groups, double limit)
    {
        RibbonGroup pick = null;
        for (var i = groups.Count - 1; i >= 0; i--)
        {
            var group = groups[i];
            if (group.CurrentVariant <= 0) continue;
            if (pick == null || group.ShrinkPriority >= pick.ShrinkPriority) pick = group;
        }

        if (pick == null) return false;

        var drawn = pick.CurrentVariant;
        pick.ApplyVariant(drawn - 1);
        if (Total(groups) <= limit) return true;

        pick.ApplyVariant(drawn);
        return false;
    }

    // Size steps stop one short of the collapsed variant. Collapsing is refused when it would make the group WIDER - a
    // group down to three icons is narrower than the button replacing it.
    private static bool CanLower(RibbonGroup group, bool collapsing)
    {
        var last = group.Variants.Count - 1;
        if (!collapsing) return group.CurrentVariant < last - 1;

        return group.CurrentVariant < last && group.WidthAt(last) < group.WidthAt(group.CurrentVariant);
    }

    private static double WidthOf(IMeasurableComponent child) =>
        child is RibbonGroup group && !double.IsNaN(group.WidthAt(group.CurrentVariant))
            ? group.WidthAt(group.CurrentVariant)
            : child.DesiredSize.Width;
}
