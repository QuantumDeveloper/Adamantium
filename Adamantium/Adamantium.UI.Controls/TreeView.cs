using System;
using System.Collections;
using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

/// <summary>
/// A hierarchical list, FLATTENED for virtualization: the tree (roots + a <see cref="HierarchicalDataTemplate"/> that
/// draws each node and points at its children) is projected into a single flat list of rows - one per visible node,
/// indented by depth - which a virtualizing panel realizes a viewport at a time. So a branch of thousands of siblings
/// (an editor entity tree, a drive's System32) costs thousands of cheap row wrappers, not thousands of nested
/// containers, and only the on-screen rows become <see cref="TreeViewItem"/> controls. Expanding a node splices its
/// children into the flat list in place (children resolve by reflecting the template's ItemsSource path, no container
/// needed); clicking a row selects it, with the <see cref="SelectionMode"/> deciding one / many / list-box (Ctrl/Shift).
/// </summary>
public class TreeView : ItemsControl
{
    static TreeView()
    {
        // The same standing a list box has, and for the same reasons: the tree takes the keyboard (its arrows walk the
        // rows), but for Tab it is a DOORWAY, not a stop - entered once, onto a row, and the next Tab leaves the whole
        // tree rather than walking every node in it.
        FocusableProperty.OverrideMetadata(typeof(TreeView), new PropertyMetadata(true));
        KeyboardNavigation.IsTabStopProperty.OverrideMetadata(typeof(TreeView), new PropertyMetadata(false));
        KeyboardNavigation.TabNavigationProperty.OverrideMetadata(typeof(TreeView),
            new PropertyMetadata(KeyboardNavigationMode.Once));
    }

    // Read-only: the DATA item behind the most-recently selected node (null = nothing selected).
    public static readonly AdamantiumProperty SelectedItemProperty = AdamantiumProperty.Register(nameof(SelectedItem),
        typeof(object), typeof(TreeView), new PropertyMetadata(null));

    /// <summary>Single (one node - the default), Multiple (each click toggles), or Extended (Ctrl/Shift like a list box).</summary>
    public static readonly AdamantiumProperty SelectionModeProperty = AdamantiumProperty.Register(nameof(SelectionMode),
        typeof(TreeViewSelectionMode), typeof(TreeView), new PropertyMetadata(TreeViewSelectionMode.Single));

    /// <summary>Whether double-clicking a node that has children toggles its expansion (true by default). A leaf does nothing.</summary>
    public static readonly AdamantiumProperty ExpandOnDoubleClickProperty = AdamantiumProperty.Register(nameof(ExpandOnDoubleClick),
        typeof(bool), typeof(TreeView), new PropertyMetadata(true));

    /// <summary>The vertical scroll offset (px), two-way. Bind it to the view-model to PERSIST the scroll position across a
    /// view rebuild (a tab switch recreates this view) - or to DRIVE the scroll from the VM. Reflects live scrolling; setting
    /// it scrolls the tree (restored once the content's extent is known).</summary>
    public static readonly AdamantiumProperty VerticalOffsetProperty = AdamantiumProperty.Register(nameof(VerticalOffset),
        typeof(double), typeof(TreeView), new PropertyMetadata(0.0, OnVerticalOffsetChanged));

    // The tree projected flat. Rebuilt when the roots (ItemsSource) or the template (child-resolution path) change; the
    // base Items is pointed at its Rows, so the whole ItemsControl virtualization pipeline drives the flat list unchanged.
    private TreeFlattener _flattener;
    private IEnumerable _roots;
    // Selection lives on the ROWS (survives virtualization: a selected row scrolled out + back stays selected); realized
    // containers just mirror their row. The set is the currently-selected rows; the anchor is the Shift-range fixed end.
    private readonly HashSet<TreeRow> _selectedRows = [];
    private TreeRow _anchorRow;
    // The row that owns the KEYBOARD place - the same reason selection lives on rows, and it survives virtualization the
    // same way. See PrepareContainer: a focus pinned to a container drifts when the panel recycles that container.
    private TreeRow _focusedRow;
    // Writes a node's selection onto the node itself (from the ItemContainerStyle's IsSelected binding path), so a selection
    // persists across a view rebuild and reaches OFF-SCREEN rows that have no container. No-op if the style doesn't bind it.
    private Action<object, bool> _setNodeSelected = static (_, _) => { };

