using System;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;

namespace Adamantium.UI.Controls.Panels;

/// <summary>The items host of a <see cref="RibbonGallery"/>: equal cells in a grid of <see cref="Columns"/> columns, of
/// which <see cref="Rows"/> are shown at a time starting at <see cref="FirstRow"/>. Equal cells and not a WrapPanel:
/// a gallery is a grid of CHOICES, and choices that shift sideways because one thumbnail is wider cannot be compared.</summary>
public class RibbonGalleryPanel : Panel
{
    public static readonly AdamantiumProperty ColumnsProperty = AdamantiumProperty.Register(nameof(Columns),
        typeof(int), typeof(RibbonGalleryPanel),
        new PropertyMetadata(5, PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));

    public static readonly AdamantiumProperty RowsProperty = AdamantiumProperty.Register(nameof(Rows),
        typeof(int), typeof(RibbonGalleryPanel),
        new PropertyMetadata(1, PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));

    public static readonly AdamantiumProperty FirstRowProperty = AdamantiumProperty.Register(nameof(FirstRow),
        typeof(int), typeof(RibbonGalleryPanel),
        new PropertyMetadata(0, PropertyMetadataOptions.AffectsArrange));

    /// <summary>How many choices stand side by side.</summary>
    public int Columns
    {
        get => GetValue<int>(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <summary>How many rows are shown at once. The rest are reached with the scroll arrows or in the drop-down.</summary>
    public int Rows
    {
        get => GetValue<int>(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    /// <summary>The topmost shown row. Rows, not pixels: a half-row reads as damage, exactly as a half-group does in
    /// <see cref="RibbonGroupsPanel"/>.</summary>
    public int FirstRow
    {
        get => GetValue<int>(FirstRowProperty);
        set => SetValue(FirstRowProperty, value);
    }

    /// <summary>How many rows the items make at the current <see cref="Columns"/> - what the gallery's arrows are
    /// decided by.</summary>
    public int RowCount => (Visible() + Math.Max(1, Columns) - 1) / Math.Max(1, Columns);

    private double _cellWidth;
    private double _cellHeight;

    /// <summary>A GRID: the generic order-based walk would make Right at the end of a row jump a row down without
    /// Down ever working.</summary>
    public override IUIComponent Navigate(IUIComponent from, FocusNavigationDirection direction)
    {
        if (!IsArrow(direction)) return base.Navigate(from, direction);

        if (from is not IMeasurableComponent measurable) return null;

        var index = Children.IndexOf(measurable);
        if (index < 0) return null;

        var columns = Math.Max(1, Columns);
        var step = IsVertical(direction) ? columns : 1;
        var next = index + (IsForward(direction) ? step : -step);

        // Left/Right stay inside the row - leaving it is what Tab is for.
        if (!IsVertical(direction) && next / columns != index / columns) return null;

        return next >= 0 && next < Children.Count ? Children[next] : null;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _cellWidth = 0;
        _cellHeight = 0;
        foreach (var child in Children)
        {
            if (child.Visibility != Visibility.Visible) continue;

            child.Measure(Size.Infinity);
            _cellWidth = Math.Max(_cellWidth, child.DesiredSize.Width);
            _cellHeight = Math.Max(_cellHeight, child.DesiredSize.Height);
        }

        var columns = Math.Min(Math.Max(1, Columns), Math.Max(1, Visible()));
        var rows = Math.Min(Math.Max(1, Rows), Math.Max(1, RowCount));

        return new Size(columns * _cellWidth, rows * _cellHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = Math.Max(1, Columns);
        var top = FirstRow * _cellHeight;

        var at = 0;
        foreach (var child in Children)
        {
            if (child.Visibility != Visibility.Visible) continue;

            // Every cell is arranged, scrolled-away rows included: the gallery clips them. Arranging them to nothing
            // would make their measured width zero, and a zero always fits - the same trap the quick-access overflow hit.
            child.Arrange(new Rect(at % columns * _cellWidth, at / columns * _cellHeight - top, _cellWidth, _cellHeight));
            at++;
        }

        return finalSize;
    }

    private int Visible()
    {
        var count = 0;
        foreach (var child in Children)
        {
            if (child.Visibility == Visibility.Visible) count++;
        }

        return count;
    }
}
