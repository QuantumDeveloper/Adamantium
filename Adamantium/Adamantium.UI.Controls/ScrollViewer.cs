using System;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;

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
        new PropertyMetadata(ScrollBarVisibility.Auto, PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty VerticalScrollBarVisibilityProperty = AdamantiumProperty.Register(
        nameof(VerticalScrollBarVisibility), typeof(ScrollBarVisibility), typeof(ScrollViewer),
        new PropertyMetadata(ScrollBarVisibility.Auto, PropertyMetadataOptions.AffectsMeasure));

    public ScrollViewer()
    {
        MouseWheel += OnMouseWheel;
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

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_presenter == null) return;
        var offset = _presenter.Offset;
        var delta = -(e.Delta / 120.0) * WheelLinesPerNotch * LineStep;   // wheel up -> scroll towards the top
        _presenter.SetOffset(new Vector2(offset.X, offset.Y + delta));
        e.Handled = true;
    }

    private static Visibility ComputeVisibility(ScrollBarVisibility mode, double extent, double viewport) => mode switch
    {
        ScrollBarVisibility.Visible => Visibility.Visible,
        ScrollBarVisibility.Auto => extent > viewport + 0.5 ? Visibility.Visible : Visibility.Collapsed,
        _ => Visibility.Collapsed   // Disabled, Hidden: no bar (Hidden still scrolls via wheel/programmatic)
    };
}
