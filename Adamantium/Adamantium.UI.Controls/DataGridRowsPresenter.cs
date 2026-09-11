using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls;

/// <summary>The rows' host: a vertical virtualizing stack that also tells the scroller how WIDE the table is.
/// <para>It has to, because a row cannot: a desired size is clamped to the slot it was measured in, so a row asked to
/// measure inside a 600-pixel viewport reports 600 however many columns it holds, and the scroller concludes there is
/// nothing to scroll sideways. The columns' total is the one honest number, and the grid already has it.</para></summary>
public class DataGridRowsPresenter : StackPanel
{
    /// <summary>The grid this panel hosts the rows of, found once through the visual tree.</summary>
    public TreeDataGrid Owner
    {
        get
        {
            if (_owner != null) return _owner;
            for (IUIComponent node = this; node != null; node = node.VisualParent)
            {
                if (node is TreeDataGrid grid) return _owner = grid;
            }

            return null;
        }
    }

    private TreeDataGrid _owner;

    /// <summary>The rows that are not rows: a record's details panel stands at its own height while everything else
    /// takes the row height. There are a handful of them at most - a table is opened at three records, not ten
    /// thousand - which is why the stack stays uniform arithmetic with a short list of exceptions rather than a walk.</summary>
    protected override IReadOnlyList<(int Index, double Extra)> ItemExtentExceptions =>
        Owner?.RowExtentExceptions;

    protected override Size MeasureVirtualized(Size availableSize, Vector2 offset)
    {
        var extent = base.MeasureVirtualized(availableSize, offset);
        var width = Owner?.ColumnsWidth ?? 0;
        return width > extent.Width ? new Size(width, extent.Height) : extent;
    }

    protected override void ArrangeVirtualized(Size finalSize, Vector2 offset)
    {
        var width = Owner?.ColumnsWidth ?? 0;
        base.ArrangeVirtualized(width > finalSize.Width ? new Size(width, finalSize.Height) : finalSize, offset);
    }
}
