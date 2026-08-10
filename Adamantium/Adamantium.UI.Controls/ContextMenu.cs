using System;
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
    private ScrollViewer _scroll;         // wraps the items; capped to the window height so a long menu scrolls

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

    // A flyout occupies NO space where it is authored - its rows live in the popup overlay. Measuring to zero is not
    // enough: a stretching slot arranges it to the whole cell anyway, and being last in z-order it then swallows every
    // press meant for what it sits over (the quick-access buttons went dead under their own overflow menu).
    protected override Size MeasureOverride(Size availableSize)
    {
        base.MeasureOverride(availableSize);
        return Size.Zero;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        base.ArrangeOverride(Size.Zero);
        return Size.Zero;
    }

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
        DetachParts();   // a template swap re-runs this; drop the old wiring first

        _popup = GetTemplateChild("PART_Popup") as Popup;
        _clickRoot = GetTemplateChild("PART_ItemsPresenter") as IInputComponent;
        _scroll = GetTemplateChild("PART_MenuScroll") as ScrollViewer;
        // Scrolling the list = browsing it, not navigating a submenu: close any open submenu so it doesn't ride along with
        // the scrolled row it's anchored to. A NAMED handler, not a lambda: a lambda cannot be taken off again, and this
        // one has to come off when the template goes.
        if (_scroll != null) _scroll.ScrollChanged += OnMenuScrolled;
        if (_popup != null)
        {
            _popup.PlacementTarget = PlacementTarget ?? this;
            _popup.Placement = Placement;
            _popup.HorizontalOffset = HorizontalOffset;
            _popup.VerticalOffset = VerticalOffset;
            _popup.FlipToFit = true;
            _popup.KeepOpen = false;                     // click-outside-to-close (submenu overlays included), owned by Popup
            // ...but the TARGET is not "outside". Without this the press that opens the menu also dismisses it: the
            // popup light-dismisses first, the button's click then re-opens it, and a toggle can never close what it
            // opened - the second press looks like it does nothing.
            _popup.IgnoreTargetPress = true;
            _popup.Closed -= OnPopupClosed;
            _popup.Closed += OnPopupClosed;
            _popup.IsOpen = IsOpen;
        }
        // Any leaf row's Click bubbles up to the items presenter - close the menu after the command has run.
        // The handler INSTANCE is kept: RemoveHandler matches on the delegate, so a freshly-made one would not take off
        // the one that was added.
        _itemClicked ??= OnItemClicked;
        _clickRoot?.AddHandler(MenuItem.ClickEvent, _itemClicked, handledEventsToo: true);
    }

    /// <summary>Let the template's parts go when the template does - see ScrollBar.OnRemoveTemplate.</summary>
    public override void OnRemoveTemplate()
    {
        base.OnRemoveTemplate();
        DetachParts();
    }

    private void DetachParts()
    {
        if (_scroll != null) _scroll.ScrollChanged -= OnMenuScrolled;
        if (_clickRoot != null && _itemClicked != null) _clickRoot.RemoveHandler(MenuItem.ClickEvent, _itemClicked);
        if (_popup != null) _popup.Closed -= OnPopupClosed;
        _scroll = null;
        _clickRoot = null;
        _popup = null;
    }

    private RoutedEventHandler _itemClicked;

    private void OnMenuScrolled(object sender, EventArgs e) => CloseAllSubmenus();

    private void OnItemClicked(object sender, RoutedEventArgs e) => IsOpen = false;

    // The popup light-dismissed (a press outside the menu's overlay) - drive our IsOpen false so the whole menu tears down.
    private void OnPopupClosed(object sender, EventArgs e) => IsOpen = false;

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
        if ((bool)e.NewValue)
        {
            if (menu._scroll != null) menu._scroll.MaxHeight = Popup.WindowHeightCap(Popup.FindPopupHost(menu));
        }
        else menu.CloseAllSubmenus();
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

}
