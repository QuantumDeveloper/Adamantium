using System;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Animation;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>
/// A zoom + pan viewport. Hosts <see cref="ContentControl.Content"/> at a bindable scale inside a
/// <see cref="ScrollViewer"/>: the scale is a <see cref="UIComponent.LayoutTransform"/>, so it PARTICIPATES in layout and
/// the scrollable extent grows with the zoom - scrollbars and drag-pan track the zoomed size (a render-only scale would
/// not). Built-in gestures: the wheel zooms toward the cursor, dragging pans. All state is bindable two-way:
/// <see cref="ScaleX"/>/<see cref="ScaleY"/> and <see cref="OffsetX"/>/<see cref="OffsetY"/>. Scenario: a fixed-size
/// window onto large content (a map), zoomed and scrolled inside.
/// </summary>
public class ZoomBox : ContentControl
{
    private ScrollViewer _scroll;
    private ContentPresenter _surface;
    private readonly Transform _scale = new();   // reused as the surface's LayoutTransform (a fresh one would re-promote each change)
    private bool _syncingOffset;                 // guards the ScrollViewer <-> OffsetX/Y two-way sync from bouncing
    private Vector2? _pendingOffset;             // zoom-to-cursor target, applied once the extent reflects the new scale

    // Smooth zoom: the wheel accumulates a TARGET scale, and a heartbeat ticker eases the live scale toward it (instead of
    // jumping per event), keeping the anchor content-point under the cursor the whole way.
    private double _targetScale = 1;
    private bool _zoomActive;
    private bool _zoomTickerRegistered;
    private Vector2 _zoomAnchorViewport;
    private Vector2 _zoomAnchorContent;

    public static readonly AdamantiumProperty ScaleXProperty = AdamantiumProperty.Register(nameof(ScaleX),
        typeof(double), typeof(ZoomBox),
        new PropertyMetadata(1.0, PropertyMetadataOptions.BindsTwoWayByDefault, OnScaleChanged));

    public static readonly AdamantiumProperty ScaleYProperty = AdamantiumProperty.Register(nameof(ScaleY),
        typeof(double), typeof(ZoomBox),
        new PropertyMetadata(1.0, PropertyMetadataOptions.BindsTwoWayByDefault, OnScaleChanged));

    public static readonly AdamantiumProperty MinScaleProperty = AdamantiumProperty.Register(nameof(MinScale),
        typeof(double), typeof(ZoomBox), new PropertyMetadata(0.1));

    public static readonly AdamantiumProperty MaxScaleProperty = AdamantiumProperty.Register(nameof(MaxScale),
        typeof(double), typeof(ZoomBox), new PropertyMetadata(10.0));

    public static readonly AdamantiumProperty ZoomStepProperty = AdamantiumProperty.Register(nameof(ZoomStep),
        typeof(double), typeof(ZoomBox), new PropertyMetadata(1.2));

    public static readonly AdamantiumProperty ZoomSmoothRateProperty = AdamantiumProperty.Register(nameof(ZoomSmoothRate),
        typeof(double), typeof(ZoomBox), new PropertyMetadata(10.0));

    public static readonly AdamantiumProperty ZoomWithWheelProperty = AdamantiumProperty.Register(nameof(ZoomWithWheel),
        typeof(bool), typeof(ZoomBox), new PropertyMetadata(true));

    public static readonly AdamantiumProperty OffsetXProperty = AdamantiumProperty.Register(nameof(OffsetX),
        typeof(double), typeof(ZoomBox),
        new PropertyMetadata(0.0, PropertyMetadataOptions.BindsTwoWayByDefault, OnOffsetChanged));

    public static readonly AdamantiumProperty OffsetYProperty = AdamantiumProperty.Register(nameof(OffsetY),
        typeof(double), typeof(ZoomBox),
        new PropertyMetadata(0.0, PropertyMetadataOptions.BindsTwoWayByDefault, OnOffsetChanged));

