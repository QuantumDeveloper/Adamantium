using System;
using System.Collections.Generic;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>A grid of visual choices standing in the group itself - styles, materials, shapes. A
/// <see cref="Selector"/>, because "which of these is the current one" is exactly the question it answers.
/// <para>Its items are DATA. A gallery is shown TWICE at once - in the band and, when the chevron drops it, in full -
/// and a control can only be in one place; the drop-down builds its own cells from the same
/// <see cref="ItemsControl.ItemTemplate"/>, the same way the quick-access bar builds its own compact form.</para></summary>
public class RibbonGallery : Selector
{
    public static readonly AdamantiumProperty ColumnsProperty = AdamantiumProperty.Register(nameof(Columns),
        typeof(int), typeof(RibbonGallery), new PropertyMetadata(5, PropertyMetadataOptions.AffectsMeasure, OnShapeChanged));

    public static readonly AdamantiumProperty CompactColumnsProperty = AdamantiumProperty.Register(nameof(CompactColumns),
        typeof(int), typeof(RibbonGallery), new PropertyMetadata(3, PropertyMetadataOptions.AffectsMeasure, OnShapeChanged));

    public static readonly AdamantiumProperty RowsProperty = AdamantiumProperty.Register(nameof(Rows),
        typeof(int), typeof(RibbonGallery), new PropertyMetadata(1, PropertyMetadataOptions.AffectsMeasure, OnShapeChanged));

    public static readonly AdamantiumProperty DropDownColumnsProperty = AdamantiumProperty.Register(nameof(DropDownColumns),
        typeof(int), typeof(RibbonGallery), new PropertyMetadata(0));

