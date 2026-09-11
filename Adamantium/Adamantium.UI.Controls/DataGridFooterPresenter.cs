using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls;

/// <summary>The strip of totals under the table. Placed by the SAME numbers the rows and the header use - the grid's
/// one width pass - so a total can never stand under another column.</summary>
public class DataGridFooterPresenter : Panel
{
    /// <summary>The strip scrolls sideways with the columns, so a total that has slid past the left edge is CUT there
    /// rather than drawn over the table's frame - see <see cref="DataGridColumnHeader"/> for why this is set here and
    /// not through OverrideMetadata.</summary>
    public DataGridFooterPresenter()
    {
        ClipToBounds = true;
    }

    private TreeDataGrid _owner;

    /// <summary>The grid whose totals this strip shows. Setting it REGISTERS the strip with that grid, which is what
    /// lets a re-count reach it.</summary>
    public TreeDataGrid Owner
    {
        get => _owner;
        internal set
        {
            if (ReferenceEquals(_owner, value)) return;
            _owner = value;
            _owner?.AdoptFooter(this);
        }
    }

    private readonly Dictionary<int, DataGridFooterCell> _cells = new();
    private readonly List<int> _leaving = new();

    internal void Sync()
    {
        var columns = Owner?.Columns;
        var count = columns?.Count ?? 0;

        _leaving.Clear();
        foreach (var pair in _cells)
        {
            // A column the table is GROUPED BY leaves the strip with the rest of it - see DataGridColumn.IsShown.
            if (pair.Key >= count || !columns[pair.Key].IsShown) _leaving.Add(pair.Key);
        }

        foreach (var index in _leaving)
        {
            Children.Remove(_cells[index]);
            _cells.Remove(index);
        }

        for (var i = 0; i < count; i++)
        {
            if (!columns[i].IsShown) continue;

            if (!_cells.TryGetValue(i, out var cell))
            {
                cell = new DataGridFooterCell();
                _cells[i] = cell;
                Children.Add(cell);
            }

            var column = columns[i];
            var total = Owner.TotalFor(column);
            cell.HasTotal = column.Aggregate != DataGridAggregate.None;
            cell.Content = DataGridTotals.Text(column, total);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Sync();

        var columns = Owner?.Columns;
        if (columns == null || columns.Count == 0) return new Size();

        double height = 0;
        foreach (var pair in _cells)
        {
            if (pair.Key >= columns.Count) continue;

            pair.Value.Measure(new Size(columns[pair.Key].ActualWidth, availableSize.Height));
            height = System.Math.Max(height, pair.Value.DesiredSize.Height);
        }

        return new Size(Owner.ColumnsWidth, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = Owner?.Columns;
        if (columns == null) return finalSize;

        // The same three placements the header strip uses, for the same reason: the strip sits outside the rows'
        // scroller and carries the offset itself, and a pinned column is not subject to it.
        var offset = Owner.HorizontalOffset;
        foreach (var pair in _cells)
        {
            if (pair.Key >= columns.Count) continue;

            var column = columns[pair.Key];
            var x = column.IsFrozenLeft ? column.Offset
                : column.IsFrozenRight ? column.Offset + Owner.RightPinShift - offset
                : column.Offset - offset;
            pair.Value.Arrange(new Rect(x, 0, column.ActualWidth, finalSize.Height));
        }

        return finalSize;
    }
}
