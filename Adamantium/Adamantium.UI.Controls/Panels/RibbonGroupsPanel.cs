using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;

namespace Adamantium.UI.Controls.Panels;

/// <summary>The items host of a <see cref="RibbonTab"/>: its groups in a row, each at its OWN width and the full band
/// height. NOT a <see cref="StackPanel"/> - as an items host that virtualizes, giving every group one uniform width
/// probed from a single one.</summary>
public class RibbonGroupsPanel : Panel
{
    /// <summary>Groups sit in a ROW: only Left/Right cross from one to the next, and Up/Down must not be turned into
    /// "the next group" by the generic order-based walk. Reached only once <see cref="RibbonGroupPanel"/> runs out.</summary>
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

            // Unbounded: a group asks for the room it wants. What it GETS is the arrange slot, and a smaller variant is
            // chosen from that (docs/RIBBON_PLAN.md §3.3), never from here.
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            width += child.DesiredSize.Width;
            height = Math.Max(height, child.DesiredSize.Height);
        }

        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double x = 0;
        foreach (var child in Children)
        {
            if (child.Visibility != Visibility.Visible) continue;

            var width = child.DesiredSize.Width;
            // Full band height, so every group's caption sits on the same line whatever its commands stack up to.
            child.Arrange(new Rect(x, 0, width, finalSize.Height));
            x += width;
        }

        return new Size(x, finalSize.Height);
    }
}
