using System.Linq;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.RoutedEvents;

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

    /// <summary>Opens the menu positioned against <paramref name="target"/>.</summary>
    public void Open(UIComponent target)
    {
        PlacementTarget = target;
        IsOpen = true;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _popup = GetTemplateChild("PART_Popup") as Popup;
        _clickRoot = GetTemplateChild("PART_ItemsPresenter") as IInputComponent;
        if (_popup != null)
        {
            _popup.PlacementTarget = PlacementTarget ?? this;
            _popup.Placement = Placement;
            _popup.FlipToFit = true;
            _popup.IsOpen = IsOpen;
        }
        // Any leaf row's Click bubbles up to the items presenter - close the menu after the command has run.
        _clickRoot?.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnItemClicked), handledEventsToo: true);
    }

    private void OnItemClicked(object sender, RoutedEventArgs e) => IsOpen = false;

    private static void OnIsOpenChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        var menu = (ContextMenu)a;
        if (menu._popup != null)
        {
            menu._popup.PlacementTarget = menu.PlacementTarget ?? menu;
            menu._popup.IsOpen = (bool)e.NewValue;
        }
        if ((bool)e.NewValue) menu.HookLightDismiss(); else menu.UnhookLightDismiss();
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
        if (e.OriginalSource is IUIComponent src && _popup?.Child is IUIComponent child && IsWithin(src, child))
            return;   // inside the open menu - not a dismiss
        IsOpen = false;
    }

    private static bool IsWithin(IUIComponent node, IUIComponent ancestor)
    {
        if (ancestor == null) return false;
        for (var n = node; n != null; n = n.VisualParent)
            if (ReferenceEquals(n, ancestor)) return true;
        return false;
    }
}
