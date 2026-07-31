using System;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;

namespace Adamantium.UI.Controls.Panels;

/// <summary>
/// The grip between two neighbours in a <see cref="PaneHost"/>. Dragging it moves the boundary by fixing both
/// neighbours at the PIXELS they now occupy - the layout's own numbers - rather than any size of its own.
/// <para>Pixels because a drag is a statement about size: the user put that boundary exactly there. Written as a share
/// it would silently mean something else the moment the row gained or lost a pane. The pair keeps the same total, so
/// nobody else in the row moves.</para>
/// <para>That is the point of not building this on a Grid: a GridSplitter writes lengths into row/column definitions,
/// which then have to be mirrored back into the layout that gets saved. Here there is one number, and the drag edits
/// it in place - the area copies it back into the model after the pass, so a rebuild cannot undo the drag.</para>
/// </summary>
public class PaneSplitter : Thumb
{
    private double _originBefore;
    private double _originAfter;
    private double _extent;

    /// <summary>Which way this splitter resizes - set by the host from its own orientation. Setting it also picks the
    /// cursor: without the resize arrows there is nothing telling the user this thin strip can be dragged at all.</summary>
    public Orientation Orientation
    {
        get => _orientation;
        internal set
        {
            _orientation = value;
            Cursor = value == Orientation.Horizontal ? CursorType.SizeEWE : CursorType.SizeNS;
        }
    }

    private Orientation _orientation = Orientation.Horizontal;

    protected override void OnDragStarted(DragStartedEventArgs e)
    {
        base.OnDragStarted(e);

        if (VisualParent is not PaneHost host) return;

        var (before, after) = Neighbours();
        if (before == null || after == null) return;

        // Where the two neighbours START, in PIXELS - which is also what the drag will write. A pixel of mouse is a
        // pixel of boundary, with no basis to convert to and nothing to compound: Thumb reports a CUMULATIVE change, so
        // every delta is measured from here rather than added to whatever the last one produced.
        _originBefore = host.PixelsOf(before);
        _originAfter = host.PixelsOf(after);
    }

    protected override void OnDragDelta(DragEventArgs e)
    {
        base.OnDragDelta(e);

        var (before, after) = Neighbours();
        if (before == null || after == null) return;

        var moved = Orientation == Orientation.Horizontal ? e.Change.X : e.Change.Y;
        var total = _originBefore + _originAfter;

        // Neither side may be squeezed past what it says it needs - that MinSize is also what stops the tree from
        // being split into slivers, so the two rules are the same rule.
        var floor = VisualParent is PaneHost host ? Math.Max(0, host.MinFraction) * total : 0;
        var minBefore = Math.Max(floor, MinPixelsOf(before));
        var minAfter = Math.Max(floor, MinPixelsOf(after));
        moved = Math.Clamp(moved, minBefore - _originBefore, total - minAfter - _originBefore);

        var newBefore = _originBefore + moved;
        var newAfter = _originAfter - moved;

        var lengthBefore = PaneHost.GetPaneLength(before);
        var lengthAfter = PaneHost.GetPaneLength(after);

        // The boundary moves, and the pair keeps the same total, so everyone else in the row is untouched. What each of
        // them is STATED IN does not change, though: a share stays a share and a fixed size stays fixed.
        // Writing pixels into both - which is what this did - turned the document area's share into a number. The row's
        // lengths then no longer added up to the row (measured: 370px stated across 682px of host), the last pane
        // swallowed the difference, and the layout stopped growing with the window: after one drag the panels were
        // pinned to the sizes the window happened to have, and the grip could not move them any more.
        if (lengthBefore.IsStar && lengthAfter.IsStar)
        {
            // Two shares of one pool: divide their COMBINED weight the way the pixels now divide, so the pair is worth
            // what it was worth together.
            var weight = Weight(lengthBefore) + Weight(lengthAfter);
            PaneHost.SetPaneLength(before, PaneLength.Stars(weight * newBefore / total));
            PaneHost.SetPaneLength(after, PaneLength.Stars(weight * newAfter / total));
        }
        else if (lengthBefore.IsStar)
        {
            // A share takes whatever is left over, so moving this boundary is entirely the FIXED one's business.
            PaneHost.SetPaneLength(after, PaneLength.Pixels(newAfter));
        }
        else if (lengthAfter.IsStar)
        {
            PaneHost.SetPaneLength(before, PaneLength.Pixels(newBefore));
        }
        else
        {
            PaneHost.SetPaneLength(before, PaneLength.Pixels(newBefore));
            PaneHost.SetPaneLength(after, PaneLength.Pixels(newAfter));
        }

        if (PaneHost.LogLayout)
        {
            Console.WriteLine($"[PaneSplitter {Orientation}] moved={moved:F1} origin={_originBefore:F1}/{_originAfter:F1} " +
                              $"min={minBefore:F1}/{minAfter:F1} -> {newBefore:F1}/{newAfter:F1} " +
                              $"units={PaneHost.GetPaneLength(before)}/{PaneHost.GetPaneLength(after)}");
        }
    }

    /// <summary>A share's weight, treating an unstated one as a single share - the same reading the host uses.</summary>
    private static double Weight(PaneLength length) => length.Value > 0 ? length.Value : 1;

    /// <summary>The smallest this neighbour may become, in pixels. An explicit MinWidth/MinHeight wins; otherwise the
    /// neighbour is asked what it needs - and a docking area answers with its own policy, so a group cannot be squeezed
    /// under what its panes declared (nor, therefore, under its own tab strip). A nested host answers for its children,
    /// since squeezing it squeezes them.</summary>
    private double MinPixelsOf(IUIComponent neighbour)
    {
        if (neighbour is MeasurableUIComponent measurable)
        {
            var explicitMin = Orientation == Orientation.Horizontal ? measurable.MinWidth : measurable.MinHeight;
            if (!double.IsNaN(explicitMin) && explicitMin > 0) return explicitMin;
        }

        return neighbour is IPaneMinimum owner ? owner.MinimumExtent(Orientation) : 0;
    }

    /// <summary>The two content children this splitter sits between (other splitters are not content).</summary>
    private (IUIComponent Before, IUIComponent After) Neighbours()
    {
        if (VisualParent is not PaneHost host) return (null, null);

        IUIComponent before = null;
        var seenSelf = false;
        foreach (var child in host.Children)
        {
            if (ReferenceEquals(child, this))
            {
                seenSelf = true;
                continue;
            }

            if (child is PaneSplitter) continue;

            if (!seenSelf) before = child;
            else return (before, child);
        }

        return (before, null);
    }
}