    // Writes a node's OWN IsExpanded (the ItemContainerStyle's IsExpanded binding path), so SyncRowExpansion can trigger a
    // lazy branch's load synchronously BEFORE the flattener reads its children - no reliance on binding-vs-callback order.
    private Action<object, bool> _setNodeExpanded = static (_, _) => { };

    // Two-way scroll-offset plumbing: _scrollViewer is the template's ScrollViewer; _applyingOffset guards the property
    // <-> scrollbar echo from looping; a set offset that can't land yet (extent not measured) is kept _desired and retried
    // as the metrics settle.
    private ScrollViewer _scrollViewer;
    private bool _applyingOffset;
    private bool _hasDesiredOffset;
    private double _desiredOffset;

    /// <summary>The data item of the most-recently selected node. Read-only - set selection by clicking a node.</summary>
    public object SelectedItem { get => GetValue<object>(SelectedItemProperty); private set => SetValue(SelectedItemProperty, value); }

    /// <summary>How many nodes may be selected and how clicks combine (Single, Multiple, Extended).</summary>
    public TreeViewSelectionMode SelectionMode { get => GetValue<TreeViewSelectionMode>(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }

    /// <summary>Whether a double-click on a branch toggles its expansion (default true).</summary>
    public bool ExpandOnDoubleClick { get => GetValue<bool>(ExpandOnDoubleClickProperty); set => SetValue(ExpandOnDoubleClickProperty, value); }

    /// <summary>The vertical scroll offset (px). Two-way - bind it to the view-model to persist / drive the scroll position.</summary>
    public double VerticalOffset { get => GetValue<double>(VerticalOffsetProperty); set => SetValue(VerticalOffsetProperty, value); }

    // The roots changed: keep them and (re)build the flat projection. Items ends up pointed at the flattener's Rows, NOT
    // the raw roots, so the generator/panel realize flat rows.
    protected override void ApplyItemsSource(IEnumerable newValue)
    {
        _roots = newValue;
        RebuildFlattener();
    }

    // The template carries the child-resolution path (HierarchicalDataTemplate.ItemsSource, e.g. {Binding Children}); a
    // new template can point elsewhere, so re-derive the resolver and re-flatten.
    protected override void OnItemTemplateChangedCore() => RebuildFlattener();

    private void RebuildFlattener()
    {
        _flattener?.Clear();
        // Child path from the HierarchicalDataTemplate's ItemsSource binding (e.g. {Binding Children} -> "Children").
        var childPath = (ItemTemplate as HierarchicalDataTemplate)?.ItemsSource is Binding binding ? binding.Path?.Path : null;
        // Expansion + selection paths from the ItemContainerStyle's IsExpanded / IsSelected setter bindings. Letting a
        // rebuild RESTORE the branches/rows the view-model still marks (a tab switch recreates this view, but the VM - and
        // its expanded + selected state - persists). Read from the style's own setters, so there's no second place to declare them.
        var expandPath = MemberPath(nameof(TreeViewItem.IsExpanded));
        var selectPath = MemberPath(nameof(TreeViewItem.IsSelected));
        _flattener = new TreeFlattener(TreeChildResolver.ForPath(childPath),
            TreeChildResolver.ForBoolPath(expandPath),
            TreeChildResolver.ForBoolPath(selectPath));
        _setNodeExpanded = TreeChildResolver.SetterForBoolPath(expandPath);
        _setNodeSelected = TreeChildResolver.SetterForBoolPath(selectPath);
        _flattener.SetRoots(_roots);
        Items.SetSource(_flattener.Rows);   // the flat rows are now the effective item list -> virtualized directly
        RestoreSelectionFromRows();
        _anchorRow = null;
    }

    // The node property the ItemContainerStyle binds <paramref name="property"/> to (a persisted state member), or null if
    // the style doesn't bind it. Read from the style's own setter so there's no second place to declare the same path.
    private string MemberPath(string property)
    {
        if (ItemContainerStyle?.Setters is not { } setters)
        {
            return null;
        }

        foreach (var setter in setters)
        {
            if (setter is Setter s && s.Property == property && s.Value is Binding b)
            {
                return b.Path?.Path;
            }
        }

        return null;
    }

    // After a (re)build, rebuild the selected-row set from the rows the flattener restored as selected, and pick a primary.
    private void RestoreSelectionFromRows()
    {
        _selectedRows.Clear();
        object primary = null;
        foreach (var row in _flattener.Rows)
        {
            if (row.IsSelected)
            {
                _selectedRows.Add(row);
                primary ??= row.Node;
            }
        }

        SelectedItem = primary;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (_scrollViewer != null)
        {
            _scrollViewer.ScrollChanged -= OnScrollChanged;
        }

        _scrollViewer = GetTemplateChild("PART_ScrollViewer") as ScrollViewer;
        if (_scrollViewer != null)
        {
            _scrollViewer.ScrollChanged += OnScrollChanged;
        }

        TryApplyDesiredOffset();   // a VerticalOffset bound before the template applied gets applied now (retried as the extent settles)
    }

    /// <summary>Let the template's parts go when the template does - see ScrollBar.OnRemoveTemplate.</summary>
    public override void OnRemoveTemplate()
    {
        base.OnRemoveTemplate();
        if (_scrollViewer != null) _scrollViewer.ScrollChanged -= OnScrollChanged;
        _scrollViewer = null;
    }

    // The scroll metrics moved: either publish the live offset to the bindable property (user scroll -> VM) or, while a
    // set offset is still pending (the extent wasn't tall enough yet), retry applying it now that the metrics grew.
    private void OnScrollChanged(object sender, EventArgs e)
    {
        if (_scrollViewer == null || _applyingOffset)
        {
            return;
        }

        if (_hasDesiredOffset)
        {
            TryApplyDesiredOffset();
            return;
        }

        _applyingOffset = true;
        VerticalOffset = _scrollViewer.ScrollOffset.Y;
        _applyingOffset = false;
    }

    // Apply the desired (VM-set) offset to the ScrollViewer; stop once it lands (or clamps at the bottom because the
    // content isn't that tall). Before the extent is measured the set clamps to 0, so we keep it pending and OnScrollChanged
    // retries as the metrics settle.
    private void TryApplyDesiredOffset()
    {
        if (_scrollViewer == null || !_hasDesiredOffset)
        {
            return;
        }

        _applyingOffset = true;
        _scrollViewer.SetScrollOffset(new Vector2(0, _desiredOffset));
        var y = _scrollViewer.ScrollOffset.Y;
        _applyingOffset = false;
        // Give up ONLY when we actually reached the target, or when the content is measured (maxY > 0) yet genuinely too
        // short to reach it (clamped at the real bottom). Before the extent is measured maxY is 0 and the offset clamps to
        // 0 - that is NOT "reached", so keep the request pending and let the next metrics change (the list gaining height)
        // retry it. Without the maxY>0 guard, `0 >= maxY-1` was true at template time and abandoned every restore.
        var maxY = Math.Max(0, _scrollViewer.ExtentSize.Height - _scrollViewer.ViewportSize.Height);
        if (Math.Abs(y - _desiredOffset) < 1 || (maxY > 0 && y >= maxY - 1))
        {
            _hasDesiredOffset = false;
        }
    }

    private static void OnVerticalOffsetChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        var tree = (TreeView)a;
        if (tree._applyingOffset)
        {
            return;   // our own echo of a live scroll, not an external (VM) set
        }

        tree._desiredOffset = (double)e.NewValue;
        tree._hasDesiredOffset = true;
        tree.TryApplyDesiredOffset();
    }

