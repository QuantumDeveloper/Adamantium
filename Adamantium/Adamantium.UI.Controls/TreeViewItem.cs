using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

/// <summary>
/// A realized row of a <see cref="TreeView"/>: the container for one visible node of the FLATTENED tree. It draws the
/// node's <see cref="Header"/> (via the tree's HierarchicalDataTemplate), indented by <see cref="Indent"/> for its depth,
/// with an expander when the node has children. It hosts NO child nodes of its own - the tree is flattened, so a node's
/// children are sibling rows in the same virtualized list, not nested inside this control. Clicking the header selects the
/// node; the expander (or a double-click) toggles the node's branch in the flat list. The expander glyph is
/// <see cref="ExpanderTemplate"/> - swap it to restyle the arrow without touching the item template.
/// </summary>
public class TreeViewItem : ItemsControl, IHeaderedItemsControl, ISpringLoadable
{
    // The step (px) each depth level insets the row - Indent = Depth * this.
    private const double IndentStep = 16;

    /// <summary>Spring-load: a drag has dwelled over this node, so expand it to reveal drop targets deeper in the tree.
    /// The tree is flattened, so expansion must route through the owner (which splices the child rows into the flat list) -
    /// setting IsExpanded alone only flips the glyph. Collapsed branches only; a leaf or an already-open node does nothing.</summary>
    public void SpringLoad()
    {
        if (Row is { HasChildren: true, IsExpanded: false }) 
            FindOwnerTreeView()?.ToggleRow(this);
    }

    public static readonly AdamantiumProperty HeaderProperty = AdamantiumProperty.Register(nameof(Header),
        typeof(object), typeof(TreeViewItem), new PropertyMetadata(null));

    public static readonly AdamantiumProperty HeaderTemplateProperty = AdamantiumProperty.Register(nameof(HeaderTemplate),
        typeof(DataTemplate), typeof(TreeViewItem), new PropertyMetadata(null));

    /// <summary>The expander glyph's own template (applied to the PART_Expander toggle), so the arrow can be restyled in
    /// one place. Its checked state = <see cref="IsExpanded"/>, so a trigger inside it can rotate/swap the arrow.</summary>
    public static readonly AdamantiumProperty ExpanderTemplateProperty = AdamantiumProperty.Register(nameof(ExpanderTemplate),
        typeof(ControlTemplate), typeof(TreeViewItem), new PropertyMetadata(null));

    public static readonly AdamantiumProperty IsExpandedProperty = AdamantiumProperty.Register(nameof(IsExpanded),
        typeof(bool), typeof(TreeViewItem), new PropertyMetadata(false, OnIsExpandedChanged));

    public static readonly AdamantiumProperty IsSelectedProperty = AdamantiumProperty.Register(nameof(IsSelected),
        typeof(bool), typeof(TreeViewItem), new PropertyMetadata(false, OnIsSelectedChanged));

    /// <summary>The row's indent (px) for its depth: Depth × step. The template insets the header by this, so a deep node
    /// sits further right - the tree's shape, now expressed by indent instead of nesting.</summary>
    public static readonly AdamantiumProperty IndentProperty = AdamantiumProperty.Register(nameof(Indent),
        typeof(double), typeof(TreeViewItem), new PropertyMetadata(0.0,
            PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));

    // Read-only: true when the node has children (a branch, not a leaf). Drives the expander's visibility. Set by the
    // owner when the container is bound to its row (the flattened design resolves this from the data, not nested Items).
    public static readonly AdamantiumProperty HasItemsProperty = AdamantiumProperty.Register(nameof(HasItems),
        typeof(bool), typeof(TreeViewItem), new PropertyMetadata(false));

    // Read-only: true while the pointer is over THIS node's header row (not a descendant node). IsMouseOver can't be used
    // for the hover highlight - it's true for the whole subtree, so hovering a deep node would light up its ancestors too.
    public static readonly AdamantiumProperty IsPointerOverHeaderProperty = AdamantiumProperty.Register(nameof(IsPointerOverHeader),
        typeof(bool), typeof(TreeViewItem), new PropertyMetadata(false));

