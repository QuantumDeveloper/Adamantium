using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

/// <summary>
/// A node in a <see cref="TreeView"/>: a <see cref="Header"/> plus, when it has children, those child nodes shown indented
/// below it while <see cref="IsExpanded"/>. Data-driven via <c>ItemsSource</c> + a <see cref="HierarchicalDataTemplate"/>
/// (the same container seam as MenuItem), so a tree of any depth unrolls from the view-model. Clicking the header selects
/// the node; clicking the expander toggles its children. The expander glyph is <see cref="ExpanderTemplate"/> - swap it to
/// restyle the arrow without touching the item template.
/// </summary>
public class TreeViewItem : ItemsControl, IHeaderedItemsControl
{
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

    // Read-only: true once the node has children (a branch, not a leaf). Drives the expander's visibility.
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

    public TreeViewItem()
    {
        Items.CollectionChanged += (_, _) => HasItems = Items.Count > 0;
    }

    /// <summary>The node's label.</summary>
    public object Header { get => GetValue<object>(HeaderProperty); set => SetValue(HeaderProperty, value); }

    /// <summary>Template that renders <see cref="Header"/> (set to the HierarchicalDataTemplate for a data-driven tree).</summary>
    public DataTemplate HeaderTemplate { get => GetValue<DataTemplate>(HeaderTemplateProperty); set => SetValue(HeaderTemplateProperty, value); }

    /// <summary>The expander arrow's template - restyle the glyph here without rewriting the node template.</summary>
    public ControlTemplate ExpanderTemplate { get => GetValue<ControlTemplate>(ExpanderTemplateProperty); set => SetValue(ExpanderTemplateProperty, value); }

    /// <summary>Whether this branch's children are shown.</summary>
    public bool IsExpanded { get => GetValue<bool>(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    /// <summary>Whether this node is the tree's selected one.</summary>
    public bool IsSelected { get => GetValue<bool>(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    /// <summary>True when the node has children (a branch). Read-only.</summary>
    public bool HasItems { get => GetValue<bool>(HasItemsProperty); private set => SetValue(HasItemsProperty, value); }

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
        if (itemContainerStyle != null) container.Styles.Add(itemContainerStyle);
        return container;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();   // connects PART_ItemsPresenter (the indented child host)
        // The expander is a ToggleButton whose CHECKED state mirrors IsExpanded (so ExpanderTemplate can rotate the arrow off
        // its own IsChecked trigger). Sync both ways in code rather than relying on a two-way template binding.
        _expander = GetTemplateChild("PART_Expander") as ToggleButton;
        if (_expander != null)
        {
            _expander.IsChecked = IsExpanded;
            _expander.Click += (_, _) => IsExpanded = _expander.IsChecked == true;
        }
        // Track hover on the HEADER row only. The children live in a sibling ItemsPresenter (outside Root), so Root's
        // mouse enter/leave is exactly "over this node's own row" - unlike IsMouseOver, which stays true over descendants.
        if (GetTemplateChild("Root") is IInputComponent root)
        {
            root.AddHandler(Mouse.MouseEnterEvent, new MouseEventHandler((_, _) => IsPointerOverHeader = true), true);
            root.AddHandler(Mouse.MouseLeaveEvent, new MouseEventHandler((_, _) => IsPointerOverHeader = false), true);
        }
    }

    protected override void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(sender, e);
        // Select only the node whose OWN header row is under the pointer. e.Handled CAN'T gate this: MouseDownEvent bubbles,
        // and its class handler re-raises a FRESH MouseLeftButtonDown (Handled=false) for every ancestor - so a child's
        // Handled is invisible to them, and each ancestor would select, leaving only the topmost (root) selected after
        // Single-mode's deselect. IsPointerOverHeader is true only for the row directly under the pointer (a descendant's
        // children live in a sibling presenter, so an ancestor's header is NOT under the pointer). The WPF-canonical guard.
        if (!IsPointerOverHeader) return;
        e.Handled = true;
        // Double-click a branch toggles its expansion (the first click already selected on ClickCount==1). A leaf does
        // nothing. The owner TreeView's ExpandOnDoubleClick (true by default) gates it.
        if (e.ClickCount >= 2)
        {
            if (HasItems && (FindOwnerTreeView()?.ExpandOnDoubleClick ?? true)) IsExpanded = !IsExpanded;
            return;
        }
        // Route the click (with its Ctrl/Shift modifiers) to the owner TreeView - the Single/Multiple/Extended policy lives
        // there, since it must reach across nodes to clear or range-select the others.
        FindOwnerTreeView()?.OnItemClicked(this, e.Modifiers);
    }

    // The TreeView hosting this node (shared by every node at any depth); its SelectionMode decides the click policy.
    private TreeView FindOwnerTreeView()
    {
        for (IUIComponent c = VisualParent; c != null; c = c.VisualParent)
            if (c is TreeView tv) return tv;
        return null;
    }

    private static void OnIsExpandedChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is TreeViewItem item && item._expander != null) item._expander.IsChecked = (bool)e.NewValue;
    }

    private static void OnIsSelectedChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not TreeViewItem item) return;
        if ((bool)e.NewValue) item.RaiseEvent(new RoutedEventArgs(SelectedEvent, item));
    }
}
