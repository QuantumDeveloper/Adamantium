using System.Linq;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>A tab's label in the ribbon's strip - the item container the <see cref="Ribbon"/> generates. It stands FOR
/// a <see cref="RibbonTab"/> rather than being it: the tab is the body in the groups area, and one control cannot be
/// in both places.</summary>
public class RibbonTabHeader : ContentControl, ISelectable
{
    public static readonly AdamantiumProperty IsSelectedProperty = AdamantiumProperty.Register(nameof(IsSelected),
        typeof(bool), typeof(RibbonTabHeader), new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    // State brushes the theme's triggers project onto the chrome. Null = no change in that state.
    public static readonly AdamantiumProperty BackgroundPointerOverProperty = AdamantiumProperty.Register(
        nameof(BackgroundPointerOver), typeof(Brush), typeof(RibbonTabHeader), new PropertyMetadata(default(Brush)));

    public static readonly AdamantiumProperty BackgroundSelectedProperty = AdamantiumProperty.Register(
        nameof(BackgroundSelected), typeof(Brush), typeof(RibbonTabHeader), new PropertyMetadata(default(Brush)));

    public static readonly AdamantiumProperty ForegroundSelectedProperty = AdamantiumProperty.Register(
        nameof(ForegroundSelected), typeof(Brush), typeof(RibbonTabHeader), new PropertyMetadata(default(Brush)));

    static RibbonTabHeader()
    {
        FocusableProperty.OverrideMetadata(typeof(RibbonTabHeader), new PropertyMetadata(true));
    }

    public bool IsSelected
    {
        get => GetValue<bool>(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public Brush BackgroundPointerOver
    {
        get => GetValue<Brush>(BackgroundPointerOverProperty);
        set => SetValue(BackgroundPointerOverProperty, value);
    }

    public Brush BackgroundSelected
    {
        get => GetValue<Brush>(BackgroundSelectedProperty);
        set => SetValue(BackgroundSelectedProperty, value);
    }

    public Brush ForegroundSelected
    {
        get => GetValue<Brush>(ForegroundSelectedProperty);
        set => SetValue(ForegroundSelectedProperty, value);
    }

    private Ribbon Owner => this.GetVisualAncestors().OfType<Ribbon>().FirstOrDefault();

    /// <summary>The selection only reflects onto containers when it CHANGES, so a header realized after the fact pulls
    /// the current state here - which is what lights up the initially selected tab.</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Owner is { } owner) IsSelected = owner.IsHeaderSelected(this);
    }

    protected override void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(sender, e);
        if (!IsEnabled || e.Handled) return;

        e.Handled = true;
        Focus();

        // Double-click minimizes the band and restores it, as Office does. The first click has already opened this tab.
        if (e.ClickCount >= 2)
        {
            Owner?.ToggleMinimized();
            return;
        }

        Owner?.ClickTab(this);
    }

    /// <summary>Enter/Space OPENS the tab: picks it and steps into its first command. Tab keeps walking the headers.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.OriginalSource != this) return;

        if (e.Key is not (Key.Enter or Key.Space)) return;

        Owner?.EnterTab(this);
        e.Handled = true;
    }
}
