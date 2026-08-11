using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>The quick-access bar: DOCUMENT commands kept within one click, shown in the caption through
/// <see cref="TitleBar.LeadingContent"/>. Its own control and its own list - the user reorders these, while the
/// window's commands belong to the application, and the reorder gesture must not reach across. It holds no reference
/// to a <see cref="Ribbon"/>: the two meet at a collection in the shell's view model. See docs/RIBBON_PLAN.md §7.1.</summary>
public class RibbonQuickAccess : ItemsControl
{
    static RibbonQuickAccess()
    {
        // The bar's own choice, so the theme states only the LOOK. Stateless, hence shared as the metadata default.
        ItemTemplateSelectorProperty.OverrideMetadata(typeof(RibbonQuickAccess),
            new PropertyMetadata(new RibbonQuickAccessTemplateSelector()));
    }

    /// <summary>Where the bar is shown. The application hosts an instance in BOTH slots, bound to the one collection,
    /// and each shows itself only while this names the slot it is standing in.</summary>
    public static readonly AdamantiumProperty PlacementProperty = AdamantiumProperty.Register(nameof(Placement),
        typeof(RibbonQuickAccessPlacement), typeof(RibbonQuickAccess),
        new PropertyMetadata(RibbonQuickAccessPlacement.Caption, PropertyMetadataOptions.AffectsMeasure, OnPlacementChanged));