    public static readonly AdamantiumProperty PanningModeProperty = AdamantiumProperty.Register(nameof(PanningMode),
        typeof(PanningMode), typeof(ZoomBox), new PropertyMetadata(PanningMode.Both, OnPanningModeChanged));

    /// <summary>Horizontal zoom factor (1 = 100%). Bindable two-way.</summary>
    public double ScaleX { get => GetValue<double>(ScaleXProperty); set => SetValue(ScaleXProperty, value); }

    /// <summary>Vertical zoom factor (1 = 100%). Bindable two-way.</summary>
    public double ScaleY { get => GetValue<double>(ScaleYProperty); set => SetValue(ScaleYProperty, value); }

    /// <summary>Lower zoom clamp for the wheel gesture. Default 0.1.</summary>
    public double MinScale { get => GetValue<double>(MinScaleProperty); set => SetValue(MinScaleProperty, value); }

    /// <summary>Upper zoom clamp for the wheel gesture. Default 10.</summary>
    public double MaxScale { get => GetValue<double>(MaxScaleProperty); set => SetValue(MaxScaleProperty, value); }

    /// <summary>Zoom multiplier per wheel notch. Default 1.2.</summary>
    public double ZoomStep { get => GetValue<double>(ZoomStepProperty); set => SetValue(ZoomStepProperty, value); }

    /// <summary>Wheel-zoom ease-to-target rate per second - higher snaps to the target scale sooner, lower glides longer.
    /// Default 10.</summary>
    public double ZoomSmoothRate { get => GetValue<double>(ZoomSmoothRateProperty); set => SetValue(ZoomSmoothRateProperty, value); }

    /// <summary>When true (default) the wheel zooms toward the cursor; when false it falls through to normal wheel-scroll.</summary>
    public bool ZoomWithWheel { get => GetValue<bool>(ZoomWithWheelProperty); set => SetValue(ZoomWithWheelProperty, value); }

    /// <summary>Current horizontal pan offset (content-space). Bindable two-way; also driven by drag/wheel.</summary>
    public double OffsetX { get => GetValue<double>(OffsetXProperty); set => SetValue(OffsetXProperty, value); }

    /// <summary>Current vertical pan offset (content-space). Bindable two-way; also driven by drag/wheel.</summary>
    public double OffsetY { get => GetValue<double>(OffsetYProperty); set => SetValue(OffsetYProperty, value); }

    /// <summary>Which axes a content drag pans. Default <see cref="PanningMode.Both"/>.</summary>
    public PanningMode PanningMode { get => GetValue<PanningMode>(PanningModeProperty); set => SetValue(PanningModeProperty, value); }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        // Wheel = zoom, on the TUNNELLING PreviewMouseWheel: it fires on us (an ancestor) BEFORE the inner ScrollViewer's
        // bubbling wheel-scroll, and setting Handled then suppresses that scroll (Preview + Main share one args). Subscribed
        // per template (undone in OnRemoveTemplate) since the wheel only matters once the parts exist.
        PreviewMouseWheel += OnWheel;

        _scroll = GetTemplateChild("PART_Scroll") as ScrollViewer;
        _surface = GetTemplateChild("PART_Surface") as ContentPresenter;

