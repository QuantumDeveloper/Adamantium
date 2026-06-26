using System;
using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Panels;

/// <summary>
/// Base for panels that can host an <see cref="ItemsControl"/>'s items with virtualization: it owns the whole mechanism
/// (the <see cref="IScrollableContent"/> seam, the realized window, realize/recycle through the generator, and the
/// measure/arrange dispatch) and leaves only the geometry to subclasses (StackPanel = 1D, WrapPanel = 2D). There is no
/// user "turn virtualization off" knob: as a plain container (no owner) it lays its <see cref="Panel.Children"/> out via
/// <see cref="MeasurePlain"/>/<see cref="ArrangePlain"/> exactly as before; as an items host it realizes only the visible
/// window. When given an unbounded extent on the scroll axis (no viewport) it realizes everything (the degenerate case)
/// and reports it via <see cref="OnNoViewport"/> instead of silently being slow.
/// </summary>
public abstract class VirtualizingPanel : Panel, IScrollableContent
{
    private Size _extent;
    private Size _viewport;
    private Vector2 _offset;

    /// <summary>The items control this panel hosts (set by the <see cref="ItemsPresenter"/>); null = plain container.</summary>
    internal ItemsControl Owner { get; private set; }

    protected bool IsItemsHost => Owner != null;

    public Size Extent => _extent;
    public Size Viewport => _viewport;
    public Vector2 Offset => _offset;
    public bool CanScrollHorizontally { get; set; } = true;
    public bool CanScrollVertically { get; set; } = true;
    public event EventHandler ScrollMetricsChanged;

    /// <summary>Switches the panel into items-host mode for <paramref name="owner"/>; it now virtualizes its items.</summary>
    internal void AttachOwner(ItemsControl owner)
    {
        Children.Clear();   // drop any plain children; the window is managed via the generator from here
        Owner = owner;
        InvalidateMeasure();
    }

    /// <summary>Drops the realized window (e.g. the collection reset) so the next measure rebuilds it from scratch.</summary>
    internal void Revirtualize()
    {
        var generator = Owner?.ItemContainerGenerator;
        if (generator != null)
        {
            foreach (var index in generator.RealizedIndices.ToList())
                if (generator.ContainerFromIndex(index) is { } container)
                {
                    RemoveVisualChild(container);
                    RemoveLogicalChild(container);
                }
            generator.Clear();
        }
        InvalidateMeasure();
    }

    public void SetOffset(Vector2 offset)
    {
        var clamped = ClampOffset(offset, _extent, _viewport);
        if (clamped == _offset) return;
        _offset = clamped;
        InvalidateMeasure();   // a new window must be realized/measured
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (!IsItemsHost) return MeasurePlain(availableSize);

        _offset = ClampOffset(_offset, _extent, _viewport);
        var extent = MeasureVirtualized(availableSize, _offset);
        _extent = extent;
        // Occupy the viewport on a bounded axis, the extent on an axis the parent left unbounded.
        _viewport = new Size(
            double.IsInfinity(availableSize.Width) ? extent.Width : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? extent.Height : availableSize.Height);
        RaiseMetrics();
        return _viewport;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (!IsItemsHost) return ArrangePlain(finalSize);

        _viewport = finalSize;
        _offset = ClampOffset(_offset, _extent, finalSize);
        ArrangeVirtualized(finalSize, _offset);
        RaiseMetrics();
        return finalSize;
    }

    // Plain (non items-host) layout — the panel used as an ordinary container. Subclass = its existing measure/arrange.
    protected abstract Size MeasurePlain(Size availableSize);
    protected abstract Size ArrangePlain(Size finalSize);

    // Virtualized layout — realize/measure/arrange only the visible window (subclass owns the geometry).
    protected abstract Size MeasureVirtualized(Size availableSize, Vector2 offset);
    protected abstract void ArrangeVirtualized(Size finalSize, Vector2 offset);

    /// <summary>Realizes the container for <paramref name="index"/> and attaches it (visual + logical) if newly realized.</summary>
    protected IUIComponent RealizeInWindow(int index)
    {
        var generator = Owner.ItemContainerGenerator;
        var alreadyRealized = generator.ContainerFromIndex(index) != null;
        var container = generator.Realize(index);
        if (!alreadyRealized)
        {
            AddVisualChild(container);
            AddLogicalChild(container);
        }
        return container;
    }

    /// <summary>Recycles (and detaches) every realized container whose index falls outside <c>[first, last]</c>.</summary>
    protected void RecycleOutsideWindow(int first, int last)
    {
        var generator = Owner.ItemContainerGenerator;
        foreach (var index in generator.RealizedIndices.Where(i => i < first || i > last).ToList())
        {
            if (generator.ContainerFromIndex(index) is { } container)
            {
                RemoveVisualChild(container);
                RemoveLogicalChild(container);
            }
            generator.Recycle(index);
        }
    }

    /// <summary>Called when the scroll axis is unbounded (no viewport) so everything has to be realized. Override to log.</summary>
    protected virtual void OnNoViewport()
    {
        System.Diagnostics.Debug.WriteLine(
            $"[Adamantium] {GetType().Name} has no bounded viewport on its scroll axis - realizing all {Owner?.Items.Count} items (not virtualizing). Wrap the ItemsControl in a sized ScrollViewer.");
    }

    private void RaiseMetrics() => ScrollMetricsChanged?.Invoke(this, EventArgs.Empty);

    private static Vector2 ClampOffset(Vector2 offset, Size extent, Size viewport)
    {
        var maxX = Math.Max(0, extent.Width - viewport.Width);
        var maxY = Math.Max(0, extent.Height - viewport.Height);
        return new Vector2(Math.Clamp(offset.X, 0, maxX), Math.Clamp(offset.Y, 0, maxY));
    }
}
