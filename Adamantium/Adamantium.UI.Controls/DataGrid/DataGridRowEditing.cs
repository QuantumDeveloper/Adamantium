using System;
using System.Collections;
using System.Collections.Generic;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.DataGrid;

/// <summary>A row is about to be removed, or a new one is about to be made. Cancel it, or hand over the item yourself -
/// only the application knows whether a record may go, and what a NEW record even is.</summary>
public class DataGridRowEventArgs : EventArgs
{
    public DataGridRowEventArgs(object row, object parent)
    {
        Row = row;
        Parent = parent;
    }

    /// <summary>The row being removed; null when one is being added.</summary>
    public object Row { get; }

    /// <summary>The node the row hangs under, or null at the top level.</summary>
    public object Parent { get; }

    /// <summary>The item to add. Set it to supply your own; left null the grid builds one with the item type's
    /// parameterless constructor, and adds nothing if there is none.</summary>
    public object NewItem { get; set; }

    public bool Cancel { get; set; }
}

public partial class TreeDataGrid
{
    public static readonly AdamantiumProperty CanUserDeleteRowsProperty = AdamantiumProperty.Register(
        nameof(CanUserDeleteRows), typeof(bool), typeof(TreeDataGrid), new PropertyMetadata(true));

    public static readonly AdamantiumProperty CanUserAddRowsProperty = AdamantiumProperty.Register(
        nameof(CanUserAddRows), typeof(bool), typeof(TreeDataGrid), new PropertyMetadata(true));

    /// <summary>Whether <c>Delete</c> removes the selected rows. On by default, and harmless where it cannot be done:
    /// a source that is not a mutable list simply refuses.</summary>
    public bool CanUserDeleteRows
    {
        get => GetValue<bool>(CanUserDeleteRowsProperty);
        set => SetValue(CanUserDeleteRowsProperty, value);
    }

    /// <summary>Whether <c>Insert</c> adds a row beside the current one.</summary>
    public bool CanUserAddRows
    {
        get => GetValue<bool>(CanUserAddRowsProperty);
        set => SetValue(CanUserAddRowsProperty, value);
    }

    /// <summary>Raised before a row is removed. Cancel to keep it - a record with children, one the server owns.</summary>
    public event EventHandler<DataGridRowEventArgs> RowDeleting;

    /// <summary>Raised before a row is added, so the application can supply the item. Without a handler the grid builds
    /// one from the item type's parameterless constructor.</summary>
    public event EventHandler<DataGridRowEventArgs> RowAdding;

    /// <summary>Removes every selected row, deepest first so an index cannot go stale under its own removal. Returns
    /// false when nothing was removed - nothing selected, refused, or a source that cannot lose items.</summary>
    public bool DeleteSelectedRows()
    {
        if (!CanUserDeleteRows || Rows is not { } rows) return false;

        var indices = new List<int>();
        for (var i = 0; i < rows.Count; i++)
        {
            if (SelectedCells.ContainsRow(i)) indices.Add(i);
        }

        if (indices.Count == 0 && ActiveRow >= 0) indices.Add(ActiveRow);

        // ONE act, however many rows went: clearing a selection is one thing the user did.
        _history.Begin();

        var removed = false;
        for (var i = indices.Count - 1; i >= 0; i--) removed |= DeleteRow(indices[i]);

        _history.End();

        if (removed) SelectedCells.Clear();
        return removed;
    }

    /// <summary>Removes one row from the collection that owns it - the source at the top level, its parent's children
    /// below. False when the row cannot be reached, a handler refused, or the collection is not a mutable list.</summary>
    public bool DeleteRow(int index)
    {
        if (Rows is not { } rows || index < 0 || index >= rows.Count) return false;

        var node = rows[index].Node;
        var owner = OwnerCollectionOf(index, out var parent);
        if (owner is not { IsReadOnly: false } || !owner.Contains(node)) return false;

        var args = new DataGridRowEventArgs(node, parent);
        RowDeleting?.Invoke(this, args);
        if (args.Cancel) return false;

        // WHERE it was, not just what it was: a record put back at the end of the list is not the record that was taken
        // out of the middle of it.
        var at = owner.IndexOf(node);
        owner.Remove(node);
        _history.Record(new DataGridRowEdit(owner, node, at, added: false));

        if (parent != null || owner is not System.Collections.Specialized.INotifyCollectionChanged) Refresh();
        return true;
    }

