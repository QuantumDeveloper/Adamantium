using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;

namespace Adamantium.UI.Controls.Panels;

/// <summary>The items host of a <see cref="RibbonGroup"/>: commands packed into COLUMNS. A <see cref="RibbonSize.Large"/>
/// one fills a column alone, smaller ones stack up to <see cref="RowsPerColumn"/> deep, a <see cref="Separator"/> takes
/// a column and ends the run beside it. Each command's drawn size is decided here too.</summary>
public class RibbonGroupPanel : Panel
{
    public static readonly AdamantiumProperty RowsPerColumnProperty = AdamantiumProperty.Register(nameof(RowsPerColumn),
        typeof(int), typeof(RibbonGroupPanel),
        new PropertyMetadata(3, PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));

    /// <summary>How many non-large commands stack in one column. Three is the Office metric the theme is drawn to.</summary>
    public int RowsPerColumn
    {
        get => GetValue<int>(RowsPerColumnProperty);
        set => SetValue(RowsPerColumnProperty, value);
    }

    // The partition the last measure produced, reused by arrange - re-deriving it there would be the rule written twice.
    private readonly List<List<IMeasurableComponent>> _columns = [];
    private readonly List<double> _columnWidths = [];

    /// <summary>Left/Right walk the columns, Up/Down walk within one - answered from the shape the panel laid out.</summary>
    public override IUIComponent Navigate(IUIComponent from, FocusNavigationDirection direction)
    {
        if (!IsArrow(direction)) return base.Navigate(from, direction);

        if (from is not IMeasurableComponent measurable) return null;

        var column = _columns.FindIndex(c => c.Contains(measurable));
        if (column < 0) return null;

        var row = _columns[column].IndexOf(measurable);
        var forward = IsForward(direction);

        if (IsVertical(direction))
        {
            var next = row + (forward ? 1 : -1);
            return next >= 0 && next < _columns[column].Count ? _columns[column][next] : null;
        }

        var nextColumn = column + (forward ? 1 : -1);
        if (nextColumn < 0 || nextColumn >= _columns.Count) return null;

        // Landing in a shorter column: the nearest row it actually has.
        var target = _columns[nextColumn];
        return target[Math.Min(row, target.Count - 1)];
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Repartition();

        double width = 0, height = 0;
        for (var i = 0; i < _columns.Count; i++)
        {
            double columnWidth = 0, columnHeight = 0;
            foreach (var child in _columns[i])
            {
                // Unbounded: a group asks for the room it wants; a measure constraint is not the viewport.
                child.Measure(Size.Infinity);
                columnWidth = Math.Max(columnWidth, child.DesiredSize.Width);
                columnHeight += child.DesiredSize.Height;
            }

            _columnWidths[i] = columnWidth;
            width += columnWidth;
            height = Math.Max(height, columnHeight);
        }

        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double x = 0;
        for (var i = 0; i < _columns.Count; i++)
        {
            // Centred in the band, not hung from the top: columns hold different numbers of commands, and a short one
            // stacked from y=0 floats above its neighbour.
            double total = 0;
            foreach (var child in _columns[i]) total += child.DesiredSize.Height;

            var y = Math.Max(0, (finalSize.Height - total) / 2);
            foreach (var child in _columns[i])
            {
                var childHeight = child.DesiredSize.Height;
                child.Arrange(new Rect(x, y, _columnWidths[i], childHeight));
                y += childHeight;
            }

            x += _columnWidths[i];
        }

        return new Size(x, finalSize.Height);
    }

    // Size each command, then cut the children into columns: a large one or a separator owns its column, anything else
    // joins the run being filled until it holds RowsPerColumn.
    private void Repartition()
    {
        _columns.Clear();
        _columnWidths.Clear();
        List<IMeasurableComponent> run = null;

        foreach (var child in Children)
        {
            if (child.Visibility != Visibility.Visible) continue;

            var size = ResolveSize(child);

            if (size == RibbonSize.Large || child is Separator)
            {
                run = null;
                Add([child]);
                continue;
            }

            if (run == null || run.Count >= Math.Max(1, RowsPerColumn))
            {
                run = [];
                Add(run);
            }

            run.Add(child);
        }

        void Add(List<IMeasurableComponent> column)
        {
            _columns.Add(column);
            _columnWidths.Add(0);
        }
    }

    // The largest size its author allows. Written only when it differs: the property is AffectsMeasure, and re-stating
    // it every pass would invalidate the measure we are inside.
    private static RibbonSize ResolveSize(IMeasurableComponent child)
    {
        var wanted = Ribbon.GetMaxSize(child);
        if (Ribbon.GetSize(child) != wanted) Ribbon.SetSize(child, wanted);
        return wanted;
    }
}