    public RibbonQuickAccessPlacement Placement
    {
        get => GetValue<RibbonQuickAccessPlacement>(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    private static void OnPlacementChanged(AdamantiumComponent sender, AdamantiumPropertyChangedEventArgs e)
    {
        (sender as RibbonQuickAccess)?.ShowOnlyInItsOwnSlot();
    }

    /// <summary>How an ordinary item is drawn - the icon button the theme states. The THEME owns the look; which items
    /// are ordinary is the selector's decision, and an application replacing that decision
    /// (<see cref="ItemsControl.ItemTemplateSelector"/>) keeps this look for everything it does not treat specially.</summary>
    public static readonly AdamantiumProperty DefaultItemTemplateProperty = AdamantiumProperty.Register(
        nameof(DefaultItemTemplate), typeof(Core.Templates.DataTemplate), typeof(RibbonQuickAccess),
        new PropertyMetadata(null));

    public Core.Templates.DataTemplate DefaultItemTemplate
    {
        get => GetValue<Core.Templates.DataTemplate>(DefaultItemTemplateProperty);
        set => SetValue(DefaultItemTemplateProperty, value);
    }

    /// <summary>The commands the caption had no room for. They are not lost - the bar offers them under its chevron,
    /// which is the whole difference between a bar that overflows and one that runs off the edge.</summary>
    public static readonly AdamantiumProperty OverflowItemsProperty = AdamantiumProperty.Register(nameof(OverflowItems),
        typeof(IEnumerable), typeof(RibbonQuickAccess), new PropertyMetadata(null));

    public static readonly AdamantiumProperty HasOverflowItemsProperty = AdamantiumProperty.Register(
        nameof(HasOverflowItems), typeof(bool), typeof(RibbonQuickAccess), new PropertyMetadata(false));

    /// <summary>Open state of the overflow list. <see cref="Primitives.ToggleButton.IsChecked"/> on the chevron IS this,
    /// the way a drop-down command marks itself while its menu is down.</summary>
    public static readonly AdamantiumProperty IsOverflowOpenProperty = AdamantiumProperty.Register(nameof(IsOverflowOpen),
        typeof(bool), typeof(RibbonQuickAccess), new PropertyMetadata(false));

    /// <summary>What the chevron costs. The panel reserves it from the buttons' budget ALWAYS - a budget that depended on
    /// whether the chevron is currently shown would flip-flop, and the caption would re-lay-out every frame.</summary>
    public static readonly AdamantiumProperty OverflowButtonWidthProperty = AdamantiumProperty.Register(
        nameof(OverflowButtonWidth), typeof(double), typeof(RibbonQuickAccess),
        new PropertyMetadata(28.0, PropertyMetadataOptions.AffectsMeasure));

    public IEnumerable OverflowItems
    {
        get => GetValue<IEnumerable>(OverflowItemsProperty);
        private set => SetValue(OverflowItemsProperty, value);
    }

    public bool HasOverflowItems
    {
        get => GetValue<bool>(HasOverflowItemsProperty);
        private set => SetValue(HasOverflowItemsProperty, value);
    }

    public bool IsOverflowOpen
    {
        get => GetValue<bool>(IsOverflowOpenProperty);
        set => SetValue(IsOverflowOpenProperty, value);
    }

    public double OverflowButtonWidth
    {
        get => GetValue<double>(OverflowButtonWidthProperty);
        set => SetValue(OverflowButtonWidthProperty, value);
    }

    /// <summary>Told by the panel, which is the only thing that knows what fit. The ITEMS are published, not the
    /// containers: the overflow list draws them through its own template, and a container can only be in one place.</summary>
    internal void SetOverflow(IReadOnlyList<IUIComponent> hidden)
    {
        var items = new List<object>(hidden.Count);
        foreach (var container in hidden)
        {
            var index = ItemContainerGenerator.IndexFromContainer(container);
            items.Add(index >= 0 && index < Items.Count ? Items[index] : container);
        }

        OverflowItems = items;
        HasOverflowItems = items.Count > 0;

        if (items.Count == 0)
        {
            IsOverflowOpen = false;
        }
    }

    private Primitives.ButtonBase _overflowButton;
    private ContextMenu _overflowMenu;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (_overflowButton != null)
        {
            _overflowButton.Click -= OnOverflowClick;
        }

        _overflowButton = GetTemplateChild("PART_OverflowButton") as Primitives.ButtonBase;
        if (_overflowButton != null)
        {
            _overflowButton.Click += OnOverflowClick;
        }

        if (_overflowMenu != null)
        {
            _overflowMenu.PropertyChanged -= OnOverflowMenuChanged;
        }

        _overflowMenu = GetTemplateChild("PART_OverflowMenu") as ContextMenu;
        if (_overflowMenu != null)
        {
            // The chevron closes what it opened: without this the press dismisses the list first and the click then
            // re-opens it, so the second press looks like it does nothing.
            _overflowMenu.IgnoreTargetPress = true;
            _overflowMenu.PropertyChanged += OnOverflowMenuChanged;
        }
    }

    // Two-state: the second press puts the list away. What "open" means is asked of the MENU - a state mirrored onto the
    // button would have to be put back in step every time the list closed itself (a pick, Escape, a press outside).
    private void OnOverflowClick(object sender, RoutedEventArgs e)
    {
        if (_overflowMenu == null) return;

        if (_overflowMenu.IsOpen)
        {
            _overflowMenu.IsOpen = false;
            return;
        }

        _overflowMenu.Open(_overflowButton);
    }

    private void OnOverflowMenuChanged(object sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (e.Property != ContextMenu.IsOpenProperty) return;

        IsOverflowOpen = Equals(e.NewValue, true);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ShowOnlyInItsOwnSlot();
    }

    /// <summary>Stamps the item's key on its visual, so a request raised from inside the bar names the command the item
    /// stands for. Without it the only identity a bar item has is the command it runs - and an item that runs none (a
    /// state a command shows, a slider) could be put in and never taken out.</summary>
    protected internal override void PrepareContainer(IUIComponent container, object item)
    {
        base.PrepareContainer(container, item);

        if (item is IQuickAccessItem quick && quick.Key != null)
        {
            Ribbon.SetQuickAccessKey(container, quick.Key);
        }
    }

    // Which slot this instance stands in is a fact about the tree, not something the application should have to repeat:
    // a caption is a TitleBar, anything else is the ribbon's own row.
    private void ShowOnlyInItsOwnSlot()
    {
        var slot = this.GetVisualAncestors().OfType<TitleBar>().Any()
            ? RibbonQuickAccessPlacement.Caption
            : RibbonQuickAccessPlacement.BelowRibbon;

        // Not a plain write: a Local value would mask an author's own binding on Visibility.
        SetCurrentValue(VisibilityProperty, slot == Placement ? Visibility.Visible : Visibility.Collapsed);
    }
}
