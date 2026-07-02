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

    private double _offset;      // how far the strip is panned along the axis
    private double _extent;      // the child's length along the axis (from measure)
    private double _viewport;    // our own length along the axis (from arrange)

    public TabStripScroller()
    {
        ClipToBounds = true;
        MouseWheel += OnMouseWheel;
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

        // Give the strip unbounded room along the axis so it lays out at full length (never shrinks); constrain the cross.
        var probe = IsHorizontal
            ? new Size(double.PositiveInfinity, availableSize.Height)
            : new Size(availableSize.Width, double.PositiveInfinity);
        child.Measure(probe);
        var d = child.DesiredSize;
        _extent = IsHorizontal ? d.Width : d.Height;

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
            var rect = IsHorizontal
                ? new Rect(-_offset, 0, Math.Max(_extent, finalSize.Width), finalSize.Height)
                : new Rect(0, -_offset, finalSize.Width, Math.Max(_extent, finalSize.Height));
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

    // IContainer: the AUML loader nests the ItemsPresenter as Child.
    public void AddOrSetChildComponent(object component) { if (component is IMeasurableComponent c) Child = c; }
    public void RemoveAllChildComponents() => Child = null;
    public IReadOnlyList<object> GetChildComponents() => Child != null ? [Child] : [];
    public void InsertChildComponent(int index, object component) { if (component is IMeasurableComponent c) Child = c; }
    public void RemoveChildComponentAt(int index) => Child = null;
}
