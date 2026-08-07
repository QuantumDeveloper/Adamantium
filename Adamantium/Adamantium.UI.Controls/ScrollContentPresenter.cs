using System;
using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media.Animation;

namespace Adamantium.UI.Controls;

/// <summary>
/// The default physical <see cref="IScrollableContent"/>: it hosts a <see cref="ScrollViewer"/>'s content, measures
/// it unbounded on the scrollable axes (so it reports its full extent), and shows a window onto it by translating the
/// child by -<see cref="Offset"/> and clipping to the viewport (<see cref="UIComponent.ClipToBounds"/> -&gt; a
/// renderer scissor). A virtualizing panel can replace it later by implementing the same seam.
/// </summary>
public class ScrollContentPresenter : ContentPresenter, IScrollableContent
{
    // Pan (touch-style content drag): grab anywhere on the content and drag to scroll, no scrollbars needed. A small
    // threshold keeps a plain click on interactive content from being swallowed as a pan.
    private const double PanThreshold = 4;
    private bool _isPanning;
    private Vector2 _panStartPoint;
    private Vector2 _panStartOffset;

    private Size _extent;
    private Size _viewport;
    private Vector2 _offset;

    // --- Inertia (kinetic) scrolling: smooth wheel + flick momentum, driven by the AnimationManager heartbeat. The feel
    // is per-instance (tunable per control via the ScrollViewer that owns this presenter), not hardcoded. ---

    /// <summary>Flick velocity decay per second - higher stops a fling sooner (coasts less), lower glides further.</summary>
    public double InertiaFriction { get; set; } = 6.0;

    /// <summary>Wheel ease-to-target rate per second - higher is snappier, lower glides longer to the target.</summary>
    public double InertiaSmoothRate { get; set; } = 14.0;

    /// <summary>Speed (px/s) below which a flick is treated as stopped.</summary>
    public double MinFlickSpeed { get; set; } = 12.0;

    /// <summary>Release speed (px/s) needed to start a flick.</summary>
    public double FlickStartSpeed { get; set; } = 80.0;

    private bool _inertiaActive;        // logically scrolling under inertia
    private bool _inertiaRegistered;    // a ticker is registered with the AnimationManager
    private Vector2? _inertiaTarget;    // set => smooth-to-target (wheel); null => flick (velocity) mode
    private Vector2 _inertiaVelocity;   // px/s, flick mode

    // Pan velocity tracking, to fling on release.
    private long _lastMoveTimestamp;
    private Vector2 _lastMoveOffset;
    private Vector2 _panVelocity;

    /// <summary>Enables inertial scrolling (smooth wheel + flick momentum). On by default; a ScrollViewer/theme can
    /// turn it off to get instant scrolling back.</summary>
    public bool IsInertiaEnabled { get; set; } = true;

    // When CanContentScroll is on and the hosted content contains an IScrollableContent (a virtualizing panel), this
    // presenter delegates the scroll mechanism to it instead of physically translating: the panel realizes only the
    // visible window. WPF's CanContentScroll, on the clean IScrollableContent seam.
    private IScrollableContent _inner;

    /// <summary>Delegate scrolling to an inner virtualizing panel (item scrolling) instead of pixel-translating the content.</summary>
    public bool CanContentScroll { get; set; }

    private bool Delegating => CanContentScroll && _inner != null;

    public ScrollContentPresenter()
    {
        // The whole point of the presenter: the overflowing content is scissored to this viewport (see the renderer's
        // per-unit clip, which intersects every ClipToBounds ancestor).
        ClipToBounds = true;
        // (Focusable=false comes from ContentPresenter's metadata override - a passive presenter is never a focus target.)
    }

    /// <summary>This clip is a VIEWPORT, so it binds adorners too: a focus ring on a row scrolled half out of the list
    /// must be cut by the list, the way the row itself is. See <see cref="IUIComponent.ClipsAdorners"/> for why that is
    /// the exception rather than the rule.</summary>
    public override bool ClipsAdorners => true;

    public Size Extent => Delegating ? _inner.Extent : _extent;

    public Size Viewport => _viewport;

    public Vector2 Offset => Delegating ? _inner.Offset : _offset;

