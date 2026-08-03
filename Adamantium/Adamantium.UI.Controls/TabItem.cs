using System.Linq;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

/// <summary>
/// A tab in a <see cref="TabControl"/>: a clickable <see cref="Header"/> shown in the tab strip, plus the
/// <see cref="ContentControl.Content"/> that fills the control's body while this tab is selected. Clicking the header
/// selects it. Like every item container its <see cref="IsSelected"/> state is driven BY the owning TabControl, so it
/// stays correct as tabs are added/removed; the rest/hover/selected chrome is projected from the theme via triggers.
/// </summary>
public class TabItem : ContentControl, ISelectable, ISpringLoadable
{
    public static readonly AdamantiumProperty HeaderProperty = AdamantiumProperty.Register(nameof(Header),
        typeof(object), typeof(TabItem), new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure));

    // How a data Header is projected into the strip. The template's header presenter binds these; the owning TabControl
    // flows its ItemTemplate/ItemTemplateSelector here for generated (data-bound) tabs, so a header VM renders as a
    // proper visual (e.g. a TextBlock bound to its Header) instead of ToString(). Null = the header hosts as-is.
    public static readonly AdamantiumProperty HeaderTemplateProperty = AdamantiumProperty.Register(nameof(HeaderTemplate),
        typeof(DataTemplate), typeof(TabItem),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure, OnHeaderTemplateChanged));

    /// <summary>Dirty the header PRESENTER, not just this tab. The presenter learns of the new template through a
    /// {TemplateBinding}, and those are flushed in a batch - so on the tab's next measure the presenter is still
    /// measure-VALID, holding the size of a visual built from the OLD template, and the row simply reuses it. Only a
    /// LATER direct measure of the presenter rebuilds it, and by then this tab has already answered.
    /// <para>Measured, folding a tool strip: the presenter reported the turned label at 17x54 while its tab kept the
    /// 78x29 it had lying flat, three measures deep - and the one tab that happened to get a fourth measure was the only
    /// one that came out right.</para></summary>
    private static void OnHeaderTemplateChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is TabItem tab && tab.GetTemplateChild("PART_ContentPresenter") is IMeasurableComponent presenter)
        {
            presenter.InvalidateMeasure();
        }
    }

    public static readonly AdamantiumProperty HeaderTemplateSelectorProperty = AdamantiumProperty.Register(nameof(HeaderTemplateSelector),
        typeof(DataTemplateSelector), typeof(TabItem), new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty IsSelectedProperty = AdamantiumProperty.Register(nameof(IsSelected),
        typeof(bool), typeof(TabItem), new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    // State brushes the default template's triggers project onto the tab chrome (hover / selected). Exposed as properties
    // so ONE template serves every state - the theme just sets these. Null = no change in that state.
    public static readonly AdamantiumProperty BackgroundPointerOverProperty = AdamantiumProperty.Register(
        nameof(BackgroundPointerOver), typeof(Brush), typeof(TabItem), new PropertyMetadata(default(Brush)));

    public static readonly AdamantiumProperty BackgroundSelectedProperty = AdamantiumProperty.Register(
        nameof(BackgroundSelected), typeof(Brush), typeof(TabItem), new PropertyMetadata(default(Brush)));

    public static readonly AdamantiumProperty ForegroundSelectedProperty = AdamantiumProperty.Register(
        nameof(ForegroundSelected), typeof(Brush), typeof(TabItem), new PropertyMetadata(default(Brush)));

    /// <summary>What the tab is marked with, next to its header - DATA, not a control: a key, a glyph, a view model, an
    /// image source. <see cref="IconTemplate"/> says how to draw it.
    /// <para>Data on purpose. The same tab is drawn in more than one place at once - the strip and the overflow flyout -
    /// and a control can only ever be in one of them; handing the same instance to both takes it out of the first. Data
    /// plus a template builds a fresh visual per place, so an icon can appear in as many as it likes.</para></summary>
    public static readonly AdamantiumProperty IconProperty = AdamantiumProperty.Register(nameof(Icon),
        typeof(object), typeof(TabItem), new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure, OnIconChanged));

    /// <summary>Whether this tab has an icon at all - the one thing a template needs to know about
    /// <see cref="Icon"/> that a template binding cannot answer (a presenter fed nothing still reserves its own margin,
    /// which is 6px of dead space in every tab without one, and it shows the moment the strip turns narrow). Folded here
    /// like <see cref="ShowCloseButton"/>, so the theme collapses the icon slot with a plain trigger.</summary>
    public static readonly AdamantiumProperty HasIconProperty = AdamantiumProperty.Register(nameof(HasIcon),
        typeof(bool), typeof(TabItem), new PropertyMetadata(false));

    private static void OnIconChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is TabItem tab)
        {
            tab.HasIcon = e.NewValue != null;
        }
    }

    /// <summary>How to draw <see cref="Icon"/>. An EFFECTIVE value pulled from the owning
    /// <see cref="TabControl.IconTemplate"/>, so one template serves every tab in a strip; set it here to override it
    /// for a single tab.</summary>
    public static readonly AdamantiumProperty IconTemplateProperty = AdamantiumProperty.Register(nameof(IconTemplate),
        typeof(DataTemplate), typeof(TabItem), new PropertyMetadata(null));

    // Close button (see TabControl.ShowCloseButton). IsClosable is the per-tab opt-out (author it False to keep a pinned
    // tab open). ShowCloseButton + CloseButtonTemplate are EFFECTIVE values pulled from the owning TabControl on attach
    // (so authored + generated tabs behave alike); the tab template binds the button's visibility/look to them.
    public static readonly AdamantiumProperty IsClosableProperty = AdamantiumProperty.Register(nameof(IsClosable),
        typeof(bool), typeof(TabItem), new PropertyMetadata(true, OnCloseConfigChanged));

    /// <summary>Kept: this tab does not go with a bulk close, and it lives in the strip's PINNED row rather than among
    /// the tabs that come and go (see <see cref="TabControl.PinnedTabsPlacement"/>).</summary>
    public static readonly AdamantiumProperty IsPinnedProperty = AdamantiumProperty.Register(nameof(IsPinned),
        typeof(bool), typeof(TabItem), new PropertyMetadata(false, OnIsPinnedChanged));


    // Whoever set it - the button, a menu, a view-model - the button must end up showing it.
    private static void OnIsPinnedChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
        => ((TabItem)a).SyncPinButtonState();
    public static readonly AdamantiumProperty ShowCloseButtonProperty = AdamantiumProperty.Register(nameof(ShowCloseButton),
        typeof(bool), typeof(TabItem), new PropertyMetadata(false));

    public static readonly AdamantiumProperty CloseButtonTemplateProperty = AdamantiumProperty.Register(nameof(CloseButtonTemplate),
        typeof(ControlTemplate), typeof(TabItem), new PropertyMetadata(null));

    // Pin button - the same arrangement as the close one: WHETHER it is there and WHAT it looks like are effective
    // values pulled from the owner, so a host restyles the button by handing the strip another template instead of
    // rewriting the tab's whole ControlTemplate.
    public static readonly AdamantiumProperty ShowPinButtonProperty = AdamantiumProperty.Register(nameof(ShowPinButton),
        typeof(bool), typeof(TabItem), new PropertyMetadata(false));

    public static readonly AdamantiumProperty PinButtonTemplateProperty = AdamantiumProperty.Register(nameof(PinButtonTemplate),
        typeof(ControlTemplate), typeof(TabItem), new PropertyMetadata(null));

    private static void OnCloseConfigChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
        => ((TabItem)a).SyncCloseButton();

    static TabItem()
    {
        // A tab is a keyboard-focus target (arrow-key navigation between tabs) - opt in, since the base default is now
        // false. Metadata priority, so a {Binding}/Style/Trigger can still override it.
        FocusableProperty.OverrideMetadata(typeof(TabItem), new PropertyMetadata(true));
        FontSizeProperty.OverrideMetadata(typeof(TabItem),
            new PropertyMetadata(13.0, PropertyMetadataOptions.Inherits | PropertyMetadataOptions.AffectsMeasure));
    }

    /// <summary>The tab-strip label - a string or any UI content. Distinct from <see cref="ContentControl.Content"/>,
    /// which is the body shown when the tab is selected.</summary>
    public object Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <summary>Template used to render a data <see cref="Header"/> in the strip. Set from the owning
    /// <see cref="TabControl.ItemTemplate"/> for generated tabs.</summary>
    public DataTemplate HeaderTemplate
    {
        get => GetValue<DataTemplate>(HeaderTemplateProperty);
        set => SetValue(HeaderTemplateProperty, value);
    }

    /// <summary>What marks this tab, drawn by <see cref="IconTemplate"/>. Data rather than a control - see the property
    /// declaration for why.</summary>
    public object Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>True when <see cref="Icon"/> is set. Read it in a template trigger; it is maintained from Icon.</summary>
    public bool HasIcon
    {
        get => GetValue<bool>(HasIconProperty);
        private set => SetValue(HasIconProperty, value);
    }

    /// <summary>How <see cref="Icon"/> is drawn. Comes from the owning <see cref="TabControl.IconTemplate"/> unless set
    /// here.</summary>
    public DataTemplate IconTemplate
    {
        get => GetValue<DataTemplate>(IconTemplateProperty);
        set => SetValue(IconTemplateProperty, value);
    }

    /// <summary>Picks the header template per item (from the owning <see cref="TabControl.ItemTemplateSelector"/>).</summary>
    public DataTemplateSelector HeaderTemplateSelector
    {
        get => GetValue<DataTemplateSelector>(HeaderTemplateSelectorProperty);
        set => SetValue(HeaderTemplateSelectorProperty, value);
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

    /// <summary>Whether THIS tab may show a close button when the owning <see cref="TabControl.ShowCloseButton"/> is on.
    /// Author False to keep a pinned tab open. Default true.</summary>
    public bool IsClosable
    {
        get => GetValue<bool>(IsClosableProperty);
        set => SetValue(IsClosableProperty, value);
    }

    /// <summary>Whether this tab is pinned - kept out of a bulk close and shown in the strip's pinned row.</summary>
    public bool IsPinned
    {
        get => GetValue<bool>(IsPinnedProperty);
        set => SetValue(IsPinnedProperty, value);
    }

    /// <summary>Whether this tab shows a pin button (effective value, from the owning strip).</summary>
    public bool ShowPinButton
    {
        get => GetValue<bool>(ShowPinButtonProperty);
        set => SetValue(ShowPinButtonProperty, value);
    }

    /// <summary>The look of that button, from the owning strip - swap it to restyle pinning everywhere at once.</summary>
    public ControlTemplate PinButtonTemplate
    {
        get => GetValue<ControlTemplate>(PinButtonTemplateProperty);
        set => SetValue(PinButtonTemplateProperty, value);
    }

    /// <summary>Effective close-button visibility for this tab (owner's ShowCloseButton AND this tab's IsClosable),
    /// pulled from the owning TabControl. The tab template binds the button's visibility to it.</summary>
    public bool ShowCloseButton
    {
        get => GetValue<bool>(ShowCloseButtonProperty);
        set => SetValue(ShowCloseButtonProperty, value);
    }

    /// <summary>The close button's look, pulled from <see cref="TabControl.CloseButtonTemplate"/>.</summary>
    public ControlTemplate CloseButtonTemplate
    {
        get => GetValue<ControlTemplate>(CloseButtonTemplateProperty);
        set => SetValue(CloseButtonTemplateProperty, value);
    }

    private TabControl _closeOwner;
    private ButtonBase _closeButton;
    private ButtonBase _pinButton;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Own-container tabs (authored <TabItem/>) get no PrepareContainer call, and the owner reflects selection only on
        // change - so a tab realized into the strip pulls its current state here, lighting up the initially-selected tab.
        if (this.GetVisualAncestors().OfType<TabControl>().FirstOrDefault() is { } owner)
        {
            IsSelected = owner.IsContainerSelected(this);
            IsStretched = owner.HasStretchedTab;
            // Pull the close-button config from the owner (authored + generated tabs alike) and follow later changes.
            _closeOwner = owner;
            _closeOwner.PropertyChanged += OnOwnerPropertyChanged;
            SyncCloseButton();
            SyncPinButton();
            SyncIconTemplate();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_closeOwner != null)
        {
            _closeOwner.PropertyChanged -= OnOwnerPropertyChanged;
            _closeOwner = null;
        }
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (_closeButton != null) _closeButton.Click -= OnCloseButtonClick;
        _closeButton = GetTemplateChild("PART_CloseButton") as ButtonBase;
        if (_closeButton != null) _closeButton.Click += OnCloseButtonClick;

        if (_pinButton != null) _pinButton.Click -= OnPinButtonClick;
        _pinButton = GetTemplateChild("PART_PinButton") as ButtonBase;
        if (_pinButton != null) _pinButton.Click += OnPinButtonClick;
        SyncPinButtonState();
    }


    // Owner's close config changed at runtime (e.g. ShowCloseButton toggled) -> re-sync this tab.
    private void OnOwnerPropertyChanged(object sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (e.Property == TabControl.ShowCloseButtonProperty || e.Property == TabControl.CloseButtonTemplateProperty)
            SyncCloseButton();
        else if (e.Property == TabControl.ShowPinButtonProperty || e.Property == TabControl.PinButtonTemplateProperty)
            SyncPinButton();
        else if (e.Property == TabControl.IconTemplateProperty)
            SyncIconTemplate();
        else if (e.Property == TabControl.HasStretchedTabProperty && sender is TabControl owner)
            IsStretched = owner.HasStretchedTab;
    }

    /// <summary>This tab fills its whole strip because it is the only one (see <see cref="TabControl.StretchSingleTab"/>).
    /// With nothing to choose between, the tab IS the title: the theme fills it with the accent instead of sliding an
    /// indicator under it. Set by the owner, never authored.</summary>
    public static readonly AdamantiumProperty IsStretchedProperty = AdamantiumProperty.Register(
        nameof(IsStretched), typeof(bool), typeof(TabItem), new PropertyMetadata(false));

    public bool IsStretched
    {
        get => GetValue<bool>(IsStretchedProperty);
        internal set => SetValue(IsStretchedProperty, value);
    }

    // Effective button state = owner shows close buttons AND this tab is closable; the look comes from the owner.
    private void SyncCloseButton()
    {
        var owner = _closeOwner ?? Owner;
        ShowCloseButton = owner is { ShowCloseButton: true } && IsClosable;
        CloseButtonTemplate = owner?.CloseButtonTemplate;
    }

    private void SyncPinButton()
    {
        var owner = _closeOwner ?? Owner;
        ShowPinButton = owner is { ShowPinButton: true };
        PinButtonTemplate = owner?.PinButtonTemplate;
    }

    /// <summary>The pin button: this tab changes rows. Nothing else happens here - the strip re-reads the split from the
    /// flag, which is the same path a menu command or a view-model takes.</summary>
    private void OnPinButtonClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;   // don't let the click fall through to tab selection / drag
        IsPinned = !IsPinned;
        SyncPinButtonState();
    }

    // Pinning is a STATE, not an action, so the button is a toggle and carries that state itself - which is what lets
    // its template draw the two apart (a hollow tack against a filled one). Pushed rather than bound: IsPinned can be
    // set from anywhere - the button, a menu, a view-model - and all of them must leave the button looking right.
    private void SyncPinButtonState()
    {
        if (_pinButton is ToggleButton toggle) toggle.IsChecked = IsPinned;
    }

    // One icon template for the whole strip, taken from the owner - unless this tab was given its own.
    private void SyncIconTemplate()
    {
        var owner = _closeOwner ?? Owner;
        if (owner?.IconTemplate != null) IconTemplate = owner.IconTemplate;
    }

    private void OnCloseButtonClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;   // don't let the click fall through to tab selection / drag
        Owner?.RequestClose(this);
    }

    private Vector2 _pressPos;
    private bool _pressed;
    private bool _dragging;

    private TabControl Owner => this.GetVisualAncestors().OfType<TabControl>().FirstOrDefault();

    /// <summary>Spring-load: a drag has dwelled over this tab, so activate it (same path as a click) - the body swaps in
    /// and you can drop into its content.</summary>
    public void SpringLoad() => Owner?.SelectTab(this);

    protected override void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(sender, e);
        if (!IsEnabled || e.Handled) return;   // e.Handled -> the close button (or other child) took the click
        e.Handled = true;
        Focus();
        Owner?.SelectTab(this);
        _pressPos = e.GetPosition(this);
        _pressed = true;
        _dragging = false;
        // Do NOT capture the mouse here: capturing on press swallows a plain click meant for an interactive child (the
        // close button) - the up would route to this captured tab, not the button. Capture only once a drag actually
        // starts (below), which is when we truly need to track the pointer past this tab's bounds.
    }

    protected override void OnMouseMove(object sender, MouseEventArgs e)
    {
        base.OnMouseMove(sender, e);
        if (!_pressed) return;

        // Whether the button is DOWN is the device's to answer, not ours to remember. The flag above is only where the
        // gesture started; kept as the sole authority it goes stale - pressing a folded strip re-templates the tree
        // under the cursor (the group expands), the button-up lands in the new tree and never reaches this tab, and the
        // flag stays latched for the life of the control. After that a plain hover, with nothing held down, walked
        // straight into the drag path and slid the tabs away.
        if (e.MouseDevice.LeftButton != MouseButtonState.Pressed)
        {
            AbandonDrag();
            return;
        }

        if (!_dragging && Owner is { AllowTabDrag: false }) return;

        if (!_dragging && PlatformSettings.ExceedsDragThreshold(e.GetPosition(this) - _pressPos))
        {
            _dragging = true;
            CaptureMouse();   // now the drag must keep tracking even as the pointer leaves this tab
            Owner?.BeginDrag(this, e);
        }
        if (_dragging) Owner?.UpdateDrag(this, e);
    }

    protected override void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(sender, e);
        if (_dragging) Owner?.EndDrag(this);
        AbandonDrag();
    }

    /// <summary>Forget the gesture in progress. Called when the drag ends somewhere OTHER than this tab's button-up -
    /// a tear-off, where the platform's move loop takes the button and the up never arrives here.
    /// <para>Without it these flags stay true for the life of the control: dock the pane back and the first mouse move
    /// over it, with nothing pressed, walks straight into the drag path and tears it out again.</para></summary>
    internal void AbandonDrag()
    {
        if (IsMouseCaptured) ReleaseMouseCapture();
        _pressed = false;
        _dragging = false;
    }
}
