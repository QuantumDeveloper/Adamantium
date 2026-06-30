using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>
/// An <see cref="ItemsControl"/> with selectable items. Each item is hosted in a <see cref="ListBoxItem"/> container
/// (generated + recycled like any ItemsControl). The selection lives on the ListBox (as the set of selected items) and is
/// reflected onto the containers' <see cref="ListBoxItem.IsSelected"/> - including when a recycled container is rebound on
/// scroll. <see cref="SelectionMode"/> chooses single / multiple / extended selection; <see cref="SelectedItem"/> /
/// <see cref="SelectedIndex"/> are the primary (anchor) selection, and <see cref="SelectedItems"/> is the full set.
/// <para/>
/// Unlike WPF, <see cref="SelectedItems"/> is a settable, two-way-bindable collection: a view-model can hand the ListBox
/// its OWN <see cref="ObservableCollection{T}"/> and the two stay in sync (the control mutates that same instance as the
/// user selects, and listens to it so the view-model can drive the selection) - no attached-behaviour workaround needed.
/// </summary>
public class ListBox : ItemsControl
{
    private bool _syncingSelection;             // guards our own writes (index/item/bound-list) from re-entering
    private HashSet<object> _selectedSet = [];  // O(1) membership truth - used to reflect selection onto (recycled) containers
    private int _anchorIndex = -1;              // Extended-mode Shift-range anchor

    public static readonly AdamantiumProperty SelectionModeProperty = AdamantiumProperty.Register(nameof(SelectionMode),
        typeof(SelectionMode), typeof(ListBox), new PropertyMetadata(SelectionMode.Single));

    public static readonly AdamantiumProperty SelectedIndexProperty = AdamantiumProperty.Register(nameof(SelectedIndex),
        typeof(int), typeof(ListBox),
        new PropertyMetadata(-1, PropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedIndexChanged));

    public static readonly AdamantiumProperty SelectedItemProperty = AdamantiumProperty.Register(nameof(SelectedItem),
        typeof(object), typeof(ListBox),
        new PropertyMetadata(null, PropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

    public static readonly AdamantiumProperty SelectedItemsProperty = AdamantiumProperty.Register(nameof(SelectedItems),
        typeof(IList), typeof(ListBox),
        new PropertyMetadata(null, PropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemsChanged));

    /// <summary>Raised when the selection changes (in any mode). For granular changes, observe <see cref="SelectedItems"/>.</summary>
    public event EventHandler SelectionChanged;

    /// <summary>How clicks change the selection. Default <see cref="SelectionMode.Single"/>.</summary>
    public SelectionMode SelectionMode
    {
        get => GetValue<SelectionMode>(SelectionModeProperty);
        set => SetValue(SelectionModeProperty, value);
    }

    /// <summary>Index of the primary selected item, or -1 when nothing is selected.</summary>
    public int SelectedIndex
    {
        get => GetValue<int>(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <summary>The primary selected item, or null when nothing is selected.</summary>
    public object SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <summary>
    /// The full set of selected items. Settable and two-way bindable: bind a view-model <see cref="ObservableCollection{T}"/>
    /// and the control keeps it in sync both ways (it mutates this instance as the user selects, and reflects external
    /// mutations onto the selection). Use an <see cref="INotifyCollectionChanged"/> collection for the view-model->control
    /// direction.
    /// </summary>
    public IList SelectedItems
    {
        get => GetValue<IList>(SelectedItemsProperty);
        set => SetValue(SelectedItemsProperty, value);
    }

    // --- Property callbacks ---------------------------------------------------------------------------------------

    private static void OnSelectedIndexChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        var listBox = (ListBox)a;
        if (listBox._syncingSelection) return;
        var index = (int)e.NewValue;
        if (index >= 0 && index < listBox.Items.Count) listBox.SelectOnly(index); else listBox.ClearSelection();
    }

    private static void OnSelectedItemChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        var listBox = (ListBox)a;
        if (listBox._syncingSelection) return;
        var index = listBox.IndexOfItem(e.NewValue);
        if (index >= 0) listBox.SelectOnly(index); else listBox.ClearSelection();
    }

    private static void OnSelectedItemsChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        var listBox = (ListBox)a;
        if (e.OldValue is INotifyCollectionChanged oldObservable)
            oldObservable.CollectionChanged -= listBox.OnSelectedItemsCollectionChanged;
        if (e.NewValue is INotifyCollectionChanged newObservable)
            newObservable.CollectionChanged += listBox.OnSelectedItemsCollectionChanged;
        if (listBox._syncingSelection) return;   // our own create-on-demand, not an external (re)bind - don't re-read it
        listBox.AdoptBoundSelection();
    }

    // The view-model mutated the bound collection (the control->view-model direction is guarded out via _syncingSelection).
    private void OnSelectedItemsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (_syncingSelection) return;
        AdoptBoundSelection();
    }

    // --- Selection core -------------------------------------------------------------------------------------------