    // The offset our LAST arrange actually translated the content to (see IScrollableContent.RealizedOffset). When we
    // delegate to a virtualizing panel, that's the offset ITS window was realized for; a physical (non-delegating) content
    // arranges to _offset directly, so it's just _offset.
    public Vector2 RealizedOffset => Delegating ? _inner.RealizedOffset : _offset;

    public bool CanScrollHorizontally { get; set; } = true;

    public bool CanScrollVertically { get; set; } = true;

    /// <summary>Which axes a content drag pans (set by the <see cref="ScrollViewer"/>). None = drag does nothing.</summary>
    public PanningMode PanningMode { get; set; } = PanningMode.None;

    public event EventHandler ScrollMetricsChanged;

    public void SetOffset(Vector2 offset)
    {
        StopInertia();   // a direct (scrollbar / programmatic) offset set cancels any in-flight inertia
        SetOffsetCore(offset);
    }

    // The actual offset set, WITHOUT cancelling inertia - used by the inertia ticker itself.
    private void SetOffsetCore(Vector2 offset)
    {
        if (Delegating)
        {
            _inner.SetOffset(offset);   // the panel clamps, re-windows (realizes/rebinds entering rows), raises metrics
            InvalidateArrange();        // ...and WE re-translate the panel by the new -offset (transform-only scroll)
            return;
        }

        var clamped = ClampOffset(offset, _extent, _viewport);
        if (clamped == _offset) return;
        _offset = clamped;
        InvalidateArrange();   // reposition the child; the metrics event is raised from the arrange
    }

    /// <summary>Smoothly scrolls by <paramref name="delta"/> (the wheel path): eases toward an accumulating target, so
    /// rapid notches add up instead of jumping. Instant set when inertia is off.</summary>
    public void AnimateScrollBy(Vector2 delta)
    {
        if (!IsInertiaEnabled)
        {
            SetOffset(Offset + delta);
            return;
        }
        // Accumulate into the smooth target so several quick notches build one longer glide.
        var basis = _inertiaActive && _inertiaTarget.HasValue ? _inertiaTarget.Value : Offset;
        _inertiaTarget = ClampOffset(basis + delta, Extent, Viewport);
        _inertiaVelocity = default;
        _inertiaActive = true;
        EnsureInertiaTicker();
    }

    private void StartFlick(Vector2 velocity)
    {
        _inertiaVelocity = velocity;
        _inertiaTarget = null;
        _inertiaActive = true;
        EnsureInertiaTicker();
    }

    private void StopInertia()
    {
        _inertiaActive = false;   // the registered ticker self-removes on its next advance
        _inertiaTarget = null;
        _inertiaVelocity = default;
    }

    private void EnsureInertiaTicker()
    {
        if (_inertiaRegistered) return;
        _inertiaRegistered = true;
        AnimationManager.AddTicker(AdvanceInertia);
    }

    // One inertia step. Smooth (wheel): exponential ease to the target. Flick: integrate velocity with friction decay.
    // Either way it stops at the bounds (the offset stops changing) or once it has effectively settled.
    private bool AdvanceInertia(double dt)
    {
        if (!_inertiaActive) { _inertiaRegistered = false; return true; }

        var before = Offset;
        Vector2 next;
        if (_inertiaTarget.HasValue)
        {
            var t = 1.0 - Math.Exp(-InertiaSmoothRate * dt);
            next = before + (_inertiaTarget.Value - before) * t;
            if ((_inertiaTarget.Value - next).Length() < 0.5) next = _inertiaTarget.Value;
        }
        else
        {
            next = before + _inertiaVelocity * dt;
            _inertiaVelocity *= Math.Exp(-InertiaFriction * dt);
        }

        SetOffsetCore(next);
        var moved = (Offset - before).Length();   // 0 => clamped at a bound (or settled)

        var done = _inertiaTarget.HasValue
            ? (_inertiaTarget.Value - Offset).Length() < 0.5 || moved < 0.01
            : _inertiaVelocity.Length() < MinFlickSpeed || moved < 0.01;
        if (done) { _inertiaActive = false; _inertiaRegistered = false; }
        return done;
    }

    // --- Pan: drag the content itself to scroll (the no-scrollbars / touch path) ---

