using System;
using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;

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

    public ScrollContentPresenter()
    {
        // The whole point of the presenter: the overflowing content is scissored to this viewport (see the renderer's
        // per-unit clip, which intersects every ClipToBounds ancestor).
        ClipToBounds = true;
    }

    public Size Extent => _extent;

    public Size Viewport => _viewport;

    public Vector2 Offset => _offset;

    public bool CanScrollHorizontally { get; set; } = true;

    public bool CanScrollVertically { get; set; } = true;

    /// <summary>Which axes a content drag pans (set by the <see cref="ScrollViewer"/>). None = drag does nothing.</summary>
    public PanningMode PanningMode { get; set; } = PanningMode.None;

    public event EventHandler ScrollMetricsChanged;

    public void SetOffset(Vector2 offset)
    {
        var clamped = ClampOffset(offset, _extent, _viewport);
        if (clamped == _offset) return;
        _offset = clamped;
        InvalidateArrange();   // reposition the child; the metrics event is raised from the arrange
    }

    // --- Pan: drag the content itself to scroll (the no-scrollbars / touch path) ---

    protected override void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.Handled || PanningMode == PanningMode.None) return;   // pan off, or interactive content took the press
        _isPanning = false;
        _panStartPoint = e.GetPosition(this);   // the presenter is the fixed viewport, so this space is stable
        _panStartOffset = _offset;
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
        SetOffset(_panStartOffset - delta);
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (IsMouseCaptured) ReleaseMouseCapture();
        if (_isPanning) e.Handled = true;   // it was a pan, not a click
        _isPanning = false;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // Measure the content unbounded on every axis the viewer allows scrolling, so it reports its natural extent;
        // constrained on a disabled axis so it fits. base (ContentPresenter) builds the content and measures it.
        var constraint = new Size(
            CanScrollHorizontally ? double.PositiveInfinity : availableSize.Width,
            CanScrollVertically ? double.PositiveInfinity : availableSize.Height);

        var extent = base.MeasureOverride(constraint);
        if (extent != _extent)
        {
            _extent = extent;
            RaiseMetricsChanged();
        }

        // Occupy the viewport, not the content (the content extent only on an axis the viewer left unbounded).
        return new Size(
            double.IsInfinity(availableSize.Width) ? extent.Width : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? extent.Height : availableSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _viewport = finalSize;
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

    private void RaiseMetricsChanged() => ScrollMetricsChanged?.Invoke(this, EventArgs.Empty);

    private static Vector2 ClampOffset(Vector2 offset, Size extent, Size viewport)
    {
        var maxX = Math.Max(0, extent.Width - viewport.Width);
        var maxY = Math.Max(0, extent.Height - viewport.Height);
        return new Vector2(Math.Clamp(offset.X, 0, maxX), Math.Clamp(offset.Y, 0, maxY));
    }
}