    // A data-driven tree generates TreeViewItem containers (as flat, indented rows); a flat ItemTemplate keeps the base
    // ContentPresenter.
    protected internal override IUIComponent GetContainerForItem(object item)
        => ItemTemplate is HierarchicalDataTemplate ? TreeViewItem.CreateContainer(ItemContainerStyle) : base.GetContainerForItem(item);

    // Bind a (new or recycled) container to a flat row: draw the node via the template header, indent by depth, mirror
    // the row's expand/selection state. The node is the DataContext so the header's {Binding}s and the ItemContainerStyle's
    // IsExpanded two-way binding resolve against it.
    protected internal override void PrepareContainer(IUIComponent container, object item)
    {
        if (container is TreeViewItem node && item is TreeRow row)
        {
            node.BindRow(row, ItemTemplate);

            // The focus rides the ROW, exactly as the selection does. A container is not a place in the tree: the panel
            // recycles containers onto other rows as the view scrolls, so a focus left pinned to one drifts onto
            // whatever row it lands on - the focus ring a row or two below the one the arrow key actually went to.
            if (ReferenceEquals(row, _focusedRow))
            {
                if (IsKeyboardFocusWithin && !node.IsFocused)
                {
                    FocusManager.Focus(node, NavigationMethod.Directional);
                }
            }
            else if (node.IsFocused)
            {
                // ...and the other half, for the same reason BindRow drops the hover: this container WAS the keyboard's
                // visual and now shows somebody else's row. Left holding the focus it re-draws the ring on each row the
                // scroll recycles it through. The place itself is unharmed - it is the row, which simply has no visual
                // while it is off-screen - so the tree holds the focus meanwhile (its keys keep working) and hands it
                // back the moment that row is bound again. Unspecified, not Directional: a mouse scroll must not paint
                // a ring around the whole tree.
                FocusManager.Focus(this, NavigationMethod.Unspecified);
            }
        }
        else
        {
            base.PrepareContainer(container, item);
        }
    }

