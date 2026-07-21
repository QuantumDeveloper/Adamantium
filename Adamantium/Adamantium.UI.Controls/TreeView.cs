using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

/// <summary>
/// A hierarchical list: a tree of <see cref="TreeViewItem"/> nodes, data-driven via <c>ItemsSource</c> +
/// a <see cref="HierarchicalDataTemplate"/> (the same seam MenuItem uses). Clicking a node selects it; the
/// <see cref="SelectionMode"/> decides whether one, many, or a list-box-style (Ctrl/Shift) set can be selected.
/// </summary>
public class TreeView : ItemsControl
{
    // Read-only: the DATA item behind the most-recently selected node (null = nothing selected).
    public static readonly AdamantiumProperty SelectedItemProperty = AdamantiumProperty.Register(nameof(SelectedItem),
        typeof(object), typeof(TreeView), new PropertyMetadata(null));

    /// <summary>Single (one node - the default), Multiple (each click toggles), or Extended (Ctrl/Shift like a list box).</summary>
    public static readonly AdamantiumProperty SelectionModeProperty = AdamantiumProperty.Register(nameof(SelectionMode),
        typeof(TreeViewSelectionMode), typeof(TreeView), new PropertyMetadata(TreeViewSelectionMode.Single));

    /// <summary>Whether double-clicking a node that has children toggles its expansion (true by default). A leaf does nothing.</summary>
    public static readonly AdamantiumProperty ExpandOnDoubleClickProperty = AdamantiumProperty.Register(nameof(ExpandOnDoubleClick),
        typeof(bool), typeof(TreeView), new PropertyMetadata(true));

    private TreeViewItem _selectedContainer;
    // The fixed end of a Shift+click range: the node of the last plain / Ctrl click (Extended mode).
    private TreeViewItem _anchor;

    /// <summary>The data item of the most-recently selected node. Read-only - set selection by clicking a node or its IsSelected.</summary>
    public object SelectedItem { get => GetValue<object>(SelectedItemProperty); private set => SetValue(SelectedItemProperty, value); }

    /// <summary>How many nodes may be selected and how clicks combine (Single, Multiple, Extended).</summary>
    public TreeViewSelectionMode SelectionMode { get => GetValue<TreeViewSelectionMode>(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }

    /// <summary>Whether a double-click on a branch toggles its expansion (default true).</summary>
    public bool ExpandOnDoubleClick { get => GetValue<bool>(ExpandOnDoubleClickProperty); set => SetValue(ExpandOnDoubleClickProperty, value); }

    // A data-driven tree generates TreeViewItem containers so each node gets a header + its own child items; a flat
    // ItemTemplate keeps the base ContentPresenter.
    protected internal override IUIComponent GetContainerForItem(object item)
        => ItemTemplate is HierarchicalDataTemplate ? TreeViewItem.CreateContainer(ItemContainerStyle) : base.GetContainerForItem(item);

    // A node was clicked (routed here from the node - it only knows it was hit; the SELECTION POLICY lives here, in one
    // place, because Single/Extended must reach across nodes to clear or range-select the others).
    internal void OnItemClicked(TreeViewItem item, InputModifiers modifiers)
    {
        switch (SelectionMode)
        {
            case TreeViewSelectionMode.Multiple:
                Toggle(item);
                break;

            case TreeViewSelectionMode.Extended:
                var ctrl = (modifiers & (InputModifiers.LeftControl | InputModifiers.RightControl)) != 0;
                var shift = (modifiers & (InputModifiers.LeftShift | InputModifiers.RightShift)) != 0;
                if (shift && _anchor != null) SelectRange(_anchor, item);
                else if (ctrl) { Toggle(item); _anchor = item; }
                else { SelectOnly(item); _anchor = item; }
                break;

            default: // Single
                SelectOnly(item);
                break;
        }
    }

    // Flip one node, leaving the rest untouched (Multiple, and Extended's Ctrl+click).
    private void Toggle(TreeViewItem item)
    {
        item.IsSelected = !item.IsSelected;
        if (item.IsSelected) { _selectedContainer = item; SelectedItem = (item as UIComponent)?.DataContext; }
        else if (ReferenceEquals(_selectedContainer, item)) { _selectedContainer = null; SelectedItem = null; }
    }

    // Exactly one node selected - clear every other, select this (Single, and Extended's plain click).
    private void SelectOnly(TreeViewItem item)
    {
        foreach (var c in Containers(visibleOnly: false))
            if (!ReferenceEquals(c, item)) c.IsSelected = false;
        item.IsSelected = true;
        _selectedContainer = item;
        SelectedItem = (item as UIComponent)?.DataContext;
    }

    // Select the visible run between the anchor and the clicked node (inclusive), clearing everything else - the
    // list-box Shift+click. Order follows the on-screen layout (depth-first, expanded branches only).
    private void SelectRange(TreeViewItem anchor, TreeViewItem item)
    {
        var visible = Containers(visibleOnly: true);
        var a = visible.IndexOf(anchor);
        var b = visible.IndexOf(item);
        if (a < 0 || b < 0) { SelectOnly(item); return; }
        if (a > b) (a, b) = (b, a);
        for (var i = 0; i < visible.Count; i++) visible[i].IsSelected = i >= a && i <= b;
        _selectedContainer = item;
        SelectedItem = (item as UIComponent)?.DataContext;
    }

    // Every TreeViewItem in display order. visibleOnly stops at a collapsed branch (its hidden nodes aren't range-selectable);
    // the clear-all path takes them too, so a node selected then hidden still gets cleared.
    private List<TreeViewItem> Containers(bool visibleOnly)
    {
        var list = new List<TreeViewItem>();
        Collect(this, list, visibleOnly);
        return list;
    }

    private static void Collect(IUIComponent node, List<TreeViewItem> acc, bool visibleOnly)
    {
        foreach (var child in node.VisualChildren)
        {
            if (child is TreeViewItem tvi)
            {
                acc.Add(tvi);
                if (!visibleOnly || tvi.IsExpanded) Collect(tvi, acc, visibleOnly);
            }
            else
            {
                Collect(child, acc, visibleOnly);
            }
        }
    }
}
