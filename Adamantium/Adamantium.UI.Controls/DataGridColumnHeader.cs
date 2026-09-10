using System;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>One column's header. Reads its width from the column, like every cell below it does - one number from one
/// place, or the header and the body drift apart.
/// <para>It also carries the column's FILTER: the funnel opens a <see cref="DataGridFilterView"/> in a flyout, and lights
/// up while that column is narrowing the table.</para></summary>
public class DataGridColumnHeader : ContentControl
{
    /// <summary>A header is cut at its column's edge, or its caption and funnel hang over the neighbour. Kept even
    /// though a clip is a scissor (see <see cref="DataGridCell"/>): there are as many headers as COLUMNS.
    /// <para>Set HERE, not through <c>OverrideMetadata</c>: a type default raises no change, so the callback that
    /// writes the hot field behind this property never runs and the clip simply does not happen.</para></summary>
    public DataGridColumnHeader()
    {
        ClipToBounds = true;
    }

    public static readonly AdamantiumProperty SortDirectionProperty = AdamantiumProperty.Register(nameof(SortDirection),
        typeof(DataGridSortDirection), typeof(DataGridColumnHeader),
        new PropertyMetadata(DataGridSortDirection.None, PropertyMetadataOptions.AffectsRender));

    /// <summary>Whether this column's filter is narrowing anything - what the theme lights the funnel by.</summary>
    public static readonly AdamantiumProperty IsFilteredProperty = AdamantiumProperty.Register(nameof(IsFiltered),
        typeof(bool), typeof(DataGridColumnHeader), new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    /// <summary>Whether this column offers a filter at all. False hides the funnel: a column nobody can filter must not
    /// show a control that does nothing.</summary>
    public static readonly AdamantiumProperty CanFilterProperty = AdamantiumProperty.Register(nameof(CanFilter),
        typeof(bool), typeof(DataGridColumnHeader), new PropertyMetadata(true, PropertyMetadataOptions.AffectsRender));

    /// <summary>This header's column is pinned: it stands still while the strip scrolls under it, so unlike every other
    /// header it has to be OPAQUE - the band's own colour, which is a constant of the theme.</summary>
    public static readonly AdamantiumProperty IsFrozenProperty = AdamantiumProperty.Register(nameof(IsFrozen),
        typeof(bool), typeof(DataGridColumnHeader), new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    public bool IsFrozen
    {
        get => GetValue<bool>(IsFrozenProperty);
        set => SetValue(IsFrozenProperty, value);
    }

    /// <summary>This header is the one being carried to a new place. The theme shows it - a column that gives no sign
    /// it has been picked up leaves the user guessing whether the press took at all.</summary>
    public static readonly AdamantiumProperty IsDraggingProperty = AdamantiumProperty.Register(nameof(IsDragging),
        typeof(bool), typeof(DataGridColumnHeader), new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    public bool IsDragging
    {
        get => GetValue<bool>(IsDraggingProperty);
        set => SetValue(IsDraggingProperty, value);
    }

    /// <summary>Whether the strip draws the rule down this header's right edge - the grid's
    /// <see cref="TreeDataGrid.GridLinesVisibility"/>, so the header and the body agree about the column rules.</summary>
    public static readonly AdamantiumProperty ShowsVerticalLineProperty = AdamantiumProperty.Register(
        nameof(ShowsVerticalLine), typeof(bool), typeof(DataGridColumnHeader),
        new PropertyMetadata(true, PropertyMetadataOptions.AffectsRender, OnGridLineChanged));

    /// <summary>The rule as a thickness for the header's own border to carry - see <see cref="DataGridCell"/> for why
    /// it is not an element of its own.</summary>
    /// <remarks>The default MATCHES <see cref="ShowsVerticalLine"/>'s: a header attached to a grid that draws rules
    /// never changes the flag, so the callback never runs and a zero default would leave every header ruleless.</remarks>
    public static readonly AdamantiumProperty GridLineThicknessProperty = AdamantiumProperty.Register(
        nameof(GridLineThickness), typeof(Thickness), typeof(DataGridColumnHeader),
        new PropertyMetadata(new Thickness(0, 0, 1, 0), PropertyMetadataOptions.AffectsRender));

    public Thickness GridLineThickness
    {
        get => GetValue<Thickness>(GridLineThicknessProperty);
        private set => SetValue(GridLineThicknessProperty, value);
    }

    private static void OnGridLineChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is DataGridColumnHeader header)
        {
            header.GridLineThickness = new Thickness(0, 0, header.ShowsVerticalLine ? 1 : 0, 0);
        }
    }

    /// <summary>What that rule is painted with, or null to leave it to the theme.</summary>
    public static readonly AdamantiumProperty GridLineBrushProperty = AdamantiumProperty.Register(
        nameof(GridLineBrush), typeof(Brush), typeof(DataGridColumnHeader),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

    public bool ShowsVerticalLine
    {
        get => GetValue<bool>(ShowsVerticalLineProperty);
        set => SetValue(ShowsVerticalLineProperty, value);
    }

    public Brush GridLineBrush
    {
        get => GetValue<Brush>(GridLineBrushProperty);
        set => SetValue(GridLineBrushProperty, value);
    }

    private Popup _popup;
    private IInputComponent _filterButton;
    private DataGridFilterView _view;

    /// <summary>Which way this column is sorted, if it is - what the theme turns into an arrow.</summary>
    public DataGridSortDirection SortDirection
    {
        get => GetValue<DataGridSortDirection>(SortDirectionProperty);
        set => SetValue(SortDirectionProperty, value);
    }

    public bool IsFiltered
    {
        get => GetValue<bool>(IsFilteredProperty);
        set => SetValue(IsFilteredProperty, value);
    }

    public bool CanFilter
    {
        get => GetValue<bool>(CanFilterProperty);
        set => SetValue(CanFilterProperty, value);
    }

    public DataGridColumn Column { get; internal set; }

    public int ColumnIndex { get; internal set; }

    /// <summary>The grid this header belongs to - the filter has to be applied to something.</summary>
    public TreeDataGrid Owner { get; internal set; }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (_filterButton != null) _filterButton.MouseLeftButtonDown -= OnFilterPressed;
        if (_view != null) _view.Closed -= OnFilterClosed;
        _view = null;

        _popup = GetTemplateChild("PART_FilterPopup") as Popup;
        _filterButton = GetTemplateChild("PART_FilterButton") as IInputComponent;

        if (_filterButton != null) _filterButton.MouseLeftButtonDown += OnFilterPressed;
        if (_filterButton is UIComponent funnel) funnel.Cursor = Cursors.Hand;

        if (_popup != null)
        {
            // Anchored to the FUNNEL, not to the header: Bottom placement centres the flyout on its target, and on a wide
            // column the header's centre is nowhere near the button that was pressed.
            _popup.PlacementTarget = _filterButton as UIComponent ?? this;
            _popup.KeepOpen = false;
            _popup.IgnoreTargetPress = true;
        }
    }

    public override void OnRemoveTemplate()
    {
        base.OnRemoveTemplate();

        if (_filterButton != null) _filterButton.MouseLeftButtonDown -= OnFilterPressed;
        if (_view != null) _view.Closed -= OnFilterClosed;

        _popup = null;
        _filterButton = null;
        _view = null;
    }

    internal void Attach(DataGridColumn column, int index, DataGridSortDirection direction, TreeDataGrid owner)
    {
        Column = column;
        ColumnIndex = index;
        Owner = owner;
        ContentTemplate = column?.HeaderTemplate;
        Content = column?.Header;
        SortDirection = direction;
        CanFilter = column?.CanUserFilter ?? false;
        IsFiltered = owner?.IsFiltered(column) ?? false;
        IsFrozen = column?.IsFrozen ?? false;
        ZIndex = IsFrozen ? 1 : 0;
        ShowsVerticalLine = owner?.ShowsVerticalLines ?? false;

        // Only when the grid names one - see DataGridCell.Attach for why a null must not be written here.
        if (owner?.GridLinesBrush is { } brush) GridLineBrush = brush;
    }

    // A press on the funnel is NOT a press on the header: mouse-down is raised per element, so without this the strip's
    // own handler would sort the column on the way past.
    internal bool PressedFilter(object source)
    {
        for (var node = source as IUIComponent; node != null; node = node.VisualParent)
        {
            if (ReferenceEquals(node, _filterButton)) return true;
        }

        return false;
    }

    private void OnFilterPressed(object sender, MouseButtonEventArgs e)
    {
        if (!CanFilter || _popup == null) return;

        e.Handled = true;
        if (_popup.IsOpen)
        {
            _popup.IsOpen = false;
            return;
        }

        OpenFilter();
    }

    /// <summary>Opens the filter flyout and fills it from this column's current filter - what the funnel does, offered
    /// to whoever else wants it (a shortcut, a context menu). Returns the editor inside, or null when the theme has
    /// none.</summary>
    public DataGridFilterView OpenFilter()
    {
        if (_popup == null) return null;

        // Opening BUILDS the flyout's content (it is deferred until first use), so the editor inside it can only be
        // reached afterwards - asking the header's own template for it finds nothing, and the form stays blank.
        _popup.IsOpen = true;
        var view = View();
        view?.Open(Owner, Column);
        return view;
    }

    private DataGridFilterView View()
    {
        if (_popup?.FindContentChild("PART_FilterView") is not DataGridFilterView view) return _view;
        if (ReferenceEquals(view, _view)) return _view;

        if (_view != null) _view.Closed -= OnFilterClosed;
        _view = view;
        _view.Closed += OnFilterClosed;
        return _view;
    }

    private void OnFilterClosed(object sender, EventArgs e)
    {
        if (_popup != null) _popup.IsOpen = false;
        IsFiltered = Owner?.IsFiltered(Column) ?? false;
    }
}

public enum DataGridSortDirection
{
    None,
    Ascending,
    Descending
}