    // Expand/collapse a row (from its expander or a double-click). Just flip the container's IsExpanded: the change routes
    // through OnIsExpandedChanged -> SyncRowExpansion, the SINGLE splice point that also serves VM-driven expansion.
    internal void ToggleRow(TreeViewItem container)
    {
        if (container.Row is { HasChildren: true } row)
        {
            container.IsExpanded = !row.IsExpanded;
        }
    }

    // The single point where a container's IsExpanded change drives the flat list: its expander, a VM two-way binding, code,
    // or spring-load all land here (via OnIsExpandedChanged). Sync the node's OWN IsExpanded FIRST so a lazy branch reads its
    // contents BEFORE the flattener splices them - no placeholder flash - then splice/remove the subtree as one range edit.
    // No-op when the row is already in that state, so it's safe on every change: ToggleRow, restore-on-rebuild, recycling.
    internal void SyncRowExpansion(TreeViewItem container, bool expanded)
    {
        if (container.Row is not { } row) return;

        SetRowExpanded(row, expanded);
        if (expanded) container.SyncHasItems();   // a former leaf may have just become a branch -> reveal its expander
    }

    // The same splice, addressed by ROW rather than by container - what the keyboard has in hand, since it walks the flat
    // list and the row it wants to open may have no realized container at all.
    private void SetRowExpanded(TreeRow row, bool expanded)
    {
        if (_flattener == null || row.IsExpanded == expanded)
        {
            return;
        }

        if (expanded)
        {
            _setNodeExpanded(row.Node, true);   // trigger the lazy load before the flattener reads the children
            _flattener.Expand(row);
        }
        else
        {
            _flattener.Collapse(row);
            _setNodeExpanded(row.Node, false);
        }
    }

    /// <summary>The tree owns its arrows, the way a list owns its own: they move the SELECTION down and up the flat list
    /// of visible rows, and Left/Right fold a branch shut or open it. A tree that let navigation have these keys would
    /// send the focus off to whatever sits beside it on the first press.
    /// <para>Left on a leaf (or an already-closed branch) goes to its PARENT, and Right on an open branch steps into its
    /// first child - so one key both folds and climbs, which is how every tree is driven.</para></summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || Items.Count == 0) return;

