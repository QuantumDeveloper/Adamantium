using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>
/// An <see cref="ItemsControl"/> that tracks a primary selection - <see cref="SelectedItem"/> / <see cref="SelectedIndex"/>
/// (two-way bindable) plus the <see cref="SelectionChanged"/> event. The selection lives on the control (by item) and is
/// reflected onto whichever container currently hosts each item via <see cref="ISelectable.IsSelected"/> - including a
/// recycled container rebound on scroll. Subclasses layer their own semantics on top: <see cref="ListBox"/> widens it to
/// multi-select, <see cref="TabControl"/> keeps it single (the active tab).
/// <para/>
/// Distinct from <see cref="Adamantium.UI.Core.Resources.StyleSelector"/> (a style's match rule), which was renamed so
/// this control base can carry the conventional WPF name.
/// </summary>
public abstract class Selector : ItemsControl
{
    public static readonly AdamantiumProperty SelectedIndexProperty = AdamantiumProperty.Register(nameof(SelectedIndex),
        typeof(int), typeof(Selector),
        new PropertyMetadata(-1, PropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedIndexPropertyChanged));

    public static readonly AdamantiumProperty SelectedItemProperty = AdamantiumProperty.Register(nameof(SelectedItem),
        typeof(object), typeof(Selector),
        new PropertyMetadata(null, PropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemPropertyChanged));

    /// <summary>Guards the control's own writes to the selection properties from re-entering their change callbacks.</summary>
    protected bool SyncingSelection { get; set; }

    private object _selectedItemTracked;   // last item SelectSingle selected - change detection independent of DP write ordering

    /// <summary>Raised when the selection changes.</summary>
    public event EventHandler SelectionChanged;

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

    private static void OnSelectedIndexPropertyChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        var selector = (Selector)a;
        if (selector.SyncingSelection) return;   // our own write (ApplySelection / SelectSingle), not an external one
        selector.OnSelectedIndexSet((int)e.NewValue);
    }

    private static void OnSelectedItemPropertyChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        var selector = (Selector)a;
        if (selector.SyncingSelection) return;
        selector.OnSelectedItemSet(e.NewValue);
    }

    /// <summary>SelectedIndex was set from OUTSIDE (binding / code). Default = single-select that index.</summary>
    protected virtual void OnSelectedIndexSet(int index) => SelectSingle(index);

    /// <summary>A selection named before the items existed. Bindings are established before an ItemsSource has produced
    /// anything, so a view-model that names its own selected item states it while the collection is still empty - and
    /// discarding it there does not merely lose it: the discard is written BACK through a TwoWay binding, so the source
    /// loses its own choice and whatever ends up first wins instead.</summary>
    private object _pendingSelection;

    /// <summary>SelectedItem was set from OUTSIDE (binding / code). Default = single-select that item.</summary>
    protected virtual void OnSelectedItemSet(object item)
    {
        var index = IndexOfItem(item);

        // Named, but not here yet - remember it and write nothing: nothing has been decided.
        if (index < 0 && item != null && Items.Count == 0)
        {
            _pendingSelection = item;
            return;
        }

        _pendingSelection = null;
        SelectSingle(index);
    }

    /// <summary>The index of a selection named before the items existed, now that they have arrived - or -1 if there was
    /// none, or the named item is still not among them. Consumed on success, so it answers once.</summary>
    protected int TakePendingSelectionIndex()
    {
        if (_pendingSelection == null) return -1;

        var index = IndexOfItem(_pendingSelection);
        if (index >= 0) _pendingSelection = null;
        return index;
    }

    /// <summary>Makes exactly <paramref name="index"/> the selection (or clears it when out of range), writes the primary
    /// selection properties (guarded), reflects onto the containers, and raises <see cref="SelectionChanged"/> on change.</summary>
    protected void SelectSingle(int index)
    {
        if (index < 0 || index >= Items.Count) index = -1;
        var item = index >= 0 ? Items[index] : null;
        // Compare against our own tracked selection, NOT SelectedIndex/SelectedItem: when this runs from a property
        // callback the DP value is ALREADY updated, so a SelectedIndex==index check would always read "unchanged".
        var changed = !Equals(item, _selectedItemTracked);
        _selectedItemTracked = item;
        SetSelectedProperties(index, item);
        UpdateContainersSelection();
        if (changed) RaiseSelectionChanged();
    }

    /// <summary>Writes SelectedIndex/SelectedItem without re-triggering the external-change callbacks.
    /// <para>SetCurrentValue, not the CLR setters: those write at Local priority, which OUTRANKS a binding - so the
    /// control's own housekeeping (auto-selecting the first item when the collection fills) permanently masked a
    /// <c>SelectedItem="{Binding}"</c>, and the source could never state the selection it wanted. This writes the value
    /// where the binding writes it, so establishing the binding still overrules the default.</para></summary>
    protected void SetSelectedProperties(int index, object item)
    {
        SyncingSelection = true;
        SetCurrentValue(SelectedIndexProperty, index);
        SetCurrentValue(SelectedItemProperty, item);
        SyncingSelection = false;
    }

    protected void RaiseSelectionChanged() => SelectionChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>Whether an item is part of the current selection. Base = the single primary item; ListBox widens it.</summary>
    protected virtual bool IsItemSelected(object item) => Equals(item, SelectedItem);

    /// <summary>Reflects the current selection onto every realized container.</summary>
    protected void UpdateContainersSelection()
    {
        // The base ctor seeds each property with its default and fires its changed callback, so a selection callback can
        // run during construction - before ItemsControl's ctor has created the generator. Nothing to reflect yet.
        if (ItemContainerGenerator == null) return;

        foreach (var (container, item) in RealizedContainers())
        {
            if (container is ISelectable selectable) selectable.IsSelected = IsItemSelected(item);
        }
    }

    /// <summary>Every realized container, with the item it stands for. Virtual because a control may lay its items out
    /// in MORE THAN ONE list - a tab strip keeps pinned tabs in a row of their own, realized by that row's generator -
    /// and a selection that only reached this control's own generator would light up nothing there.</summary>
    protected virtual IEnumerable<(IUIComponent Container, object Item)> RealizedContainers()
    {
        foreach (var index in ItemContainerGenerator.RealizedIndices.ToList())
        {
            // An index the item list no longer has: the realized set can be a step behind the collection (an item was
            // removed and the generator has not been told yet), and there is nothing to reflect onto a container that
            // stands for nothing. It used to read straight through and take the whole app down on a closed tab.
            if (index < 0 || index >= Items.Count) continue;

            if (ItemContainerGenerator.ContainerFromIndex(index) is { } container) yield return (container, Items[index]);
        }
    }

    /// <summary>Reflects the selection onto ONE container - called from a subclass's PrepareContainer so a new or recycled
    /// container inherits the right state on (re)bind.</summary>
    protected void ApplyContainerSelection(IUIComponent container, object item)
    {
        if (container is ISelectable selectable) selectable.IsSelected = IsItemSelected(item);
    }

    protected int IndexOfItem(object item)
    {
        if (item == null || Items == null) return -1;
        for (var i = 0; i < Items.Count; i++)
            if (Equals(Items[i], item)) return i;
        return -1;
    }
}