    /// <summary>How many choices stand side by side while the group is roomy.</summary>
    public int Columns
    {
        get => GetValue<int>(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <summary>...and how many once the group has been narrowed to <see cref="RibbonSize.Medium"/>. Stated rather than
    /// derived: how few cells still read as a gallery is a fact about the thumbnails, which only their author knows.</summary>
    public int CompactColumns
    {
        get => GetValue<int>(CompactColumnsProperty);
        set => SetValue(CompactColumnsProperty, value);
    }

    /// <summary>How many rows stand in the band. The rest are reached with the arrows or in the drop-down.</summary>
    public int Rows
    {
        get => GetValue<int>(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    /// <summary>Columns in the dropped-down gallery. 0 = as many as <see cref="Columns"/>.</summary>
    public int DropDownColumns
    {
        get => GetValue<int>(DropDownColumnsProperty);
        set => SetValue(DropDownColumnsProperty, value);
    }

    // --- What the band's ladder does to it --------------------------------------------------------------------------
    //
    // The gallery rides the SAME ladder as every other command (§3.1): the group hands it a RibbonSize, and it answers
    // in columns. Small is where a gallery stops being one - three thumbnails in a strip say nothing - so there it
    // becomes the chevron alone, and the whole set is one press away.

    public static readonly AdamantiumProperty EffectiveColumnsProperty = AdamantiumProperty.RegisterReadOnly(
        nameof(EffectiveColumns), typeof(int), typeof(RibbonGallery),
        new PropertyMetadata(5, PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty IsCollapsedProperty = AdamantiumProperty.RegisterReadOnly(
        nameof(IsCollapsed), typeof(bool), typeof(RibbonGallery),
        new PropertyMetadata(false, PropertyMetadataOptions.AffectsMeasure));

    /// <summary>Columns at the size the group is drawing it - what the items panel is laid out in.</summary>
    public int EffectiveColumns
    {
        get => GetValue<int>(EffectiveColumnsProperty);
        private set => SetValue(EffectiveColumnsProperty, value);
    }

    /// <summary>Nothing but the chevron: the band has no room for a grid of pictures.</summary>
    public bool IsCollapsed
    {
        get => GetValue<bool>(IsCollapsedProperty);
        private set => SetValue(IsCollapsedProperty, value);
    }

    public RibbonGallery()
    {
        Items.CollectionChanged += (_, _) =>
        {
            Clamp();
            RefreshDropDown();
        };
    }

    static RibbonGallery()
    {
        // Metadata is MERGED, so the base AffectsMeasure stays and this callback is added to it.
        Ribbon.SizeProperty.OverrideMetadata(typeof(RibbonGallery),
            new PropertyMetadata(RibbonSize.Large, PropertyMetadataOptions.AffectsMeasure, OnRibbonSizeChanged));
    }

    private static void OnRibbonSizeChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        (a as RibbonGallery)?.ApplyShape();
    }

    private static void OnShapeChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        (a as RibbonGallery)?.ApplyShape();
    }

    private void ApplyShape()
    {
        var size = Ribbon.GetSize(this);
        IsCollapsed = size == RibbonSize.Small;
        EffectiveColumns = Math.Max(1, size == RibbonSize.Large ? Columns : CompactColumns);
        Clamp();
        RefreshDropDown();
    }

    // --- Scrolling the rows -----------------------------------------------------------------------------------------

    public static readonly AdamantiumProperty FirstRowProperty = AdamantiumProperty.Register(nameof(FirstRow),
        typeof(int), typeof(RibbonGallery), new PropertyMetadata(0, OnFirstRowChanged));

    public static readonly AdamantiumProperty CanScrollUpProperty = AdamantiumProperty.RegisterReadOnly(
        nameof(CanScrollUp), typeof(bool), typeof(RibbonGallery), new PropertyMetadata(false));

    public static readonly AdamantiumProperty CanScrollDownProperty = AdamantiumProperty.RegisterReadOnly(
        nameof(CanScrollDown), typeof(bool), typeof(RibbonGallery), new PropertyMetadata(false));

    /// <summary>The topmost shown row.</summary>
    public int FirstRow
    {
        get => GetValue<int>(FirstRowProperty);
        set => SetValue(FirstRowProperty, value);
    }

    public bool CanScrollUp
    {
        get => GetValue<bool>(CanScrollUpProperty);
        private set => SetValue(CanScrollUpProperty, value);
    }

    public bool CanScrollDown
    {
        get => GetValue<bool>(CanScrollDownProperty);
        private set => SetValue(CanScrollDownProperty, value);
    }

    /// <summary>How many rows the items make at the columns currently drawn.</summary>
    public int RowCount => (Items.Count + Math.Max(1, EffectiveColumns) - 1) / Math.Max(1, EffectiveColumns);

    private static void OnFirstRowChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        (a as RibbonGallery)?.Clamp();
    }

    public void ScrollUp() => FirstRow--;

    public void ScrollDown() => FirstRow++;

    private bool _clamping;

    private void Clamp()
    {
        if (_clamping) return;

        _clamping = true;
        try
        {
            // Rows that FIT, not rows asked for: a viewport a little shorter than two cells shows one whole row and
            // part of another, and clamping to the asked-for two left the last row permanently half cut with nowhere
            // further to scroll.
            var shown = (ItemsHostPanel as Panels.RibbonGalleryPanel)?.VisibleRows ?? Math.Max(1, Rows);
            var last = Math.Max(0, RowCount - shown);
            var clamped = Math.Min(Math.Max(0, FirstRow), last);
            if (clamped != FirstRow) FirstRow = clamped;

            CanScrollUp = clamped > 0;
            CanScrollDown = clamped < last;
        }
        finally
        {
            _clamping = false;
        }
    }

    // --- The drop-down: the whole set at once -----------------------------------------------------------------------

    public static readonly AdamantiumProperty IsDropDownOpenProperty = AdamantiumProperty.Register(nameof(IsDropDownOpen),
        typeof(bool), typeof(RibbonGallery), new PropertyMetadata(false, OnIsDropDownOpenChanged));

    public bool IsDropDownOpen
    {
        get => GetValue<bool>(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }

    private static void OnIsDropDownOpenChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not RibbonGallery gallery) return;

        var open = Equals(e.NewValue, true);
        if (open) gallery.RefreshDropDown();
        if (gallery._popup != null) gallery._popup.IsOpen = open;
        if (!open) gallery.RefreshDropDown();   // closed first, so the popup is never seen empty
    }

    private Popup _popup;
    private RibbonGalleryPanel _dropDownPanel;
    private readonly List<(IUIComponent Container, object Item)> _dropDownCells = [];

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _popup = GetTemplateChild("PART_Popup") as Popup;
        if (_popup != null)
        {
            _popup.PlacementTarget = this;
            // WE own the close - the chevron's own press must not light-dismiss first, or the press would close and
            // re-open and read as doing nothing.
            _popup.IgnoreTargetPress = true;
            // Named handlers, not lambdas: a part outlives neither its template nor a theme swap, and what is subscribed
            // here has to be given back in OnRemoveTemplate - which a lambda makes impossible.
            _popup.Closed -= OnDropDownClosed;
            _popup.Closed += OnDropDownClosed;

            // The card is built on first open, so the panel is not in this template's namescope - it arrives with the
            // content, and the cells are filled in then.
            _popup.ContentBuilt -= OnDropDownContentBuilt;
            _popup.ContentBuilt += OnDropDownContentBuilt;
            _popup.IsOpen = IsDropDownOpen;
        }

        _dropDownPanel = null;

        if (GetTemplateChild("PART_ScrollUp") is Primitives.ButtonBase up) up.Click += (_, _) => ScrollUp();
        if (GetTemplateChild("PART_ScrollDown") is Primitives.ButtonBase down) down.Click += (_, _) => ScrollDown();
        if (GetTemplateChild("PART_More") is Primitives.ButtonBase more) more.Click += (_, _) => IsDropDownOpen = !IsDropDownOpen;

        RefreshDropDown();
    }

    // How many rows FIT is only known once the panel has been given its height, so the clamp is re-asked here.
    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        Clamp();
        return size;
    }

