using System;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;

namespace Adamantium.UI.Controls.Panels;

/// <summary>
/// The grip between two neighbours in a <see cref="PaneHost"/>. Dragging it moves the boundary by rewriting the two
/// neighbours' SHARES - the model's own numbers - rather than any size of its own.
/// <para>That is the point of not building this on a Grid: a GridSplitter writes lengths into row/column definitions,
/// which then have to be mirrored back into the layout that gets saved. Here there is one number, and the drag edits
/// it in place.</para>
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

        // Settle the whole host onto ONE kind of number before touching anything: authored pixel hints become the shares
        // that currently reproduce them. Nothing moves, and from here a pixel of mouse is a pixel of boundary.
        host.FreezeShares();

        var (before, after) = Neighbours();
        if (before == null || after == null) return;

        // Remember where the shares STARTED. Thumb reports a CUMULATIVE change, so every delta must be measured from
        // here - adding it to the current share instead compounds it and runs the splitter ahead of the pointer.
        _originBefore = PaneHost.GetFraction(before);
        _originAfter = PaneHost.GetFraction(after);

        // The pixels a whole share is worth, so a pixel delta can be turned into a share delta.
        _extent = host.ContentExtent;
    }

    protected override void OnDragDelta(DragEventArgs e)
    {
        base.OnDragDelta(e);

        var (before, after) = Neighbours();
        if (before == null || after == null || _extent <= 0) return;

        var moved = Orientation == Orientation.Horizontal ? e.Change.X : e.Change.Y;
        var shift = moved / _extent;

        // Neither side may be squeezed past what it says it needs - that MinSize is also what stops the tree from
        // being split into slivers, so the two rules are the same rule.
        var floor = VisualParent is PaneHost host ? host.MinFraction : 0;
        var minBefore = Math.Max(floor, MinShare(before));
        var minAfter = Math.Max(floor, MinShare(after));
        var total = _originBefore + _originAfter;
        shift = Math.Clamp(shift, minBefore - _originBefore, total - minAfter - _originBefore);

        // Writing the shares is all this does - the host re-lays itself out when they change (PaneHost.FractionChanged),
        // so a drag and a programmatic change take the same road.
        PaneHost.SetFraction(before, _originBefore + shift);
        PaneHost.SetFraction(after, _originAfter - shift);

        if (PaneHost.LogLayout)
        {
            Console.WriteLine($"[PaneSplitter {Orientation}] moved={moved:F1} extent={_extent:F1} shift={shift:F3} " +
                              $"origin={_originBefore:F3}/{_originAfter:F3} min={minBefore:F3}/{minAfter:F3} " +
                              $"-> {_originBefore + shift:F3}/{_originAfter - shift:F3}");
        }
    }

    /// <summary>What the neighbour's minimum is worth as a share of the host's extent.</summary>
    private double MinShare(IUIComponent neighbour)
    {
        if (_extent <= 0) return 0;

        var min = MinPixelsOf(neighbour);
        if (min <= 0) return 0;
        return Math.Min(min / _extent, _originBefore + _originAfter);
    }

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
