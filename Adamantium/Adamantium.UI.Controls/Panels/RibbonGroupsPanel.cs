using System;
using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;

namespace Adamantium.UI.Controls.Panels;

/// <summary>The items host of a <see cref="RibbonTab"/>: groups in a row, each at its own width. Not a StackPanel -
/// that virtualizes, and would give every group one probed width.</summary>
public class RibbonGroupsPanel : Panel
{
    public RibbonGroupsPanel()
    {
        GotKeyboardFocus += OnFocusEntered;
    }

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
        _slots.Clear();
        foreach (var child in Children)
        {
            if (child.Visibility != Visibility.Visible) continue;

            // The chosen variant may have just changed the sizes underneath it.
            child.Measure(new Size(double.PositiveInfinity, finalSize.Height));

            var width = WidthOf(child);
            _slots.Add((child, x, width));
            // Full band height, so every caption sits on one line. Shifted by the scroll, which is the LAST resort -
            // everything above has already been shrunk and collapsed (see docs/RIBBON_PLAN.md §3.3-3.4).
            child.Arrange(new Rect(x - Offset, 0, width, finalSize.Height));
            x += width;
        }

        _extent = x;
        _viewport = finalSize.Width;
        Clamp();
        return new Size(finalSize.Width, finalSize.Height);
    }

    // --- Scrolling the row (§3.4) ------------------------------------------------------------------------------------
    //
    // Not a ScrollViewer: a bar under the band would eat height from a strip that has none to spare, and it is chrome
    // nobody asked for. The row moves by WHOLE GROUPS behind two repeat buttons that overlay its edges - a half-shown
    // group reads as damage rather than as "there is more".

    private readonly List<(IMeasurableComponent Child, double Start, double Width)> _slots = [];   // unscrolled
    private double _extent;
    private double _viewport;

    /// <summary>How far the row is scrolled. Not a property with a callback: the panel is the only thing that moves it,
    /// and every move already re-arranges.</summary>
    public double Offset { get; private set; }

    public bool CanScrollBack => Offset > 0.5;

    public bool CanScrollForward => _extent - Offset > _viewport + 0.5;

    /// <summary>Told after every arrange, so the tab can show or hide its arrows and edge fades. The tab owns the
    /// chrome because the panel is inside its items presenter, where a template cannot reach it.</summary>
    public event EventHandler ScrollStateChanged;

    /// <summary>One GROUP back. Office steps by group, not by pixels: what a scroll ends on has to be a whole command
    /// set, or the row reads as clipped.</summary>
    public void ScrollBack() => ScrollTo(PreviousEdge());

    public void ScrollForward() => ScrollTo(NextEdge());

    private double PreviousEdge()
    {
        var target = 0.0;
        foreach (var slot in _slots)
        {
            if (slot.Start >= Offset - 0.5) break;
            target = slot.Start;
        }

        return target;
    }

    private double NextEdge()
    {
        foreach (var slot in _slots)
        {
            if (slot.Start > Offset + 0.5) return slot.Start;
        }

        return Offset;
    }

    /// <summary>Tab and the arrows may land on a group that is scrolled off - focus you cannot see is worse than no
    /// focus at all, so the row follows the keyboard.</summary>
    private void OnFocusEntered(object sender, KeyboardGotFocusEventArgs e)
    {
        var slot = SlotContaining(e.NewFocus as IUIComponent);
        if (slot == null) return;

        var (_, start, width) = slot.Value;
        if (start < Offset)
        {
            ScrollTo(start);
        }
        else if (start + width > Offset + _viewport)
        {
            // Its far edge, unless the group is wider than the row - then its near edge, so it starts where it can be read.
            ScrollTo(Math.Min(start, start + width - _viewport));
        }
    }

    private (IMeasurableComponent Child, double Start, double Width)? SlotContaining(IUIComponent focused)
    {
        while (focused != null)
        {
            foreach (var slot in _slots)
            {
                if (ReferenceEquals(slot.Child, focused)) return slot;
            }

            focused = focused.VisualParent as IUIComponent;
        }

        return null;
    }

    private void ScrollTo(double offset)
    {
        if (Math.Abs(offset - Offset) < 0.5) return;

        Offset = offset;
        Clamp();
        InvalidateArrange();
    }

    // A row that shrank (a wider window, a collapsed group) must not leave the view hanging past its end.
    private void Clamp()
    {
        var max = Math.Max(0, _extent - _viewport);
        var clamped = Math.Min(Math.Max(0, Offset), max);
        var changed = Math.Abs(clamped - Offset) > 0.5;
        Offset = clamped;

        ScrollStateChanged?.Invoke(this, EventArgs.Empty);
        if (changed) InvalidateArrange();
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
            if (child is RibbonGroup group && group.Visibility == Visibility.Visible)
            {
                groups.Add(group);
            }
        }

        if (groups.Count == 0) return;

        LowerUntilItFits(groups, available);

        // Only when the tab got WIDER: collapsing frees a lot at once, and re-solving would spend it on the neighbours -
        // narrowing by a pixel made the groups on the left spring back to full size.
        if (available > _decidedFor)
        {
            RaiseWhileItStillFits(groups, available - GrowBackMargin);
        }

        _decidedFor = available;
    }

    private static double Total(List<RibbonGroup> groups)
    {
        double total = 0;
        foreach (var group in groups) total += WidthOf(group);
        return total;
    }

    // ONE group at a time, each to the end of its own ladder - collapsed is simply its last step, not a separate pass.
    // A group with room to stay whole stays whole.
    private static void LowerUntilItFits(List<RibbonGroup> groups, double available)
    {
        while (Total(groups) > available)
        {
            if (!Lower(groups)) return;
        }
    }

    // No width to test against here: Raise tries a step and puts it back itself if the result no longer fits.
    private static void RaiseWhileItStillFits(List<RibbonGroup> groups, double limit)
    {
        while (true)
        {
            if (!Raise(groups, limit)) return;
        }
    }

    // Lowest ShrinkPriority, and among equals the one furthest RIGHT - and it keeps being chosen until it has nothing
    // left, so the tab gives way from its end inwards.
    private static bool Lower(List<RibbonGroup> groups)
    {
        RibbonGroup pick = null;
        for (var i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            if (NextNarrower(group) < 0) continue;
            if (pick == null || group.ShrinkPriority <= pick.ShrinkPriority)
            {
                pick = group;
            }
        }

        if (pick == null) return false;

        pick.ApplyVariant(NextNarrower(pick));
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
            if (pick == null || group.ShrinkPriority >= pick.ShrinkPriority)
            {
                pick = group;
            }
        }

        if (pick == null) return false;

        var drawn = pick.CurrentVariant;
        pick.ApplyVariant(drawn - 1);
        if (Total(groups) <= limit) return true;

        pick.ApplyVariant(drawn);
        return false;
    }

    // A step has to BUY width, collapsing included. Ladders are not monotonic: dropping a group's only large command
    // re-packs its rows into MORE columns, so the rung below can cost more than the one above it. -1 = nothing left.
    private static int NextNarrower(RibbonGroup group)
    {
        var steps = group.ShrinkSteps;
        if (steps == RibbonGroupShrinkSteps.None) return -1;

        var variants = group.Variants;
        var current = group.WidthAt(group.CurrentVariant);
        for (var i = group.CurrentVariant + 1; i < variants.Count; i++)
        {
            var step = variants[i].IsCollapsed ? RibbonGroupShrinkSteps.Collapse : RibbonGroupShrinkSteps.Sizes;
            if ((steps & step) == 0) continue;

            if (group.WidthAt(i) < current) return i;
        }

        return -1;
    }

    private static double WidthOf(IMeasurableComponent child) =>
        child is RibbonGroup group && !double.IsNaN(group.WidthAt(group.CurrentVariant))
            ? group.WidthAt(group.CurrentVariant)
            : child.DesiredSize.Width;
}
