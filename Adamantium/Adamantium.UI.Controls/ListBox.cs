using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>
/// A <see cref="Selector"/> with selectable items. Each item is hosted in a <see cref="ListBoxItem"/> container
/// (generated + recycled like any ItemsControl). The primary selection (<see cref="Selector.SelectedItem"/> /
/// <see cref="Selector.SelectedIndex"/>) comes from the base; ListBox widens it to a full set via
/// <see cref="SelectedItems"/> + <see cref="SelectionMode"/> (single / multiple / extended). The selection lives on the
/// control (by item) and is reflected onto the containers' <see cref="ListBoxItem.IsSelected"/> - including when a recycled
/// container is rebound on scroll.
/// <para/>
/// Unlike WPF, <see cref="SelectedItems"/> is a settable, two-way-bindable collection: a view-model can hand the ListBox
/// its OWN <see cref="ObservableCollection{T}"/> and the two stay in sync (the control mutates that same instance as the
/// user selects, and listens to it so the view-model can drive the selection) - no attached-behaviour workaround needed.
/// </summary>
public class ListBox : Selector
{
    static ListBox()
    {
        // A list box takes keyboard focus (arrow-key navigation delegates to its items) - opt in, base default is false.
        FocusableProperty.OverrideMetadata(typeof(ListBox), new PropertyMetadata(true));

        // For Tab the list is a DOORWAY, not a stop: it is entered once - landing on an item - and the next Tab leaves
        // the whole list. Without the first half Tab stopped ON the list and never got inside it; without the second it
        // would walk every row before reaching whatever is underneath.
        KeyboardNavigation.IsTabStopProperty.OverrideMetadata(typeof(ListBox), new PropertyMetadata(false));
        KeyboardNavigation.TabNavigationProperty.OverrideMetadata(typeof(ListBox),
            new PropertyMetadata(KeyboardNavigationMode.Once));

        // The arrows are the LIST's own business, not the navigator's: they move the SELECTION, and the focus and the
        // scroll follow it. The navigator only ever gets keys nobody claimed, so claiming them here is what stops the
        // focus wandering off to a neighbouring control instead of down the rows.
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        OnNavigationKey(e);
    }

    private void OnNavigationKey(KeyEventArgs e)
    {
        if (e.Handled || Items == null || Items.Count == 0) return;

        var direction = DirectionOf(e.Key);
        if (direction == null || ItemsHostPanel is not INavigablePanel host) return;

        // The list KEEPS its arrows whether or not they lead anywhere: running out of rows must not hand the focus to
        // whatever sits beside the list, and one arrow too many should not cost you your place in it.
        e.Handled = true;

        // Nothing selected yet: the first press takes the first item, whichever key it was.
        if (SelectedIndex < 0)
        {
            SelectOnly(0);
            ShowSelected(0);
            return;
        }

        // WHICH item is next is the PANEL's answer, not ours: it is the one that knows whether the items stand in a
        // column, a row, or a grid of wrapped lines. Stepping the index by one instead - as this did - reads as "down"
        // meaning "the next chip along" in a wrapped list, which is not what the arrow pointed at.
        var index = NextIndex(host, direction.Value);
        if (index < 0 || index >= Items.Count) return;

        SelectOnly(index);
        ShowSelected(index);
    }

    /// <summary>The item an arrow leads to. Normally the panel's answer, from the geometry of the row the selection is
    /// on - but a selection that has been SCROLLED AWAY FROM has no container to ask about, and answering "nowhere"
    /// there left the keyboard dead until something visible was selected again. Item order is the only thing that still
    /// means anything in that state, so the arrow steps through it and the view comes back to the selection.</summary>
    private int NextIndex(INavigablePanel host, FocusNavigationDirection direction)
    {
        var current = ItemContainerGenerator.ContainerFromIndex(SelectedIndex);
        if (current is { Visibility: Visibility.Visible } && host.Navigate(current, direction) is { } next)
        {
            return ItemContainerGenerator.IndexFromContainer(next);
        }

        return current is { Visibility: Visibility.Visible }
            ? -1                                                        // on screen, and the panel says there is nothing that way
            : SelectedIndex + (direction is FocusNavigationDirection.Down or FocusNavigationDirection.Right ? 1 : -1);
    }

    private static FocusNavigationDirection? DirectionOf(Key key) => key switch
    {
        Key.LeftArrow => FocusNavigationDirection.Left,
        Key.RightArrow => FocusNavigationDirection.Right,
        Key.UpArrow => FocusNavigationDirection.Up,
        Key.DownArrow => FocusNavigationDirection.Down,
        _ => null
    };

    /// <summary>Put the focus on the newly selected row and scroll it into view.</summary>
    private void ShowSelected(int index)
    {
        var container = ItemContainerGenerator.ContainerFromIndex(index);
        if (container is IInputComponent focusable && container.Visibility == Visibility.Visible)
        {
            FocusManager.Focus(focusable, NavigationMethod.Directional);
            (container as UIComponent)?.BringIntoView();
            return;
        }

        // Not realized - the row is outside the window the panel is keeping. There is no visual to scroll to, but the
        // panel can still say WHERE the row will be, and scrolling there materialises it: the view comes back to the
        // selection now, and the focus lands on it as soon as it exists.
        if (ItemsHostPanel is VirtualizingPanel panel && panel.TryGetItemRect(index, out var rect))
        {
            EnclosingScrollViewer()?.BringIntoView(rect);
        }
    }

