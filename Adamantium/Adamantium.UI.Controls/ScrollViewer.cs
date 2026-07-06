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

    public static readonly AdamantiumProperty HorizontalScrollBarVisibilityProperty = AdamantiumProperty.Register(
        nameof(HorizontalScrollBarVisibility), typeof(ScrollBarVisibility), typeof(ScrollViewer),
        new PropertyMetadata(ScrollBarVisibility.Auto, PropertyMetadataOptions.AffectsMeasure, OnScrollBarVisibilityChanged));

    public static readonly AdamantiumProperty VerticalScrollBarVisibilityProperty = AdamantiumProperty.Register(
        nameof(VerticalScrollBarVisibility), typeof(ScrollBarVisibility), typeof(ScrollViewer),
        new PropertyMetadata(ScrollBarVisibility.Auto, PropertyMetadataOptions.AffectsMeasure, OnScrollBarVisibilityChanged));

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

    private void DetachParts()
    {
        if (_presenter != null) { _presenter.ScrollMetricsChanged -= OnScrollMetricsChanged; _presenter = null; }
        if (_verticalBar != null) { _verticalBar.ValueChanged -= OnBarValueChanged; _verticalBar = null; }
        if (_horizontalBar != null) { _horizontalBar.ValueChanged -= OnBarValueChanged; _horizontalBar = null; }
    }

    // Content metrics changed (resize, scroll): push them onto the bars (Maximum/ViewportSize/Value) and re-evaluate
    // Auto visibility. Guarded so setting Value here doesn't bounce back through OnBarValueChanged into the presenter.
    private void OnScrollMetricsChanged(object sender, EventArgs e)
    {
        if (_presenter == null) return;
        var extent = _presenter.Extent;
        var viewport = _presenter.Viewport;
        var offset = _presenter.Offset;

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
        var delta = -(e.Delta / 120.0) * WheelLinesPerNotch * LineStep;   // wheel up -> scroll towards the top
        _presenter.AnimateScrollBy(new Vector2(0, delta));   // smooth (eased) wheel; instant if inertia is off
        e.Handled = true;
    }

    private static Visibility ComputeVisibility(ScrollBarVisibility mode, double extent, double viewport) => mode switch
    {
        ScrollBarVisibility.Visible => Visibility.Visible,
        ScrollBarVisibility.Auto => extent > viewport + 0.5 ? Visibility.Visible : Visibility.Collapsed,
        _ => Visibility.Collapsed   // Disabled, Hidden: no bar (Hidden still scrolls via wheel/programmatic)
    };
}
