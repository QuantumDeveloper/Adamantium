using System;
using System.Collections.Generic;
using System.ComponentModel;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>One row of a <see cref="TreeDataGrid"/>: a strip of cells laid out at the COLUMNS' offsets, not at the
/// cells' own desired sizes - the grid computes every width once a pass and the header reads the same numbers.</summary>
public class DataGridRow : Panel
{
    public static readonly AdamantiumProperty IsSelectedProperty = AdamantiumProperty.Register(nameof(IsSelected),
        typeof(bool), typeof(DataGridRow), new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    /// <summary>Which band of the zebra this row is in - its position in the FLAT list modulo the grid's
    /// <see cref="TreeDataGrid.AlternationCount"/>. From the row's index in the DATA, never from the container's place
    /// in the panel: containers are recycled, so their order stops matching the data's as soon as you scroll.</summary>
    public static readonly AdamantiumProperty AlternationIndexProperty = AdamantiumProperty.Register(
        nameof(AlternationIndex), typeof(Int32), typeof(DataGridRow),
        new PropertyMetadata(0, PropertyMetadataOptions.AffectsRender));

    public bool IsSelected
    {
        get => GetValue<bool>(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public Int32 AlternationIndex
    {
        get => GetValue<Int32>(AlternationIndexProperty);
        set => SetValue(AlternationIndexProperty, value);
    }

    /// <summary>This row stands for a GROUP rather than for a record. A theme gives it its own colour through this -
    /// a caption is a header for the rows under it, not one of them.</summary>
    public static readonly AdamantiumProperty IsGroupProperty = AdamantiumProperty.Register(nameof(IsGroup),
        typeof(bool), typeof(DataGridRow), new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    public bool IsGroup
    {
        get => GetValue<bool>(IsGroupProperty);
        private set => SetValue(IsGroupProperty, value);
    }

    /// <summary>This row is the PANEL a record opened under itself rather than a record of its own - so a theme can
    /// leave the rules, the stripe and the hover off it: it is not a row anybody points at.</summary>
    public static readonly AdamantiumProperty IsRowDetailsProperty = AdamantiumProperty.Register(nameof(IsRowDetails),
        typeof(bool), typeof(DataGridRow), new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    public bool IsRowDetails
    {
        get => GetValue<bool>(IsRowDetailsProperty);
        private set => SetValue(IsRowDetailsProperty, value);
    }

    /// <summary>The grid this row belongs to, for the columns and the shared widths.</summary>
    public TreeDataGrid Owner { get; internal set; }

    /// <summary>The flattened row this container stands for: the item, its depth and whether it has children.</summary>
    internal TreeRow Row { get; set; }

    /// <summary>The item behind this row.</summary>
    public object Item => Row?.Node;

    // Only the columns in the window get a cell, keyed by column index. A pool because the window slides sideways and a
    // cell leaving one end is the cell entering the other - the same reasoning as rows leaving the top of a list.
    private readonly Dictionary<int, DataGridCell> _cells = new();
    private INotifyPropertyChanged _followed;
    private Decorators.Border _frozenBackdrop;
    private Decorators.Border _rightBackdrop;
    private DataGridGroupHeader _groupHeader;
    private readonly Dictionary<int, DataGridFooterCell> _groupTotals = new();
    private DataGridRowHeader _number;
    private ContentPresenter _details;
    private DataGridRowDetailsToggle _detailsToggle;

    /// <summary>This row's place in the visible order, from 1 - what the number strip shows.</summary>
    public int Number { get; private set; }
    private readonly Stack<DataGridCell> _pool = new();
    private readonly List<int> _leaving = new();

    /// <summary>Points the row at a flat row and refreshes its cells. Called on creation and on every reuse.</summary>
    internal void Attach(TreeDataGrid owner, TreeRow row, int alternationIndex, int number = 0)
    {
        Owner = owner;
        Follow(row?.Node);
        Row = row;
        IsGroup = row?.Node is DataGridGroup;
        IsRowDetails = row is { IsDetails: true };

        // A group row takes NO stripe of the zebra. The stripes count the rows of the data, so a caption that landed on
        // one band or the other by where it happened to fall in the flat list read as a mistake - and the theme gives a
        // group its own colour anyway, which the pinned zone then follows like any other row colour.
        AlternationIndex = IsGroup ? 0 : alternationIndex;
        Number = number;

        // An app-chosen tint wins; with none set the value is CLEARED rather than overwritten, so the theme's own
        // striping applies again instead of being masked by a local write nothing can take back.
        var tint = owner?.AlternationBrush;
        if (!IsGroup && tint != null && AlternationIndex != 0) Background = tint;
        else ClearValue(BackgroundProperty);

        SyncCells();
    }

    // The model moves on its own: a service answers, a background job finishes, another view writes. None of that is a
    // layout event, so a table that only re-read its cells when something happened to measure them showed a stale value
    // until the user scrolled. One subscription per REALIZED row - the rows off screen do not exist to be stale.
    private void Follow(object item)
    {
        if (ReferenceEquals(_followed, item)) return;

        if (_followed != null) _followed.PropertyChanged -= OnItemChanged;
        _followed = item as INotifyPropertyChanged;
        if (_followed != null) _followed.PropertyChanged += OnItemChanged;
    }

    private void OnItemChanged(object sender, PropertyChangedEventArgs e) => SyncCells();

    // Belt and braces: whatever drops this row - a recycle, a reset, the whole table being discarded with the page -
    // leaving the tree releases the item. The subscription points at the LONG-LIVED end, so a missed release is not a
    // stale row but a page that never goes away.
    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        Follow(null);
    }

    /// <summary>Brings the cell strip in line with the columns, reusing what is already there. Columns change rarely and
    /// rows are recycled constantly, so the common path must not rebuild anything.</summary>
    internal void SyncCells()
    {
        var columns = Owner?.Columns;
        var count = columns?.Count ?? 0;
        var item = Item;
        var expander = Owner?.ExpanderColumn;
        var depth = Row?.Depth ?? 0;

        // WHAT this row stands for, decided BEFORE any of it is built. Building a backdrop here for SyncDetails to drop
        // below is a child ADDED AND REMOVED on every measure, and each of those invalidates the row that is measuring:
        // the row never goes valid, so the whole realized set re-measures on every pass of a table nobody is touching.
        // Measured at seven measures a frame per panel and sixty milliseconds of layout AT REST. The strip carrying the
        // toggle counts as pinned leading, so a table with panels always had a pinned zone to build - the feature paid
        // this from the moment it was switched on, with no frozen column anywhere.
        var panel = Row?.Node is DataGridRowDetails;

        // The ZONES FIRST: a backdrop is painted BETWEEN two sets of siblings, and the retained paint order ranks a
        // child when it is placed.
        if (!panel) SyncFrozenBackdrop();
        SyncDetailsToggle();

        // A PANEL row has no cells and no number: it is not a record, it is the record above it said at length, and a
        // number of its own would make the record after it look like it had skipped one.
        if (SyncDetails())
        {
            if (_number != null) _number.Visibility = Visibility.Collapsed;
            return;
        }

        // A GROUP row has no cells at all: it belongs to no column, so there is nothing for a column to say about it.
        // It IS still one of the rows on screen, so it keeps its ordinal - numbers that skipped the group headers would
        // read as rows gone missing.
        if (SyncGroup())
        {
            SyncNumber();
            return;
        }

        // Cells whose column left the window go back to the pool; the ones entering take them. Frozen columns never
        // leave - they are the point of being frozen.
        _leaving.Clear();
        foreach (var pair in _cells)
        {
            if (pair.Key >= count || !Owner.IsColumnRealized(pair.Key)) _leaving.Add(pair.Key);
        }

        foreach (var index in _leaving)
        {
            var cell = _cells[index];
            _cells.Remove(index);
            cell.Visibility = Visibility.Collapsed;
            _pool.Push(cell);
        }

        for (var i = 0; i < count; i++)
        {
            if (!Owner.IsColumnRealized(i)) continue;

            if (!_cells.TryGetValue(i, out var cell))
            {
                cell = _pool.Count > 0 ? _pool.Pop() : NewCell();
                // A cell pooled on the way INTO a details panel was taken out of the row, not merely hidden, so coming
                // back means being put back. Pooling for a column leaving the window sideways still only hides.
                if (cell.VisualParent != this) Children.Add(cell);
                cell.Visibility = Visibility.Visible;
                _cells[i] = cell;
            }

            var column = columns[i];
            cell.Attach(column, i, item, Number - 1);

            // A pinned cell is drawn LAST among its siblings: the scrolling ones pass under it, and whichever is drawn
            // later wins the pixels. Paint order among siblings is ZIndex here, not the order they were added - cells
            // are pooled, so the order they were added means nothing at all.
            cell.ZIndex = column.IsFrozen ? 2 : 0;

            // ONLY the expander column knows about the hierarchy: it reserves the indent and the expander strip, and
            // every other column draws its data at its own edge, whatever the depth.
            var carries = ReferenceEquals(column, expander);
            cell.ShowsExpander = carries;
            cell.HasChildren = Row?.HasChildren ?? false;
            cell.IsExpanded = Row?.IsExpanded ?? false;
            // DEPTH only. The expander's own strip is a column of the cell template, not part of the indent - otherwise
            // the glyph is drawn past the strip that is hit-tested for it.
            cell.Indent = carries ? depth * (Owner?.Indent ?? 0) : 0;
        }

        SyncNumber();
    }

    /// <summary>The group this row stands for, or null on an ordinary row.</summary>
    public DataGridGroup Group => Row?.Node as DataGridGroup;

    // How far in a nested group's caption sits. Worked out HERE and applied by the arrange, so the step a group takes
    // is the same step a branch of the tree takes and there is one description of it.
    private double GroupIndent => (Group?.Level ?? 0) * (Owner?.Indent ?? 0);

    // The run of the row the caption gets. It begins where the PINNED ZONE ends: that zone is a column's lane, and a
    // caption drawn across it reads as that column's text. It stops at the first TOTAL,
    // because a caption and a number drawn in the same place are two things and one of them is unreadable. A total in
    // a column at the caption's own start pushes the caption past it instead: the number belongs to that column and
    // the caption to no column at all. Columns are walked in their layout order, which is the order the caption has to
    // give way in.
    private Rect CaptionRun(DataGridColumns columns, double offset, double right)
    {
        var start = offset + (Owner?.FrozenWidth ?? 0) + GroupIndent;
        var end = Owner?.ColumnsWidth ?? 0;

        for (var i = 0; i < columns.Count; i++)
        {
            if (!_groupTotals.ContainsKey(i)) continue;

            var column = columns[i];
            var at = column.IsFrozenLeft ? column.Offset + offset
                : column.IsFrozenRight ? column.Offset + right
                : column.Offset;

            if (at + column.ActualWidth <= start) continue;
            if (at <= start) start = at + column.ActualWidth;
            else end = Math.Min(end, at);
        }

        return new Rect(start, 0, Math.Max(0, end - start), 0);
    }

    // Returns whether this row IS a group. Everything a group row shows lives here: the header, and one total per
    // column that asked for one, placed at that column's offset so a sum stands under the numbers it is a sum of.
    // The panel row: one presenter over the whole row, built from the table's template against the record the panel
    // belongs to. Everything a record's row wears - its cells, its number, the pinned backdrop - goes away with it:
    // this row shows one thing, and a pinned band running under a free-form panel would cut it in two.
    private bool SyncDetails()
    {
        if (Row?.Node is not DataGridRowDetails details)
        {
            DropDetails();
            return false;
        }

        // TAKEN OUT, not hidden - the same rule the group's totals are held to, and for the same reason. This row is
        // recycled between standing for a record and standing for a whole PANEL, which is the biggest swap of parts a
        // row makes, and a part left hidden inside it keeps its place in the drawn set while it is not in the drawing.
        // That is what left panels with their tab strip cut in half or gone.
        foreach (var pair in _cells)
        {
            pair.Value.Visibility = Visibility.Collapsed;
            Children.Remove(pair.Value);
            _pool.Push(pair.Value);
        }

        _cells.Clear();
        DropGroupHeader();
        HideGroupTotals();
        DropBackdrops();

        if (_details == null)
        {
            _details = new ContentPresenter();
            Children.Add(_details);
        }

        // ONLY when it changed. This runs on every measure of the row, and writing the same template and the same item
        // back each time re-dirties the presenter, which re-dirties the row, which measures again - measured at seven
        // measures a frame per panel and sixty milliseconds of layout on a table standing still.
        var template = Owner?.RowDetailsTemplate;
        if (!ReferenceEquals(_details.ContentTemplate, template)) _details.ContentTemplate = template;
        if (!ReferenceEquals(_details.Content, details.Item)) _details.Content = details.Item;
        return true;
    }

    // ...and out again on the way back to being a record.
    private void DropDetails()
    {
        if (_details == null) return;

        _details.Content = null;
        Children.Remove(_details);
        _details = null;
    }

    private void DropGroupHeader()
    {
        if (_groupHeader == null) return;

        _groupHeader.MouseLeftButtonDown -= OnGroupPressed;
        Children.Remove(_groupHeader);
        _groupHeader = null;
    }

    private void DropBackdrops()
    {
        if (_frozenBackdrop != null)
        {
            Children.Remove(_frozenBackdrop);
            _frozenBackdrop = null;
        }

        if (_rightBackdrop != null)
        {
            Children.Remove(_rightBackdrop);
            _rightBackdrop = null;
        }
    }

    private bool SyncGroup()
    {
        if (Group is not { } group)
        {
            if (_groupHeader != null) _groupHeader.Visibility = Visibility.Collapsed;
            HideGroupTotals();
            return false;
        }

        // Back to the POOL, not merely hidden: a container is recycled between a group row and a data row, and a cell
        // left in _cells is never shown again - only a cell taken from the pool is turned back on.
        foreach (var pair in _cells)
        {
            pair.Value.Visibility = Visibility.Collapsed;
            _pool.Push(pair.Value);
        }

        _cells.Clear();

        if (_groupHeader == null)
        {
            _groupHeader = new DataGridGroupHeader();
            _groupHeader.MouseLeftButtonDown += OnGroupPressed;
            Children.Add(_groupHeader);
        }

        _groupHeader.Visibility = Visibility.Visible;
        _groupHeader.GroupName = group.Column?.Header?.ToString();
        _groupHeader.Key = group.Key;
        _groupHeader.Count = group.Count;
        _groupHeader.IsExpanded = Row?.IsExpanded ?? false;

        SyncGroupTotals(group);
        return true;
    }

    private void SyncGroupTotals(DataGridGroup group)
    {
        var columns = Owner?.Columns;
        var count = columns?.Count ?? 0;

        _leaving.Clear();
        foreach (var pair in _groupTotals)
        {
            if (pair.Key >= count || !Owner.IsColumnRealized(pair.Key)
                || columns[pair.Key].Aggregate == DataGridAggregate.None) _leaving.Add(pair.Key);
        }

        foreach (var index in _leaving) DropGroupTotal(index);

        for (var i = 0; i < count; i++)
        {
            if (!Owner.IsColumnRealized(i)) continue;

            var column = columns[i];
            if (column.Aggregate == DataGridAggregate.None) continue;

            if (!_groupTotals.TryGetValue(i, out var cell))
            {
                cell = new DataGridFooterCell { HasTotal = true, IsHitTestVisible = false };
                _groupTotals[i] = cell;
                Children.Add(cell);
            }

            // The cells' own rule: a pinned total is drawn OVER the zone's backdrop, a scrolling one under it.
            cell.ZIndex = column.IsFrozen ? 2 : 0;
            cell.Content = DataGridTotals.Text(column, Owner.TotalFor(column, group));
        }
    }

    // TAKEN OUT of the row, not hidden inside it. A row is recycled between standing for a group and standing for a
    // record over and over, and a part that comes and goes has to come and go: hiding these left the drawn set holding
    // children that were no longer in it, and whole runs of rows stopped being painted at all (measured on the stand -
    // grouping with totals, scroll away and back, and twenty-seven rows of twenty-nine drew nothing).
    private void DropGroupTotal(int index)
    {
        if (!_groupTotals.TryGetValue(index, out var cell)) return;

        _groupTotals.Remove(index);
        Children.Remove(cell);
    }

    private void HideGroupTotals()
    {
        if (_groupTotals.Count == 0) return;

        _leaving.Clear();
        foreach (var pair in _groupTotals) _leaving.Add(pair.Key);
        foreach (var index in _leaving) DropGroupTotal(index);
    }

    private void OnGroupPressed(object sender, MouseButtonEventArgs e)
    {
        if (Row == null || Owner == null) return;

        Owner.ToggleRow(Row);
        e.Handled = true;
    }

    // The handle that opens a record's panel, in a column of its own at the very head of the row - ahead of the numbers,
    // as the only thing standing further left than the table itself. Like the number strip it is NOT in Columns: it
    // belongs to the table rather than to the data, and putting it there would shift every index the page declared.
    private void SyncDetailsToggle()
    {
        if (Owner?.RowDetailsTemplate == null)
        {
            if (_detailsToggle != null) _detailsToggle.Visibility = Visibility.Collapsed;
            return;
        }

        if (_detailsToggle == null)
        {
            _detailsToggle = new DataGridRowDetailsToggle { ZIndex = 3 };
            _detailsToggle.MouseLeftButtonDown += OnDetailsTogglePressed;
            Children.Add(_detailsToggle);
        }

        _detailsToggle.Visibility = Visibility.Visible;

        // A group's caption and a panel row are not records, so there is nothing under them to open. The strip keeps its
        // width there and shows nothing - a column that closed up under some rows would make the left edge ragged.
        _detailsToggle.IsBlank = IsGroup || IsRowDetails;
        _detailsToggle.IsOpen = !_detailsToggle.IsBlank && Owner.IsRowDetailsOpen(Item);
    }

    private void OnDetailsTogglePressed(object sender, MouseButtonEventArgs e)
    {
        if (_detailsToggle is { IsBlank: false } && Owner != null)
        {
            Owner.ToggleRowDetails(Item);
            e.Handled = true;
        }
    }

    // The strip is pinned like a frozen column and drawn over what scrolls under it, so it is opaque by its theme and
    // sits at the very head of the zone. It is NOT a column: putting it in Columns would shift every index the
    // application declared - the expander's column above all - for a strip that belongs to the table, not to the data.
    private void SyncNumber()
    {
        if (Owner?.ShowRowNumbers != true)
        {
            if (_number != null) _number.Visibility = Visibility.Collapsed;
            return;
        }

        if (_number == null)
        {
            _number = new DataGridRowHeader { ZIndex = 3 };
            _number.MouseLeftButtonDown += OnNumberPressed;
            Children.Add(_number);
        }

        // Unbounded ONLY when the digits change, or when this strip is coming back from hidden. Asking every pass
        // alternates the available size with the bounded measure below, so neither call can hit the measure cache.
        // Per ROW, not off the grid's width: the first row to report makes that non-zero and the widest never measures.
        var returning = _number.Visibility != Visibility.Visible;
        _number.Visibility = Visibility.Visible;

        if (_number.Number != Number || returning)
        {
            _number.Number = Number;
            _number.Measure(Size.Infinity);
            Owner.ReportRowNumberWidth(_number.DesiredSize.Width);
        }

        RefreshNumberSelection();
    }

    /// <summary>Brings the number strip in line with the selection. The selection changes without the cells being
    /// rebuilt - an arrow key, a drag over the cells - so the strip cannot learn it from <see cref="SyncCells"/> alone.</summary>
    internal void RefreshNumberSelection()
    {
        if (_number is { Visibility: Visibility.Visible } && Owner != null)
        {
            _number.IsSelected = Owner.IsRowFullySelected(Number - 1);
        }
    }

    // Pressing the number takes the ROW - a rectangle across every column, which is what row selection IS here. Shift
    // stretches from the anchor and Ctrl adds another row, exactly as they do over the cells.
    private void OnNumberPressed(object sender, MouseButtonEventArgs e)
    {
        if (Owner == null || Number <= 0) return;

        // A group's number stands for the group, and there are no cells behind it to take: pressing it opens and closes
        // the group, which is the only thing that row can do.
        if (Group != null)
        {
            Owner.ToggleRow(Row);
            e.Handled = true;
            return;
        }

        // The modifiers carried BY THE EVENT - see OnMouseLeftButtonDown for why the keyboard is not re-read here.
        var shift = (e.Modifiers & (InputModifiers.LeftShift | InputModifiers.RightShift)) != 0;
        var control = (e.Modifiers & (InputModifiers.LeftControl | InputModifiers.RightControl)) != 0;

        Owner.SelectRow(Number - 1, extend: shift, add: control);
        e.Handled = true;
    }

    private DataGridCell NewCell()
    {
        var cell = new DataGridCell();
        Children.Add(cell);
        return cell;
    }

    // What scrolls passes UNDER the pinned zone and a cell is transparent, so the zone paints: the table's SURFACE,
    // opaque, and this row's own band brush laid over it. TWO layers and not one blended colour - a blend is a
    // SNAPSHOT, and a brush whose colour is changed in place (which is what a colour picker does to one brush object)
    // would leave the zone painted in the shade it was mixed at while every other row followed.
    private void SyncFrozenBackdrop()
    {
        SyncBackdrop(ref _frozenBackdrop, Owner?.FrozenWidth ?? 0);
        SyncBackdrop(ref _rightBackdrop, Owner?.RightFrozenWidth ?? 0);
    }

    private void SyncBackdrop(ref Decorators.Border backdrop, double width)
    {
        if (width <= 0)
        {
            if (backdrop != null) backdrop.Visibility = Visibility.Collapsed;
            return;
        }

        if (backdrop == null)
        {
            backdrop = new Decorators.Border
            {
                IsHitTestVisible = false,
                ZIndex = 1,
                Child = new Decorators.Border { IsHitTestVisible = false }
            };
            Children.Add(backdrop);
        }

        backdrop.Visibility = Visibility.Visible;
        backdrop.Background = Owner?.Background;
        ((Decorators.Border)backdrop.Child).Background = Background;
    }

    /// <summary>The cell in <paramref name="columnIndex"/>, or null when that column is outside the window and so has no
    /// cell on this row at all. Callers must survive the null - it is the ordinary case on a wide table.</summary>
    internal DataGridCell CellAt(int columnIndex) => _cells.GetValueOrDefault(columnIndex);

    /// <summary>This row's number strip, or null while the table shows no numbers.</summary>
    internal DataGridRowHeader NumberHeader => _number;

    /// <summary>What paints the LEFT pinned zone, or null while nothing is pinned there.</summary>
    internal Decorators.Border FrozenBackdrop => _frozenBackdrop;

    /// <summary>This row's group caption, or null while it has never stood for a group.</summary>
    internal DataGridGroupHeader GroupCaption => _groupHeader;

    protected override void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(sender, e);
        if (Owner == null || Row == null) return;

        // The expander is NOT handled here: the cell's own PART_Expander answers for it, so the area that reacts is the
        // area that is drawn. Working the rectangle out here meant two descriptions of one strip, and they disagreed by
        // the cell's padding.
        var columnIndex = ColumnAt(e.GetPosition(this).X);
        if (columnIndex < 0) return;

        // A press inside the cell that is BEING EDITED belongs to its editor: double-clicking a word to select it must
        // not commit the edit and open it again underneath the pointer.
        if (CellAt(columnIndex) is { IsEditing: true }) return;

        // The modifiers carried BY THE EVENT, captured when it was raised - handlers run later on the loop thread, and
        // re-reading the keyboard there misses the Ctrl or Shift that was actually held.
        var shift = (e.Modifiers & (InputModifiers.LeftShift | InputModifiers.RightShift)) != 0;
        var control = (e.Modifiers & (InputModifiers.LeftControl | InputModifiers.RightControl)) != 0;
        Owner.SelectCellFromRow(this, columnIndex, extend: shift, add: control);

        // A double-click opens the cell for editing - the gesture every table uses, and the one that makes the editing
        // machinery reachable at all. A check box column is the exception: one click IS the edit.
        if (Owner.Columns[columnIndex].TogglesOnClick) Owner.ToggleFromRow(this, columnIndex);
        else if (e.ClickCount >= 2) Owner.BeginEditFromRow(this, columnIndex);
        else Owner.BeginDragSelect(control);

        e.Handled = true;
    }

    protected override void OnMouseMove(object sender, MouseEventArgs e)
    {
        base.OnMouseMove(sender, e);
        if (Owner is not { IsDragSelecting: true }) return;

        if ((e.Modifiers & InputModifiers.LeftMouseButton) == 0)
        {
            Owner.EndDragSelect();
            return;
        }

        var columnIndex = ColumnAt(e.GetPosition(this).X);
        if (columnIndex >= 0) Owner.DragSelectFromRow(this, columnIndex);
    }

    protected override void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(sender, e);
        Owner?.EndDragSelect();
    }

    /// <summary>A cell's expander was pressed - the cell knows it was clicked, only the row knows which flat row it
    /// stands for.</summary>
    internal void ToggleFromExpander() => Owner?.ToggleRow(Row);

    private int ColumnAt(double x)
    {
        var columns = Owner?.Columns;
        if (columns == null) return -1;

        for (var i = 0; i < columns.Count; i++)
        {
            if (x >= columns[i].Offset && x < columns[i].Offset + columns[i].ActualWidth) return i;
        }

        return -1;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var columns = Owner?.Columns;
        if (columns == null || columns.Count == 0) return new Size();

        // The window of columns can move on any pass - a sideways scroll is exactly that - so the strip is brought in
        // line HERE rather than only when the row is bound. Cheap when nothing moved: a dictionary lookup per column.
        SyncCells();

        if (_detailsToggle is { Visibility: Visibility.Visible })
        {
            _detailsToggle.Measure(new Size(Owner.RowDetailsToggleWidth, availableSize.Height));
        }

        // The panel takes what is left of the VIEW after the strips - it belongs to no column, so the columns' total
        // width is not its business, and the part of the row that is off screen is not either. Its HEIGHT is asked
        // unbounded and reported back: what a panel comes to is its content's business, and the pass that placed the
        // rows under it had to guess. The guess is corrected on the next pass, as an Auto column's width is.
        if (IsRowDetails)
        {
            var strips = Owner.LeftStripsLeading;
            var seen = Owner.ViewportWidth;
            var room = Math.Max(0, (seen > 0 ? seen : availableSize.Width) - strips);
            var of = (Row.Node as DataGridRowDetails)?.Item;
            if (_details != null)
            {
                _details.Measure(new Size(room, double.PositiveInfinity));
                Owner.ReportRowDetailsHeight(of, _details.DesiredSize.Height);
            }

            // What the CONTENT came to, not what the table guessed: this row carries no fixed height, so its own desired
            // size is the one honest answer and the stack is told the same number through the report above.
            return new Size(Owner.ColumnsWidth, _details?.DesiredSize.Height ?? 0);
        }

        // A group row spans the table rather than dividing into columns, so its caption takes the run the totals leave
        // it and its totals are measured against their own columns. The SAME run the arrange will use, scroll and all:
        // text is trimmed to the width it was MEASURED at, so a caption measured wide and arranged narrow simply drew
        // over whatever stood beside it - on the stand, the group's own total.
        if (Group != null)
        {
            var run = CaptionRun(columns, Owner.HorizontalOffset, Owner.RightPinShift);
            _groupHeader.Measure(new Size(run.Width, availableSize.Height));

            foreach (var pair in _groupTotals)
            {
                if (pair.Key >= columns.Count) continue;

                pair.Value.Measure(new Size(columns[pair.Key].ActualWidth, availableSize.Height));
            }

            if (_number is { Visibility: Visibility.Visible })
            {
                _number.Measure(new Size(Owner.RowNumberWidth, availableSize.Height));
            }

            return new Size(Owner.ColumnsWidth, _groupHeader.DesiredSize.Height);
        }

        // Each cell is measured AT its column's width - that is what gives a text cell an edge to trim against instead
        // of asking for the length it would like. What each cell WOULD have liked is reported back for the Auto columns.
        double height = 0;
        foreach (var pair in _cells)
        {
            var i = pair.Key;
            if (i >= columns.Count) continue;
            var column = columns[i];
            var cell = pair.Value;
            cell.Measure(new Size(column.ActualWidth, availableSize.Height));
            height = Math.Max(height, cell.DesiredSize.Height);

            if (column.Width.IsAuto)
            {
                // Auto asks the cell what it would take unbounded; that answer is what the width pass settles on.
                cell.Measure(new Size(double.PositiveInfinity, availableSize.Height));
                Owner?.ReportAutoWidth(column, cell.DesiredSize.Width);
                cell.Measure(new Size(column.ActualWidth, availableSize.Height));
            }
        }

        // At the settled width, and only at it: one constant available size across passes is what lets the measure
        // cache hold, so a table standing still measures nothing at all.
        if (_number is { Visibility: Visibility.Visible })
        {
            _number.Measure(new Size(Owner.RowNumberWidth, availableSize.Height));
        }

        var width = columns[^1].Offset + columns[^1].ActualWidth;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = Owner?.Columns;
        if (columns == null) return finalSize;

        // The row lives INSIDE the sideways scroller, so the viewer moves the whole strip and an ordinary cell just
        // takes its column's offset. A FROZEN cell has to stay where it is while that happens, so it is pushed back by
        // exactly what the scroller moved - the pinned zone then stands still against a moving row.
        var offset = Owner?.HorizontalOffset ?? 0;
        // ...and a cell pinned RIGHT is laid out at the end of the content, so it is slid back to the viewport's edge by
        // the grid's one number for the whole zone. That number ALREADY carries the scroll - it is what is off-screen
        // to the right - so it is not pushed against the scroll a second time.
        var right = Owner?.RightPinShift ?? 0;

        // The panel stands STILL while the table scrolls sideways, and takes the whole VIEWPORT rather than the whole
        // content: it belongs to no column, so a panel that slid away with the columns would be a record's long form
        // that has to be scrolled back to, and one laid out over the full content width would be mostly off screen.
        if (IsRowDetails)
        {
            var strips = Owner?.LeftStripsLeading ?? 0;
            var seen = Owner?.ViewportWidth ?? finalSize.Width;
            if (seen <= 0) seen = finalSize.Width;
            _detailsToggle?.Arrange(new Rect(offset, 0, Owner?.RowDetailsToggleWidth ?? 0, finalSize.Height));
            _details?.Arrange(new Rect(offset + strips, 0, Math.Max(0, seen - strips), finalSize.Height));
            return finalSize;
        }

        // The group's caption stands STILL while the table scrolls sideways: it names the rows under it, and a name
        // that slides out of view names nothing. Its totals keep their columns, as any total must.
        if (Group != null)
        {
            var run = CaptionRun(columns, offset, right);
            _groupHeader.Arrange(new Rect(run.X, 0, run.Width, finalSize.Height));

            foreach (var pair in _groupTotals)
            {
                if (pair.Key >= columns.Count) continue;

                var totalColumn = columns[pair.Key];
                var at = totalColumn.IsFrozenLeft ? totalColumn.Offset + offset
                    : totalColumn.IsFrozenRight ? totalColumn.Offset + right
                    : totalColumn.Offset;
                pair.Value.Arrange(new Rect(at, 0, totalColumn.ActualWidth, finalSize.Height));
            }
        }
        else
        {
            foreach (var pair in _cells)
            {
                if (pair.Key >= columns.Count) continue;

                var column = columns[pair.Key];
                var x = column.IsFrozenLeft ? column.Offset + offset
                    : column.IsFrozenRight ? column.Offset + right
                    : column.Offset;
                pair.Value.Arrange(new Rect(x, 0, column.ActualWidth, finalSize.Height));
            }
        }

        // The zone travels with the scroll exactly as the cells in it do, so it always stands under them and over
        // whatever is passing beneath.
        if (_frozenBackdrop is { Visibility: Visibility.Visible })
        {
            _frozenBackdrop.Measure(new Size(Owner.FrozenWidth, finalSize.Height));
            _frozenBackdrop.Arrange(new Rect(offset, 0, Owner.FrozenWidth, finalSize.Height));
        }

        if (_rightBackdrop is { Visibility: Visibility.Visible })
        {
            var zone = Owner.ColumnsWidth - Owner.RightFrozenWidth + right;
            _rightBackdrop.Measure(new Size(Owner.RightFrozenWidth, finalSize.Height));
            _rightBackdrop.Arrange(new Rect(zone, 0, Owner.RightFrozenWidth, finalSize.Height));
        }

        // The toggles stand furthest left of all, and the numbers begin where they end.
        if (_detailsToggle is { Visibility: Visibility.Visible })
        {
            _detailsToggle.Arrange(new Rect(offset, 0, Owner.RowDetailsToggleWidth, finalSize.Height));
        }

        if (_number is { Visibility: Visibility.Visible })
        {
            _number.Arrange(new Rect(offset + Owner.DetailsStripLeading, 0, Owner.RowNumberWidth, finalSize.Height));
        }

        return finalSize;
    }
}
