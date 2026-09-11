using System;
using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;

namespace Adamantium.UI.Controls;

/// <summary>The strip of column headers. Placed by the SAME numbers the rows use - the grid's one width pass - so the
/// header can never drift from the body.
/// <para>It also owns the header gestures: click to sort, drag the separator to resize, double-click the separator to fit
/// the column to what is on screen.</para></summary>
public class DataGridHeadersPresenter : Panel
{
    /// <summary>The strip scrolls sideways with the columns, so a header that has slid past the left edge must be CUT
    /// there and not drawn over the table's own frame.
    /// <para>Set HERE and not through <c>OverrideMetadata</c> - see <see cref="DataGridColumnHeader"/> for why a type
    /// default never reaches the field the renderer reads.</para></summary>
    public DataGridHeadersPresenter()
    {
        ClipToBounds = true;
    }

    /// <summary>How close to a separator the pointer counts as being on it.</summary>
    private const double GripWidth = 6;

    private TreeDataGrid _owner;

    /// <summary>The grid whose columns this strip draws. Setting it REGISTERS the strip with that grid: the sort state
    /// is read in <see cref="MeasureOverride"/>, so the grid has to be able to say when it changed.</summary>
    public TreeDataGrid Owner
    {
        get => _owner;
        internal set
        {
            if (ReferenceEquals(_owner, value)) return;
            _owner = value;
            _owner?.AdoptHeaders(this);
        }
    }

    private readonly Dictionary<int, DataGridColumnHeader> _headers = new();
    private readonly List<int> _leaving = new();

    private int _resizing = -1;
    private double _resizeFrom;
    private double _startWidth;
    private int _pressed = -1;
    private int _dragging = -1;
    private int _dropTarget = -1;
    private Vector2 _pressAt;
    private DataGridRowHeader _corner;

    /// <summary>The corner above the number strip, or null while the table shows no numbers.</summary>
    internal DataGridRowHeader Corner => _corner;

    /// <summary>How far the pointer must travel before a press on a header becomes a REORDER rather than a click.</summary>
    private const double DragThreshold = 4;

    // The CORNER: the same control the number strip is made of, with no number in it. Pressing it takes the whole
    // table - the one gesture every spreadsheet has, and the reason the corner is a button at all.
    private void SyncCorner()
    {
        if (Owner?.ShowRowNumbers != true)
        {
            if (_corner != null) _corner.Visibility = Visibility.Collapsed;
            return;
        }

        if (_corner == null)
        {
            _corner = new DataGridRowHeader { IsCorner = true, ZIndex = 2 };
            _corner.MouseLeftButtonDown += OnCornerPressed;
            Children.Add(_corner);
        }

        _corner.Visibility = Visibility.Visible;
    }

    private void OnCornerPressed(object sender, MouseButtonEventArgs e)
    {
        Owner?.SelectAllCells();
        e.Handled = true;
    }

