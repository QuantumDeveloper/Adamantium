using System;
using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls;

/// <summary>
/// The default physical <see cref="IScrollableContent"/>: it hosts a <see cref="ScrollViewer"/>'s content, measures
/// it unbounded on the scrollable axes (so it reports its full extent), and shows a window onto it by translating the
/// child by -<see cref="Offset"/> and clipping to the viewport (<see cref="UIComponent.ClipToBounds"/> -&gt; a
/// renderer scissor). A virtualizing panel can replace it later by implementing the same seam.
/// </summary>
public class ScrollContentPresenter : ContentPresenter, IScrollableContent
{
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

    public event EventHandler ScrollMetricsChanged;

    public void SetOffset(Vector2 offset)
    {
        var clamped = ClampOffset(offset, _extent, _viewport);
        if (clamped == _offset) return;
        _offset = clamped;
        InvalidateArrange();   // reposition the child; the metrics event is raised from the arrange
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
