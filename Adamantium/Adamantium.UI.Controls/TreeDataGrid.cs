using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core.Media;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.RoutedEvents;

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
        grid.RefreshHeaders();
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

    /// <summary>The column <see cref="ExpanderColumnIndex"/> names, clamped to what exists.</summary>
    public DataGridColumn ExpanderColumn =>
        Columns.Count == 0 ? null : Columns[Math.Clamp(ExpanderColumnIndex, 0, Columns.Count - 1)];

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

    internal void ToggleRow(TreeRow row)
    {
        if (row is not { HasChildren: true }) return;
        _flattener.Toggle(row);
        RefreshRealizedRows();
    }

    internal void ExpandRow(TreeRow row)
    {
        if (row is not { HasChildren: true } || row.IsExpanded) return;
        _flattener.Expand(row);
        RefreshRealizedRows();
    }

    internal void CollapseRow(TreeRow row)
    {
        if (row is not { IsExpanded: true }) return;
        _flattener.Collapse(row);
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

    private void RebuildFlattener()
    {
        _flattener?.Clear();
        _rawChildren = ChildrenSelector ?? TreeChildResolver.ForPath(ChildrenPath);
        _flattener = new TreeFlattener(ShapedChildrenOf, static _ => false, static _ => false);
        _flattener.SetRoots(Shape(_roots));
        Items.SetSource(_flattener.Rows);

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
        RebuildFlattener();

        // The header strip reads the sort state in its MEASURE (that is where it syncs), so a sort that leaves the
        // widths alone never reaches it: the arrow appeared only when something else happened to re-measure the strip.
        RefreshHeaders();
    }

    private void RefreshHeaders() => (_headers as IMeasurableComponent)?.InvalidateMeasure();

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
        RefreshHeaders();
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
    private object ValueOf(DataGridColumn column, object node)
    {
        if (column.Binding != null) return column.Read(column.Binding, node);

        return column.SortMemberPath is { Length: > 0 } path ? TreeChildResolver.ForValuePath(path)(node) : null;
    }

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

    private IEnumerable ShapedChildrenOf(object node) => Shape(_rawChildren(node));

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
        if (SortColumn is { } column)
        {
            items.Sort((a, b) => Compare(ValueOf(column, a), ValueOf(column, b)) * (SortDescending ? -1 : 1));
        }

        return items;
    }

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

        RefreshHeaders();
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
        column.Width = new GridLength(Math.Clamp(widest, column.MinWidth, column.MaxWidth));
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
        (_headers as IMeasurableComponent)?.InvalidateMeasure();
    }

    private DataGridHeadersPresenter _headers;
    private MeasurableUIComponent _dropIndicator;
    private ScrollViewer _scroll;

    internal void AdoptHeaders(DataGridHeadersPresenter headers) => _headers = headers;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("PART_Headers") is DataGridHeadersPresenter headers) headers.Owner = this;

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
    private double NumberStripLeading => ShowRowNumbers ? RowNumberWidth : 0;

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
        grid.RefreshHeaders();
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
        grid.RefreshHeaders();
    }

    private int _firstColumn;
    private int _lastColumn = -1;
    private double _viewportWidth;

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
    /// writing. Enter never reaches here - the editor claims it and asks for the commit itself.</summary>
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
        if (Columns[index].IsFrozen) return true;
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

        _firstColumn = Math.Max(0, first - 1);
        _lastColumn = Math.Min(Columns.Count - 1, last + 1);
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
            NumberStripLeading);
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
                out var regrownRight, NumberStripLeading);
            FrozenWidth = regrown;
            RightFrozenWidth = regrownRight;
            UpdateColumnWindow(availableSize.Width);
            desired = base.MeasureOverride(availableSize);
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

        row.Height = RowHeight;
        row.Attach(this, flat, band, index + 1);
    }

    protected internal override void ClearContainer(IUIComponent container)
    {
        if (container is DataGridRow row) row.Attach(this, null, 0);
    }
}