    internal void Sync()
    {
        SyncCorner();

        var columns = Owner?.Columns;
        var count = columns?.Count ?? 0;

        _leaving.Clear();
        foreach (var pair in _headers)
        {
            // A column the table is GROUPED BY leaves the strip with the rest of it - see DataGridColumn.IsShown.
            if (pair.Key >= count || !columns[pair.Key].IsShown) _leaving.Add(pair.Key);
        }

        foreach (var index in _leaving)
        {
            Children.Remove(_headers[index]);
            _headers.Remove(index);
        }

        for (var i = 0; i < count; i++)
        {
            if (!columns[i].IsShown) continue;

            if (!_headers.TryGetValue(i, out var header))
            {
                header = new DataGridColumnHeader();
                _headers[i] = header;
                Children.Add(header);
            }

            var column = columns[i];
            var direction = ReferenceEquals(Owner.SortColumn, column)
                ? Owner.SortDescending ? DataGridSortDirection.Descending : DataGridSortDirection.Ascending
                : DataGridSortDirection.None;
            header.Attach(column, i, direction, Owner);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Sync();

        var columns = Owner?.Columns;
        if (columns == null || columns.Count == 0) return new Size();

        double height = 0;
        foreach (var pair in _headers)
        {
            if (pair.Key >= columns.Count) continue;
            pair.Value.Measure(new Size(columns[pair.Key].ActualWidth, availableSize.Height));
            height = Math.Max(height, pair.Value.DesiredSize.Height);
        }

        return new Size(Owner.ColumnsWidth, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = Owner?.Columns;
        if (columns == null) return finalSize;

        // The strip sits OUTSIDE the rows' scroller, so it carries the sideways offset itself. One number, the grid's,
        // so a header can never stand over another column's cells.
        var offset = Owner.HorizontalOffset;
        foreach (var pair in _headers)
        {
            if (pair.Key >= columns.Count) continue;

            var column = columns[pair.Key];
            pair.Value.Arrange(new Rect(ScreenXOf(column, offset), 0, column.ActualWidth, finalSize.Height));
        }

        // ...and the corner sits over the number strip, which now begins where the details toggles end.
        if (_corner is { Visibility: Visibility.Visible })
        {
            _corner.Measure(new Size(Owner.RowNumberWidth, finalSize.Height));
            _corner.Arrange(new Rect(Owner.DetailsStripLeading, 0, Owner.RowNumberWidth, finalSize.Height));
        }

        return finalSize;
    }

    // Where a column's header actually stands in the strip. The strip carries the sideways offset itself, so a
    // LEFT-pinned header is simply not subject to it; a RIGHT-pinned one is slid back from the content's end by the
    // grid's shift, which already carries the scroll - so the strip's own offset comes off it again. ONE description,
    // used by the arrange and by both hit-tests: a pointer that worked the placement out differently from the layout
    // answered for whatever column happened to be that far along the content.
    private double ScreenXOf(DataGridColumn column, double offset) =>
        column.IsFrozenLeft ? column.Offset
        : column.IsFrozenRight ? column.Offset + Owner.RightPinShift - offset
        : column.Offset - offset;

    /// <summary>The column whose right-hand separator is under <paramref name="x"/>, or -1.</summary>
    internal int SeparatorAt(double x)
    {
        var columns = Owner?.Columns;
        if (columns == null) return -1;

        var offset = Owner.HorizontalOffset;
        for (var pinned = 0; pinned < 2; pinned++)
        {
            for (var i = 0; i < columns.Count; i++)
            {
                // PINNED headers first: they are drawn over what scrolls beneath them, so they are also what the
                // pointer reaches there.
                if (columns[i].IsFrozen != (pinned == 0)) continue;

                var edge = ScreenXOf(columns[i], offset) + columns[i].ActualWidth;
                if (Math.Abs(x - edge) <= GripWidth / 2) return i;
            }
        }

        return -1;
    }

    protected override void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(sender, e);
        if (Owner == null) return;

        var x = e.GetPosition(this).X;
        var separator = SeparatorAt(x);

        if (separator >= 0 && Owner.Columns[separator].CanUserResize)
        {
            // Double-click on a separator fits the column to what is on screen NOW - the only width it can honestly know,
            // since measuring the rows it has not built would mean building them.
            if (e.ClickCount >= 2)
            {
                Owner.FitColumn(separator);
                e.Handled = true;
                return;
            }

            _resizing = separator;
            _resizeFrom = x;
            _startWidth = Owner.Columns[separator].ActualWidth;
            CaptureMouse();
            e.Handled = true;
            return;
        }

        var header = HeaderAt(x);

        // The funnel answers for itself. Mouse-down is raised per element and does NOT share its Handled flag, so
        // without this check the press that opened the filter would sort the column on its way past.
        if (header >= 0 && _headers.TryGetValue(header, out var pressed) && pressed.PressedFilter(e.OriginalSource)) return;

        if (header < 0) return;

        // The press is only REMEMBERED here. Sorting happens on release, because the same press may turn out to be the
        // start of a drag - a header that sorted on the way down would sort every time a column was moved.
        _pressed = header;
        _pressAt = e.GetPosition(this);

        // CAPTURED from the press, not from the threshold: a header carried UP into the grouping strip leaves this
        // strip before it has travelled far enough to count as a drag, and without the capture the moves that would
        // have started it go to whatever is up there instead. Measured - eight moves, then LEAVE, and the gesture
        // simply stopped until the pointer came back.
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(object sender, MouseEventArgs e)
    {
        base.OnMouseMove(sender, e);
        if (Owner == null) return;

        if (_resizing < 0)
        {
            var x = e.GetPosition(this).X;

            if (_dragging >= 0)
            {
                UpdateReorder(x, Owner.IsOverGroupPanel(this, e.GetPosition(this)));
                return;
            }

            // HOW FAR the pointer has travelled, not how far ALONG the strip: a header dragged straight up into the
            // grouping panel moves no distance in x at all, and a threshold that only watched x left that gesture
            // doing nothing until the hand happened to waver sideways.
            if (_pressed >= 0 && (e.GetPosition(this) - _pressAt).Length() > DragThreshold
                && Owner.Columns[_pressed].CanUserReorder)
            {
                BeginReorder(_pressed, x);
                return;
            }

            // The separator advertises itself before the press, the way the splitter and the window's resize grip do.
            var over = SeparatorAt(x);
            Cursor = over >= 0 && Owner.Columns[over].CanUserResize ? Cursors.SizeEWE : Cursors.Arrow;
            return;
        }

        var column = Owner.Columns[_resizing];
        var wanted = _startWidth + (e.GetPosition(this).X - _resizeFrom);
        // At the slot the width already sits in, not a fresh Local one: a page can bind a column's width, and a Local
        // write outranks Binding for good - one drag and that binding would never be heard from again.
        column.SetCurrentValue(DataGridColumn.WidthProperty, new GridLength(Math.Max(column.MinWidth, wanted)));
        Owner.InvalidateColumns();
    }

    protected override void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(sender, e);

        if (_resizing >= 0)
        {
            _resizing = -1;
            ReleaseMouseCapture();
            return;
        }

        if (_dragging >= 0)
        {
            var at = e.GetPosition(this);
            EndReorder(at.X, Owner.IsOverGroupPanel(this, at));
        }
        else if (_pressed >= 0 && Owner != null && _pressed < Owner.Columns.Count && Owner.Columns[_pressed].CanSort)
        {
            var column = Owner.Columns[_pressed];
            var descending = ReferenceEquals(Owner.SortColumn, column) && !Owner.SortDescending;
            Owner.SortBy(column, descending);
        }

        // The press took the capture, so the press gives it back - whether it turned into a drag or stayed a click.
        if (_pressed >= 0) ReleaseMouseCapture();
        _pressed = -1;
    }

    /// <summary>Where a column dropped at <paramref name="x"/> would land: an index in 0..Count, counting BOUNDARIES
    /// rather than columns - dropping past the last header is a position of its own.</summary>
    internal int DropTargetAt(double x)
    {
        var columns = Owner?.Columns;
        if (columns == null || columns.Count == 0) return 0;

        x += Owner.HorizontalOffset;
        for (var i = 0; i < columns.Count; i++)
        {
            if (x < columns[i].Offset + columns[i].ActualWidth / 2) return i;
        }

        return columns.Count;
    }

    // The mark itself belongs to the GRID's template, not to this strip: what the user is aiming at is a place among the
    // ROWS, and a child of the header band cannot be drawn past the band's own edge.
    private void ShowDropLine(int target)
    {
        var columns = Owner?.Columns;
        if (columns == null || columns.Count == 0 || _dropTarget == target) return;

        _dropTarget = target;

        var at = target < columns.Count
            ? columns[target].Offset
            : columns[^1].Offset + columns[^1].ActualWidth;

        Owner.ShowDropIndicator(at - Owner.HorizontalOffset);
    }

    /// <summary>Picks a column up. The gesture is named rather than spelled out inside the mouse handler: what it does
    /// - mark the header, put the drop mark where it would land - is the same whether a pointer or a test asks.</summary>
    internal void BeginReorder(int column, double x)
    {
        _dragging = column;

        // An OVERRIDE, not this strip's own cursor: a carried header spends the drag OVER SOMETHING ELSE - the grouping
        // strip above, the rows below - and a per-element cursor is only ever applied for the element the pointer is
        // actually on. Set here it showed up only once the button came back up and the pointer settled on the strip.
        Mouse.OverrideCursor = Cursors.SizeAll;
        MarkDragged(_dragging, true);
        ShowDropLine(DropTargetAt(x));
    }

    /// <summary>Moves the drop mark to where the pointer is now. Over the grouping strip there IS no place among the
    /// columns, so the mark comes off the rows and goes onto the strip instead.</summary>
    internal void UpdateReorder(double x, bool overGroupPanel = false)
    {
        Owner?.MarkGroupPanelTarget(overGroupPanel);

        if (overGroupPanel)
        {
            HideDropLine();
            return;
        }

        ShowDropLine(DropTargetAt(x));
    }

    /// <summary>Drops the carried column where the mark stands - among the columns, or into the grouping strip.</summary>
    internal void EndReorder(double x, bool intoGroupPanel = false)
    {
        if (_dragging < 0) return;

        MarkDragged(_dragging, false);
        Owner?.MarkGroupPanelTarget(false);

        if (intoGroupPanel)
        {
            Owner?.GroupBy(Owner.Columns[_dragging]);
        }
        else
        {
            var target = DropTargetAt(x);
            Owner?.MoveColumn(_dragging, target > _dragging ? target - 1 : target);
        }

        _dragging = -1;
        HideDropLine();
        Mouse.OverrideCursor = null;
    }

    private void MarkDragged(int index, bool dragging)
    {
        if (_headers.TryGetValue(index, out var header)) header.IsDragging = dragging;
    }

    private void HideDropLine()
    {
        _dropTarget = -1;
        Owner?.HideDropIndicator();
    }

    private int HeaderAt(double x)
    {
        var columns = Owner?.Columns;
        if (columns == null) return -1;

        var offset = Owner.HorizontalOffset;
        for (var pinned = 0; pinned < 2; pinned++)
        {
            for (var i = 0; i < columns.Count; i++)
            {
                if (columns[i].IsFrozen != (pinned == 0)) continue;

                var at = ScreenXOf(columns[i], offset);
                if (x >= at && x < at + columns[i].ActualWidth) return i;
            }
        }

        return -1;
    }
}