    // Apply a new selection (the full set of items) + the primary index, then propagate everywhere: the single-selection
    // properties, the bound SelectedItems collection (unless it IS the source of this change), the containers, the event.
    private void ApplySelection(IReadOnlyList<object> items, int primaryIndex, bool writeBoundList)
    {
        if (writeBoundList) EnsureSelectedItems();
        var newSet = new HashSet<object>(items);
        var changed = !newSet.SetEquals(_selectedSet);
        _selectedSet = newSet;

        _syncingSelection = true;
        SelectedIndex = primaryIndex;
        SelectedItem = primaryIndex >= 0 && primaryIndex < Items.Count ? Items[primaryIndex] : null;
        if (writeBoundList) SyncBoundList(items);
        _syncingSelection = false;

        UpdateContainersSelection();
        if (changed) SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    // Reconcile the bound SelectedItems collection to exactly 'items' (minimal diff so observers see granular changes, not
    // a Reset). Runs under _syncingSelection so the CollectionChanged handler ignores these (our own) edits.
    private void SyncBoundList(IReadOnlyList<object> items)
    {
        var list = SelectedItems;
        if (list == null) return;
        var desired = new HashSet<object>(items);
        for (var i = list.Count - 1; i >= 0; i--)
            if (!desired.Contains(list[i])) list.RemoveAt(i);
        foreach (var item in items)
            if (!list.Contains(item)) list.Add(item);
    }

    // The bound collection became the source (replaced via binding, or the view-model mutated it): rebuild the selection
    // from its contents (the primary is the last entry) WITHOUT writing back to it.
    private void AdoptBoundSelection()
    {
        var items = SelectedItems?.Cast<object>().ToList() ?? [];
        var primary = items.Count > 0 ? IndexOfItem(items[^1]) : -1;
        ApplySelection(items, primary, writeBoundList: false);
    }

    // Create the mirror collection on demand (when nothing was bound) so SelectedItems is usable without a binding. We
    // can't seed it in the ctor: a local value there outranks a {Binding} (Local > Binding in ValuePriority), which would
    // silently block two-way binding to a view-model collection. Guarded so the property callback skips AdoptBoundSelection.
    private void EnsureSelectedItems()
    {
        if (SelectedItems != null) return;
        _syncingSelection = true;
        SelectedItems = new ObservableCollection<object>();
        _syncingSelection = false;
    }

    private void SelectOnly(int index)
    {
        _anchorIndex = index;
        ApplySelection([Items[index]], index, writeBoundList: true);
    }

    private void ClearSelection()
    {
        _anchorIndex = -1;
        ApplySelection([], -1, writeBoundList: true);
    }

    private void ToggleAt(int index)
    {
        _anchorIndex = index;
        var item = Items[index];
        var items = SelectedItems?.Cast<object>().ToList() ?? [];
        if (_selectedSet.Contains(item)) items.Remove(item); else items.Add(item);
        var primary = items.Count > 0 ? IndexOfItem(items[^1]) : -1;
        ApplySelection(items, primary, writeBoundList: true);
    }

    private void SelectRangeTo(int index)
    {
        var lo = Math.Min(_anchorIndex, index);
        var hi = Math.Max(_anchorIndex, index);
        var items = new List<object>(hi - lo + 1);
        for (var i = lo; i <= hi; i++) items.Add(Items[i]);
        ApplySelection(items, index, writeBoundList: true);   // anchor stays put across a range drag
    }

    // A container was pressed -> change the selection per the current mode + keyboard modifiers (Extended).
    internal void SelectFromContainer(ListBoxItem container, InputModifiers modifiers = InputModifiers.None)
    {
        var index = ItemContainerGenerator.IndexFromContainer(container);
        if (index < 0) return;
        var ctrl = (modifiers & (InputModifiers.LeftControl | InputModifiers.RightControl)) != 0;
        var shift = (modifiers & (InputModifiers.LeftShift | InputModifiers.RightShift)) != 0;

        switch (SelectionMode)
        {
            case SelectionMode.Multiple:
                ToggleAt(index);
                break;
            case SelectionMode.Extended when shift && _anchorIndex >= 0:
                SelectRangeTo(index);
                break;
            case SelectionMode.Extended when ctrl:
                ToggleAt(index);
                break;
            default:   // Single, or Extended plain click
                SelectOnly(index);
                break;
        }
    }

    private void UpdateContainersSelection()
    {
        // The base ctor seeds each property with its default and fires its changed callback, so a selection callback can
        // run during construction - before ItemsControl's ctor has created the generator. Nothing to reflect yet.
        if (ItemContainerGenerator == null) return;
        foreach (var index in ItemContainerGenerator.RealizedIndices.ToList())
            if (ItemContainerGenerator.ContainerFromIndex(index) is ListBoxItem item)
                item.IsSelected = _selectedSet.Contains(Items[index]);
    }

    private int IndexOfItem(object item)
    {
        if (item == null || Items == null) return -1;
        for (var i = 0; i < Items.Count; i++)
            if (Equals(Items[i], item)) return i;
        return -1;
    }

    private bool IsItemSelected(object item) => _selectedSet.Contains(item);

    // --- Container seam: ListBox hosts items in ListBoxItem containers --------------------------------------------

    protected internal override bool IsItemItsOwnContainer(object item) => item is ListBoxItem;

    protected internal override IUIComponent GetContainerForItem()
    {
        var container = new ListBoxItem();
        if (ItemContainerStyle != null) container.AttachStyles(ItemContainerStyle);
        return container;
    }

    protected internal override void PrepareContainer(IUIComponent container, object item)
    {
        if (container is ListBoxItem listItem)
        {
            listItem.DataContext = item;
            listItem.ContentTemplate = ItemTemplate;
            listItem.ContentTemplateSelector = ItemTemplateSelector;
            listItem.Content = item;
            listItem.IsSelected = IsItemSelected(item);   // recycled containers inherit the new item's selection state
        }
        else
        {
            base.PrepareContainer(container, item);
        }
    }

    protected internal override void ClearContainer(IUIComponent container)
    {
        if (container is ListBoxItem listItem)
        {
            listItem.DataContext = null;
            listItem.IsSelected = false;
        }
        else
        {
            base.ClearContainer(container);
        }
    }
}
