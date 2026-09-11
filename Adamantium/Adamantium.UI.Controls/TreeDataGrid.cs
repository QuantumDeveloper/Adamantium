using System.Collections;
using System.Collections.Specialized;
using System.Text;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

/// <summary>A table whose rows may have children. A flat table is the degenerate case - no child path, every row at
/// depth 0 - which is why this is one control and not two. Rows come from <see cref="TreeFlattener"/>: expanding a
/// branch splices its children in as one range edit.</summary>
public partial class TreeDataGrid : Selector
{
    static TreeDataGrid()
    {
        FocusableProperty.OverrideMetadata(typeof(TreeDataGrid), new PropertyMetadata(true));
    }

    public TreeDataGrid()
    {
        Columns.CollectionChanged += OnColumnsChanged;
        RebuildFlattener();
    }

    /// <summary>The columns, in display order.</summary>
    public DataGridColumns Columns { get; } = new();

    /// <summary>How many bands the zebra has; 0 (the default) turns it off. The band a row falls in is its index in the
    /// FLAT list modulo this - see <see cref="DataGridRow.AlternationIndex"/> for why it cannot come from the container.</summary>
    public static readonly AdamantiumProperty AlternationCountProperty = AdamantiumProperty.Register(
        nameof(AlternationCount), typeof(Int32), typeof(TreeDataGrid),
        new PropertyMetadata(0, PropertyMetadataOptions.AffectsRender, OnAlternationCountChanged));

