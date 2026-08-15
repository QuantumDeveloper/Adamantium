using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls;

/// <summary>
/// Parks a subtree and brings it back. What <c>x:KeepAlive</c> asks for and what an <c>x:Load</c> slot does between
/// conditions: the element leaves the tree but is NOT gone - it waits by reference, keeping what was built for it.
/// <para>The subtree is marked BEFORE its owner lets go of it, because detachment is what the renderer watches: an
/// unmarked detach means "thrown away" and its cached units are freed on the next build, which is exactly the cost
/// parking exists to avoid. Whoever parks it still does the removing - a panel removes a child, a ContentControl clears
/// its Content - this only states what that removal MEANS.</para>
/// </summary>
public static class ParkedSubtree
{
    /// <summary>Mark <paramref name="root"/> and everything under it as parked, and quiet what it drives. Call BEFORE
    /// removing it from its parent.</summary>
    public static void Park(IUIComponent root)
    {
        Mark(root, true);
        (root as UIComponent)?.SuspendForPark();
    }

    /// <summary>The reverse, called AFTER the subtree is back in the tree: clear the mark and measure it once - it was out
    /// of the layout while parked, and whatever changed in the meantime has to reach it.
    /// <para>Nothing is marked render-dirty here on purpose. Re-attaching already invalidates the subtree's render
    /// (UIComponent.AttachedToVisualTree), and doing it again per node told the renderer to re-record every one of them -
    /// throwing away the units parking had just kept, which is the opposite of the point.</para></summary>
    public static void Unpark(IUIComponent root, bool remeasure = true)
    {
        Mark(root, false);

        // Only when the container it comes back into is a different size than the one it left. Measuring a page of a
        // thousand realized rows is the whole remaining cost of a return, and repeating it to arrive at the layout the
        // view already has buys nothing - it kept that layout, which is why it was parked whole.
        if (remeasure) (root as IMeasurableComponent)?.InvalidateMeasure();
    }

    /// <summary>Drops the parked mark BEFORE the subtree is attached, so the attach takes its ordinary path: everything a
    /// node revalidates on the way in is done, because the world it comes back to is not the one it left.</summary>
    public static void Revalidate(IUIComponent root) => Mark(root, false);

    private static void Mark(IUIComponent node, bool parked)
    {
        if (node is not UIComponent component) return;

        component.IsParked = parked;
        foreach (var child in component.VisualChildren)
        {
            Mark(child, parked);
        }
    }
}