    private ScrollViewer EnclosingScrollViewer()
    {
        for (IUIComponent node = ItemsHostPanel; node != null; node = node.VisualParent)
        {
            if (node is ScrollViewer viewer) return viewer;
        }

        return null;
    }

    private HashSet<object> _selectedSet = [];  // O(1) membership truth - used to reflect selection onto (recycled) containers
    private int _anchorIndex = -1;              // Extended-mode Shift-range anchor

    public static readonly AdamantiumProperty SelectionModeProperty = AdamantiumProperty.Register(nameof(SelectionMode),
        typeof(SelectionMode), typeof(ListBox), new PropertyMetadata(SelectionMode.Single));

    public static readonly AdamantiumProperty SelectedItemsProperty = AdamantiumProperty.Register(nameof(SelectedItems),
        typeof(IList), typeof(ListBox),
        new PropertyMetadata(null, PropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemsChanged));

    /// <summary>How clicks change the selection. Default <see cref="SelectionMode.Single"/>.</summary>
    public SelectionMode SelectionMode
    {
        get => GetValue<SelectionMode>(SelectionModeProperty);
        set => SetValue(SelectionModeProperty, value);
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

    // Selector raises these when SelectedIndex/SelectedItem is set from OUTSIDE (binding/code). ListBox routes them through
    // its multi-select machinery so the bound SelectedItems collection and the set stay consistent with the primary.
    protected override void OnSelectedIndexSet(int index)
    {
        if (index >= 0 && index < Items.Count) SelectOnly(index); else ClearSelection();
    }

    protected override void OnSelectedItemSet(object item)
    {
        var index = IndexOfItem(item);
        if (index >= 0) SelectOnly(index); else ClearSelection();
    }

    private static void OnSelectedItemsChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        var listBox = (ListBox)a;
        if (e.OldValue is INotifyCollectionChanged oldObservable)
            oldObservable.CollectionChanged -= listBox.OnSelectedItemsCollectionChanged;
        if (e.NewValue is INotifyCollectionChanged newObservable)
            newObservable.CollectionChanged += listBox.OnSelectedItemsCollectionChanged;
        if (listBox.SyncingSelection) return;   // our own create-on-demand, not an external (re)bind - don't re-read it
        listBox.AdoptBoundSelection();
    }

    // The view-model mutated the bound collection (the control->view-model direction is guarded out via SyncingSelection).
    private void OnSelectedItemsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (SyncingSelection) return;
        AdoptBoundSelection();
    }

    // --- Selection core -------------------------------------------------------------------------------------------

    // Apply a new selection (the full set of items) + the primary index, then propagate everywhere: the single-selection
    // properties, the bound SelectedItems collection (unless it IS the source of this change), the containers, the event.
    private void ApplySelection(IReadOnlyList<object> items, int primaryIndex, bool writeBoundList)
    {
        // Only materialise the internal mirror collection when there is actually something to store. Creating an empty one
        // eagerly (the ctor seeds SelectedIndex=-1 -> ClearSelection, and base-property seeding now runs that before the
        // SelectedItems slot exists) would pin SelectedItems at Local priority and mask a later two-way {Binding}
        // (Binding < Local), silently isolating the view-model's collection.
        if (writeBoundList && items.Count > 0) EnsureSelectedItems();
        var newSet = new HashSet<object>(items);
        var changed = !newSet.SetEquals(_selectedSet);
        _selectedSet = newSet;

        SyncingSelection = true;
        SelectedIndex = primaryIndex;
        SelectedItem = primaryIndex >= 0 && primaryIndex < Items.Count ? Items[primaryIndex] : null;
        if (writeBoundList) SyncBoundList(items);
        SyncingSelection = false;

        UpdateContainersSelection();
        if (changed) RaiseSelectionChanged();
    }

    // Reconcile the bound SelectedItems collection to exactly 'items' (minimal diff so observers see granular changes, not
    // a Reset). Runs under SyncingSelection so the CollectionChanged handler ignores these (our own) edits.
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
        SyncingSelection = true;
        SelectedItems = new ObservableCollection<object>();
        SyncingSelection = false;
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

    // The full selection set (not just the primary item) drives which containers show selected.
    protected override bool IsItemSelected(object item) => _selectedSet.Contains(item);

    // --- Container seam: ListBox hosts items in ListBoxItem containers --------------------------------------------

    protected internal override bool IsItemItsOwnContainer(object item) => item is ListBoxItem;

    protected internal override IUIComponent GetContainerForItem(object item)
    {
        var container = new ListBoxItem();
        // Into the Styles collection (a USER style), not AttachStyles: the theme is applied to the container on attach,
        // and ApplyCurrentTheme re-applies the Styles collection AFTER the theme - so the ItemContainerStyle overrides
        // the theme's default container style instead of being overwritten by it.
        if (ItemContainerStyle != null) container.Styles.Add(ItemContainerStyle);
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