    // The band is worked out when a row is BOUND, so changing the count reaches nothing that already exists. Re-bind the
    // realized rows, or the setting appears to do nothing until they happen to be recycled.
    private static void OnAlternationCountChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e) =>
        (d as TreeDataGrid)?.RefreshRealizedRows();

    /// <summary>What the tinted bands are painted with, or null to leave it to the theme. Set here it applies to every
    /// band but the first, which is the one left showing the grid's own surface.</summary>
    public static readonly AdamantiumProperty AlternationBrushProperty = AdamantiumProperty.Register(
        nameof(AlternationBrush), typeof(Brush), typeof(TreeDataGrid),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender, OnAlternationCountChanged));

    public Brush AlternationBrush
    {
        get => GetValue<Brush>(AlternationBrushProperty);
        set => SetValue(AlternationBrushProperty, value);
    }

    /// <summary>Which rules the table draws between its cells. Both by default, as every table this one is measured
    /// against does - a grid that cannot draw its own lines is not a grid.</summary>
    public static readonly AdamantiumProperty GridLinesVisibilityProperty = AdamantiumProperty.Register(
        nameof(GridLinesVisibility), typeof(DataGridGridLines), typeof(TreeDataGrid),
        new PropertyMetadata(DataGridGridLines.All, PropertyMetadataOptions.AffectsRender, OnGridLinesChanged));

    /// <summary>What the rules are painted with, or null to leave it to the theme. ONE brush for both directions: the
    /// rules ride on the cell's own border, and a border has one brush. Two would have to be two elements per cell,
    /// which measured a third of everything the table drew - too much for a colour nobody asked to split.</summary>
    public static readonly AdamantiumProperty GridLinesBrushProperty = AdamantiumProperty.Register(
        nameof(GridLinesBrush), typeof(Brush), typeof(TreeDataGrid),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender, OnGridLinesChanged));

    public DataGridGridLines GridLinesVisibility
    {
        get => GetValue<DataGridGridLines>(GridLinesVisibilityProperty);
        set => SetValue(GridLinesVisibilityProperty, value);
    }

    public Brush GridLinesBrush
    {
        get => GetValue<Brush>(GridLinesBrushProperty);
        set => SetValue(GridLinesBrushProperty, value);
    }

    /// <summary>What marks where a dragged column would land, or null to fall back to the table's own edge colour. The
    /// theme names the accent: a reorder that says nothing until the column has already moved is a gesture the user has
    /// to perform twice to understand.</summary>
    public static readonly AdamantiumProperty DropIndicatorBrushProperty = AdamantiumProperty.Register(
        nameof(DropIndicatorBrush), typeof(Brush), typeof(TreeDataGrid),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

    public Brush DropIndicatorBrush
    {
        get => GetValue<Brush>(DropIndicatorBrushProperty);
        set => SetValue(DropIndicatorBrushProperty, value);
    }

    /// <summary>Marks where a dragged column would land, from the header band down through the rows: what the user is
    /// aiming at is a place among the DATA, not a place in the strip. It lives in the grid's template rather than in the
    /// header presenter for exactly that reason - the strip cannot draw outside itself.</summary>
    internal void ShowDropIndicator(double at)
    {
        if (_dropIndicator == null) return;

        var width = double.IsNaN(_dropIndicator.Width) ? 2 : _dropIndicator.Width;
        _dropIndicator.Margin = new Thickness(at - width / 2, 0, 0, 0);
        _dropIndicator.Visibility = Visibility.Visible;
    }

    internal void HideDropIndicator()
    {
        if (_dropIndicator != null) _dropIndicator.Visibility = Visibility.Collapsed;
    }

    /// <summary>Whether cells draw the rule down their right edge.</summary>
    internal bool ShowsVerticalLines => (GridLinesVisibility & DataGridGridLines.Vertical) != 0;

    /// <summary>Whether cells draw the rule under themselves.</summary>
    internal bool ShowsHorizontalLines => (GridLinesVisibility & DataGridGridLines.Horizontal) != 0;

    // A cell reads this when it is BOUND, and the header strip when it syncs - so a change reaches neither until
    // something makes them do it again.
    private static void OnGridLinesChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
    {
        if (d is not TreeDataGrid grid) return;

        grid.RefreshRealizedRows();
        grid.RefreshStrips();
    }

    public Int32 AlternationCount
    {
        get => GetValue<Int32>(AlternationCountProperty);
        set => SetValue(AlternationCountProperty, value);
    }

    /// <summary>Member holding a node's children, e.g. <c>Children</c>. Empty means a flat table.</summary>
    public static readonly AdamantiumProperty ChildrenPathProperty = AdamantiumProperty.Register(nameof(ChildrenPath),
        typeof(String), typeof(TreeDataGrid), new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure,
            OnChildrenPathChanged));

    public String ChildrenPath
    {
        get => GetValue<String>(ChildrenPathProperty);
        set => SetValue(ChildrenPathProperty, value);
    }

    /// <summary>Height of one row, or <c>NaN</c> to take the height of what is in them. A number makes the vertical
    /// window arithmetic rather than a measurement, which is what a big table needs; the theme sets one.</summary>
    public static readonly AdamantiumProperty RowHeightProperty = AdamantiumProperty.Register(nameof(RowHeight),
        typeof(Double), typeof(TreeDataGrid), new PropertyMetadata(Double.NaN, PropertyMetadataOptions.AffectsMeasure));

    public Double RowHeight
    {
        get => GetValue<Double>(RowHeightProperty);
        set => SetValue(RowHeightProperty, value);
    }

    /// <summary>Pixels of indent per level of depth, applied by the column that shows the expander and by no other.</summary>
    public static readonly AdamantiumProperty IndentProperty = AdamantiumProperty.Register(nameof(Indent),
        typeof(Double), typeof(TreeDataGrid), new PropertyMetadata(16.0, PropertyMetadataOptions.AffectsMeasure));

    public Double Indent
    {
        get => GetValue<Double>(IndentProperty);
        set => SetValue(IndentProperty, value);
    }

    /// <summary>Width of the expander strip inside the column that carries it.</summary>
    public static readonly AdamantiumProperty ExpanderSizeProperty = AdamantiumProperty.Register(nameof(ExpanderSize),
        typeof(Double), typeof(TreeDataGrid), new PropertyMetadata(16.0, PropertyMetadataOptions.AffectsMeasure));

    public Double ExpanderSize
    {
        get => GetValue<Double>(ExpanderSizeProperty);
        set => SetValue(ExpanderSizeProperty, value);
    }

    private static void OnChildrenPathChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e) =>
        (d as TreeDataGrid)?.RebuildFlattener();

    /// <summary>Which column carries the expander and the indent - any of them will do. One property on the CONTROL
    /// rather than a flag per column: "exactly one of these" as a boolean admits two columns claiming it. An index
    /// outside the columns is clamped.</summary>
    public static readonly AdamantiumProperty ExpanderColumnIndexProperty = AdamantiumProperty.Register(
        nameof(ExpanderColumnIndex), typeof(Int32), typeof(TreeDataGrid),
        new PropertyMetadata(0, PropertyMetadataOptions.AffectsMeasure, OnExpanderColumnChanged));

    public Int32 ExpanderColumnIndex
    {
        get => GetValue<Int32>(ExpanderColumnIndexProperty);
        set => SetValue(ExpanderColumnIndexProperty, value);
    }

    private static void OnExpanderColumnChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e) =>
        (d as TreeDataGrid)?.InvalidateColumns();

    /// <summary>The column <see cref="ExpanderColumnIndex"/> names, clamped to what exists. If that column is not shown
    /// - the table is grouped by it - the expander moves to the first column that IS, or the tree could not be opened
    /// at all.</summary>
    public DataGridColumn ExpanderColumn
    {
        get
        {
            if (Columns.Count == 0) return null;

            var named = Columns[Math.Clamp(ExpanderColumnIndex, 0, Columns.Count - 1)];
            if (named.IsShown) return named;

            foreach (var column in Columns)
            {
                if (column.IsShown) return column;
            }

            return null;
        }
    }

    /// <summary>Opens or closes the row showing <paramref name="item"/> - one range edit for the whole child run.
    /// Addressed by ITEM, not by container: under virtualization the row being opened often has none.</summary>
    public void Toggle(object item) => ToggleRow(RowOf(item));

    public void Expand(object item) => ExpandRow(RowOf(item));

    public void Collapse(object item) => CollapseRow(RowOf(item));

    /// <summary>Whether the row showing <paramref name="item"/> is open.</summary>
    public bool IsExpanded(object item) => RowOf(item) is { IsExpanded: true };

    internal TreeRow RowOf(object item)
    {
        if (item is TreeRow direct) return direct;
        var rows = Rows;
        if (rows == null || item == null) return null;

        // A scan, and deliberately: this answers a click, not a layout pass, and a dictionary would have to be kept in
        // step with every splice for no gain at the rate a person can toggle rows.
        for (var i = 0; i < rows.Count; i++)
        {
            if (Equals(rows[i].Node, item)) return rows[i];
        }

        return null;
    }

    // A branch opening or shutting MOVES every row below it, and the details panels' exceptions are indexes into that
    // list - so they are found again here, where the list changed, and nowhere else.
    internal void ToggleRow(TreeRow row)
    {
        if (row is not { HasChildren: true }) return;
        _flattener.Toggle(row);
        RememberGroup(row);
        RebuildRowExceptions();
        RefreshRealizedRows();
    }

    internal void ExpandRow(TreeRow row)
    {
        if (row is not { HasChildren: true } || row.IsExpanded) return;
        _flattener.Expand(row);
        RememberGroup(row);
        RebuildRowExceptions();
        RefreshRealizedRows();
    }

    internal void CollapseRow(TreeRow row)
    {
        if (row is not { IsExpanded: true }) return;
        _flattener.Collapse(row);
        RememberGroup(row);
        RebuildRowExceptions();
        RefreshRealizedRows();
    }

    // Rows keep their own copy of depth, band and expander state, so a splice above them changes what they should show.
    internal void RefreshRealizedRows()
    {
        var rows = Rows;
        foreach (var index in ItemContainerGenerator.RealizedIndices)
        {
            if (ItemContainerGenerator.ContainerFromIndex(index) is not DataGridRow row) continue;
            if (rows != null && index >= 0 && index < rows.Count) PrepareContainer(row, rows[index]);
        }

        InvalidateRealizedRows();
    }

    private void InvalidateRealizedRows()
    {
        foreach (var index in ItemContainerGenerator.RealizedIndices)
        {
            (ItemContainerGenerator.ContainerFromIndex(index) as IMeasurableComponent)?.InvalidateMeasure();
        }
    }

    private TreeFlattener _flattener;
    private IEnumerable _roots;

    /// <summary>The flattened rows this grid actually realizes. Public for tests and for anything that needs to address a
    /// row by index - which, under virtualization, is the only way to address one that has no container.</summary>
    internal FlatRowCollection Rows => _flattener?.Rows;

    protected override void ApplyItemsSource(IEnumerable newValue)
    {
        if (_roots is INotifyCollectionChanged was) was.CollectionChanged -= OnSourceChanged;
        _roots = newValue;
        if (_roots is INotifyCollectionChanged now) now.CollectionChanged += OnSourceChanged;

        RebuildFlattener();
    }

    // A SHAPED view is a list of this control's own making, so the flattener's splices land on it and never on the
    // application's collection - a row removed while the table is sorted or filtered would stay on screen. Re-shaping is
    // the honest answer anyway: where a new row belongs depends on the sort.
    private void OnSourceChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (Filter != null || HasColumnFilters || SortColumn != null) RebuildFlattener();
    }

    /// <summary>Where a row's children come from, when a member path cannot say it - a lookup in a dictionary, a flat
    /// table joined on a parent id, children that are computed. Wins over <see cref="ChildrenPath"/>; set it and call
    /// <see cref="Refresh"/>.</summary>
    public Func<object, IEnumerable> ChildrenSelector { get; set; }

    /// <summary>Re-reads the tree from its source - what to call after changing <see cref="ChildrenSelector"/> or after
    /// the data changed in a way the collection did not announce.</summary>
    public void Refresh() => RebuildFlattener();

    // sameRows: the caller is only REORDERING what the table already holds. A total over a set does not care what order
    // the set is in, and working one out costs a reading of the column per row - measured at 25 ms per aggregating
    // column over ten thousand rows, paid by every sort for nothing. False by default: adding or removing a row is the
    // case that must never silently keep a stale total.
    private void RebuildFlattener(bool sameRows = false)
    {
        _flattener?.Clear();
        _rawChildren = ChildrenSelector ?? TreeChildResolver.ForPath(ChildrenPath);
        // EVERYTHING starts shut, a group exactly like a branch. Open was tried and is wrong: a table is grouped in
        // order to FOLD it, and an open group is the table back again with captions in it. Measured on the stand -
        // grouping ten thousand rows by Region and Batch makes 495 batch groups, and closing one only uncovered the
        // next open one, five hundred times over. A group the user HAS opened comes back open: that is what
        // IsGroupOpen answers, by path.
        _flattener = new TreeFlattener(ShapedChildrenOf, IsGroupOpen, static _ => false, DetailsFor);
        _shapedRoots = Group(Shape(_roots));
        _flattener.SetRoots(_shapedRoots);
        RebuildRowExceptions();
        Items.SetSource(_flattener.Rows);
        if (!sameRows) RefreshTotals();

        // The columns' Auto widths described the OLD data. Measuring them again against the rows on screen now is the
        // point of the reset - see DataGridColumnLayout.RecordMeasured.
        ResetAutoWidths();
    }

    private Func<object, IEnumerable> _rawChildren = static _ => null;

    /// <summary>The column the rows are sorted by, or null for the source's own order.</summary>
    public DataGridColumn SortColumn { get; private set; }

    /// <summary>Whether <see cref="SortColumn"/> runs the other way.</summary>
    public bool SortDescending { get; private set; }

    /// <summary>Which rows to keep, or null for all of them.</summary>
    public Func<object, bool> Filter { get; private set; }

    /// <summary>Sorts by a column, or clears the sort when given null. WITHIN SIBLINGS: a global reordering of a tree
    /// puts children beside strangers. In a flat table every row is a sibling, so this is the ordinary sort.</summary>
    public void SortBy(DataGridColumn column, bool descending = false)
    {
        SortColumn = column is { CanSort: true } ? column : null;
        SortDescending = descending;
        ResetAutoWidths();
        RebuildFlattener(sameRows: true);

        // The header strip reads the sort state in its MEASURE (that is where it syncs), so a sort that leaves the
        // widths alone never reaches it: the arrow appeared only when something else happened to re-measure the strip.
        RefreshStrips();
    }

    // EVERY strip that follows the columns sideways, not just the header band. Both it and the strip of totals sit
    // OUTSIDE the rows' scroller and carry the offset themselves, so both have to be told when that offset moves -
    // told only the headers, the totals stayed at the offset they were last measured at and stood under the wrong
    // columns the moment the table was scrolled.
    private void RefreshStrips()
    {
        (_headers as IMeasurableComponent)?.InvalidateMeasure();
        (_footer as IMeasurableComponent)?.InvalidateMeasure();
    }

    /// <summary>Keeps only rows the predicate accepts - AND their ancestors, because a match nobody can reach is not a
    /// match. An ancestor kept this way is a signpost, not a result.</summary>
    public void SetFilter(Func<object, bool> filter)
    {
        Filter = filter;
        ResetAutoWidths();
        RebuildFlattener();
    }

    /// <summary>This column's filter, made on demand. Editing it changes nothing until <see cref="ApplyFilters"/> is
    /// called - a filter is set up and then applied, never one keystroke at a time.</summary>
    public DataGridColumnFilter FilterFor(DataGridColumn column)
    {
        if (column == null) return null;
        _columnFilters ??= new Dictionary<DataGridColumn, DataGridColumnFilter>();
        if (!_columnFilters.TryGetValue(column, out var filter)) _columnFilters[column] = filter = new DataGridColumnFilter();
        return filter;
    }

    /// <summary>Whether this column is narrowing the table right now - what the header's funnel is lit by.</summary>
    public bool IsFiltered(DataGridColumn column) =>
        column != null && _columnFilters != null && _columnFilters.TryGetValue(column, out var filter) && filter.IsActive;

    /// <summary>Re-runs the rows through the column filters. Called when a filter is applied or cleared.</summary>
    public void ApplyFilters()
    {
        ResetAutoWidths();
        RebuildFlattener();
        RefreshStrips();
    }

    /// <summary>Drops one column's filter and re-runs the rest.</summary>
    public void ClearColumnFilter(DataGridColumn column)
    {
        if (column == null || _columnFilters == null || !_columnFilters.Remove(column)) return;
        ApplyFilters();
    }

    /// <summary>Every value this column holds, in the order first seen, taken from the WHOLE source and not from the
    /// rows on screen - a value list that showed only what is visible would be a list of what you already have.
    /// <para>Rows hidden by OTHER columns' filters are still counted: the point of the list is to get them back.</para></summary>
    public IReadOnlyList<object> DistinctValues(DataGridColumn column)
    {
        var values = new List<object>();
        if (column == null) return values;

        var seen = new HashSet<string>();
        Collect(ItemsSource, column, values, seen);
        return values;
    }

    private void Collect(IEnumerable source, DataGridColumn column, List<object> values, HashSet<string> seen)
    {
        if (source == null) return;

        foreach (var item in source)
        {
            var value = ValueOf(column, item);
            if (seen.Add(DataGridColumnFilter.Text(value))) values.Add(value);
            Collect(_rawChildren(item), column, values, seen);
        }
    }

    /// <summary>What a column stands for on one row - the one value sorting, filtering and copying all read. The
    /// column's own binding answers, so it is what the cell shows; a template column states a
    /// <see cref="DataGridColumn.SortMemberPath"/> instead.</summary>
    private object ValueOf(DataGridColumn column, object node) =>
        column.Binding != null ? column.Read(column.Binding, node) : column.ReadPath(node);

    private Dictionary<DataGridColumn, DataGridColumnFilter> _columnFilters;

    private bool HasColumnFilters
    {
        get
        {
            if (_columnFilters == null) return false;
            foreach (var pair in _columnFilters)
            {
                if (pair.Value.IsActive) return true;
            }

            return false;
        }
    }

    /// <summary>The columns the rows are grouped by, outermost first; empty means no grouping. Filled by dropping a
    /// header into the grouping strip, or through <see cref="GroupBy"/>.</summary>
    public DataGridGroupDescriptions GroupDescriptions { get; } = new();

    /// <summary>Groups the rows by a column, or removes it from the grouping when it is already there - what dropping a
    /// header into the grouping panel does.</summary>
    public void GroupBy(DataGridColumn column)
    {
        if (column == null) return;

        if (!GroupDescriptions.Remove(column)) GroupDescriptions.Add(column);
        RebuildFlattener();
        _groupPanel?.Sync();
    }

    /// <summary>Moves a column to another place in the grouping - what carrying its chip along the strip does. The
    /// order of <see cref="GroupDescriptions"/> IS the nesting, so this is how deep a column groups.</summary>
    public void MoveGrouping(int from, int to)
    {
        if (from < 0 || from >= GroupDescriptions.Count || to < 0 || to >= GroupDescriptions.Count || from == to) return;

        var column = GroupDescriptions[from];
        GroupDescriptions.RemoveAt(from);
        GroupDescriptions.Insert(to, column);

        // The nesting changed, so every path did: what was open was open in a grouping that no longer exists.
        _openGroups.Clear();
        RebuildFlattener();
        _groupPanel?.Sync();
    }

    /// <summary>Ungroups everything.</summary>
    public void ClearGrouping()
    {
        if (GroupDescriptions.Count == 0) return;

        GroupDescriptions.Clear();
        _openGroups.Clear();
        RebuildFlattener();
        _groupPanel?.Sync();
    }

    // WHICH GROUPS STAND OPEN, by path rather than by object. A sort, a filter or a regrouping builds every group
    // afresh, so remembering the objects would forget the user's work every time the table was re-shaped - open a
    // group, sort a column, and it shut again.
    private readonly HashSet<string> _openGroups = new();

    private bool IsGroupOpen(object node) => node is DataGridGroup group && _openGroups.Contains(group.Path);

    private void RememberGroup(TreeRow row)
    {
        if (row?.Node is not DataGridGroup group) return;

        if (row.IsExpanded) _openGroups.Add(group.Path);
        else _openGroups.Remove(group.Path);
    }

    // WHICH RECORDS SHOW THEIR PANEL, by the record itself - not by row number, which moves the moment a group opens or
    // a sort runs, and not by path, which a record has none of. Reference identity is what a record IS here.
    private readonly HashSet<object> _openDetails = new();

    private object DetailsFor(object node) =>
        RowDetailsTemplate != null && _openDetails.Contains(node) ? new DataGridRowDetails(node) : null;

    /// <summary>Whether this record is showing the panel under itself.</summary>
    public bool IsRowDetailsOpen(object item) => item != null && _openDetails.Contains(item);

    /// <summary>Shows or hides the panel under a record. Does nothing without a <see cref="RowDetailsTemplate"/>: there
    /// would be nothing to put in it, and an empty band opening under a row is a table that looks broken.</summary>
    public void ToggleRowDetails(object item)
    {
        if (item == null || RowDetailsTemplate == null) return;

        // Shutting one FORGETS its height: the panel is rebuilt from the template when it opens again, and a remembered
        // number would place the new one at the old one's size for a pass.
        if (!_openDetails.Remove(item)) _openDetails.Add(item);
        else _detailsHeights.Remove(item);

        RebuildFlattener(sameRows: true);
    }

    // Where the details rows sit and how much taller than a row each one stands - the panel's uniform stack plus these
    // few exceptions. Rebuilt whenever the flat list is, because an index is only meaningful against one list.
    private readonly List<(int Index, double Extra)> _rowExceptions = new();

    internal IReadOnlyList<(int Index, double Extra)> RowExtentExceptions => _rowExceptions;

    // The exceptions ARE the panels, in ascending order, so counting the ones above a row is a short walk over the
    // handful that are open rather than over the rows.
    private int DetailsRowsBefore(int index)
    {
        var count = 0;
        for (var i = 0; i < _rowExceptions.Count && _rowExceptions[i].Index < index; i++) count++;
        return count;
    }

    // What each open panel MEASURED, by the record it belongs to. Kept apart from the flat list because a panel's height
    // is a property of its content, not of where the row happens to sit: a sort moves the row and the panel is the same
    // height it was.
    private readonly Dictionary<object, double> _detailsHeights = new();

    /// <summary>How tall a record's panel stands: what it measured, or <see cref="RowDetailsHeight"/> until it has.
    /// The default is the FIRST GUESS rather than the answer - a panel is placed before it is built, and the stack has
    /// to be told some number to put the rows below it at.</summary>
    public double HeightOfRowDetails(object item) =>
        item != null && _detailsHeights.TryGetValue(item, out var measured) ? measured : RowDetailsHeight;

    // A realized panel reporting what its content came to - the same shape as the number strip reporting its width, and
    // for the same reason: what a thing takes cannot be known before it is built, and the pass that placed it ran first.
    // BOTH ways, unlike the strip: a tab switched inside a panel makes it shorter as readily as taller, and a height
    // that only ever grew would leave a band of nothing under the short one.
    /// <summary>What a built panel came to. Public because it is the seam a test drives: a tab switched inside a panel
    /// is exactly this call, and reaching it through a real layout pass would test the tab control instead.</summary>
    public void ReportRowDetailsHeight(object item, double height)
    {
        if (item == null || height <= 0) return;
        if (_detailsHeights.TryGetValue(item, out var known) && Math.Abs(known - height) < 0.5) return;

        _detailsHeights[item] = height;
        RefreshRowExceptionHeights();
        _detailsHeightChanged = true;
        InvalidateMeasure();
    }

    // The report is made from INSIDE the rows panel's own measure, and a virtualizing panel mutes its invalidation for
    // the length of that pass - so telling it there is telling nobody. The flag carries the news out to the end of this
    // grid's measure, where the panel is no longer measuring and can be asked again, exactly as a re-grown Auto column
    // is handled below.
    private bool _detailsHeightChanged;

    // WHERE the panels are. A walk over every row, so it runs only when the flat list itself changed - a rebuild, a
    // branch opened or shut - because that is the only thing that can move an index. Ten thousand rows walked on every
    // measure is what a panel reporting its height used to cost: eight open panels, twice a frame, and the scroll
    // stuttered.
    private void RebuildRowExceptions()
    {
        _rowExceptions.Clear();
        if (_openDetails.Count == 0 || Rows == null) return;

        for (var i = 0; i < Rows.Count; i++)
        {
            if (Rows[i].Node is DataGridRowDetails details)
                _rowExceptions.Add((i, HeightOfRowDetails(details.Item) - RowHeight));
        }
    }

    // ...and HOW TALL they are, which changes without anything moving (a tab switched inside one). The rows are where
    // they were, so this re-reads the few exceptions in place instead of looking for them again.
    private void RefreshRowExceptionHeights()
    {
        var rows = Rows;
        if (rows == null) return;

        for (var i = 0; i < _rowExceptions.Count; i++)
        {
            var at = _rowExceptions[i].Index;
            if (at >= 0 && at < rows.Count && rows[at].Node is DataGridRowDetails details)
                _rowExceptions[i] = (at, HeightOfRowDetails(details.Item) - RowHeight);
        }
    }

    /// <summary>Shuts every open panel.</summary>
    public void CollapseAllRowDetails()
    {
        if (_openDetails.Count == 0) return;

        _openDetails.Clear();
        RebuildFlattener(sameRows: true);
    }

    /// <summary>What a record's panel is built from, bound against the record itself. Null (the default) means the table
    /// has no such panel at all and no row offers to open one.</summary>
    public static readonly AdamantiumProperty RowDetailsTemplateProperty = AdamantiumProperty.Register(
        nameof(RowDetailsTemplate), typeof(DataTemplate), typeof(TreeDataGrid),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure, OnRowDetailsTemplateChanged));

    /// <summary>How tall that panel stands. One number for every panel, because the rows are virtualized against a
    /// uniform pitch and a panel that measured itself would have to be measured before it was built.</summary>
    public static readonly AdamantiumProperty RowDetailsHeightProperty = AdamantiumProperty.Register(
        nameof(RowDetailsHeight), typeof(Double), typeof(TreeDataGrid),
        new PropertyMetadata(160.0, PropertyMetadataOptions.AffectsMeasure));

    public DataTemplate RowDetailsTemplate
    {
        get => GetValue<DataTemplate>(RowDetailsTemplateProperty);
        set => SetValue(RowDetailsTemplateProperty, value);
    }

    public Double RowDetailsHeight
    {
        get => GetValue<Double>(RowDetailsHeightProperty);
        set => SetValue(RowDetailsHeightProperty, value);
    }

    // Taking the template away shuts every panel with it: the rows would otherwise stand open over nothing.
    private static void OnRowDetailsTemplateChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
    {
        if (d is TreeDataGrid { RowDetailsTemplate: null } grid) grid.CollapseAllRowDetails();
    }

    /// <summary>Opens every group down to <paramref name="depth"/> levels and shuts the rest: 0 folds the table to its
    /// outermost captions, <see cref="GroupDescriptions"/>.Count opens all of it.
    /// <para>A table grouped two or three columns deep is not something anyone opens by hand - ten thousand rows by
    /// region and batch make 495 captions.</para></summary>
    public void ExpandGroupsTo(int depth)
    {
        if (GroupDescriptions.Count == 0) return;

        _openGroups.Clear();
        foreach (var group in AllGroups())
        {
            if (group.Level < depth) _openGroups.Add(group.Path);
        }

        RebuildFlattener(sameRows: true);
    }

    /// <summary>Opens every group, however deep.</summary>
    public void ExpandAllGroups() => ExpandGroupsTo(GroupDescriptions.Count);

    /// <summary>Shuts every group, leaving the outermost captions.</summary>
    public void CollapseAllGroups() => ExpandGroupsTo(0);

    private IEnumerable _shapedRoots;

    // Groups are built ONCE, over the shaped (filtered, sorted) items, and handed to the flattener as its roots. From
    // there nothing else in the control knows about grouping: a group is a node with children, so expanding one is the
    // same splice as expanding a branch and the virtualizer realizes its header like any other row.
    private IEnumerable Group(IEnumerable source)
    {
        if (source == null || GroupDescriptions.Count == 0) return source;

        return BuildGroups(source, 0, string.Empty);
    }

    // U+001F, the ASCII unit separator - invisible in an editor, and the one character a key's TEXT cannot contain, so
    // "Iberia" inside "B-01" can never read as the same path as some other pairing of the two.
    private const string GroupPathSeparator = "";

    private List<object> BuildGroups(IEnumerable source, int level, string parentPath)
    {
        var column = GroupDescriptions[level];
        var order = new List<object>();
        var byKey = new Dictionary<string, DataGridGroup>();

        foreach (var item in source)
        {
            var key = ValueOf(column, item);
            // Keyed by the value's TEXT, which is what the header shows and what the filter list already keys by - two
            // rows that read the same in the table belong in one group whatever their boxes say.
            var id = DataGridColumnFilter.Text(key);

            if (!byKey.TryGetValue(id, out var group))
            {
                group = new DataGridGroup(column, key, level, parentPath + GroupPathSeparator + id);
                byKey[id] = group;
                order.Add(group);
            }

            group.Add(item);
        }

        if (level + 1 < GroupDescriptions.Count)
        {
            foreach (DataGridGroup group in order)
            {
                group.Replace(BuildGroups(new List<object>(group.Items), level + 1, group.Path));
            }
        }

        return order;
    }

    private IEnumerable ShapedChildrenOf(object node) =>
        node is DataGridGroup group ? group.Children : Shape(_rawChildren(node));

    /// <summary>Whether any column asks for a total. The footer band exists exactly when one does - a strip that is
    /// always there and always empty is a strip nobody wanted - so the theme hangs the band on this.</summary>
    public static readonly AdamantiumProperty HasTotalsProperty = AdamantiumProperty.Register(nameof(HasTotals),
        typeof(bool), typeof(TreeDataGrid),
        new PropertyMetadata(false, PropertyMetadataOptions.AffectsMeasure));

    /// <summary>Whether the strip that a header is dropped into to group by it is shown.</summary>
    public static readonly AdamantiumProperty ShowGroupPanelProperty = AdamantiumProperty.Register(
        nameof(ShowGroupPanel), typeof(bool), typeof(TreeDataGrid),
        new PropertyMetadata(false, PropertyMetadataOptions.AffectsMeasure, OnShowGroupPanelChanged));

    // TAKING THE STRIP AWAY UNGROUPS. The strip is where a grouping is shown and where it is undone, so hiding it over
    // a grouped table would stand the table in groups with nothing saying why and no way back. Only this TRANSITION
    // does it: a page that never shows the strip and groups from code is left alone, because nothing ever turns off.
    private static void OnShowGroupPanelChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
    {
        if (d is TreeDataGrid { ShowGroupPanel: false } grid) grid.ClearGrouping();
    }

    public bool HasTotals
    {
        get => GetValue<bool>(HasTotalsProperty);
        private set => SetValue(HasTotalsProperty, value);
    }

    public bool ShowGroupPanel
    {
        get => GetValue<bool>(ShowGroupPanelProperty);
        set => SetValue(ShowGroupPanelProperty, value);
    }

    private bool AnyColumnAggregates()
    {
        foreach (var column in Columns)
        {
            if (column.Aggregate != DataGridAggregate.None) return true;
        }

        return false;
    }

    private readonly DataGridSearch _search = new();

    /// <summary>What the search panel is looking for. Setting it does NOT search - <see cref="Search"/> does, because
    /// one pass reads every shown column of every row and that is not something to spend on each keystroke: measured
    /// at some 380 ms over ten thousand rows and thirteen columns. TWO-WAY by default: the strip's own field writes
    /// here, so a page binding it and never learning what was typed would be the binding lying about the control.</summary>
    public static readonly AdamantiumProperty SearchTextProperty = AdamantiumProperty.Register(nameof(SearchText),
        typeof(String), typeof(TreeDataGrid),
        new PropertyMetadata(null, PropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>Whether the strip that searches the table is shown. TWO-WAY by default for the same reason: the strip
    /// closes ITSELF from its own button, and a one-way binding would leave the switch that opened it standing on.</summary>
    public static readonly AdamantiumProperty ShowSearchPanelProperty = AdamantiumProperty.Register(
        nameof(ShowSearchPanel), typeof(bool), typeof(TreeDataGrid),
        new PropertyMetadata(false, PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.BindsTwoWayByDefault,
            OnShowSearchPanelChanged));

    public String SearchText
    {
        get => GetValue<String>(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public bool ShowSearchPanel
    {
        get => GetValue<bool>(ShowSearchPanelProperty);
        set => SetValue(ShowSearchPanelProperty, value);
    }

    // Taking the strip away calls the search off, for the same reason taking the grouping strip away ungroups: what it
    // paints over the table would otherwise stay with nothing left to explain or undo it.
    private static void OnShowSearchPanelChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
    {
        if (d is TreeDataGrid { ShowSearchPanel: false } grid) grid.ClearSearch();
    }

    /// <summary>What a cell the search found is washed with, or null to leave it to the theme. A WASH, never a plate:
    /// a cell holds a check box and a meaning, and a solid colour swallows both.</summary>
    public static readonly AdamantiumProperty SearchMatchBrushProperty = AdamantiumProperty.Register(
        nameof(SearchMatchBrush), typeof(Brush), typeof(TreeDataGrid),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender, OnSearchBrushChanged));

    /// <summary>...and what the cell the search is ON is washed with.</summary>
    public static readonly AdamantiumProperty SearchCurrentMatchBrushProperty = AdamantiumProperty.Register(
        nameof(SearchCurrentMatchBrush), typeof(Brush), typeof(TreeDataGrid),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender, OnSearchBrushChanged));

    public Brush SearchMatchBrush
    {
        get => GetValue<Brush>(SearchMatchBrushProperty);
        set => SetValue(SearchMatchBrushProperty, value);
    }

    public Brush SearchCurrentMatchBrush
    {
        get => GetValue<Brush>(SearchCurrentMatchBrushProperty);
        set => SetValue(SearchCurrentMatchBrushProperty, value);
    }

    // The cells take their colours when they are attached, so the ones already built have to be told.
    private static void OnSearchBrushChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e) =>
        (d as TreeDataGrid)?.RefreshRealizedRows();

    /// <summary>How many cells hold what is being searched for.</summary>
    public int MatchCount => _search.Count;

    /// <summary>Which match is being looked at, from 1; 0 when none is.</summary>
    public int CurrentMatch => _search.Current;

    /// <summary>Runs the search over what the table HOLDS - every shown column of every row behind the current shape,
    /// open or shut - and goes to the first match. A cell inside a closed group counts, and stepping onto it opens
    /// that group: a count that only covered what happens to be unfolded would be a count of the screen, not of the
    /// table.</summary>
    public void Search()
    {
        var sought = SearchText;
        _search.Begin(sought);

        // A RUN of its own, so a search started while another is still walking simply takes over: the old one sees a
        // number that is no longer its and stops where it stands.
        _searchRun++;
        _searchAt = 0;
        _searchItems = null;

        if (string.IsNullOrEmpty(sought))
        {
            RefreshSearchVisuals();
            return;
        }

        var items = new List<object>();
        CollectItems(_shapedRoots, items);
        _searchItems = items;

        // OFF THE INTERFACE'S THREAD when every column it has to read can be read there - see
        // DataGridColumn.ReadsWithoutTheUI. Matches come back in batches, so the table lights up while the walk is
        // still going. When some column needs its binding, the walk stays on the loop and is spread over its turns
        // instead: the binding engine belongs to that thread and cannot be taken off it.
        if (SearchReadsWithoutTheUI()) WalkSearchAway(_searchRun, items);
        else WalkSearch(_searchRun);
    }

    private bool SearchReadsWithoutTheUI()
    {
        foreach (var column in Columns)
        {
            if (!column.IsShown || !column.IsSearchable) continue;
            if (!column.ReadsWithoutTheUI) return false;
        }

        return true;
    }

    /// <summary>Whether a search is still walking the table.</summary>
    public bool IsSearching => _searchItems != null;

    // How many rows one turn of the search reads. Small enough that a turn is a few milliseconds: the whole pass over
    // ten thousand rows takes some 380 ms, and a window that stops answering for that long reads as a hang.
    private const int SearchChunk = 256;

    private int _searchRun;
    private int _searchAt;
    private List<object> _searchItems;

    // The walk runs on a THREAD OF ITS OWN and sends what it finds back to the loop in batches, so the table lights
    // up while it is still going. What it reads are plain getters on the application's own objects - no part of the
    // interface is touched from here, which is the whole reason this is allowed.
    private void WalkSearchAway(int run, List<object> items)
    {
        var loop = UIAppContext.Current?.Dispatcher;
        if (loop == null)
        {
            // Nothing to come back to - a test, a grid built by hand. Then it is simply done here and now.
            WalkSearch(run);
            return;
        }

        var sought = _search.Text;
        var columns = SearchableColumns();

        Task.Run(() =>
        {
            var batch = new List<(object Item, int Column)>();

            for (var at = 0; at < items.Count; at++)
            {
                if (run != _searchRun) return;   // a newer search took over

                var item = items[at];
                foreach (var (index, column) in columns)
                {
                    if (DataGridSearch.Matches(DataGridColumnFilter.Text(column.ReadWithoutTheUI(item)), sought))
                    {
                        batch.Add((item, index));
                    }
                }

                if (batch.Count == 0 || (at + 1) % SearchChunk != 0) continue;

                var found = batch;
                batch = new List<(object, int)>();
                loop.Post(() => TakeSearchBatch(run, found, false));
            }

            var last = batch;
            loop.Post(() => TakeSearchBatch(run, last, true));
        });
    }

    private List<(int Index, DataGridColumn Column)> SearchableColumns()
    {
        var columns = new List<(int, DataGridColumn)>();
        for (var i = 0; i < Columns.Count; i++)
        {
            if (Columns[i].IsShown && Columns[i].IsSearchable) columns.Add((i, Columns[i]));
        }

        return columns;
    }

    // What the walk found, taken on the LOOP: the search state and everything painted from it belong to this thread.
    private void TakeSearchBatch(int run, List<(object Item, int Column)> found, bool last)
    {
        if (run != _searchRun) return;

        var hadNone = _search.Count == 0;
        foreach (var (item, column) in found) _search.Add(item, column);

        // The FIRST match is shown the moment it arrives, not when the walk ends.
        if (hadNone && _search.Count > 0)
        {
            _search.Step(1);
            GoToMatch();
        }

        if (last) _searchItems = null;
        RefreshSearchVisuals();
    }

    // The pass is spread over the UI LOOP, one chunk per turn, when some column has to be read through its BINDING:
    // the binding engine belongs to that thread and cannot be taken off it. With no loop to spread over - a test, a
    // grid built by hand - it is done here and now instead.
    private void WalkSearch(int run)
    {
        if (run != _searchRun) return;

        var loop = UIAppContext.Current?.Dispatcher;

        do
        {
            SearchOneChunk();
        }
        while (_searchItems != null && loop == null);

        if (_searchItems != null) loop.Post(() => WalkSearch(run));
    }

    private void SearchOneChunk()
    {
        var sought = _search.Text;
        var stop = Math.Min(_searchAt + SearchChunk, _searchItems.Count);
        var hadNone = _search.Count == 0;

        for (; _searchAt < stop; _searchAt++)
        {
            var item = _searchItems[_searchAt];
            for (var i = 0; i < Columns.Count; i++)
            {
                var column = Columns[i];
                if (!column.IsShown || !column.IsSearchable) continue;

                // The SAME reading the walk on its own thread does: cheap where that is the same answer, through the
                // binding where it is not. One rule, so the two passes can never find different things.
                var value = column.ReadsWithoutTheUI ? column.ReadWithoutTheUI(item) : ValueOf(column, item);
                if (DataGridSearch.Matches(DataGridColumnFilter.Text(value), sought)) _search.Add(item, i);
            }
        }

        // The FIRST match is shown the moment it is found, not when the walk ends: a hit on the first screen should
        // not wait behind ten thousand rows that are read after it.
        if (hadNone && _search.Count > 0)
        {
            _search.Step(1);
            GoToMatch();
        }

        if (_searchAt >= _searchItems.Count) _searchItems = null;
        RefreshSearchVisuals();
    }

    /// <summary>Forgets the search and the paint that goes with it.</summary>
    public void ClearSearch()
    {
        if (_search.Count == 0 && _search.Current == 0) return;

        _search.Clear();
        RefreshSearchVisuals();
    }

    /// <summary>Steps to the next match and shows it, wrapping round at the end.</summary>
    public void FindNext() => StepSearch(1);

    /// <summary>Steps to the previous match and shows it, wrapping round at the start.</summary>
    public void FindPrevious() => StepSearch(-1);

    private void StepSearch(int by)
    {
        if (!_search.Step(by)) return;

        GoToMatch();
        RefreshSearchVisuals();
    }

    // The rows carry the paint, the strip carries the count - both have to be told, and told in one place, or the
    // strip goes on saying "3 of 47" over a table that is no longer showing any of them.
    private void RefreshSearchVisuals()
    {
        RefreshRealizedRows();
        _searchPanel?.Sync();
    }

    // Puts the current match on screen: opens whatever groups hold it, then takes the keyboard to it - which is what
    // scrolls the rows, since the cell the keyboard is on is the cell the table keeps in view.
    private void GoToMatch()
    {
        if (_search.At is not { } at) return;

        var row = RowIndexOf(at.Item);
        if (row < 0)
        {
            RevealItem(at.Item);
            row = RowIndexOf(at.Item);
        }

        if (row < 0) return;

        SelectCell(row, at.Column);
        ScrollIntoView(row, at.Column);
    }

    /// <summary>Scrolls the least it can so a cell is fully in view. By INDEX, not by container: the row a search
    /// landed on is usually thousands of rows away and has no element at all - which is exactly when it is needed.
    /// <para>The rows stand at a uniform step, so where one is takes no walk to work out.</para></summary>
    public void ScrollIntoView(int row, int column = -1)
    {
        if (_scroll == null || row < 0) return;

        var height = RowHeight > 0 ? RowHeight : 1;
        var x = _scroll.ScrollOffset.X;
        var width = 1.0;

        // The column too, when one is named: a match in a column that is off to the right is not shown by scrolling
        // down to its row. A PINNED column needs no scrolling - it is never off screen.
        if (column >= 0 && column < Columns.Count && Columns[column] is { IsFrozen: false } wanted)
        {
            x = wanted.Offset;
            width = wanted.ActualWidth;
        }

        _scroll.BringIntoView(new Rect(x, row * height, width, height));
    }

    private int RowIndexOf(object item)
    {
        var rows = Rows;
        if (rows == null) return -1;

        for (var i = 0; i < rows.Count; i++)
        {
            if (ReferenceEquals(rows[i].Node, item)) return i;
        }

        return -1;
    }

    // Opens the chain of groups that holds an item, outermost first. A match inside a shut group is still a match, and
    // a Find that could not reach it would be counting things it cannot show.
    private void RevealItem(object item)
    {
        if (GroupDescriptions.Count == 0) return;

        var level = _shapedRoots;
        while (level != null)
        {
            DataGridGroup holding = null;
            foreach (var node in level)
            {
                if (node is DataGridGroup group && group.Items.Contains(item))
                {
                    holding = group;
                    break;
                }
            }

            if (holding == null) break;

            _openGroups.Add(holding.Path);
            level = holding.Children;
        }

        RebuildFlattener(sameRows: true);
    }

    /// <summary>Whether this cell holds what is being searched for - what a realized cell paints itself from.</summary>
    internal bool IsSearchMatch(object item, int column) => _search.Holds(item, column);

    /// <summary>Whether this cell is the match being looked at.</summary>
    internal bool IsCurrentSearchMatch(object item, int column) => _search.IsCurrent(item, column);

    private readonly Dictionary<DataGridColumn, object> _totals = new();

    /// <summary>Re-reads every column's total. Worked out ONCE per shape - a walk of the data per frame is what a
    /// footer must never cost - and again when a column changes what it asks for.</summary>
    public void RefreshTotals()
    {
        _totals.Clear();

        foreach (var group in AllGroups()) group.ForgetTotals();

        HasTotals = AnyColumnAggregates();

        if (HasTotals)
        {
            var items = new List<object>();
            CollectItems(_shapedRoots, items);

            foreach (var column in Columns)
            {
                if (column.Aggregate == DataGridAggregate.None) continue;

                _totals[column] = DataGridAggregates.Compute(column.Aggregate, Values(column, items));
            }
        }

        _footer?.Sync();
        RefreshRealizedRows();
    }

    /// <summary>This column's total over the whole table, or null when it asks for none.</summary>
    public object TotalFor(DataGridColumn column) =>
        column != null && _totals.TryGetValue(column, out var total) ? total : null;

    /// <summary>This column's total within one group - the number its header shows.</summary>
    public object TotalFor(DataGridColumn column, DataGridGroup group)
    {
        if (column == null || group == null || column.Aggregate == DataGridAggregate.None) return null;

        return group.TotalFor(column, (c, g) => DataGridAggregates.Compute(c.Aggregate, Values(c, g.Items)));
    }

    private IEnumerable<object> Values(DataGridColumn column, IEnumerable<object> items)
    {
        foreach (var item in items) yield return ValueOf(column, item);
    }

    // The rows of the TOP level behind the current shape - groups looked through, tree children NOT. A total counts what
    // the table holds rather than what is on screen, so a collapsed group changes nothing; but a branch's children
    // belong to that branch, and counting them here would make the table's total disagree with the sum of its groups'
    // (measured: 58000 against 5 x 2000) and would double-count any hierarchy that rolls up into its parent.
    private void CollectItems(IEnumerable source, List<object> into)
    {
        if (source == null) return;

        foreach (var node in source)
        {
            if (node is DataGridGroup group)
            {
                CollectItems(group.Children, into);
                continue;
            }

            into.Add(node);
        }
    }

    private IEnumerable<DataGridGroup> AllGroups()
    {
        if (_shapedRoots == null || GroupDescriptions.Count == 0) yield break;

        var pending = new Stack<object>();
        foreach (var node in _shapedRoots) pending.Push(node);

        while (pending.Count > 0)
        {
            if (pending.Pop() is not DataGridGroup group) continue;

            yield return group;
            foreach (var child in group.Children) pending.Push(child);
        }
    }

    private IEnumerable Shape(IEnumerable source)
    {
        if (source == null) return null;

        var filtering = Filter != null || HasColumnFilters;
        if (!filtering && SortColumn == null) return source;

        var items = new List<object>();
        foreach (var item in source)
        {
            if (!filtering || Keep(item)) items.Add(item);
        }

        // Sorted by the SAME value the cells show and the filters test - one reading of a column, never two.
        if (SortColumn is not { } column) return items;

        // The READINGS are sorted, not the rows: a comparison sort asks 2n log n times, and reading a column is not
        // free - it re-points a live binding at the row. Measured at 3900 readings for 256 rows where 256 will do, and
        // 889 ms to sort ten thousand.
        var rows = items.ToArray();
        var keys = new object[rows.Length];
        for (var i = 0; i < rows.Length; i++) keys[i] = ValueOf(column, rows[i]);

        Array.Sort(keys, rows, SortDescending ? Descending : Ascending);
        return rows;
    }

    private static readonly IComparer<object> Ascending = Comparer<object>.Create(Compare);
    private static readonly IComparer<object> Descending = Comparer<object>.Create(static (a, b) => Compare(b, a));

    // A node survives the filter if it matches, or if anything under it does - otherwise the match would be unreachable.
    private bool Keep(object node)
    {
        if (Matches(node)) return true;

        if (_rawChildren(node) is not { } children) return false;
        foreach (var child in children)
        {
            if (Keep(child)) return true;
        }

        return false;
    }

    // The app's own filter and every column's, as one question. A row has to satisfy all of them - that is what a second
    // filtered column MEANS, and it is why they are asked together rather than one narrowing the other's leftovers.
    private bool Matches(object node)
    {
        if (Filter != null && !Filter(node)) return false;
        if (_columnFilters == null) return true;

        foreach (var pair in _columnFilters)
        {
            if (pair.Value.IsActive && !pair.Value.Passes(ValueOf(pair.Key, node))) return false;
        }

        return true;
    }

    private static int Compare(object a, object b)
    {
        if (ReferenceEquals(a, b)) return 0;
        if (a == null) return -1;
        if (b == null) return 1;
        return a is IComparable comparable && a.GetType() == b.GetType()
            ? comparable.CompareTo(b)
            : string.Compare(a.ToString(), b.ToString(), StringComparison.CurrentCulture);
    }

    /// <summary>Moves a column to another place in the display order - what dragging its header does. The expander
    /// follows its OWN column rather than staying on an index that now belongs to another one.</summary>
    public void MoveColumn(int from, int to)
    {
        if (from < 0 || from >= Columns.Count || to < 0 || to >= Columns.Count || from == to) return;

        var carrier = ExpanderColumn;
        var column = Columns[from];

        Columns.RemoveAt(from);
        Columns.Insert(to, column);

        if (carrier != null)
        {
            var index = Columns.IndexOf(carrier);
            if (index >= 0) ExpanderColumnIndex = index;
        }

        RefreshStrips();
    }

    private void OnColumnsChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        // A column JOINS the grid's logical tree - that is what gives it a DataContext, and with it {Binding},
        // {Ancestor} and every resource lookup on its own properties. Without this step a column is an object floating
        // beside the tree, and a list of choices owned by a view-model is unreachable from it.
        foreach (var gone in e.OldItems?.OfType<DataGridColumn>() ?? Enumerable.Empty<DataGridColumn>())
            RemoveLogicalChild(gone);

        foreach (var added in e.NewItems?.OfType<DataGridColumn>() ?? Enumerable.Empty<DataGridColumn>())
            AddLogicalChild(added);

        ResetAutoWidths();

        // The rows resync inside their own measure - but a row whose measure is still VALID short-circuits and never
        // runs it, so the pass has to be asked for on each of them, not just on the grid.
        InvalidateRealizedRows();
        InvalidateMeasure();
        (ItemsHostPanel as IMeasurableComponent)?.InvalidateMeasure();
    }

    /// <summary>Fits a column to the widest cell VISIBLE NOW plus its header - what a double-click on the separator does.
    /// Only the visible ones, and it cannot be otherwise: measuring the rows it has not built would mean building them,
    /// which is the whole cost virtualizing avoids. So a second double-click after scrolling may well give another width.</summary>
    public void FitColumn(int index)
    {
        if (index < 0 || index >= Columns.Count) return;

        var column = Columns[index];
        double widest = 0;

        foreach (var realized in ItemContainerGenerator.RealizedIndices)
        {
            if (ItemContainerGenerator.ContainerFromIndex(realized) is not DataGridRow row) continue;
            if (row.CellAt(index) is not { } cell) continue;
            cell.Measure(new Size(double.PositiveInfinity, Double.IsNaN(RowHeight) ? double.PositiveInfinity : RowHeight));
            widest = Math.Max(widest, cell.DesiredSize.Width);
        }

        if (widest <= 0) return;
        // At the slot the width already sits in - see the resize in DataGridHeadersPresenter.
        column.SetCurrentValue(DataGridColumn.WidthProperty,
            new GridLength(Math.Clamp(widest, column.MinWidth, column.MaxWidth)));
        column.ResetMeasuredWidth();
        InvalidateColumns();
    }

    /// <summary>Re-runs the width pass and the rows that depend on it.</summary>
    public void InvalidateColumns()
    {
        // RE-SYNCED, not merely re-measured: a column change changes WHICH CELLS a row has, and a row builds cells
        // only in its measure.
        RefreshRealizedRows();
        InvalidateMeasure();
        (ItemsHostPanel as IMeasurableComponent)?.InvalidateMeasure();

        // EVERY strip placed by the width pass, not just the headers: each one measures its parts at the columns'
        // widths, and a measure nobody invalidates never runs. The strip of totals kept the width it had, so a widened
        // column left its total trimmed to an ellipsis.
        (_headers as IMeasurableComponent)?.InvalidateMeasure();
        (_footer as IMeasurableComponent)?.InvalidateMeasure();
    }

    private DataGridHeadersPresenter _headers;
    private DataGridFooterPresenter _footer;
    private DataGridGroupPanel _groupPanel;
    private DataGridSearchPanel _searchPanel;
    private MeasurableUIComponent _dropIndicator;
    private ScrollViewer _scroll;

    internal void AdoptHeaders(DataGridHeadersPresenter headers) => _headers = headers;

    internal void AdoptFooter(DataGridFooterPresenter footer) => _footer = footer;

    internal void AdoptGroupPanel(DataGridGroupPanel panel) => _groupPanel = panel;

    internal void AdoptSearchPanel(DataGridSearchPanel panel) => _searchPanel = panel;

    /// <summary>Whether <paramref name="point"/>, given in <paramref name="from"/>'s space, is over the grouping strip -
    /// what the header band asks before it decides whether a dropped column is being MOVED or being grouped by.</summary>
    internal bool IsOverGroupPanel(IUIComponent from, Vector2 point)
    {
        if (_groupPanel == null || !ShowGroupPanel || from == null) return false;

        var local = from.TranslatePoint(point, _groupPanel);
        var size = _groupPanel.RenderSize;
        return local.X >= 0 && local.Y >= 0 && local.X <= size.Width && local.Y <= size.Height;
    }

    /// <summary>Marks the grouping strip as the place a carried header would land, or takes the mark off it.</summary>
    internal void MarkGroupPanelTarget(bool over)
    {
        if (_groupPanel != null) _groupPanel.IsDropTarget = over;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("PART_Headers") is DataGridHeadersPresenter headers) headers.Owner = this;
        if (GetTemplateChild("PART_Footer") is DataGridFooterPresenter footer) footer.Owner = this;
        if (GetTemplateChild("PART_GroupPanel") is DataGridGroupPanel groupPanel) groupPanel.Owner = this;
        if (GetTemplateChild("PART_SearchPanel") is DataGridSearchPanel searchPanel) searchPanel.Owner = this;

        _dropIndicator = GetTemplateChild("PART_DropIndicator") as MeasurableUIComponent;

        if (_scroll != null) _scroll.ScrollChanged -= OnScrolled;
        _scroll = GetTemplateChild("PART_ScrollHost") as ScrollViewer;
        if (_scroll != null) _scroll.ScrollChanged += OnScrolled;
    }

    public override void OnRemoveTemplate()
    {
        base.OnRemoveTemplate();
        if (_scroll != null) _scroll.ScrollChanged -= OnScrolled;
        _scroll = null;
    }

    // The rows scroll sideways inside their scroller; the HEADER strip is outside it (it must not scroll away
    // vertically), so it is moved by hand - and the column window is taken from the same number, so what is realized is
    // what can be seen.
    private void OnScrolled(object sender, EventArgs e) => HorizontalOffset = _scroll?.ScrollOffset.X ?? 0;

    /// <summary>Forgets what the Auto columns had settled on, so the next pass takes their widths from the rows visible
    /// NOW. Called on a source change, a column change, a sort, a filter, and a double-click on a separator.</summary>
    public void ResetAutoWidths()
    {
        foreach (var column in Columns) column.ResetMeasuredWidth();
    }

    /// <summary>A realized cell reporting what it would have taken unbounded. Only Auto columns care, and only growth
    /// counts.</summary>
    internal void ReportAutoWidth(DataGridColumn column, double desired)
    {
        if (DataGridColumnLayout.RecordMeasured(column, desired)) _autoWidthGrew = true;
    }

    private bool _autoWidthGrew;

    /// <summary>Total width of the columns as the last pass sized them.</summary>
    public double ColumnsWidth { get; private set; }

    /// <summary>How wide the LEFT pinned zone is: the row numbers first, then the columns pinned left in their declared
    /// order, and the scrolling ones start after it. Zero when nothing is pinned there.</summary>
    public double FrozenWidth { get; private set; }

    /// <summary>How wide the RIGHT pinned zone is. Zero when nothing is pinned there.</summary>
    public double RightFrozenWidth { get; private set; }

    /// <summary>Slides the right-pinned columns from the end of the content, where the width pass puts them, back to
    /// the viewport's right edge. ONE number for the whole zone, and it ALREADY carries the scroll. Zero when the table
    /// does not overflow, and zero again when it is scrolled fully across.</summary>
    /// <remarks>Measured against THE SCROLLER's own extent, not against <see cref="ColumnsWidth"/>: the two differ by
    /// whatever the presenter adds around the columns, and the difference showed as the zone stepping a couple of
    /// pixels sideways at the far end - the one place where the two definitions of "fully scrolled" disagree.</remarks>
    internal double RightPinShift
    {
        get
        {
            if (RightFrozenWidth <= 0) return 0;

            var travel = (_scroll?.ExtentSize.Width ?? ColumnsWidth) - (_scroll?.ViewportSize.Width ?? _viewportWidth);
            return travel <= 0 || double.IsInfinity(travel) ? 0 : Math.Min(0, HorizontalOffset - travel);
        }
    }

    /// <summary>The left strip with the row's ordinal - and the handle for the row as a whole: pressing a number takes
    /// the row, pressing the corner takes the table. Off by default: a strip that costs a column of width is a choice,
    /// not a default.</summary>
    public static readonly AdamantiumProperty ShowRowNumbersProperty = AdamantiumProperty.Register(
        nameof(ShowRowNumbers), typeof(bool), typeof(TreeDataGrid),
        new PropertyMetadata(false, PropertyMetadataOptions.AffectsMeasure, OnShowRowNumbersChanged));

    public bool ShowRowNumbers
    {
        get => GetValue<bool>(ShowRowNumbersProperty);
        set => SetValue(ShowRowNumbersProperty, value);
    }

    /// <summary>How wide the number strip came out - as wide as the widest number REALIZED so far, so five digits are
    /// not clipped and two digits do not reserve five. Zero when the strip is off.</summary>
    public double RowNumberWidth { get; private set; }

    // What the WIDTH PASS is given: the measured width while the strip is shown, nought while it is not. The measured
    // number is kept either way, so switching the strip off and on again does not have to discover it a second time -
    // and the pass in which it was still being discovered is exactly when the numbers were drawn over the first column.
    internal double NumberStripLeading => ShowRowNumbers ? RowNumberWidth : 0;

    /// <summary>How wide the column of details toggles is. One number rather than a measured one: every toggle holds the
    /// same sign, so there is nothing to discover.</summary>
    public static readonly AdamantiumProperty RowDetailsToggleWidthProperty = AdamantiumProperty.Register(
        nameof(RowDetailsToggleWidth), typeof(Double), typeof(TreeDataGrid),
        new PropertyMetadata(28.0, PropertyMetadataOptions.AffectsMeasure));

    public Double RowDetailsToggleWidth
    {
        get => GetValue<Double>(RowDetailsToggleWidthProperty);
        set => SetValue(RowDetailsToggleWidthProperty, value);
    }

    // The toggles appear with the TEMPLATE and go with it: a column of handles that open nothing is a column that lies,
    // and asking a page to switch on both the template and the column would be asking it twice for one decision.
    internal double DetailsStripLeading => RowDetailsTemplate != null ? Math.Max(0, RowDetailsToggleWidth) : 0;

    // Everything standing to the LEFT of the first column - the toggles, then the numbers. The width pass is given this
    // one number, because the columns only need to know how much of the row is already spoken for.
    internal double LeftStripsLeading => DetailsStripLeading + NumberStripLeading;

    // Only ever GROWS within a scroll, exactly as an Auto column does: recomputing it downwards as rows come and go
    // makes the whole table breathe sideways under the pointer.
    internal void ReportRowNumberWidth(double desired)
    {
        if (!ShowRowNumbers || desired <= RowNumberWidth) return;

        RowNumberWidth = desired;
        _autoWidthGrew = true;

        // The width pass has already run with the old width, and the in-pass re-run only catches reports made while
        // this grid's measure is still on the stack - a virtualized row measured later is not.
        InvalidateMeasure();
    }

    private static void OnShowRowNumbersChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
    {
        if (d is not TreeDataGrid grid) return;

        // The width is NOT reset: the switch itself gives the row back to the columns (see NumberStripLeading), and
        // keeping the measured number is what lets the strip come back at the right size on the FIRST pass.
        grid.ResetAutoWidths();
        grid.InvalidateRealizedRows();
        grid.RefreshStrips();
    }

    /// <summary>How far the columns are scrolled sideways.</summary>
    public static readonly AdamantiumProperty HorizontalOffsetProperty = AdamantiumProperty.Register(
        nameof(HorizontalOffset), typeof(Double), typeof(TreeDataGrid),
        new PropertyMetadata(0.0, PropertyMetadataOptions.AffectsMeasure, OnHorizontalOffsetChanged));

    public Double HorizontalOffset
    {
        get => GetValue<Double>(HorizontalOffsetProperty);
        set => SetValue(HorizontalOffsetProperty, value);
    }

    private static void OnHorizontalOffsetChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
    {
        if (d is not TreeDataGrid grid) return;
        grid.RefreshRealizedRows();
        grid.RefreshStrips();
    }

    private int _firstColumn;
    private int _lastColumn = -1;
    private double _viewportWidth;

    // How wide the table can be SEEN, which is not how wide it is. Anything that belongs to no column - a record's
    // details panel - is laid out against this, so it fills the view instead of the whole scrollable content.
    internal double ViewportWidth => _scroll?.ViewportSize.Width ?? _viewportWidth;

    /// <summary>The selected cells, as rectangles. Settable from a view-model, which is the point: highlighting search
    /// hits or bad values, and restoring a selection on the way back to a tab, are things a grid normally cannot be
    /// asked to do from the model at all.</summary>
    public DataGridSelection SelectedCells { get; } = new();

    /// <summary>Row of the cell the keyboard is on, or -1.</summary>
    public int ActiveRow { get; private set; } = -1;

    /// <summary>Column of the cell the keyboard is on, or -1.</summary>
    public int ActiveColumn { get; private set; } = -1;

    private int _anchorRow = -1;
    private int _anchorColumn = -1;

    /// <summary>Puts the keyboard on a cell and, unless told otherwise, makes it the whole selection - a plain click.
    /// The ACTIVE cell is separate from the selection, as in a spreadsheet: it moves inside a selected block without
    /// clearing it.</summary>
    public void SelectCell(int row, int column, bool extend = false, bool add = false)
    {
        var rowCount = Rows?.Count ?? 0;
        if (row < 0 || row >= rowCount || column < 0 || column >= Columns.Count) return;

        // In FullRow the click still lands on a cell - it just takes the whole row. Answered HERE, at the one funnel
        // every gesture passes through (a press, a drag, an arrow key), so the mode cannot hold for some of them.
        if (SelectionUnit == DataGridSelectionUnit.FullRow)
        {
            ActiveColumn = column;
            SelectRow(row, extend, add);
            return;
        }

        if (extend && _anchorRow >= 0)
        {
            SelectedCells.ExtendTo(_anchorRow, _anchorColumn, row, column);
        }
        else if (add)
        {
            SelectedCells.Add(new CellRange(row, column, row, column));
            _anchorRow = row;
            _anchorColumn = column;
        }
        else
        {
            SelectedCells.Set(new CellRange(row, column, row, column));
            _anchorRow = row;
            _anchorColumn = column;
        }

        ActiveRow = row;
        ActiveColumn = column;
        RefreshCellSelectionVisuals();
    }

    /// <summary>Selects whole rows - a click on a row header.</summary>
    public void SelectRows(int firstRow, int lastRow, bool add = false)
    {
        if (Columns.Count == 0) return;
        var range = new CellRange(firstRow, 0, lastRow, Columns.Count - 1);
        if (add) SelectedCells.Add(range); else SelectedCells.Set(range);
        RefreshCellSelectionVisuals();
    }

    /// <summary>What a click takes: one cell, or the whole row it landed in. Cell by default - this table is a sheet
    /// of values first - and a list of records asks for <see cref="DataGridSelectionUnit.FullRow"/>.</summary>
    public static readonly AdamantiumProperty SelectionUnitProperty = AdamantiumProperty.Register(
        nameof(SelectionUnit), typeof(DataGridSelectionUnit), typeof(TreeDataGrid),
        new PropertyMetadata(DataGridSelectionUnit.Cell));

    public DataGridSelectionUnit SelectionUnit
    {
        get => GetValue<DataGridSelectionUnit>(SelectionUnitProperty);
        set => SetValue(SelectionUnitProperty, value);
    }

    /// <summary>Selects whole columns - a click on a column header. One rectangle however many rows there are.</summary>
    public void SelectColumns(int firstColumn, int lastColumn, bool add = false)
    {
        var rowCount = Rows?.Count ?? 0;
        if (rowCount == 0) return;
        var range = new CellRange(0, firstColumn, rowCount - 1, lastColumn);
        if (add) SelectedCells.Add(range); else SelectedCells.Set(range);
        RefreshCellSelectionVisuals();
    }

    /// <summary>Moves the keyboard by a step, optionally dragging the selection with it (Shift+arrows); clamped at the
    /// edges. By INDEX, not by walking realized containers - the neighbour at the edge of the window has none.</summary>
    public void MoveActive(int rowStep, int columnStep, bool extend = false)
    {
        var rowCount = Rows?.Count ?? 0;
        if (rowCount == 0 || Columns.Count == 0) return;

        var row = ActiveRow < 0 ? 0 : Math.Clamp(ActiveRow + rowStep, 0, rowCount - 1);
        var column = ActiveColumn < 0 ? 0 : Math.Clamp(ActiveColumn + columnStep, 0, Columns.Count - 1);
        SelectCell(row, column, extend);
    }

    /// <summary>Takes the whole row - what pressing its number does. A row is a RECTANGLE across every column, which is
    /// what selection already is here. <paramref name="extend"/> is Shift, <paramref name="add"/> is Ctrl.</summary>
    public void SelectRow(int row, bool extend = false, bool add = false)
    {
        var rows = Rows;
        if (rows == null || row < 0 || row >= rows.Count || Columns.Count == 0) return;

        var last = Columns.Count - 1;
        if (extend && _anchorRow >= 0)
        {
            SelectedCells.Set(new CellRange(Math.Min(_anchorRow, row), 0, Math.Max(_anchorRow, row), last));
        }
        else
        {
            var range = new CellRange(row, 0, row, last);
            if (add) SelectedCells.Add(range); else SelectedCells.Set(range);
            _anchorRow = row;
            _anchorColumn = 0;
        }

        ActiveRow = row;
        RefreshCellSelectionVisuals();
    }

    /// <summary>Whether every column of this row is taken - what the number strip shows as a selected row.</summary>
    internal bool IsRowFullySelected(int row)
    {
        if (row < 0 || Columns.Count == 0) return false;

        for (var column = 0; column < Columns.Count; column++)
        {
            if (!SelectedCells.Contains(row, column)) return false;
        }

        return true;
    }

    public void SelectAllCells()
    {
        var rowCount = Rows?.Count ?? 0;
        if (rowCount == 0 || Columns.Count == 0) return;
        SelectedCells.Set(new CellRange(0, 0, rowCount - 1, Columns.Count - 1));
        RefreshCellSelectionVisuals();
    }

    /// <summary>A row container reporting a click. It knows its column; only the grid knows which ROW index that
    /// container currently stands for, and the index is what the selection is made of.</summary>
    internal void SelectCellFromRow(DataGridRow container, int column, bool extend, bool add)
    {
        var index = ItemContainerGenerator.IndexFromContainer(container);
        if (index >= 0) SelectCell(index, column, extend, add);
    }

    internal void BeginEditFromRow(DataGridRow container, int column)
    {
        var index = ItemContainerGenerator.IndexFromContainer(container);
        if (index >= 0) BeginEdit(index, column);
    }

    /// <summary>Whether the pointer is dragging a selection out - pressed on one cell and still down.</summary>
    public bool IsDragSelecting { get; private set; }

    private bool _dragAdds;

    internal void BeginDragSelect(bool adds)
    {
        IsDragSelecting = true;
        _dragAdds = adds;
    }

    internal void EndDragSelect() => IsDragSelecting = false;

    // Dragging EXTENDS from the cell the press landed on - the anchor - so the block follows the pointer in every
    // direction and shrinks again when it comes back, which is what a spreadsheet does.
    internal void DragSelectFromRow(DataGridRow container, int column)
    {
        if (!IsDragSelecting) return;

        var index = ItemContainerGenerator.IndexFromContainer(container);
        if (index < 0 || (index == ActiveRow && column == ActiveColumn)) return;

        SelectCell(index, column, extend: true, add: _dragAdds);
    }

    internal void ToggleFromRow(DataGridRow container, int column)
    {
        var index = ItemContainerGenerator.IndexFromContainer(container);
        if (index >= 0) ToggleCell(index, column);
    }

    /// <summary>Flips a boolean cell and writes it, in one gesture. Goes through the ordinary edit - the read-only
    /// checks, the events and the provider are the same ones a typed edit answers to; only the editor is skipped,
    /// because a check box has nothing to type into.</summary>
    public bool ToggleCell(int row, int column)
    {
        if (!BeginEdit(row, column)) return false;

        var cell = CellFor(row, column);
        if (cell != null) cell.EditedValue = cell.Content as bool? is true ? false : true;

        return CommitEdit();
    }

    /// <summary>What a copy would put on the clipboard, kept current as the selection changes - so a view can show it
    /// without a command. Big selections report their size instead of their contents: building a string for a million
    /// cells to display it is not something a selection change should do.</summary>
    public string SelectionText { get; private set; } = string.Empty;

    private const int TextPreviewLimit = 500;

    private void UpdateSelectionText()
    {
        if (!SelectedCells.TryGetBounds(out var bounds))
        {
            SelectionText = string.Empty;
            return;
        }

        var cells = (long)(bounds.LastRow - bounds.FirstRow + 1) * (bounds.LastColumn - bounds.FirstColumn + 1);
        SelectionText = cells > TextPreviewLimit
            ? $"{cells} cells selected"
            : GetSelectionAsText();
    }

    // Only the realized cells can show anything; the rest carry their state in the ranges and pick it up when built.
    private void RefreshCellSelectionVisuals()
    {
        UpdateSelectionText();

        foreach (var index in ItemContainerGenerator.RealizedIndices)
        {
            if (ItemContainerGenerator.ContainerFromIndex(index) is not DataGridRow row) continue;
            for (var column = 0; column < Columns.Count; column++)
            {
                if (row.CellAt(column) is not { } cell) continue;

                // The cell being EDITED is never drawn as selected: the selection fill would sit over the editor, and
                // what you are typing into has to be the thing you can see.
                cell.IsSelected = SelectedCells.Contains(index, column) && !cell.IsEditing;
                cell.IsActive = index == ActiveRow && column == ActiveColumn;
            }

            row.RefreshNumberSelection();
        }
    }

    /// <summary>The grid owns the keys that drive editing: F2 opens the active cell, Escape leaves an edit without
    /// writing - and, with nothing open, takes the search strip away. Enter never reaches here - the editor claims it
    /// and asks for the commit itself.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled) return;

        var control = (e.Modifiers & (InputModifiers.LeftControl | InputModifiers.RightControl)) != 0;

        switch (e.Key)
        {
            case Key.F2 when !IsEditing:
                e.Handled = BeginEdit(ActiveRow, ActiveColumn);
                break;

            case Key.Escape when IsEditing:
                CancelEdit();
                e.Handled = true;
                break;

            // AFTER the edit case, and that order is the whole rule: Escape leaves the innermost thing you are in, so an
            // open editor goes first and the strip only when there is no editor left to leave. The key reaches here from
            // the search field too - it travels up through every element, and the field does not claim it - so one case
            // covers both "focus is in the strip" and "focus is in the table".
            case Key.Escape when ShowSearchPanel:
                SetCurrentValue(ShowSearchPanelProperty, false);
                e.Handled = true;
                break;

            case Key.C when control && !IsEditing:
                e.Handled = CopySelection();
                break;

            case Key.Delete when !IsEditing:
                e.Handled = DeleteSelectedRows();
                break;

            case Key.Insert when !IsEditing:
                e.Handled = AddRow() != null;
                break;
        }
    }

    /// <summary>Puts the selected cells on the clipboard as tab-separated text - the shape a spreadsheet, a database
    /// tool and a text editor all read. False when there was nothing selected.</summary>
    public bool CopySelection()
    {
        var text = GetSelectionAsText();
        if (string.IsNullOrEmpty(text)) return false;

        Clipboard.SetText(text);
        return true;
    }

    /// <summary>Row being edited, or -1.</summary>
    public int EditingRow { get; private set; } = -1;

    /// <summary>Column being edited, or -1.</summary>
    public int EditingColumn { get; private set; } = -1;

    /// <summary>Whether a cell is open for editing. A real property, not a computed one, because a view-model binds to
    /// it - a plain CLR property would look bindable and quietly never resolve.</summary>
    public static readonly AdamantiumProperty IsEditingProperty = AdamantiumProperty.RegisterReadOnly(
        nameof(IsEditing), typeof(bool), typeof(TreeDataGrid), new PropertyMetadata(false));

    public bool IsEditing
    {
        get => GetValue<bool>(IsEditingProperty);
        private set => SetValue(IsEditingProperty, value);
    }

    public event EventHandler<DataGridCellEditEventArgs> CellEditBeginning;

    public event EventHandler<DataGridCellEditEventArgs> CellEditEnding;

    /// <summary>Puts one cell into edit. Refused when the cell is read-only - by its column or by the view-model - or when
    /// a handler says no.</summary>
    public bool BeginEdit(int row, int column)
    {
        if (IsEditing && !CommitEdit()) return false;

        var rows = Rows;
        if (rows == null || row < 0 || row >= rows.Count || column < 0 || column >= Columns.Count) return false;

        var item = rows[row].Node;
        var dataColumn = Columns[column];
        if (IsCellReadOnly(dataColumn, item)) return false;

        var args = new DataGridCellEditEventArgs(item, dataColumn);
        CellEditBeginning?.Invoke(this, args);
        if (args.Cancel) return false;

        EditingRow = row;
        EditingColumn = column;
        IsEditing = true;
        RefreshEditingCell();
        return true;
    }

    /// <summary>Writes what the editor holds and leaves edit mode. False means the write was refused - by a handler or by
    /// the view-model - and the cell stays in edit with what the user typed, which is what validation looks like here.</summary>
    public bool CommitEdit()
    {
        if (!IsEditing) return true;

        var rows = Rows;
        var item = rows[EditingRow].Node;
        var column = Columns[EditingColumn];
        var value = EditedValue();

        var args = new DataGridCellEditEventArgs(item, column, value);
        CellEditEnding?.Invoke(this, args);
        if (args.Cancel) return false;

        if (!WriteThroughColumn(column, item, args.Value)) return false;

        EndEdit();
        return true;
    }

    /// <summary>Leaves edit mode without writing anything.</summary>
    public void CancelEdit()
    {
        if (!IsEditing) return;
        EndEdit();
    }

    private void EndEdit()
    {
        EditingRow = -1;
        EditingColumn = -1;
        IsEditing = false;
        RefreshEditingCell();
        RefreshRealizedRows();
        Focus();
    }

    private object EditedValue() => CellFor(EditingRow, EditingColumn)?.ValueForCommit();

    // A template column writes through its OWN two-way bindings while the user is in it, so there is nothing left here
    // to write and refusing would leave the cell stuck in edit.
    private static bool WriteThroughColumn(DataGridColumn column, object item, object value) =>
        column.Binding == null || column.Write(item, value);

    /// <summary>Whether this one cell refuses editing: the column first, then the row's own answer through
    /// <see cref="DataGridColumn.IsReadOnlyBinding"/>.</summary>
    private static bool IsCellReadOnly(DataGridColumn column, object item) =>
        column.IsReadOnly
        || (column.IsReadOnlyBinding != null && column.Read(column.IsReadOnlyBinding, item) as bool? == true);

    private void RefreshEditingCell()
    {
        foreach (var index in ItemContainerGenerator.RealizedIndices)
        {
            if (ItemContainerGenerator.ContainerFromIndex(index) is not DataGridRow row) continue;
            for (var column = 0; column < Columns.Count; column++)
            {
                if (row.CellAt(column) is { } cell) cell.IsEditing = index == EditingRow && column == EditingColumn;
            }
        }

        RefreshCellSelectionVisuals();
    }

    internal DataGridCell CellFor(int row, int column) =>
        ItemContainerGenerator.ContainerFromIndex(row) is DataGridRow container ? container.CellAt(column) : null;

    /// <summary>What the selection would put on the clipboard: tabs between columns, newlines between rows. Walks the
    /// BOUNDING rectangle so disjoint ranges line up; a cell inside the bounds but outside every range comes out
    /// empty.</summary>
    public string GetSelectionAsText()
    {
        if (!SelectedCells.TryGetBounds(out var bounds) || Rows is not { } rows) return string.Empty;

        var text = new StringBuilder();
        for (var row = bounds.FirstRow; row <= bounds.LastRow && row < rows.Count; row++)
        {
            if (row > bounds.FirstRow) text.Append('\n');
            for (var column = bounds.FirstColumn; column <= bounds.LastColumn && column < Columns.Count; column++)
            {
                if (column > bounds.FirstColumn) text.Append('\t');
                if (!SelectedCells.Contains(row, column)) continue;
                text.Append(Columns[column].CellContentFor(rows[row].Node));
            }
        }

        return text.ToString();
    }

    /// <summary>Whether column <paramref name="index"/> has a cell on a realized row. Frozen columns always do; the
    /// rest depend on the scroll. No estimate is needed - every width is already known from the width pass.</summary>
    internal bool IsColumnRealized(int index)
    {
        if (index < 0 || index >= Columns.Count) return false;

        var column = Columns[index];
        if (!column.IsShown) return false;
        if (column.IsFrozen) return true;
        return index >= _firstColumn && index <= _lastColumn;
    }

    // Which columns intersect the viewport, plus one either side so a partly-scrolled column never leaves a gap.
    private void UpdateColumnWindow(double viewport)
    {
        _viewportWidth = viewport;
        if (Columns.Count == 0)
        {
            _firstColumn = 0;
            _lastColumn = -1;
            return;
        }

        if (double.IsInfinity(viewport) || viewport <= 0)
        {
            _firstColumn = 0;
            _lastColumn = Columns.Count - 1;
            return;
        }

        // The pinned zone is NOT part of what scrolls: a scrolling column is visible when its offset falls in the
        // window that starts after that zone. Frozen columns are always realized anyway (IsColumnRealized), so they are
        // simply skipped here rather than measured against a window they do not live in.
        var from = HorizontalOffset + FrozenWidth;
        var to = from + Math.Max(0, viewport - FrozenWidth - RightFrozenWidth);

        var first = Columns.Count - 1;
        var last = 0;
        for (var i = 0; i < Columns.Count; i++)
        {
            var column = Columns[i];
            if (column.IsFrozen) continue;
            if (column.Offset + column.ActualWidth <= from || column.Offset >= to) continue;
            first = Math.Min(first, i);
            last = Math.Max(last, i);
        }

        var wasFirst = _firstColumn;
        var wasLast = _lastColumn;
        _firstColumn = Math.Max(0, first - 1);
        _lastColumn = Math.Min(Columns.Count - 1, last + 1);

        // A row builds its cells from this window IN ITS OWN MEASURE, and the window is moved HERE - in the grid's.
        // A row measured before this ran has last pass's window, and nothing else would ever tell it otherwise: on the
        // stand, scrolling sideways left the rows holding the columns that had just gone off the left and none of the
        // ones that had come in from the right. Resizing any column put it right, which is what said the rows were
        // stale rather than misplaced - it is the only other thing that re-syncs them.
        if (_firstColumn != wasFirst || _lastColumn != wasLast) InvalidateRealizedRows();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // The scroller's offset is RECONCILED here as well as pushed by its event: an event can be raised while the
        // offset it announces is still settling, and a header strip standing one scroll behind its rows is exactly what
        // that looks like. Reading it once a pass costs nothing and cannot be missed.
        if (_scroll != null && Math.Abs(_scroll.ScrollOffset.X - HorizontalOffset) > 0.01)
            HorizontalOffset = _scroll.ScrollOffset.X;

        // ONE width pass for the whole grid, before anything is measured against it: the header and every row read these
        // same numbers. Two calculations are how a table's header and body drift apart.
        ColumnsWidth = DataGridColumnLayout.Arrange(Columns, availableSize.Width, out var frozen, out var pinnedRight,
            LeftStripsLeading);
        FrozenWidth = frozen;
        RightFrozenWidth = pinnedRight;
        UpdateColumnWindow(availableSize.Width);

        var desired = base.MeasureOverride(availableSize);

        // A cell may have reported a wider Auto column than the pass assumed, and the rows have already been laid out
        // against the old number. Re-run the widths so this pass ends consistent instead of showing a torn frame.
        if (_autoWidthGrew)
        {
            _autoWidthGrew = false;
            ColumnsWidth = DataGridColumnLayout.Arrange(Columns, availableSize.Width, out var regrown,
                out var regrownRight, LeftStripsLeading);
            FrozenWidth = regrown;
            RightFrozenWidth = regrownRight;
            UpdateColumnWindow(availableSize.Width);
            desired = base.MeasureOverride(availableSize);
        }

        // A panel measured to a different height than the stack had reserved for it. Telling the stack is all that
        // happens here - it lays the rows out again on the NEXT pass. Re-running the measure now instead would measure
        // every open panel a second time in the same frame, and a panel is a whole templated subtree: measured on the
        // stand with eight of them open, layout went to 60 ms a pass. One frame at the previous height is not worth
        // doubling every frame.
        if (_detailsHeightChanged)
        {
            _detailsHeightChanged = false;
            (ItemsHostPanel as IMeasurableComponent)?.InvalidateMeasure();
        }

        return desired;
    }

    protected internal override bool IsItemItsOwnContainer(object item) => false;

    protected internal override IUIComponent GetContainerForItem(object item) => new DataGridRow();

    protected internal override void PrepareContainer(IUIComponent container, object item)
    {
        if (container is not DataGridRow row) return;

        var flat = item as TreeRow;
        var index = flat != null && Rows != null ? Rows.IndexOf(flat) : -1;
        var band = AlternationCount > 0 && index >= 0 ? index % AlternationCount : 0;

        // Every row takes the row height - except a panel, which takes NO fixed height at all. A fixed one makes the
        // container a MEASURE BOUNDARY, and a boundary is exactly what a panel must not be: something changing inside it
        // (a tab switched to a taller one) would then never reach the row, the row would never re-measure, and the table
        // would go on reserving the height the panel used to want. Measured on the stand - the chart drew clipped.
        row.Height = flat?.Node is DataGridRowDetails ? Double.NaN : RowHeight;

        // The panels are NOT counted. A panel is the record above it said at length, so a number spent on one makes the
        // record after it look as though a row had gone missing - on the stand, opening the first record's panel
        // renumbered the second one 3.
        row.Attach(this, flat, band, index + 1 - DetailsRowsBefore(index));
    }

    protected internal override void ClearContainer(IUIComponent container)
    {
        if (container is DataGridRow row) row.Attach(this, null, 0);
    }
}
