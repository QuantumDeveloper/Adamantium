using System;
using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>
/// Clips a single child (a tab strip's ItemsPresenter) to its own bounds and PANS it with the mouse wheel along
/// <see cref="Orientation"/> - no scrollbar. Lets a <see cref="TabControl"/>'s headers overflow and be wheeled through,
/// instead of a ScrollViewer (whose bar overlays the tabs) or the tabs shrinking to fit. Horizontal for a top/bottom
/// strip, vertical for a left/right one.
/// </summary>
public class TabStripScroller : InputUIComponent, IContainer
{
    private const double WheelStep = 48;   // px panned per wheel notch

    public static readonly AdamantiumProperty ChildProperty = AdamantiumProperty.Register(nameof(Child),
        typeof(IMeasurableComponent), typeof(TabStripScroller),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange, OnChildChanged));

    public static readonly AdamantiumProperty OrientationProperty = AdamantiumProperty.Register(nameof(Orientation),
        typeof(Orientation), typeof(TabStripScroller),
        new PropertyMetadata(Orientation.Horizontal, PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));

    // Read-only scroll state, for the overflow affordances (fade edges / chevron buttons) to bind their visibility to:
    // CanScrollBack = panned away from the start (content hidden before the viewport); CanScrollForward = more past the end.
    public static readonly AdamantiumProperty CanScrollBackProperty = AdamantiumProperty.Register(nameof(CanScrollBack),
        typeof(bool), typeof(TabStripScroller), new PropertyMetadata(false));

    public static readonly AdamantiumProperty CanScrollForwardProperty = AdamantiumProperty.Register(nameof(CanScrollForward),
        typeof(bool), typeof(TabStripScroller), new PropertyMetadata(false));

    private double _offset;      // how far the strip is panned along the axis
    private double _extent;      // the child's length along the axis (from measure)
    private double _viewport;    // our own length along the axis (from arrange)

    // The strip's panel, when it virtualizes (TabPanel as an items host). It owns which tabs exist, so it has to be told
    // where the strip has been panned to and how much of it shows - otherwise it realizes the window for offset zero
    // forever and panning reveals empty space. Null for a plain strip (no items host), where this control pans the whole
    // child itself exactly as before.
    private IScrollableContent _inner;
    private bool Delegating => _inner != null;

    public TabStripScroller()
    {
        ClipToBounds = true;
        MouseWheel += OnMouseWheel;

        // A drag crossing the strip pans it - but that is driven by the drag itself (DragDrop.AutoScroll), not by a
        // handler here: drop events only reach a DROP TARGET, and a strip that declared itself one would start
        // answering for payloads it has no business with.
    }

    // Lays out from the Child property, so a child taken by another parent has to be dropped there - see
    // UIComponent.DisownVisualChild. Leaving the property set would keep this host measuring and arranging a control it
    // no longer owns, and two parents placing one control is decided by whichever the layout pass reaches last.
    protected internal override void DisownVisualChild(IUIComponent child)
    {
        if (ReferenceEquals(Child, child)) Child = null;
        base.DisownVisualChild(child);
    }

    [Content]
    public IMeasurableComponent Child
    {
        get => GetValue<IMeasurableComponent>(ChildProperty);
        set => SetValue(ChildProperty, value);
    }

    public Orientation Orientation
    {
        get => GetValue<Orientation>(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    private bool IsHorizontal => Orientation == Orientation.Horizontal;

    /// <summary>True when the strip is panned off its start (hidden content before the viewport). Drives a start-edge
    /// fade / chevron.</summary>
    public bool CanScrollBack
    {
        get => GetValue<bool>(CanScrollBackProperty);
        private set => SetValue(CanScrollBackProperty, value);
    }

    /// <summary>True when there is hidden content past the end of the viewport. Drives an end-edge fade / chevron.</summary>
    public bool CanScrollForward
    {
        get => GetValue<bool>(CanScrollForwardProperty);
        private set => SetValue(CanScrollForwardProperty, value);
    }

    /// <summary>Pan a step toward the start (direction &lt; 0) or the end (direction &gt; 0) - the chevron buttons call this.</summary>
    public void LineScroll(int direction)
    {
        var max = Math.Max(0, _extent - _viewport);
        if (max <= 0) return;
        var step = Math.Max(WheelStep * 2, _viewport * 0.5);
        _offset = Math.Clamp(_offset + Math.Sign(direction) * step, 0, max);
        InvalidateArrange();
    }

    /// <summary>Pan by an arbitrary amount, clamped to what there is to scroll. Returns whether anything moved.
    /// <para>Used by a DRAG held near the strip's edge: a tab that is off-screen cannot be reordered past, or dropped
    /// onto, unless the strip comes to meet the pointer. <see cref="LineScroll"/> is no use there - it steps half a
    /// viewport at a time, which during a drag jumps the tabs out from under the hand.</para></summary>
    public bool Pan(double delta)
    {
        var max = Math.Max(0, _extent - _viewport);
        if (max <= 0) return false;

        var next = Math.Clamp(_offset + delta, 0, max);
        if (next.Equals(_offset)) return false;

        _offset = next;
        InvalidateArrange();
        return true;
    }

    /// <summary>How close to an edge a drag must come before the strip pans, in pixels. On the STRIP rather than on the
    /// tab control: three different drags pan it - a tab being reordered, a window being docked, and a payload being
    /// dragged over - and one of them does not belong to a tab control at all.</summary>
    public static readonly AdamantiumProperty AutoScrollMarginProperty = AdamantiumProperty.Register(
        nameof(AutoScrollMargin), typeof(double), typeof(TabStripScroller), new PropertyMetadata(48.0));

    public double AutoScrollMargin
    {
        get => GetValue<double>(AutoScrollMarginProperty);
        set => SetValue(AutoScrollMarginProperty, value);
    }

    /// <summary>How much of the overshoot becomes panning, per move. One would track the pointer exactly and overshoot
    /// wildly, since moves arrive far faster than the eye follows.</summary>
    public static readonly AdamantiumProperty AutoScrollRateProperty = AdamantiumProperty.Register(
        nameof(AutoScrollRate), typeof(double), typeof(TabStripScroller), new PropertyMetadata(0.35));

    public double AutoScrollRate
    {
        get => GetValue<double>(AutoScrollRateProperty);
        set => SetValue(AutoScrollRateProperty, value);
    }

    /// <summary>Pans when a drag is held near an EDGE of the strip, by how far past that edge's margin it is. One rule
    /// in one place, because three things drag over a strip - a tab being reordered, a whole window being docked, and a
    /// payload being dragged over - and they must feel the same.</summary>
    /// <param name="along">Where the pointer is along the strip's own axis, in the strip's coordinates.</param>
    public bool PanNear(double along, double margin, double rate)
    {
        var extent = IsHorizontal ? RenderSize.Width : RenderSize.Height;
        if (extent <= 0) return false;

        // A margin that would meet in the middle is not a margin - it would pan whatever the pointer did.
        var edge = Math.Min(margin, extent / 3);
        var overshoot = along < edge ? along - edge
            : along > extent - edge ? along - (extent - edge)
            : 0;

        return overshoot != 0 && Pan(overshoot * rate);
    }

    /// <summary>Pan just enough to bring <paramref name="element"/> (a tab) fully into view - the overflow menu calls this
    /// when a hidden tab is picked. No-op if it is already visible.</summary>
    /// <summary>Scrolls the element into view. Returns true when it was ALREADY fully visible, so a caller can keep
    /// asking until it settles.
    /// <para>One scroll is not enough: the overflow button sits in an Auto column beside this scroller and its
    /// visibility is decided AFTER a layout pass, so the moment it appears the star column - this viewport - gets
    /// narrower than it was when the offset was computed, and the tail of the tab (its close button) ends up past the
    /// new edge.</para></summary>
    public bool ScrollIntoView(IUIComponent element)
    {
        if (element == null || Child is not IUIComponent child) return true;

        double start = 0;
        for (var n = element; n != null && !ReferenceEquals(n, child); n = n.VisualParent)
            start += IsHorizontal ? n.Bounds.X : n.Bounds.Y;

        return ScrollIntoView(start, IsHorizontal ? element.Bounds.Width : element.Bounds.Height);
    }

    /// <summary>The same, addressed by POSITION along the strip rather than by an element - for a tab that has no
    /// container to point at. A virtualized strip only builds the tabs in view, so the one being scrolled TO is usually
    /// exactly the one that does not exist yet; the panel knows where it would be (VirtualizingPanel.TryGetItemRect) and
    /// that is what this takes.</summary>
    public bool ScrollIntoView(double start, double size)
    {
        var max = Math.Max(0, _extent - _viewport);

        // A tab WIDER than the viewport can never be "fully visible", so the caller's retry loop had nothing to settle
        // on and the two answers alternated forever - measured at 55,262 calls, the offset flipping 0 <-> 24 for a 69px
        // tab in a 45px strip, which is the text visibly sliding back and forth. Show its START and call it done.
        if (size >= _viewport)
        {
            var wanted = Math.Clamp(start, 0, max);
            if (!wanted.Equals(_offset))
            {
                _offset = wanted;
                InvalidateArrange();
            }
            return true;
        }

        if (start < _offset)
        {
            _offset = Math.Clamp(start, 0, max);
        }
        else if (start + size > _offset + _viewport)
        {
            _offset = Math.Clamp(start + size - _viewport, 0, max);
        }
        else
        {
            return true;   // nothing to do - it is all visible
        }

        InvalidateArrange();
        return false;
    }

    /// <summary>Raised when <see cref="CanScrollBack"/>/<see cref="CanScrollForward"/> change, so an owner (TabControl) can
    /// refresh the fade / chevron affordances without polling.</summary>
    public event EventHandler ScrollStateChanged;

    // Recompute CanScrollBack/Forward from the current offset/extent/viewport. Called from every arrange (which runs after
    // any offset change via InvalidateArrange), so the affordances stay in step with wheeling, resizing and tab add/close.
    private void UpdateScrollState()
    {
        var max = Math.Max(0, _extent - _viewport);
        var back = _offset > 0.5;
        var forward = _offset < max - 0.5;
        if (back == CanScrollBack && forward == CanScrollForward) return;
        CanScrollBack = back;
        CanScrollForward = forward;
        ScrollStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void OnChildChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        var scroller = (TabStripScroller)a;
        if (e.OldValue is IUIComponent oldChild)
        {
            scroller.LogicalChildrenCollection.Remove(oldChild);
            scroller.VisualChildrenCollection.Remove(oldChild);
        }
        if (e.NewValue is IUIComponent newChild)
        {
            scroller.LogicalChildrenCollection.Add(newChild);
            scroller.VisualChildrenCollection.Add(newChild);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var child = Child;
        if (child == null) return Size.Zero;

        ResolveInner();

        // A VIRTUALIZING strip is measured with the real length, because that length IS the panel's viewport and the
        // panel decides which tabs to realize from it; unbounded would read as "everything is visible" and realize the
        // whole strip, which is the thing virtualization is here to avoid. Its extent then comes from the panel, which
        // is the only one that knows how long the strip would be if all of it existed.
        // A PLAIN strip keeps the old bargain: unbounded room so it lays out at full length and never shrinks.
        var probe = Delegating
            ? availableSize
            : (IsHorizontal
                ? new Size(double.PositiveInfinity, availableSize.Height)
                : new Size(availableSize.Width, double.PositiveInfinity));
        child.Measure(probe);
        var d = child.DesiredSize;
        _extent = Delegating
            ? (IsHorizontal ? _inner.Extent.Width : _inner.Extent.Height)
            : (IsHorizontal ? d.Width : d.Height);

        // Take the child's cross size, but only as much of the axis as offered (so we clip, never overflow the parent).
        return IsHorizontal
            ? new Size(Math.Min(d.Width, availableSize.Width), d.Height)
            : new Size(d.Width, Math.Min(d.Height, availableSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var child = Child;
        if (child != null)
        {
            _viewport = IsHorizontal ? finalSize.Width : finalSize.Height;
            ClampOffset();
            UpdateScrollState();
            PushOffset();
            // The panned child is a render MOTION NODE: its subtree bakes in ITS space and rides its transform-table
            // slot, so a pan rewrites one matrix and replays the recorded frame instead of re-walking the window.
            if (child is UIComponent panned)
                panned.IsRenderMotionNode = true;

            // Translate by what the panel has actually REALIZED, not by where we have been panned to: the two differ for
            // the frame between a pan and the panel's next measure, and translating to the newer value slides tabs that
            // do not exist yet into view - a gap at the leading edge. For a plain strip the two are the same number.
            var shift = Delegating ? (IsHorizontal ? _inner.RealizedOffset.X : _inner.RealizedOffset.Y) : _offset;
            _lastShift = shift;
            var rect = IsHorizontal
                ? new Rect(-shift, 0, Math.Max(_extent, finalSize.Width), finalSize.Height)
                : new Rect(0, -shift, finalSize.Width, Math.Max(_extent, finalSize.Height));
            child.Arrange(rect);
        }
        return finalSize;
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var max = Math.Max(0, _extent - _viewport);
        if (max <= 0) return;   // nothing to pan
        _offset = Math.Clamp(_offset - e.Delta / 120.0 * WheelStep, 0, max);
        InvalidateArrange();
        e.Handled = true;
    }

    private void ClampOffset() => _offset = Math.Clamp(_offset, 0, Math.Max(0, _extent - _viewport));

    // Hand the pan to the panel so it realizes the window we are about to show. Only when it CHANGED: SetOffset raises
    // metrics, and re-raising them from inside our own arrange every pass is how a host and a panel invalidate each
    // other forever (the ScrollContentPresenter carries the same guard, for the same crash).
    private void PushOffset()
    {
        if (!Delegating) return;
        var wanted = IsHorizontal ? new Vector2((float)_offset, 0) : new Vector2(0, (float)_offset);
        if (_inner.Offset != wanted) _inner.SetOffset(wanted);
    }

    // The panel that virtualizes the strip, if there is one. Re-asked on every measure because a template can be applied
    // (or re-applied on a theme change) long after this control was built.
    private void ResolveInner()
    {
        var found = Child is IUIComponent root ? ScrollableContent.FindIn(root) : null;
        if (ReferenceEquals(found, _inner)) return;
        if (_inner != null) _inner.ScrollMetricsChanged -= OnInnerMetricsChanged;
        _inner = found;
        if (_inner != null) _inner.ScrollMetricsChanged += OnInnerMetricsChanged;
    }

    // The panel re-clamped or re-realized: re-translate so the shift follows its realized offset. Guarded on an actual
    // change - this also fires from the panel's own arrange, and an unconditional invalidate would loop.
    private void OnInnerMetricsChanged(object sender, EventArgs e)
    {
        var realized = IsHorizontal ? _inner.RealizedOffset.X : _inner.RealizedOffset.Y;
        if (!realized.Equals(_lastShift)) InvalidateArrange();
    }

    private double _lastShift = double.NaN;

    // IContainer: the AUML loader nests the ItemsPresenter as Child.
    public void AddOrSetChildComponent(object component) { if (component is IMeasurableComponent c) Child = c; }
    public void RemoveAllChildComponents() => Child = null;
    public IReadOnlyList<object> GetChildComponents() => Child != null ? [Child] : [];
    public void InsertChildComponent(int index, object component) { if (component is IMeasurableComponent c) Child = c; }
    public void RemoveChildComponentAt(int index) => Child = null;
}
