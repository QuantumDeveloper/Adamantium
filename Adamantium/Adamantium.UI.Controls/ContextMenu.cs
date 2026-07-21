using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

/// <summary>
/// A flyout list of <see cref="MenuItem"/> rows, shown in the window's popup overlay (no OS window) against a target or a
/// point. Light-dismisses on an outside click, and closes when any leaf row is clicked. Used for right-click menus and as
/// the overflow menu of a command bar. Items host in the popup via the control template's <c>PART_ItemsPresenter</c>.
/// </summary>
public class ContextMenu : ItemsControl
{
    public static readonly AdamantiumProperty IsOpenProperty = AdamantiumProperty.Register(nameof(IsOpen),
        typeof(bool), typeof(ContextMenu), new PropertyMetadata(false, OnIsOpenChanged));

    public static readonly AdamantiumProperty PlacementTargetProperty = AdamantiumProperty.Register(nameof(PlacementTarget),
        typeof(UIComponent), typeof(ContextMenu), new PropertyMetadata(null));

    public static readonly AdamantiumProperty PlacementProperty = AdamantiumProperty.Register(nameof(Placement),
        typeof(PlacementMode), typeof(ContextMenu), new PropertyMetadata(PlacementMode.Bottom));

    private Popup _popup;
    private IInputComponent _clickRoot;   // the items presenter; leaf-row clicks bubble to it (it IS an input element)
    private IPopupHost _host;
    private readonly MouseButtonEventHandler _lightDismiss;

    public ContextMenu()
    {
        _lightDismiss = OnGlobalPreviewDown;
    }

    /// <summary>Whether the menu is shown. Set true (with a PlacementTarget) to open; cleared on pick / outside click.</summary>
    public bool IsOpen
    {
        get => GetValue<bool>(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>The element the menu positions against (e.g. the button that opened it).</summary>
    public UIComponent PlacementTarget
    {
        get => GetValue<UIComponent>(PlacementTargetProperty);
        set => SetValue(PlacementTargetProperty, value);
    }

    public PlacementMode Placement
    {
        get => GetValue<PlacementMode>(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    /// <summary>Extra offset of the popup from its placement (used to open AT the cursor for a right-click menu:
    /// Placement=Relative + the click point).</summary>
    public double HorizontalOffset { get; set; }
    public double VerticalOffset { get; set; }

    /// <summary>Opens the menu positioned against <paramref name="target"/>.</summary>
    public void Open(UIComponent target)
    {
        PlacementTarget = target;
        IsOpen = true;
    }

    /// <summary>Opens the menu AT <paramref name="point"/> (in <paramref name="target"/>-local coordinates) - the right-click
    /// entry point: places relative to the target's top-left, then offsets to the click point.</summary>
    public void Open(UIComponent target, Vector2 point)
    {
        Placement = PlacementMode.Relative;
        HorizontalOffset = point.X;
        VerticalOffset = point.Y;
        PlacementTarget = target;
        IsOpen = true;
    }

    // A data-driven menu (ItemsSource + a HierarchicalDataTemplate) generates MenuItem containers so each node gets a
    // header + its own submenu; a node flagged ISeparatorItem becomes a Separator (drawn from its own style); a flat
    // ItemTemplate keeps the base ContentPresenter (e.g. the command-bar overflow menu).
    protected internal override IUIComponent GetContainerForItem(object item)
        => item is ISeparatorItem { IsSeparator: true } ? new Separator()
         : ItemTemplate is HierarchicalDataTemplate ? MenuItem.CreateContainer(ItemContainerStyle)
         : base.GetContainerForItem(item);

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _popup = GetTemplateChild("PART_Popup") as Popup;
        _clickRoot = GetTemplateChild("PART_ItemsPresenter") as IInputComponent;
        if (_popup != null)
        {
            _popup.PlacementTarget = PlacementTarget ?? this;
            _popup.Placement = Placement;
            _popup.HorizontalOffset = HorizontalOffset;
            _popup.VerticalOffset = VerticalOffset;
            _popup.FlipToFit = true;
            _popup.IsOpen = IsOpen;
        }
        // Any leaf row's Click bubbles up to the items presenter - close the menu after the command has run.
        _clickRoot?.AddHandler(MenuItem.ClickEvent, new RoutedEventHandler(OnItemClicked), handledEventsToo: true);
    }

    private void OnItemClicked(object sender, RoutedEventArgs e) => IsOpen = false;

    private static void OnIsOpenChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        var menu = (ContextMenu)a;
        if (menu._popup != null)
        {
            menu._popup.PlacementTarget = menu.PlacementTarget ?? menu;
            menu._popup.Placement = menu.Placement;
            menu._popup.HorizontalOffset = menu.HorizontalOffset;
            menu._popup.VerticalOffset = menu.VerticalOffset;
            menu._popup.IsOpen = (bool)e.NewValue;
        }
        if ((bool)e.NewValue) menu.HookLightDismiss();
        else { menu.CloseAllSubmenus(); menu.UnhookLightDismiss(); }
    }

    // Closing the menu must close every open submenu flyout too (they are independent overlay popups). Close each top-level
    // row's submenu - which cascades down through MenuItem's own close handler.
    private void CloseAllSubmenus()
    {
        // OnIsOpenChanged fires for the default during construction, before the base ItemsControl builds the generator.
        if (ItemContainerGenerator is null) return;
        foreach (var index in ItemContainerGenerator.RealizedIndices.ToList())
            if (ItemContainerGenerator.ContainerFromIndex(index) is MenuItem child)
                child.IsSubmenuOpen = false;
    }

    // Light dismiss: a preview mouse-down anywhere that is NOT inside the popup closes the menu (mirrors DropDown).
    private void HookLightDismiss()
    {
        _host ??= this.GetVisualAncestors().OfType<IPopupHost>().FirstOrDefault()
                  ?? PlacementTarget?.GetVisualAncestors().OfType<IPopupHost>().FirstOrDefault();
        if (_host is IInputComponent root)
            root.AddHandler(Mouse.PreviewMouseDownEvent, _lightDismiss, handledEventsToo: true);
    }

    private void UnhookLightDismiss()
    {
        if (_host is IInputComponent root)
            root.RemoveHandler(Mouse.PreviewMouseDownEvent, _lightDismiss);
    }

    private void OnGlobalPreviewDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is IUIComponent src && IsInsideMenu(src)) return;   // inside the open menu - not a dismiss
        IsOpen = false;
    }

    // A press is "inside" the menu when it lands in the root popup card OR in any MenuItem: every submenu flyout's rows are
    // MenuItems of this open menu, but those flyouts are SEPARATE overlay popups that the root popup's card doesn't contain -
    // so a plain "inside the root card" test would wrongly dismiss the menu on a click in a submenu.
    private bool IsInsideMenu(IUIComponent node)
    {
        var card = _popup?.Child as IUIComponent;
        for (var n = node; n != null; n = n.VisualParent)
        {
            if (n is MenuItem) return true;
            if (card != null && ReferenceEquals(n, card)) return true;
        }
        return false;
    }
}