    /// <summary>Let the template's parts go when the template does - see ScrollBar.OnRemoveTemplate.</summary>
    public override void OnRemoveTemplate()
    {
        base.OnRemoveTemplate();
        if (_popup != null)
        {
            _popup.Closed -= OnDropDownClosed;
            _popup.ContentBuilt -= OnDropDownContentBuilt;
        }

        _popup = null;
        _dropDownPanel = null;
    }

    private void OnDropDownClosed(object sender, EventArgs e) => SetCurrentValue(IsDropDownOpenProperty, false);

    private void OnDropDownContentBuilt(object sender, EventArgs e)
    {
        _dropDownPanel = ((Popup)sender).FindContentChild("PART_DropDownPanel") as RibbonGalleryPanel;
        RefreshDropDown();
    }

    // Cells exist only while the drop-down is down - a set nobody is looking at is a set of visuals nobody is looking at.
    private void RefreshDropDown()
    {
        if (_dropDownPanel == null) return;

        _dropDownPanel.Children.Clear();
        _dropDownCells.Clear();

        if (!IsDropDownOpen) return;

        _dropDownPanel.Columns = DropDownColumns > 0 ? DropDownColumns : Math.Max(1, Columns);
        _dropDownPanel.Rows = Math.Max(1, (Items.Count + _dropDownPanel.Columns - 1) / _dropDownPanel.Columns);

        foreach (var item in Items)
        {
            var cell = new RibbonGalleryItem
            {
                Owner = this,
                DataContext = item,
                ContentTemplate = ItemTemplate,
                ContentTemplateSelector = ItemTemplateSelector,
                Content = item,
                IsSelected = IsItemSelected(item)
            };

            if (ItemContainerStyle != null) cell.Styles.Add(ItemContainerStyle);

            _dropDownPanel.Children.Add(cell);
            _dropDownCells.Add((cell, item));
        }
    }

    /// <summary>The dropped-down cells stand for the same items, so the selection has to reach them too - they are not
    /// this control's generator's, and the base would light up nothing there.</summary>
    protected override IEnumerable<(IUIComponent Container, object Item)> RealizedContainers()
    {
        foreach (var pair in base.RealizedContainers()) yield return pair;
        foreach (var pair in _dropDownCells) yield return pair;
    }

    // --- Items ------------------------------------------------------------------------------------------------------

    protected internal override IUIComponent GetContainerForItem(object item)
    {
        var cell = new RibbonGalleryItem { Owner = this };
        if (ItemContainerStyle != null) cell.Styles.Add(ItemContainerStyle);
        return cell;
    }

    protected internal override void PrepareContainer(IUIComponent container, object item)
    {
        if (container is not RibbonGalleryItem cell)
        {
            base.PrepareContainer(container, item);
            return;
        }

        if (!ReferenceEquals(cell, item))
        {
            cell.DataContext = item;
            cell.ContentTemplate = ItemTemplate;
            cell.ContentTemplateSelector = ItemTemplateSelector;
            cell.Content = item;
        }

        cell.IsSelected = IsItemSelected(item);
    }

    protected internal override void ClearContainer(IUIComponent container)
    {
        if (container is RibbonGalleryItem cell)
        {
            cell.DataContext = null;
            cell.IsSelected = false;
            return;
        }

        base.ClearContainer(container);
    }

    /// <summary>A cell was picked - in the band or in the drop-down. Picking closes the drop-down: the choice is made.</summary>
    internal void PickFromContainer(RibbonGalleryItem cell)
    {
        var index = IndexOfItem(ItemFor(cell));
        if (index < 0) return;

        SelectSingle(index);
        IsDropDownOpen = false;
    }

    /// <summary>Whether the item this cell stands for is the current choice - asked by a cell realized after the fact.</summary>
    internal bool IsItemSelectedFor(RibbonGalleryItem cell)
    {
        var item = ItemFor(cell);
        return item != null && IsItemSelected(item);
    }

    private object ItemFor(RibbonGalleryItem cell)
    {
        foreach (var (container, item) in _dropDownCells)
        {
            if (ReferenceEquals(container, cell)) return item;
        }

        var index = ItemContainerGenerator?.IndexFromContainer(cell) ?? -1;
        return index >= 0 && index < Items.Count ? Items[index] : null;
    }

}
