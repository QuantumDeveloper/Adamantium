using System;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>
/// Scrolls content larger than its own size, showing two overlay scrollbars (no arrow buttons). It owns scroll
/// policy - wheel step, page size, bar visibility - and drives a <see cref="IScrollableContent"/> (the default
/// <see cref="ScrollContentPresenter"/>) that owns the mechanism. Bars and content are kept in sync in pixel units:
/// a bar's Maximum is Extent-Viewport, its ViewportSize is the viewport, its Value is the offset. Mirrors WPF's
/// ScrollViewer, minus the IScrollInfo surface.
/// </summary>
public class ScrollViewer : ContentControl
{
    // A wheel notch (120 units) scrolls this many lines; a line is this many device-independent pixels.
    private const double WheelLinesPerNotch = 3;
    private const double LineStep = 16;

    private ScrollContentPresenter _presenter;
    private ScrollBar _verticalBar;
    private ScrollBar _horizontalBar;
    private bool _syncingBars;   // guards the metrics->bars push from bouncing back through ValueChanged

    // ATTACHED (WPF-style): set ScrollViewer.HorizontalScrollBarVisibility on ANY element (e.g. a ListBox) and its templated
    // ScrollViewer picks it up via {TemplateBinding (ScrollViewer.HorizontalScrollBarVisibility)} - no per-control property.
    // Default Disabled(H)/Auto(V) matches the item hosts' hardcoded policy, so unset hosts behave exactly as before.
    public static readonly AdamantiumProperty HorizontalScrollBarVisibilityProperty = AdamantiumProperty.RegisterAttached(
        nameof(HorizontalScrollBarVisibility), typeof(ScrollBarVisibility), typeof(AdamantiumComponent),
        new PropertyMetadata(ScrollBarVisibility.Disabled, PropertyMetadataOptions.AffectsMeasure, OnScrollBarVisibilityChanged));

    public static readonly AdamantiumProperty VerticalScrollBarVisibilityProperty = AdamantiumProperty.RegisterAttached(
        nameof(VerticalScrollBarVisibility), typeof(ScrollBarVisibility), typeof(AdamantiumComponent),
        new PropertyMetadata(ScrollBarVisibility.Auto, PropertyMetadataOptions.AffectsMeasure, OnScrollBarVisibilityChanged));

    public static ScrollBarVisibility GetHorizontalScrollBarVisibility(AdamantiumComponent e) => e.GetValue<ScrollBarVisibility>(HorizontalScrollBarVisibilityProperty);
    public static void SetHorizontalScrollBarVisibility(AdamantiumComponent e, ScrollBarVisibility value) => e.SetValue(HorizontalScrollBarVisibilityProperty, value);

    public static ScrollBarVisibility GetVerticalScrollBarVisibility(AdamantiumComponent e) => e.GetValue<ScrollBarVisibility>(VerticalScrollBarVisibilityProperty);
    public static void SetVerticalScrollBarVisibility(AdamantiumComponent e, ScrollBarVisibility value) => e.SetValue(VerticalScrollBarVisibilityProperty, value);

    // Scroll chaining (attached, default ON): when the wheel reaches this viewer's edge in the scroll direction, DON'T
    // swallow the event - leave it unhandled so it bubbles to a parent ScrollViewer (nested lists hand off instead of
    // dead-ending under the cursor, the classic nested-scroll annoyance). Set False for the isolated WPF behaviour.
    public static readonly AdamantiumProperty ScrollChainingProperty = AdamantiumProperty.RegisterAttached(
        "ScrollChaining", typeof(bool), typeof(AdamantiumComponent), new PropertyMetadata(true));

    public static bool GetScrollChaining(AdamantiumComponent e) => e.GetValue<bool>(ScrollChainingProperty);
    public static void SetScrollChaining(AdamantiumComponent e, bool value) => e.SetValue(ScrollChainingProperty, value);

