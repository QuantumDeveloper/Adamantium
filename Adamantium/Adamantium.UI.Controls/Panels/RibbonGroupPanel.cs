using System;
using System.Collections.Generic;
using System.Linq;
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
        // Until something CHOOSES, the commands are drawn the way their authors asked.
        if (_applied < 0) Apply(0);

        return MeasurePacked();
    }

    // Cut into columns and measured unbounded: a measure constraint is not the viewport.
    private Size MeasurePacked()
    {
        Repartition();

        double width = 0, height = 0;
        for (var i = 0; i < _columns.Count; i++)
        {
            double columnWidth = 0, columnHeight = 0;
            foreach (var child in _columns[i])
            {
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
        // The BLOCK is centred and every column starts on that one line - centring each column separately left a
        // one-command column floating at the middle beside a three-command one.
        double tallest = 0;
        for (var i = 0; i < _columns.Count; i++)
        {
            double total = 0;
            foreach (var child in _columns[i]) total += child.DesiredSize.Height;
            tallest = Math.Max(tallest, total);
        }

        var top = Math.Max(0, (finalSize.Height - tallest) / 2);

        double x = 0;
        for (var i = 0; i < _columns.Count; i++)
        {
            var y = top;
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

    // --- Variants: the ways this group can be drawn, and what each of them costs in width ---------------------------

    private RibbonGroupVariant[] _variants;
    private double[] _variantWidths;

    // Our own size writes must not drop the cache they are keyed by. Depth-counted: a probe applies inside itself.
    private int _sizingSelf;

    /// <summary>The ways this group can be drawn, roomiest first - derived from what its commands allow.</summary>
    public IReadOnlyList<RibbonGroupVariant> Variants => _variants ??= BuildVariants();

    /// <summary>How wide this group is at <paramref name="index"/>. Kept, because the search asks repeatedly. NaN for
    /// the collapsed variant - that one is a button the theme draws.</summary>
    public double WidthAt(int index)
    {
        var variants = Variants;
        if (variants[index].IsCollapsed) return double.NaN;

        _variantWidths ??= NotMeasured(variants.Count);
        if (!double.IsNaN(_variantWidths[index])) return _variantWidths[index];

        // A PROBE leaves no trace: sizes restored AND re-measured, because MeasurePacked also derives the columns, and
        // those would otherwise stay at the probed variant's.
        var drawn = _applied;
        _sizingSelf++;
        try
        {
            Apply(index);
            _variantWidths[index] = MeasurePacked().Width;

            Apply(drawn);
            MeasurePacked();
        }
        finally
        {
            _sizingSelf--;
        }

        return _variantWidths[index];
    }

    // Only so a probe can put the sizes back. Which variant the group IS at is the group's own business.
    private int _applied = -1;   // nothing applied yet

    /// <summary>Draw the group as <paramref name="index"/> from now on.</summary>
    public void Apply(int index)
    {
        // The steps are rebuilt when the commands change.
        index = Math.Min(Math.Max(index, 0), Variants.Count - 1);
        _applied = index;

        var sizes = Variants[index].Sizes;
        if (sizes.Count == 0) return;

        _sizingSelf++;
        try
        {
            var at = 0;
            foreach (var child in SizedChildren())
            {
                if (at >= sizes.Count) break;

                    if (Ribbon.GetSize(child) != sizes[at]) Ribbon.SetSize(child, sizes[at]);
                at++;
            }
        }
        finally
        {
            _sizingSelf--;
        }
    }

    // A separator has no size to state, and counting it would generate variants that change nothing.
    private IEnumerable<IMeasurableComponent> SizedChildren() =>
        Children.Where(c => c.Visibility == Visibility.Visible && c is not Separator);

    private RibbonGroupVariant[] BuildVariants()
    {
        var commands = SizedChildren()
            .Select(c => (Ribbon.GetMaxSize(c), Ribbon.GetCollapseToMedium(c), Ribbon.GetCollapseToSmall(c)))
            .ToList();

        return RibbonGroupVariant.Generate(commands).ToArray();
    }

    private static double[] NotMeasured(int count)
    {
        var widths = new double[count];
        Array.Fill(widths, double.NaN);
        return widths;
    }

    // A command added, a label rebound, a font changed: the measured widths are about content that is gone.
    public override void InvalidateMeasure()
    {
        if (_sizingSelf == 0)
        {
            _variants = null;
            _variantWidths = null;
        }

        base.InvalidateMeasure();
    }

    // A large command or a separator owns its column; anything else joins the run until it holds RowsPerColumn.
    private void Repartition()
    {
        _columns.Clear();
        _columnWidths.Clear();
        List<IMeasurableComponent> run = null;

        foreach (var child in Children)
        {
            if (child.Visibility != Visibility.Visible) continue;

            var size = Ribbon.GetSize(child);

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
}