        if (_surface != null) _surface.LayoutTransform = _scale;
        if (_scroll != null)
        {
            _scroll.PanningMode = PanningMode;
            _scroll.ScrollChanged += OnScrollChanged;
        }
        ApplyScale();
    }

    public override void OnRemoveTemplate()
    {
        base.OnRemoveTemplate();
        PreviewMouseWheel -= OnWheel;
        if (_scroll != null) _scroll.ScrollChanged -= OnScrollChanged;
    }

    private static void OnScaleChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e) => (d as ZoomBox)?.ApplyScale();

    // Mirror ScaleX/ScaleY onto the surface's LayoutTransform; being a layout transform, this re-measures the surface and
    // grows (or shrinks) the scrollable extent.
    private void ApplyScale()
    {
        _scale.ScaleX = ScaleX;
        _scale.ScaleY = ScaleY;
    }

    private static void OnPanningModeChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
    {
        if (d is ZoomBox z && z._scroll != null) z._scroll.PanningMode = z.PanningMode;
    }

    private static void OnOffsetChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
    {
        if (d is not ZoomBox z || z._scroll == null || z._syncingOffset) return;
        z._scroll.SetScrollOffset(new Vector2((float)z.OffsetX, (float)z.OffsetY));
    }

    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        if (!ZoomWithWheel || _scroll == null || _surface == null) return;
        e.Handled = true; // consumed as zoom, not scroll

        // ZoomStep is the factor for ONE standard notch (120); raise it to Delta/120 so the zoom is proportional to how far
        // the wheel actually turned (a hi-res / inertial wheel firing many fractional-delta events then zooms the same total
        // as a standard wheel). Accumulate into the TARGET so quick spins add up; the ticker eases the live scale to it.
        var factor = Math.Pow(ZoomStep, e.Delta / 120.0);
        var basis = _zoomActive ? _targetScale : ScaleX;
        _targetScale = Math.Clamp(basis * factor, MinScale, MaxScale);

        // Anchor the content point currently under the cursor (and its viewport position) so the ease keeps it fixed.
        _zoomAnchorViewport = e.GetPosition(_scroll);
        _zoomAnchorContent = (_scroll.ScrollOffset + _zoomAnchorViewport) * (float)(1.0 / ScaleX);

        _zoomActive = true;
        if (!_zoomTickerRegistered)
        {
            _zoomTickerRegistered = true;
            AnimationManager.AddTicker(AdvanceZoom);
        }
    }

    // One eased zoom step: move the live scale a fraction toward the target, re-anchoring the offset so the content point
    // under the cursor stays put. Returns true (dropping the ticker) once the target is reached.
    private bool AdvanceZoom(double dt)
    {
        if (!_zoomActive || _scroll == null || _surface == null) { _zoomTickerRegistered = false; return true; }

        var cur = ScaleX;
        var next = cur + (_targetScale - cur) * (1.0 - Math.Exp(-ZoomSmoothRate * dt));
        if (Math.Abs(_targetScale - next) < _targetScale * 1e-3) next = _targetScale;

        // Offset that keeps the anchor content-point at the cursor for THIS scale; applied once the extent reflects it
        // (see OnScrollChanged), so it clamps against the grown extent, not the old one.
        _pendingOffset = _zoomAnchorContent * (float)next - _zoomAnchorViewport;
        SetCurrentValue(ScaleXProperty, next);
        SetCurrentValue(ScaleYProperty, next);

        var done = next == _targetScale;
        if (done) { _zoomActive = false; _zoomTickerRegistered = false; }
        return done;
    }

    private void OnScrollChanged(object sender, EventArgs e)
    {
        // A scale change grew the extent and raised this; the clamp now uses the NEW extent, so the zoom-to-cursor target
        // lands correctly (applying it in the wheel handler would clamp against the OLD, smaller extent).
        if (_pendingOffset is { } target)
        {
            _pendingOffset = null;
            _scroll.SetScrollOffset(target);
            return;   // that set re-raises ScrollChanged; the offset sync runs then
        }

        _syncingOffset = true;
        var off = _scroll.ScrollOffset;
        SetCurrentValue(OffsetXProperty, (double)off.X);
        SetCurrentValue(OffsetYProperty, (double)off.Y);
        _syncingOffset = false;
    }

    /// <summary>Resets zoom to 100% and scroll to the origin.</summary>
    public void Reset()
    {
        SetCurrentValue(ScaleXProperty, 1.0);
        SetCurrentValue(ScaleYProperty, 1.0);
        _scroll?.SetScrollOffset(default);
    }
}