        var index = CurrentRowIndex();
        var row = index >= 0 ? Items[index] as TreeRow : null;

        switch (e.Key)
        {
            case Key.DownArrow:
                GoTo(index + 1);   // nothing selected yet (-1) -> the first row
                break;

            case Key.UpArrow:
                GoTo(index - 1);
                break;

            case Key.Home:
                GoTo(0);
                break;

            case Key.End:
                GoTo(Items.Count - 1);
                break;

            case Key.RightArrow:
                if (row is { HasChildren: true, IsExpanded: false }) Expand(index, row, true);
                else GoTo(index + 1);
                break;

            case Key.LeftArrow:
                if (row is { IsExpanded: true }) Expand(index, row, false);
                else if (row != null) GoTo(ParentIndex(index, row.Depth));
                break;

            default:
                return;   // not the tree's key - leave it for whoever wants it
        }

        // Claimed whether or not it led anywhere: running out of rows must not hand the focus to a neighbouring control,
        // and one arrow too many should not cost you your place in the tree.
        e.Handled = true;
    }

    /// <summary>Open or fold a row THROUGH ITS CONTAINER where there is one. The expander glyph is drawn from the
    /// container's <see cref="TreeViewItem.IsExpanded"/>, so a keyboard that spliced the flat list directly left the
    /// branch open with an arrow still pointing sideways. Going through the container drives the one path everything
    /// else uses (the expander, a view-model binding, a double-click). A row nobody has realized has no glyph to keep
    /// honest, so there the splice IS the whole job.</summary>
    private void Expand(int index, TreeRow row, bool expanded)
    {
        if (ItemContainerGenerator.ContainerFromIndex(index) is TreeViewItem container)
        {
            container.IsExpanded = expanded;
            return;
        }

        SetRowExpanded(row, expanded);
    }

    // A container took the focus (a click, a Tab in): its row is the keyboard place from now on.
    internal void NoteFocusedRow(TreeRow row) => _focusedRow = row;

    // Where the keyboard is: the row that owns the place, else the focused container's row, else the selection.
    private int CurrentRowIndex()
    {
        if (_focusedRow != null && Items.IndexOf(_focusedRow) is var known && known >= 0)
            return known;

        if (FocusManager.Focused is TreeViewItem { Row: { } focused } && Items.IndexOf(focused) is var index && index >= 0)
            return index;

        for (var i = 0; i < Items.Count; i++)
        {
            if (Items[i] is TreeRow row && ReferenceEquals(row.Node, SelectedItem)) return i;
        }

        return -1;
    }

    // The row this one hangs under: the nearest row ABOVE it that stands one level shallower.
    private int ParentIndex(int index, int depth)
    {
        for (var i = index - 1; i >= 0; i--)
        {
            if (Items[i] is TreeRow row && row.Depth < depth) return i;
        }

        return -1;
    }

    // Select the row at this index and take the view + the focus to it. Out of range = nothing to go to.
    private void GoTo(int index)
    {
        if (index < 0 || index >= Items.Count || Items[index] is not TreeRow row) return;

        SelectOnly(row);
        SyncSelectionToContainers();
        ShowRow(index);
    }

    /// <summary>Put the focus on the row and bring it into view. A row the panel has not realized has no visual to focus
    /// - but the panel can still say where it WILL be, and scrolling there materialises it (the same two-step the list
    /// makes, and for the same reason: a selection scrolled far out of the window otherwise goes unreachable).</summary>
    private void ShowRow(int index)
    {
        // The ROW owns the keyboard place from here on, whether or not it has a visual yet: the scroll below can rebind
        // every container under us, and PrepareContainer hands the focus to whichever one ends up showing this row.
        _focusedRow = Items[index] as TreeRow;

        var container = ItemContainerGenerator.ContainerFromIndex(index);
        if (container is IInputComponent focusable && container.Visibility == Visibility.Visible)
        {
            FocusManager.Focus(focusable, NavigationMethod.Directional);
            (container as UIComponent)?.BringIntoView();
            return;
        }

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

    // A node was clicked: apply the selection policy over the flat ROWS (Single/Multiple/Extended), then mirror the new
    // selection onto the realized containers.
    internal void OnItemClicked(TreeViewItem container, InputModifiers modifiers)
    {
        if (container.Row is not { } row)
        {
            return;
        }

        switch (SelectionMode)
        {
            case TreeViewSelectionMode.Multiple:
                ToggleSelection(row);
                break;

            case TreeViewSelectionMode.Extended:
                var ctrl = (modifiers & (InputModifiers.LeftControl | InputModifiers.RightControl)) != 0;
                var shift = (modifiers & (InputModifiers.LeftShift | InputModifiers.RightShift)) != 0;
                if (shift && _anchorRow != null)
                {
                    SelectRange(_anchorRow, row);
                }
                else if (ctrl)
                {
                    ToggleSelection(row);
                    _anchorRow = row;
                }
                else
                {
                    SelectOnly(row);
                    _anchorRow = row;
                }
                break;

            default: // Single
                SelectOnly(row);
                break;
        }

        SyncSelectionToContainers();
    }

    // Exactly one row selected - clear every other, select this (Single, and Extended's plain click).
    private void SelectOnly(TreeRow row)
    {
        ClearSelection();
        Select(row);
        SelectedItem = row.Node;
    }

    // Flip one row, leaving the rest untouched (Multiple, and Extended's Ctrl+click).
    private void ToggleSelection(TreeRow row)
    {
        if (row.IsSelected)
        {
            row.IsSelected = false;
            _setNodeSelected(row.Node, false);
            _selectedRows.Remove(row);
            if (ReferenceEquals(SelectedItem, row.Node))
            {
                SelectedItem = null;
            }
        }
        else
        {
            Select(row);
            SelectedItem = row.Node;
        }
    }

    // Select the run between the anchor and the clicked row (inclusive) in flat (on-screen) order, clearing everything
    // else - the list-box Shift+click. The flat list IS the visible order, so the range is a contiguous slice of it.
    private void SelectRange(TreeRow anchor, TreeRow row)
    {
        var rows = _flattener.Rows;
        var a = rows.IndexOf(anchor);
        var b = rows.IndexOf(row);
        if (a < 0 || b < 0)
        {
            SelectOnly(row);
            return;
        }

        if (a > b)
        {
            (a, b) = (b, a);
        }

        ClearSelection();
        for (var i = a; i <= b; i++)
        {
            Select(rows[i]);
        }

        SelectedItem = row.Node;
    }

    // Mark a row selected - on the row (the container mirror) AND on its node (persisted, reaches off-screen rows).
    private void Select(TreeRow row)
    {
        row.IsSelected = true;
        _setNodeSelected(row.Node, true);
        _selectedRows.Add(row);
    }

    private void ClearSelection()
    {
        foreach (var r in _selectedRows)
        {
            r.IsSelected = false;
            _setNodeSelected(r.Node, false);
        }

        _selectedRows.Clear();
    }

    // Push each realized container's row selection onto the container (its IsSelected trigger paints the highlight).
    // Only the viewport's containers exist, so this is O(viewport); freshly realized/recycled rows are synced in BindRow.
    private void SyncSelectionToContainers()
    {
        if (ItemsHostPanel is not { } panel)
        {
            return;
        }

        foreach (var child in panel.VisualChildren)
        {
            if (child is not TreeViewItem tvi || tvi.Row is not { } row) continue;

            // _selectedRows is the ONE truth, and a row that disagrees with it is put right here. A row can be left
            // marked without us: the item container style may bind IsSelected two-way to the node, so a container being
            // recycled onto another row writes through that binding - and the row it marked is not in the set, which is
            // exactly what ClearSelection walks. The rows would then stay lit with nothing able to clear them (seen
            // while holding an arrow key: the list scrolls fast, containers recycle, and highlights pile up behind).
            tvi.IsSelected = row.IsSelected;
        }
    }
}
