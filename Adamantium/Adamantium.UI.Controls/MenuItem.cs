using System.Collections.Specialized;
using System.Linq;
using Adamantium.Core.Commands;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls.Primitives;

/// <summary>
/// A row in a <see cref="ContextMenu"/> (or a nested submenu). A LEAF item runs its <see cref="Command"/> / raises
/// <see cref="Click"/> when clicked; a PARENT item (one that has child items) instead opens a submenu flyout to the right
/// on hover. As an <see cref="ItemsControl"/> its children ARE the submenu, so it hosts either literal markup children
/// (<c>&lt;MenuItem Header="New"/&gt;</c>) or a data-driven tree via <see cref="ItemsControl.ItemsSource"/> +
/// a <see cref="HierarchicalDataTemplate"/>. The label is <see cref="Header"/>; <see cref="Icon"/> and
/// <see cref="InputGestureText"/> (a shortcut hint) are optional. A divider is a separate <see cref="Separator"/>, generated
/// for an <see cref="ISeparatorItem"/> node - never a MenuItem.
/// </summary>
public class MenuItem : ItemsControl, IHeaderedItemsControl
{
    public static readonly AdamantiumProperty HeaderProperty = AdamantiumProperty.Register(nameof(Header),
        typeof(object), typeof(MenuItem), new PropertyMetadata(null));

    public static readonly AdamantiumProperty HeaderTemplateProperty = AdamantiumProperty.Register(nameof(HeaderTemplate),
        typeof(DataTemplate), typeof(MenuItem), new PropertyMetadata(null));

    public static readonly AdamantiumProperty IconProperty = AdamantiumProperty.Register(nameof(Icon),
        typeof(object), typeof(MenuItem), new PropertyMetadata(null));

    public static readonly AdamantiumProperty InputGestureTextProperty = AdamantiumProperty.Register(nameof(InputGestureText),
        typeof(string), typeof(MenuItem), new PropertyMetadata(null));

    public static readonly AdamantiumProperty CommandProperty = AdamantiumProperty.Register(nameof(Command),
        typeof(ICommand), typeof(MenuItem), new PropertyMetadata(null));

    public static readonly AdamantiumProperty CommandParameterProperty = AdamantiumProperty.Register(nameof(CommandParameter),
        typeof(object), typeof(MenuItem), new PropertyMetadata(null));

    // Read-only: true once the item has children (so it's a submenu parent, not a leaf). Drives the chevron + the flyout.
    public static readonly AdamantiumProperty HasItemsProperty = AdamantiumProperty.Register(nameof(HasItems),
        typeof(bool), typeof(MenuItem), new PropertyMetadata(false));

    // Whether this parent item's submenu flyout is shown. The template binds a Popup.IsOpen to it.
    public static readonly AdamantiumProperty IsSubmenuOpenProperty = AdamantiumProperty.Register(nameof(IsSubmenuOpen),
        typeof(bool), typeof(MenuItem), new PropertyMetadata(false, OnIsSubmenuOpenChanged));