    protected override void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.Handled || PanningMode == PanningMode.None) return;   // pan off, or interactive content took the press
        StopInertia();   // grabbing the content halts any in-flight inertia
        _isPanning = false;
        _panStartPoint = e.GetPosition(this);   // the presenter is the fixed viewport, so this space is stable
        _panStartOffset = Offset;
        _lastMoveTimestamp = 0;
        _panVelocity = default;
        // Capture so the drag keeps tracking once the pointer leaves the content (raw moves only honour capture).
        CaptureMouse();
    }

    protected override void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!IsMouseCaptured) return;

        var raw = e.GetPosition(this) - _panStartPoint;
        if (!_isPanning)
        {
            // Hold off until the drag clears the threshold, so a plain click isn't consumed as a (zero) pan.
            if (Math.Abs(raw.X) < PanThreshold && Math.Abs(raw.Y) < PanThreshold) return;
            _isPanning = true;
        }

        // Restrict to the allowed axis (the other stays put). Dragging the content one way reveals what's behind it,
        // i.e. the offset moves opposite the pointer.
        var delta = PanningMode switch
        {
            PanningMode.HorizontalOnly => new Vector2(raw.X, 0),
            PanningMode.VerticalOnly => new Vector2(0, raw.Y),
            _ => raw
        };
        SetOffsetCore(_panStartOffset - delta);   // pan drives the offset directly (Down already stopped inertia)

        // Track the offset's velocity so a flick can continue it on release; blend a little so one jittery sample
        // doesn't dominate the release velocity.
        var now = System.Diagnostics.Stopwatch.GetTimestamp();
        var current = Offset;
        if (_lastMoveTimestamp != 0)
        {
            var moveDt = (now - _lastMoveTimestamp) / (double)System.Diagnostics.Stopwatch.Frequency;
            if (moveDt > 0.0001)
                _panVelocity = _panVelocity * 0.4 + (current - _lastMoveOffset) * (1.0 / moveDt) * 0.6;
        }
        _lastMoveTimestamp = now;
        _lastMoveOffset = current;
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (IsMouseCaptured) ReleaseMouseCapture();
        if (_isPanning)
        {
            e.Handled = true;   // it was a pan, not a click
            if (IsInertiaEnabled && _panVelocity.Length() > FlickStartSpeed)
                StartFlick(_panVelocity);   // fling: keep scrolling with decaying velocity
        }
        _isPanning = false;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (CanContentScroll)
        {
            // Measure the content at the VIEWPORT (not unbounded) so an inner virtualizing panel realizes only what's
            // visible; then delegate the scroll surface to it. base (ContentPresenter) builds + measures the content.
            base.MeasureOverride(availableSize);
            ResolveInner();
            if (_inner != null)
            {
                // An unbounded axis is the parent ASKING for our natural size (a Grid star row probes its child that way
                // before it resolves the row), not telling us the whole content is on screen. Taking it for a viewport
                // wipes the scroll range and zeroes the offset every pass - the list then refuses to scroll. The viewport
                // is what ARRANGE hands us; measure only answers with a desired size.
                if (!double.IsInfinity(availableSize.Width)) _viewport = new Size(availableSize.Width, _viewport.Height);
                if (!double.IsInfinity(availableSize.Height)) _viewport = new Size(_viewport.Width, availableSize.Height);
                RaiseMetricsChanged();
                return new Size(
                    double.IsInfinity(availableSize.Width) ? _inner.Extent.Width : availableSize.Width,
                    double.IsInfinity(availableSize.Height) ? _inner.Extent.Height : availableSize.Height);
            }
        }

        // Physical path: measure the content unbounded on every axis the viewer allows scrolling, so it reports its
        // natural extent; constrained on a disabled axis so it fits.
        var constraint = new Size(
            CanScrollHorizontally ? double.PositiveInfinity : availableSize.Width,
            CanScrollVertically ? double.PositiveInfinity : availableSize.Height);

        var extent = base.MeasureOverride(constraint);
        if (extent != _extent)
        {
            _extent = extent;
            RaiseMetricsChanged();
        }

        // Desired size = the content, capped by the viewport (an unbounded axis reports the full extent). NOT the whole
        // viewport: a viewer measured content-to-fit but capped by MaxHeight (e.g. a DropDown popup) must shrink to its
        // content when it's shorter than the cap, not pad out to the cap. A viewer that should FILL a fixed slot still
        // does - its Stretch alignment fills at ARRANGE regardless of this (smaller) desired size.
        return new Size(
            double.IsInfinity(availableSize.Width) ? extent.Width : Math.Min(extent.Width, availableSize.Width),
            double.IsInfinity(availableSize.Height) ? extent.Height : Math.Min(extent.Height, availableSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _viewport = finalSize;

        if (Delegating)
        {
            // The inner virtualizing panel realizes only the visible window and arranges those tiles at ABSOLUTE slots
            // (offset NOT baked in). We scroll it like physical content: keep the panel VIEWPORT-sized (so its clamp +
            // scrollbar stay correct) and slide it up/left by the offset. Tiles that scrolled off overflow the panel; our
            // ClipToBounds trims to the viewport. A scroll is then ONE translation here plus the panel rebinding the row
            // that entered - staying tiles keep their slots and Arrange short-circuits.
            var offset = _inner.Offset;
            _lastTranslatedInnerOffset = offset;   // so OnInnerMetricsChanged only re-invalidates on a REAL offset change
            if (VisualChildren.FirstOrDefault() is IMeasurableComponent inner)
            {
                // The element WE translate (the ItemsPresenter hosting the virtualizing panel) is the render MOTION NODE:
                // its subtree's batched instances bake in ITS space + reference its transform-table slot, so this
                // per-scroll-frame arrange marks ONE node (a 64-byte matrix rewrite + replay) instead of the global
                // transform flag (which re-baked every instance in the window - the scroll full-walk).
                if (inner is Base.UIComponent innerUi) innerUi.IsRenderMotionNode = true;
                inner.Arrange(new Rect(-offset.X, -offset.Y, finalSize.Width, finalSize.Height));
            }
            RaiseMetricsChanged();
            return finalSize;
        }

        _offset = ClampOffset(_offset, _extent, finalSize);

        if (VisualChildren.FirstOrDefault() is IMeasurableComponent content)
        {
            // Arrange the child at its full extent (at least the viewport), shifted up/left by the offset; the
            // ClipToBounds scissor trims the overflow to the viewport.
            var width = Math.Max(_extent.Width, finalSize.Width);
            var height = Math.Max(_extent.Height, finalSize.Height);
            content.Arrange(new Rect(-_offset.X, -_offset.Y, width, height));
        }

        RaiseMetricsChanged();
        return finalSize;
    }

    // Find the inner virtualizing panel (first IScrollableContent in the content subtree) and track its metrics.
    private void ResolveInner()
    {
        var found = FindInner(this);
        if (ReferenceEquals(found, _inner)) return;
        if (_inner != null) _inner.ScrollMetricsChanged -= OnInnerMetricsChanged;
        _inner = found;
        if (_inner != null) _inner.ScrollMetricsChanged += OnInnerMetricsChanged;
    }

    private static IScrollableContent FindInner(IUIComponent root)
    {
        foreach (var child in root.VisualChildren)
        {
            if (child is IScrollableContent scrollable) return scrollable;
            var deeper = FindInner(child);
            if (deeper != null) return deeper;
        }
        return null;
    }

    private Vector2 _lastTranslatedInnerOffset;

    private void OnInnerMetricsChanged(object sender, EventArgs e)
    {
        // The inner panel re-clamped its offset (e.g. the content shrank): re-translate the panel by the new -Offset so the
        // content snaps into view WITHOUT a manual scroll nudge. GUARD on an actual offset change - this event also fires
        // from the panel's OWN arrange, so an unconditional InvalidateArrange would re-enqueue us mid-arrange, re-arrange
        // the panel, re-fire this, and loop (the crash). The re-clamp is idempotent, so once we've translated to the new
        // offset the values match and the loop stops.
        if (Delegating && _inner.Offset != _lastTranslatedInnerOffset) InvalidateArrange();
        RaiseMetricsChanged();
    }

    private void RaiseMetricsChanged() => ScrollMetricsChanged?.Invoke(this, EventArgs.Empty);

    private static Vector2 ClampOffset(Vector2 offset, Size extent, Size viewport)
    {
        var maxX = Math.Max(0, extent.Width - viewport.Width);
        var maxY = Math.Max(0, extent.Height - viewport.Height);
        return new Vector2(Math.Clamp(offset.X, 0, maxX), Math.Clamp(offset.Y, 0, maxY));
    }
}