    /// <summary>Adds a row beside the current one - at the same level, right after it - and returns the item added, or
    /// null when nothing was. The application supplies the item through <see cref="RowAdding"/>; without a handler the
    /// item type's parameterless constructor is used.</summary>
    public object AddRow()
    {
        if (!CanUserAddRows) return null;

        var rows = Rows;
        var index = rows != null && ActiveRow >= 0 && ActiveRow < rows.Count ? ActiveRow : -1;
        var owner = index >= 0 ? OwnerCollectionOf(index, out var found) : Roots(out _);
        var parent = index >= 0 ? ParentOf(index) : null;

        if (owner is not { IsReadOnly: false }) return null;

        var args = new DataGridRowEventArgs(null, parent);
        RowAdding?.Invoke(this, args);
        if (args.Cancel) return null;

        var item = args.NewItem ?? NewItemLike(index >= 0 ? rows[index].Node : FirstItem(owner));
        if (item == null) return null;

        var at = Math.Clamp(index >= 0 ? owner.IndexOf(rows[index].Node) + 1 : owner.Count, 0, owner.Count);
        owner.Insert(at, item);
        _history.Record(new DataGridRowEdit(owner, item, at, added: true));

        if (parent != null || owner is not System.Collections.Specialized.INotifyCollectionChanged) Refresh();
        return item;
    }

    /// <summary>Makes the record the placeholder row was standing in for, writes what was just typed into it, and adds
    /// it at the END of the top level - where the blank row was. Null when nothing was made: no mutable source, a
    /// handler that refused, or an item type that cannot be built and was not supplied.
    /// <para>The same RowAdding the Insert gesture raises, so an application answers for both in one place.</para>
    /// </summary>
    internal object CreateFromNewRow(IEnumerable<KeyValuePair<DataGridColumn, object>> values)
    {
        if (!CanUserAddRows || Roots(out _) is not { IsReadOnly: false } owner) return null;

        var args = new DataGridRowEventArgs(null, null);
        RowAdding?.Invoke(this, args);
        if (args.Cancel) return null;

        var item = args.NewItem ?? NewItemLike(FirstItem(owner));
        if (item == null) return null;

        // EVERY field that was filled in, and all of them BEFORE the record goes in. Added first, it would be sorted,
        // filtered and grouped by what it does not hold yet, and then move again the moment it did - a record that
        // lands twice in two places.
        // Written STRAIGHT, not through the history: the record is not in the table yet, so there is nothing to take a
        // value back to. Taking the record out again is what undoing this act means, and that is the one thing recorded.
        foreach (var pair in values)
        {
            if (pair.Key?.Binding != null) pair.Key.Write(item, pair.Value);
        }

        owner.Add(item);
        _history.Record(new DataGridRowEdit(owner, item, owner.Count - 1, added: true));
        if (owner is not System.Collections.Specialized.INotifyCollectionChanged) Refresh();
        return item;
    }

    private static object FirstItem(IList list) => list.Count > 0 ? list[0] : null;

    private static object NewItemLike(object sibling)
    {
        var type = sibling?.GetType();
        if (type == null || type.GetConstructor(Type.EmptyTypes) == null) return null;

        return Activator.CreateInstance(type);
    }

    private IList Roots(out object parent)
    {
        parent = null;
        return ItemsSource as IList;
    }

    private object ParentOf(int index)
    {
        var rows = Rows;
        if (rows == null || index <= 0) return null;

        var depth = rows[index].Depth;
        if (depth == 0) return null;

        for (var i = index - 1; i >= 0; i--)
        {
            if (rows[i].Depth == depth - 1) return rows[i].Node;
        }

        return null;
    }

    private IList OwnerCollectionOf(int index, out object parent)
    {
        parent = ParentOf(index);
        return parent == null ? ItemsSource as IList : _rawChildren(parent) as IList;
    }
}