    // Changing bar visibility at runtime (e.g. a TextBox toggling wrap) must re-push the CanScroll flags onto the
    // presenter - Disabled means "don't scroll this axis" (measure the content to the viewport so it wraps/fits), any
    // other value means "scrollable". Without this the presenter kept the flags captured once in OnApplyTemplate.
    private static void OnScrollBarVisibilityChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer sv || sv._presenter == null) return;
        sv._presenter.CanScrollHorizontally = sv.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled;
        sv._presenter.CanScrollVertically = sv.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled;
        sv._presenter.InvalidateMeasure();
    }

    public static readonly AdamantiumProperty PanningModeProperty = AdamantiumProperty.Register(
        nameof(PanningMode), typeof(PanningMode), typeof(ScrollViewer),
        new PropertyMetadata(PanningMode.None, OnPanningModeChanged));

    public static readonly AdamantiumProperty CanContentScrollProperty = AdamantiumProperty.Register(
        nameof(CanContentScroll), typeof(bool), typeof(ScrollViewer),
        new PropertyMetadata(false, OnCanContentScrollChanged));

    // Per-control inertia tuning - flows to this control's ScrollContentPresenter (the feel is not hardcoded).
    public static readonly AdamantiumProperty IsInertiaEnabledProperty = AdamantiumProperty.Register(
        nameof(IsInertiaEnabled), typeof(bool), typeof(ScrollViewer),
        new PropertyMetadata(true, OnInertiaSettingChanged));

    public static readonly AdamantiumProperty InertiaFrictionProperty = AdamantiumProperty.Register(
        nameof(InertiaFriction), typeof(double), typeof(ScrollViewer),
        new PropertyMetadata(6.0, OnInertiaSettingChanged));

    public static readonly AdamantiumProperty InertiaSmoothRateProperty = AdamantiumProperty.Register(
        nameof(InertiaSmoothRate), typeof(double), typeof(ScrollViewer),
        new PropertyMetadata(14.0, OnInertiaSettingChanged));

    // A scroll viewer is a passive container - not a keyboard-focus target (matches WPF). Comes for free from the
    // Focusable=false default (see InputUIComponent). A specific scrollable region that needs arrow-key scrolling can
    // opt back in with Focusable="True" in its markup.
    public ScrollViewer()
    {
        MouseWheel += OnMouseWheel;
    }

    // How much of the viewport a page keeps: a page that moved the WHOLE height would leave the reader with no line in
    // common between the two screens, which is why every reader (and every browser) keeps a couple of lines.
    private const double PageOverlap = 2 * LineStep;

    /// <summary>Scrolls one viewport-worth, minus a couple of lines of overlap. False when there is nothing to scroll
    /// or it is already parked at that end - the caller then leaves the key alone, so an enclosing viewer can take it
    /// (the same hand-off the wheel does at its edge).</summary>
    /// <remarks>Public and driven from the WINDOW rather than from a key handler here: routed keys travel up from the
    /// FOCUSED element, and a ScrollViewer is deliberately not focusable - so with the focus outside it (or nowhere at
    /// all) it never sees the key. Which is exactly the reading case, where a page gesture is wanted most.</remarks>
    public bool PageVertically(bool back)
    {
        if (_presenter is not { CanScrollVertically: true }) return false;

        var extent = _presenter.Extent;
        var viewport = _presenter.Viewport;
        if (extent.Height <= viewport.Height + 0.5) return false;

        var max = extent.Height - viewport.Height;
        var offset = ScrollOffset;
        if (back ? offset.Y <= 0.5 : offset.Y >= max - 0.5) return false;

        _presenter.AnimateScrollBy(new Vector2(0, back ? -Math.Max(viewport.Height - PageOverlap, LineStep)
                                                      : Math.Max(viewport.Height - PageOverlap, LineStep)));
        return true;
    }

    /// <summary>Jumps to the very top or the very bottom of the content (Home / End). Same contract as
    /// <see cref="PageVertically"/>: false when there is nothing to scroll or it is already parked there, so the
    /// caller can leave the key for someone else.</summary>
    public bool ScrollToVerticalEdge(bool toStart)
    {
        if (_presenter is not { CanScrollVertically: true }) return false;

        var extent = _presenter.Extent;
        var viewport = _presenter.Viewport;
        if (extent.Height <= viewport.Height + 0.5) return false;

        var max = extent.Height - viewport.Height;
        var offset = ScrollOffset;
        if (toStart ? offset.Y <= 0.5 : offset.Y >= max - 0.5) return false;

        _presenter.AnimateScrollBy(new Vector2(0, (toStart ? 0 : max) - offset.Y));
        return true;
    }

    /// <summary>Which axes a content drag pans (default <see cref="PanningMode.None"/> - scrollbars/wheel only). Opt in
    /// to touch-style panning per axis without it being forced on every ScrollViewer.</summary>
    public PanningMode PanningMode
    {
        get => GetValue<PanningMode>(PanningModeProperty);
        set => SetValue(PanningModeProperty, value);
    }

    private static void OnPanningModeChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
    {
        if (d is ScrollViewer sv && sv._presenter != null) sv._presenter.PanningMode = sv.PanningMode;
    }

    /// <summary>When true, scrolling is delegated to a virtualizing panel inside the content (item scrolling) rather
    /// than pixel-translating the whole content. Set by item controls whose panel virtualizes.</summary>
    public bool CanContentScroll
    {
        get => GetValue<bool>(CanContentScrollProperty);
        set => SetValue(CanContentScrollProperty, value);
    }

    private static void OnCanContentScrollChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
    {
        if (d is ScrollViewer sv && sv._presenter != null) sv._presenter.CanContentScroll = sv.CanContentScroll;
    }

    /// <summary>Enables inertial scrolling (smooth wheel + flick momentum) for this control. Default true.</summary>
    public bool IsInertiaEnabled
    {
        get => GetValue<bool>(IsInertiaEnabledProperty);
        set => SetValue(IsInertiaEnabledProperty, value);
    }

    /// <summary>Flick velocity decay per second - higher stops a fling sooner (coasts less). Default 6.</summary>
    public double InertiaFriction
    {
        get => GetValue<double>(InertiaFrictionProperty);
        set => SetValue(InertiaFrictionProperty, value);
    }

    /// <summary>Wheel ease-to-target rate per second - higher is snappier, lower glides longer. Default 14.</summary>
    public double InertiaSmoothRate
    {
        get => GetValue<double>(InertiaSmoothRateProperty);
        set => SetValue(InertiaSmoothRateProperty, value);
    }

    private static void OnInertiaSettingChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
    {
        if (d is ScrollViewer sv) sv.PushInertiaSettings();
    }

    private void PushInertiaSettings()
    {
        if (_presenter == null) return;
        _presenter.IsInertiaEnabled = IsInertiaEnabled;
        _presenter.InertiaFriction = InertiaFriction;
        _presenter.InertiaSmoothRate = InertiaSmoothRate;
    }

    /// <summary>When the horizontal bar appears (default <see cref="ScrollBarVisibility.Auto"/>).</summary>
    public ScrollBarVisibility HorizontalScrollBarVisibility
    {
        get => GetValue<ScrollBarVisibility>(HorizontalScrollBarVisibilityProperty);
        set => SetValue(HorizontalScrollBarVisibilityProperty, value);
    }

    /// <summary>When the vertical bar appears (default <see cref="ScrollBarVisibility.Auto"/>).</summary>
    public ScrollBarVisibility VerticalScrollBarVisibility
    {
        get => GetValue<ScrollBarVisibility>(VerticalScrollBarVisibilityProperty);
        set => SetValue(VerticalScrollBarVisibilityProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        DetachParts();   // a template swap re-runs this; drop the old wiring first

        _presenter = GetTemplateChild("PART_ScrollContentPresenter") as ScrollContentPresenter;
        _verticalBar = GetTemplateChild("PART_VerticalScrollBar") as ScrollBar;
        _horizontalBar = GetTemplateChild("PART_HorizontalScrollBar") as ScrollBar;

        if (_presenter != null)
        {
            _presenter.CanScrollHorizontally = HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled;
            _presenter.CanScrollVertically = VerticalScrollBarVisibility != ScrollBarVisibility.Disabled;
            _presenter.PanningMode = PanningMode;
            _presenter.CanContentScroll = CanContentScroll;
            PushInertiaSettings();
            _presenter.ScrollMetricsChanged += OnScrollMetricsChanged;
        }
        if (_verticalBar != null)
        {
            _verticalBar.Orientation = Orientation.Vertical;
            _verticalBar.ValueChanged += OnBarValueChanged;
        }
        if (_horizontalBar != null)
        {
            _horizontalBar.Orientation = Orientation.Horizontal;
            _horizontalBar.ValueChanged += OnBarValueChanged;
        }
    }

    /// <summary>Let the template's parts go when the template does - see ScrollBar.OnRemoveTemplate.</summary>
    public override void OnRemoveTemplate()
    {
        base.OnRemoveTemplate();
        DetachParts();
    }

    private void DetachParts()
    {
        if (_presenter != null) { _presenter.ScrollMetricsChanged -= OnScrollMetricsChanged; _presenter = null; }
        if (_verticalBar != null) { _verticalBar.ValueChanged -= OnBarValueChanged; _verticalBar = null; }
        if (_horizontalBar != null) { _horizontalBar.ValueChanged -= OnBarValueChanged; _horizontalBar = null; }
    }

    // Content metrics changed (resize, scroll): push them onto the bars (Maximum/ViewportSize/Value) and re-evaluate
    // Auto visibility. Guarded so setting Value here doesn't bounce back through OnBarValueChanged into the presenter.
    private Size _lastPushedExtent = new(-1, -1);
    private Size _lastPushedViewport;
    private Vector2 _lastPushedOffset;

    private void OnScrollMetricsChanged(object sender, EventArgs e)
    {
        if (_presenter == null) return;
        var extent = _presenter.Extent;
        var viewport = _presenter.Viewport;
        var offset = _presenter.Offset;

        // Coalesce the SUB-PIXEL scroll stream. A high-resolution wheel / touchpad pushes metrics every frame with the
        // offset creeping by a fraction of a pixel; each push re-writes both bars' Value, which re-arranges the whole
        // scrollbar template - a per-frame render churn that kept the scene in constant partials (and let the odd full
        // walk catch a virtualization row-transition mid-flight = the row that "blinks"). The thumb travel for such a
        // tiny offset move is invisible (its length maps the whole extent onto the trough), so skip the push until the
        // thumb would actually move a visible amount, or the extent/viewport themselves change (resize / count change).
        if (extent == _lastPushedExtent && viewport == _lastPushedViewport)
        {
            // "Visible" is 0.5 DEVICE pixels: the thumb travel is in logical px, so the imperceptibility floor scales
            // with the display's DPI (0.5 logical px is a coarser skip on a hi-DPI panel where it maps to more device px).
            var dpi = (RootVisual as WindowBase)?.DpiScale ?? new Vector2(1, 1);
            var span = new Vector2((float)Math.Max(1, extent.Width - viewport.Width), (float)Math.Max(1, extent.Height - viewport.Height));
            var thumbMoveX = Math.Abs(offset.X - _lastPushedOffset.X) * viewport.Width / span.X * dpi.X;
            var thumbMoveY = Math.Abs(offset.Y - _lastPushedOffset.Y) * viewport.Height / span.Y * dpi.Y;
            if (thumbMoveX < 0.5 && thumbMoveY < 0.5) return;
        }
        _lastPushedExtent = extent;
        _lastPushedViewport = viewport;
        _lastPushedOffset = offset;

        _syncingBars = true;
        if (_verticalBar != null)
        {
            _verticalBar.Minimum = 0;
            _verticalBar.Maximum = Math.Max(0, extent.Height - viewport.Height);
            _verticalBar.ViewportSize = viewport.Height;
            _verticalBar.LargeChange = viewport.Height;
            _verticalBar.SmallChange = LineStep;
            _verticalBar.Value = offset.Y;
            _verticalBar.Visibility = ComputeVisibility(VerticalScrollBarVisibility, extent.Height, viewport.Height);
        }
        if (_horizontalBar != null)
        {
            _horizontalBar.Minimum = 0;
            _horizontalBar.Maximum = Math.Max(0, extent.Width - viewport.Width);
            _horizontalBar.ViewportSize = viewport.Width;
            _horizontalBar.LargeChange = viewport.Width;
            _horizontalBar.SmallChange = LineStep;
            _horizontalBar.Value = offset.X;
            _horizontalBar.Visibility = ComputeVisibility(HorizontalScrollBarVisibility, extent.Width, viewport.Width);
        }
        _syncingBars = false;
        ScrollChanged?.Invoke(this, EventArgs.Empty);
    }

    // A bar moved (thumb drag, page, line, wheel-synced): push both bars' values as the new offset.
    private void OnBarValueChanged(object sender, ValueChangedEventArgs e)
    {
        if (_syncingBars || _presenter == null) return;
        var x = _horizontalBar?.Value ?? _presenter.Offset.X;
        var y = _verticalBar?.Value ?? _presenter.Offset.Y;
        _presenter.SetOffset(new Vector2(x, y));
    }

    /// <summary>Current scroll offset of the content (top-left of the viewport within the extent). Zero before templating.</summary>
    public Vector2 ScrollOffset => _presenter?.Offset ?? default;

    /// <summary>Size of the visible window onto the content. Zero before templating.</summary>
    public Size ViewportSize => _presenter?.Viewport ?? default;

    /// <summary>Total size of the (possibly scaled) content. Zero before templating.</summary>
    public Size ExtentSize => _presenter?.Extent ?? default;

    /// <summary>Sets the absolute scroll offset (content-space top-left of the viewport), clamped to the current extent.
    /// No-op before templating.</summary>
    public void SetScrollOffset(Vector2 offset) => _presenter?.SetOffset(offset);

    /// <summary>Raised after the scroll metrics (offset / extent / viewport) actually changed - lets a host react once the
    /// extent reflects a new layout (e.g. a ZoomBox applying a zoom-to-cursor offset after a scale change grew it).</summary>
    public event EventHandler ScrollChanged;

    /// <summary>Scroll the minimum amount so <paramref name="rect"/> (in CONTENT coordinates) is fully inside the viewport
    /// - the caret-follow primitive for a TextBox hosting its surface here. No-op until the presenter exists.</summary>
    public void BringIntoView(Rect rect)
    {
        if (_presenter == null) return;
        var off = _presenter.Offset;
        var vp = _presenter.Viewport;
        double x = off.X, y = off.Y;
        if (rect.X < x) x = rect.X;
        else if (rect.Right > x + vp.Width) x = rect.Right - vp.Width;
        if (rect.Y < y) y = rect.Y;
        else if (rect.Bottom > y + vp.Height) y = rect.Bottom - vp.Height;
        if (x != off.X || y != off.Y) _presenter.SetOffset(new Vector2(x, y));
    }

    /// <summary>Scroll so a descendant element becomes visible. Its bounds are projected from its own local space into the
    /// content space (via the world transforms) and handed to <see cref="BringIntoView(Rect)"/>. No-op if the element is
    /// not actually under this viewer's content, or before templating.</summary>
    public void BringDescendantIntoView(IUIComponent target)
    {
        if (_presenter == null || target == null) return;
        // target-local -> world -> content-presenter-local (== viewport space; the content is already shifted by -offset
        // there, so add the current offset back to land in content space, which is what BringIntoView compares against).
        var toContent = target.WorldTransform * Matrix4x4F.Invert(_presenter.WorldTransform);
        var size = target.RenderSize;
        var p0 = Vector3F.TransformCoordinate(new Vector3F(0f, 0f, 0f), toContent);
        var p1 = Vector3F.TransformCoordinate(new Vector3F((float)size.Width, (float)size.Height, 0f), toContent);
        var off = _presenter.Offset;
        BringIntoView(new Rect(
            Math.Min(p0.X, p1.X) + off.X, Math.Min(p0.Y, p1.Y) + off.Y,
            Math.Abs(p1.X - p0.X), Math.Abs(p1.Y - p0.Y)));
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_presenter == null) return;
        var step = (e.Delta / 120.0) * WheelLinesPerNotch * LineStep;

        var extent = _presenter.Extent;
        var viewport = _presenter.Viewport;
        var offset = ScrollOffset;
        var canV = _presenter.CanScrollVertically && extent.Height > viewport.Height + 0.5;
        var canH = _presenter.CanScrollHorizontally && extent.Width > viewport.Width + 0.5;

        // A HORIZONTAL (tilt) wheel scrolls X; a vertical wheel scrolls Y, or X when there's no vertical range (a
        // horizontally-scrolling list). `delta` is the signed move on the active axis, with current/max for edge testing.
        Vector2 by;
        double delta, current, max;
        if (e.IsHorizontal)
        {
            if (!canH) return;                         // horizontal wheel, nothing to scroll -> bubble
            delta = step;                              // tilt right (delta>0) -> scroll right (offset +)
            by = new Vector2(delta, 0); current = offset.X; max = extent.Width - viewport.Width;
        }
        else if (canV)
        {
            delta = -step;                             // wheel up (delta>0) -> scroll toward the top (offset -)
            by = new Vector2(0, delta); current = offset.Y; max = extent.Height - viewport.Height;
        }
        else if (canH)
        {
            delta = -step;                             // vertical wheel over a horizontally-scrolling list
            by = new Vector2(delta, 0); current = offset.X; max = extent.Width - viewport.Width;
        }
        else return;   // nothing scrollable here -> leave the wheel unhandled so a parent ScrollViewer takes it

        // Scroll chaining: at the edge in the scroll direction, DON'T handle - let the event bubble to a parent ScrollViewer
        // so a nested list hands off instead of dead-ending under the cursor. Disabled -> classic (always swallow).
        if (GetScrollChaining(this))
        {
            var atEdge = delta > 0 ? current >= max - 0.5 : current <= 0.5;
            if (atEdge) return;   // e.Handled stays false -> bubbles up
        }

        _presenter.AnimateScrollBy(by);   // smooth (eased) wheel; instant if inertia is off
        e.Handled = true;
    }

    private static Visibility ComputeVisibility(ScrollBarVisibility mode, double extent, double viewport) => mode switch
    {
        ScrollBarVisibility.Visible => Visibility.Visible,
        ScrollBarVisibility.Auto => extent > viewport + 0.5 ? Visibility.Visible : Visibility.Collapsed,
        _ => Visibility.Collapsed   // Disabled, Hidden: no bar (Hidden still scrolls via wheel/programmatic)
    };
}