    public static readonly RoutedEvent ClickEvent = EventManager.RegisterRoutedEvent(nameof(Click),
        RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuItem));

    static MenuItem()
    {
        // A menu row is a keyboard-focus target (arrow-key navigation) - opt in, since the base default is false.
        FocusableProperty.OverrideMetadata(typeof(MenuItem), new PropertyMetadata(true));
    }

    public MenuItem()
    {
        Items.CollectionChanged += OnItemsCollectionChanged;
    }

    /// <summary>The row's label.</summary>
    public object Header { get => GetValue<object>(HeaderProperty); set => SetValue(HeaderProperty, value); }

    /// <summary>Template that renders <see cref="Header"/> (set to the HierarchicalDataTemplate for a data-driven menu).</summary>
    public DataTemplate HeaderTemplate { get => GetValue<DataTemplate>(HeaderTemplateProperty); set => SetValue(HeaderTemplateProperty, value); }

    /// <summary>Optional icon/glyph shown at the left of the row.</summary>
    public object Icon { get => GetValue<object>(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>Optional shortcut hint shown right-aligned (e.g. "Ctrl+S"). Display only - it wires nothing.</summary>
    public string InputGestureText { get => GetValue<string>(InputGestureTextProperty); set => SetValue(InputGestureTextProperty, value); }

    /// <summary>Command run when a LEAF item is clicked.</summary>
    public ICommand Command { get => GetValue<ICommand>(CommandProperty); set => SetValue(CommandProperty, value); }

    public object CommandParameter { get => GetValue<object>(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

    /// <summary>True when the item has children (a submenu parent). Read-only.</summary>
    public bool HasItems { get => GetValue<bool>(HasItemsProperty); private set => SetValue(HasItemsProperty, value); }

    /// <summary>Whether this parent's submenu flyout is open.</summary>
    public bool IsSubmenuOpen { get => GetValue<bool>(IsSubmenuOpenProperty); set => SetValue(IsSubmenuOpenProperty, value); }

    /// <summary>Raised when a LEAF item is activated (after its Command runs). A hosting menu listens for this to close
    /// the whole flyout.</summary>
    public event RoutedEventHandler Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    private void OnItemsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) => HasItems = Items.Count > 0;

    // Closing a submenu must close everything BELOW it too: the flyout's rows live in an overlay popup that isn't detached
    // when the popup closes, so a deeper open submenu would otherwise linger. Walk this item's realized child containers and
    // close each - which recurses through their own callback.
    private static void OnIsSubmenuOpenChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        // The property system fires this for the default during construction (ItemContainerGenerator not built yet) - skip.
        if ((bool)e.NewValue || a is not MenuItem mi || mi.ItemContainerGenerator is null) return;
        foreach (var index in mi.ItemContainerGenerator.RealizedIndices.ToList())
            if (mi.ItemContainerGenerator.ContainerFromIndex(index) is MenuItem child)
                child.IsSubmenuOpen = false;
    }

    // --- Container seam: a MenuItem hosts its submenu items in nested MenuItem containers (data-driven via ItemsSource +
    // a HierarchicalDataTemplate). Mirrors ListBox -> ListBoxItem. A node flagged ISeparatorItem becomes a Separator; only a
    // HierarchicalDataTemplate needs the headered MenuItem container; a flat ItemTemplate keeps the base ContentPresenter. --
    protected internal override IUIComponent GetContainerForItem(object item)
        => item is ISeparatorItem { IsSeparator: true } ? new Separator()
         : ItemTemplate is HierarchicalDataTemplate ? CreateContainer(ItemContainerStyle)
         : base.GetContainerForItem(item);

    /// <summary>Creates a MenuItem container carrying the owner's ItemContainerStyle (into Styles, applied AFTER the theme).</summary>
    internal static MenuItem CreateContainer(Style itemContainerStyle)
    {
        var container = new MenuItem();
        if (itemContainerStyle != null) container.Styles.Add(itemContainerStyle);
        return container;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        // A leaf's Click bubbles only to the presenter of ITS popup, not across to the outer menu (submenus are separate
        // overlay branches). So each parent watches its own submenu's presenter: on a descendant Click it closes its flyout
        // and re-raises Click on itself - which lives in the OUTER popup - so the whole chain collapses out to the ContextMenu.
        if (GetTemplateChild("PART_ItemsPresenter") is IInputComponent presenter)
            presenter.AddHandler(ClickEvent, new RoutedEventHandler(OnDescendantClicked), handledEventsToo: true);
    }

    private void OnDescendantClicked(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(e.Source, this)) return;   // our own re-raise, already bubbling outward
        IsSubmenuOpen = false;
        RaiseEvent(new RoutedEventArgs(ClickEvent, this) { RoutedEvent = ClickEvent });
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // When the owning flyout closes this row detaches; drop the submenu state so it doesn't spontaneously re-open the
        // next time the menu is shown.
        IsSubmenuOpen = false;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        // Hovering a row is what drives a menu: this row's submenu opens (if it has one) and any sibling's submenu that
        // was open closes, so only one branch is ever expanded at a time.
        CloseSiblingSubmenus();
        if (HasItems) IsSubmenuOpen = true;
    }

    protected override void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(sender, e);
        if (HasItems)
        {
            // A parent row: a click just (re)opens its submenu, it doesn't dismiss the menu.
            IsSubmenuOpen = true;
            e.Handled = true;
            return;
        }
        // A leaf: run the command and announce the click so the flyout closes.
        if (!IsEnabled) return;
        e.Handled = true;
        if (Command != null && Command.CanExecute(CommandParameter))
            Command.Execute(CommandParameter);
        RaiseEvent(new RoutedEventArgs(ClickEvent, this) { RoutedEvent = ClickEvent });
    }

    // Close the submenus of THIS item's siblings (the other rows in the same menu), so hovering across rows swaps which
    // branch is open instead of leaving a trail of open flyouts.
    private void CloseSiblingSubmenus()
    {
        if (VisualParent is not { } parent) return;
        foreach (var sibling in parent.VisualChildren)
            if (!ReferenceEquals(sibling, this) && sibling is MenuItem { IsSubmenuOpen: true } item)
                item.IsSubmenuOpen = false;
    }
}
