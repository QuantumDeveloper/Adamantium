using System.Linq;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>A tab's label in the ribbon's strip - the item container the <see cref="Ribbon"/> generates. It stands FOR
/// a <see cref="RibbonTab"/> rather than being it: the tab is the body in the groups area, and one control cannot be
/// in both places.</summary>
public class RibbonTabHeader : ContentControl, ISelectable, IKeyTipTarget, IKeyTipScope
{
    /// <summary>Its key tip selects the tab - the same thing a click on it does, flyout and all - and then the band
    /// shows that tab's commands, which are the next level's badges.</summary>
    public void PressKeyTip() => Owner?.ClickTab(this);

    /// <summary>The band, not this header: a tab's commands are shown by the ribbon's content host, not underneath the
    /// strip. Asked after the tab is selected, so what it holds is what the level offers.</summary>
    public Core.IUIComponent KeyTipContent => Owner?.SelectedContentHost;

    public static readonly AdamantiumProperty IsSelectedProperty = AdamantiumProperty.Register(nameof(IsSelected),
        typeof(bool), typeof(RibbonTabHeader), new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    // State brushes the theme's triggers project onto the chrome. Null = no change in that state.
    public static readonly AdamantiumProperty BackgroundPointerOverProperty = AdamantiumProperty.Register(
        nameof(BackgroundPointerOver), typeof(Brush), typeof(RibbonTabHeader), new PropertyMetadata(default(Brush)));

    public static readonly AdamantiumProperty BackgroundSelectedProperty = AdamantiumProperty.Register(
        nameof(BackgroundSelected), typeof(Brush), typeof(RibbonTabHeader), new PropertyMetadata(default(Brush)));

    public static readonly AdamantiumProperty ForegroundSelectedProperty = AdamantiumProperty.Register(
        nameof(ForegroundSelected), typeof(Brush), typeof(RibbonTabHeader), new PropertyMetadata(default(Brush)));

    /// <summary>The context its tab belongs to, copied here by the ribbon when the container is prepared. The STRIP is
    /// what needs it - the panel cuts its runs from neighbouring headers, and the theme paints the header in the
    /// group's colour - and the strip holds headers, not tabs.</summary>
    public static readonly AdamantiumProperty ContextualGroupProperty = AdamantiumProperty.Register(
        nameof(ContextualGroup), typeof(RibbonContextualGroup), typeof(RibbonTabHeader),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsParentMeasure, OnContextualGroupChanged));

    /// <summary>The group's colour, projected onto this header. Its own property, so a theme trigger can use it while
    /// an ordinary header (null group, null accent) keeps the plain look.</summary>
    public static readonly AdamantiumProperty AccentProperty = AdamantiumProperty.Register(nameof(Accent),
        typeof(Brush), typeof(RibbonTabHeader), new PropertyMetadata(default(Brush), PropertyMetadataOptions.AffectsRender));

    public RibbonContextualGroup ContextualGroup
    {
        get => GetValue<RibbonContextualGroup>(ContextualGroupProperty);
        set => SetValue(ContextualGroupProperty, value);
    }

    public Brush Accent
    {
        get => GetValue<Brush>(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    /// <summary>Whether this header stands for a contextual tab. A trigger cannot ask "is <see cref="Accent"/> set", so
    /// the fact is stated as its own flag - and it is the ONE thing the colouring hangs on, which is what tells at a
    /// glance which tabs belong together even when their ledge is turned off.</summary>
    public static readonly AdamantiumProperty IsContextualProperty = AdamantiumProperty.RegisterReadOnly(
        nameof(IsContextual), typeof(bool), typeof(RibbonTabHeader),
        new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    public bool IsContextual
    {
        get => GetValue<bool>(IsContextualProperty);
        private set => SetValue(IsContextualProperty, value);
    }

    private static void OnContextualGroupChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not RibbonTabHeader header) return;

        header.Accent = header.ContextualGroup?.Accent;
        header.IsContextual = header.ContextualGroup != null;
    }

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

    /// <summary>Enter/Space OPENS the tab: picks it and steps into its first command. Tab keeps walking the headers.
    /// <para>DOWN does the same, because that is where the commands are. The generic outward walk cannot answer it: the
    /// strip and the groups area are separate subtrees of the ribbon's template, and the panel between them can only
    /// answer by the order its own children stand in.</para></summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.OriginalSource != this) return;

        if (e.Key is not (Key.Enter or Key.Space or Key.DownArrow)) return;

        Owner?.EnterTab(this);
        e.Handled = true;
    }
}
