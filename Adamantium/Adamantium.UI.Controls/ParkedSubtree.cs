using System.Collections.Generic;
using System.Runtime.InteropServices;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls;

/// <summary>
/// Parks a subtree and brings it back: it leaves the tree but waits by reference, keeping what was built for it. Marked
/// before its owner removes it, or the renderer reads the detach as "thrown away" and frees its units.
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

    /// <summary>The reverse, after the subtree is back in the tree: clears the mark and remeasures. Nothing is marked
    /// render-dirty: that would re-record the units parking kept.</summary>
    public static void Unpark(IUIComponent root, bool remeasure = true)
    {
        Mark(root, false);

        if (remeasure) (root as IMeasurableComponent)?.InvalidateMeasure();
    }

    /// <summary>Drops the parked mark BEFORE the subtree is attached, so the attach takes its ordinary path: everything a
    /// node revalidates on the way in is done, because the world it comes back to is not the one it left.</summary>
    public static void Revalidate(IUIComponent root) => Mark(root, false);

    /// <summary>Ends the park for good: nothing will come back for <paramref name="root"/>, so it is discarded like any
    /// destroyed subtree. A parked element refuses a discard on its own, so the mark is dropped first.</summary>
    public static void Discard(IUIComponent root)
    {
        Mark(root, false);

        var gone = new List<IFundamentalUIComponent>();
        Collect(root, gone);
        DiscardedVisuals.Publish(CollectionsMarshal.AsSpan(gone));
    }

    private static void Collect(IUIComponent node, List<IFundamentalUIComponent> gone)
    {
        if (node is IFundamentalUIComponent fundamental)
        {
            gone.Add(fundamental);
        }

        foreach (var child in node.VisualChildren)
        {
            Collect(child, gone);
        }
    }

    private static void Mark(IUIComponent node, bool parked)
    {
        if (node is not UIComponent component) return;

        if (parked) component.MarkParked(); else component.Revive();
        foreach (var child in component.VisualChildren)
        {
            Mark(child, parked);
        }
    }
}