    /// <summary>Bubbles when this node becomes selected; the owning <see cref="TreeView"/> listens to move the selection.</summary>
    public static readonly RoutedEvent SelectedEvent = EventManager.RegisterRoutedEvent(nameof(Selected),
        RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TreeViewItem));

    private ToggleButton _expander;

    static TreeViewItem()
    {
        // A tree node is a keyboard-focus target (arrow-key navigation) - opt in, since the base default is false.
        FocusableProperty.OverrideMetadata(typeof(TreeViewItem), new PropertyMetadata(true));
    }

    /// <summary>The flat row this container currently shows (its depth / expand / selection model). Null before binding.</summary>
    internal TreeRow Row { get; private set; }

    /// <summary>The node's label.</summary>
    public object Header { get => GetValue<object>(HeaderProperty); set => SetValue(HeaderProperty, value); }

    /// <summary>Template that renders <see cref="Header"/> (set to the HierarchicalDataTemplate for a data-driven tree).</summary>
    public DataTemplate HeaderTemplate { get => GetValue<DataTemplate>(HeaderTemplateProperty); set => SetValue(HeaderTemplateProperty, value); }

    /// <summary>The expander arrow's template - restyle the glyph here without rewriting the node template.</summary>
    public ControlTemplate ExpanderTemplate { get => GetValue<ControlTemplate>(ExpanderTemplateProperty); set => SetValue(ExpanderTemplateProperty, value); }

    /// <summary>Whether this branch is open (its children are shown as following rows in the flat list).</summary>
    public bool IsExpanded { get => GetValue<bool>(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    /// <summary>Whether this node is one of the tree's selected ones.</summary>
    public bool IsSelected { get => GetValue<bool>(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    /// <summary>The row's depth indent (px). Read-only; set by the owner from the row's depth.</summary>
    public double Indent { get => GetValue<double>(IndentProperty); private set => SetValue(IndentProperty, value); }

    /// <summary>True when the node has children (a branch). Read-only.</summary>
    public bool HasItems { get => GetValue<bool>(HasItemsProperty); private set => SetValue(HasItemsProperty, value); }

    // Re-read HasItems from the row after its HasChildren was re-evaluated live (a leaf that just gained its first child):
    // the expander glyph appears without recycling the container. Called by the owner from SyncRowExpansion.
    internal void SyncHasItems()
    {
        if (Row is { } row) HasItems = row.HasChildren;
    }

    /// <summary>True while the pointer is over this node's OWN header row (not a descendant). Read-only; drives the hover highlight.</summary>
    public bool IsPointerOverHeader { get => GetValue<bool>(IsPointerOverHeaderProperty); private set => SetValue(IsPointerOverHeaderProperty, value); }

    public event RoutedEventHandler Selected { add => AddHandler(SelectedEvent, value); remove => RemoveHandler(SelectedEvent, value); }

    // Container seam (mirrors MenuItem): a data-driven tree generates TreeViewItem containers via the HierarchicalDataTemplate;
    // a flat ItemTemplate keeps the base ContentPresenter.
    protected internal override IUIComponent GetContainerForItem(object item)
        => ItemTemplate is HierarchicalDataTemplate ? CreateContainer(ItemContainerStyle) : base.GetContainerForItem(item);

    /// <summary>Creates a TreeViewItem container carrying the owner's ItemContainerStyle (into Styles, applied AFTER the theme).</summary>
    internal static TreeViewItem CreateContainer(Style itemContainerStyle)
    {
        var container = new TreeViewItem();
        if (itemContainerStyle != null)
        {
            container.Styles.Add(itemContainerStyle);
        }

        return container;
    }

    /// <summary>Binds this container to a flat <paramref name="row"/>: draw its node via <paramref name="headerTemplate"/>,
    /// indent by depth, mirror the row's expand/selection state. The node is the DataContext so the header's {Binding}s and
    /// the ItemContainerStyle's IsExpanded two-way binding resolve against it. Setting IsExpanded here only syncs the glyph
    /// (the flattener already holds the row's state) - it does NOT re-drive expansion, which routes through the owner.</summary>
    internal void BindRow(TreeRow row, DataTemplate headerTemplate)
    {
        // Whatever the pointer was over, it was over the row this container USED to show. Hover is state about the
        // pointer and THIS row, and a recycled container knows nothing about it any more: the mouse has not moved, so
        // no Leave will ever arrive to put it out. Left standing, the rows a fast keyboard scroll recycles through
        // carry the highlight away with them and it never clears. If the pointer really is over this row, the next
        // mouse move lights it again.
        IsPointerOverHeader = false;

        Row = row;
        DataContext = row.Node;
        Header = row.Node;
        HeaderTemplate = headerTemplate;
        Indent = row.Depth * IndentStep;
        HasItems = row.HasChildren;
        IsExpanded = row.IsExpanded;
        IsSelected = row.IsSelected;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        DetachParts();   // a template swap re-runs this; drop the old wiring first

        // The expander is a ToggleButton whose CHECKED state mirrors IsExpanded (so ExpanderTemplate can rotate the arrow off
        // its own IsChecked trigger). Its click routes to the owner, which splices/removes the branch in the flat list.
        _expander = GetTemplateChild("PART_Expander") as ToggleButton;
        if (_expander != null)
        {
            _expander.IsChecked = IsExpanded;
            _expander.Click += OnExpanderClick;
        }
        // Track hover on the HEADER row only. Root's mouse enter/leave is exactly "over this node's own row" - unlike
        // IsMouseOver, which would stay true over descendants (though the flat design has none nested, this stays correct).
        // NAMED handlers kept in fields, not lambdas: RemoveHandler matches on the delegate, so a lambda - or a freshly
        // made one - could never be taken off again.
        _rowRoot = GetTemplateChild("Root") as IInputComponent;
        if (_rowRoot != null)
        {
            _enterHeader ??= (_, _) => IsPointerOverHeader = true;
            _leaveHeader ??= (_, _) => IsPointerOverHeader = false;
            _rowRoot.AddHandler(Mouse.MouseEnterEvent, _enterHeader, true);
            _rowRoot.AddHandler(Mouse.MouseLeaveEvent, _leaveHeader, true);
        }
    }

    /// <summary>Let the template's parts go when the template does - see ScrollBar.OnRemoveTemplate.</summary>
    public override void OnRemoveTemplate()
    {
        base.OnRemoveTemplate();
        DetachParts();
    }

    private void DetachParts()
    {
        if (_expander != null) _expander.Click -= OnExpanderClick;
        if (_rowRoot != null)
        {
            if (_enterHeader != null) _rowRoot.RemoveHandler(Mouse.MouseEnterEvent, _enterHeader);
            if (_leaveHeader != null) _rowRoot.RemoveHandler(Mouse.MouseLeaveEvent, _leaveHeader);
        }
        _expander = null;
        _rowRoot = null;
    }

    private void OnExpanderClick(object sender, RoutedEventArgs e) => FindOwnerTreeView()?.ToggleRow(this);

    private IInputComponent _rowRoot;
    private MouseEventHandler _enterHeader;
    private MouseEventHandler _leaveHeader;

    protected override void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(sender, e);
        // Select only the node whose OWN header row is under the pointer. e.Handled CAN'T gate this: MouseDownEvent bubbles,
        // and its class handler re-raises a FRESH MouseLeftButtonDown (Handled=false) for every ancestor - so a child's
        // Handled is invisible to them. IsPointerOverHeader is true only for the row directly under the pointer. WPF-canonical.
        if (!IsPointerOverHeader)
        {
            return;
        }

        e.Handled = true;
        // Double-click a branch toggles its expansion (the first click already selected on ClickCount==1). A leaf does
        // nothing. The owner TreeView's ExpandOnDoubleClick (true by default) gates it, and it owns the flat-list splice.
        if (e.ClickCount >= 2)
        {
            if (HasItems && (FindOwnerTreeView()?.ExpandOnDoubleClick ?? true))
            {
                FindOwnerTreeView()?.ToggleRow(this);
            }

            return;
        }
        // Route the click (with its Ctrl/Shift modifiers) to the owner TreeView - the Single/Multiple/Extended policy lives
        // there, since it must reach across rows to clear or range-select the others.
        FindOwnerTreeView()?.OnItemClicked(this, e.Modifiers);
    }

    // Focus arriving by any route (a click, a Tab into the tree, the arrow keys) tells the tree WHICH ROW now owns the
    // keyboard place - the tree keeps that as a row, not as this container, and re-delivers it when containers recycle.
    protected override void OnGotFocus(RoutedEventArgs e)
    {
        base.OnGotFocus(e);
        if (IsFocused && Row is { } row)
        {
            FindOwnerTreeView()?.NoteFocusedRow(row);
        }
    }

    // The TreeView hosting this node; its SelectionMode decides the click policy and it owns the flat-list expansion.
    private TreeView FindOwnerTreeView()
    {
        for (IUIComponent c = VisualParent; c != null; c = c.VisualParent)
        {
            if (c is TreeView tv)
            {
                return tv;
            }
        }

        return null;
    }

    private static void OnIsExpandedChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not TreeViewItem item)
        {
            return;
        }

        var expanded = (bool)e.NewValue;
        if (item._expander != null)
        {
            item._expander.IsChecked = expanded;
        }

        // Route EVERY IsExpanded change into the flattener - a VM two-way binding, code, or spring-load, not just the
        // expander click - so the child rows actually splice/unsplice. The owner no-ops when the row is already in that
        // state, so this stays idempotent with ToggleRow and with container recycling (BindRow sets it to the row's state).
        item.FindOwnerTreeView()?.SyncRowExpansion(item, expanded);
    }

    private static void OnIsSelectedChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not TreeViewItem item)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            item.RaiseEvent(new RoutedEventArgs(SelectedEvent, item));
        }
    }
}
