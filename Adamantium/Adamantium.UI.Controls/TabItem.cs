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
public class TabItem : ContentControl, ISelectable
{
    public static readonly AdamantiumProperty HeaderProperty = AdamantiumProperty.Register(nameof(Header),
        typeof(object), typeof(TabItem), new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure));

    // How a data Header is projected into the strip. The template's header presenter binds these; the owning TabControl
    // flows its ItemTemplate/ItemTemplateSelector here for generated (data-bound) tabs, so a header VM renders as a
    // proper visual (e.g. a TextBlock bound to its Header) instead of ToString(). Null = the header hosts as-is.
    public static readonly AdamantiumProperty HeaderTemplateProperty = AdamantiumProperty.Register(nameof(HeaderTemplate),
        typeof(DataTemplate), typeof(TabItem), new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty HeaderTemplateSelectorProperty = AdamantiumProperty.Register(nameof(HeaderTemplateSelector),
        typeof(DataTemplateSelector), typeof(TabItem), new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty IsSelectedProperty = AdamantiumProperty.Register(nameof(IsSelected),
        typeof(bool), typeof(TabItem), new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty BorderBrushProperty = AdamantiumProperty.Register(nameof(BorderBrush),
        typeof(Brush), typeof(TabItem), new PropertyMetadata(Brushes.Transparent, PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty BorderThicknessProperty = AdamantiumProperty.Register(nameof(BorderThickness),
        typeof(Thickness), typeof(TabItem), new PropertyMetadata(default(Thickness), PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty CornerRadiusProperty = AdamantiumProperty.Register(nameof(CornerRadius),
        typeof(CornerRadius), typeof(TabItem), new PropertyMetadata(default(CornerRadius), PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty PaddingProperty = AdamantiumProperty.Register(nameof(Padding),
        typeof(Thickness), typeof(TabItem),
        new PropertyMetadata(default(Thickness), PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));

    public static readonly AdamantiumProperty FontSizeProperty = AdamantiumProperty.Register(nameof(FontSize),
        typeof(double), typeof(TabItem), new PropertyMetadata(13.0, PropertyMetadataOptions.AffectsMeasure));

    // State brushes the default template's triggers project onto the tab chrome (hover / selected). Exposed as properties
    // so ONE template serves every state - the theme just sets these. Null = no change in that state.
    public static readonly AdamantiumProperty BackgroundPointerOverProperty = AdamantiumProperty.Register(
        nameof(BackgroundPointerOver), typeof(Brush), typeof(TabItem), new PropertyMetadata(default(Brush)));

    public static readonly AdamantiumProperty BackgroundSelectedProperty = AdamantiumProperty.Register(
        nameof(BackgroundSelected), typeof(Brush), typeof(TabItem), new PropertyMetadata(default(Brush)));

    public static readonly AdamantiumProperty ForegroundSelectedProperty = AdamantiumProperty.Register(
        nameof(ForegroundSelected), typeof(Brush), typeof(TabItem), new PropertyMetadata(default(Brush)));

    // Close button (see TabControl.ShowCloseButton). IsClosable is the per-tab opt-out (author it False to keep a pinned
    // tab open). ShowCloseButton + CloseButtonTemplate are EFFECTIVE values pulled from the owning TabControl on attach
    // (so authored + generated tabs behave alike); the tab template binds the button's visibility/look to them.
    public static readonly AdamantiumProperty IsClosableProperty = AdamantiumProperty.Register(nameof(IsClosable),
        typeof(bool), typeof(TabItem), new PropertyMetadata(true, OnCloseConfigChanged));

    public static readonly AdamantiumProperty ShowCloseButtonProperty = AdamantiumProperty.Register(nameof(ShowCloseButton),
        typeof(bool), typeof(TabItem), new PropertyMetadata(false));

    public static readonly AdamantiumProperty CloseButtonTemplateProperty = AdamantiumProperty.Register(nameof(CloseButtonTemplate),
        typeof(ControlTemplate), typeof(TabItem), new PropertyMetadata(null));

    private static void OnCloseConfigChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
        => ((TabItem)a).SyncCloseButton();

    static TabItem()
    {
        // A tab is a keyboard-focus target (arrow-key navigation between tabs) - opt in, since the base default is now
        // false. Metadata priority, so a {Binding}/Style/Trigger can still override it.
        FocusableProperty.OverrideMetadata(typeof(TabItem), new PropertyMetadata(true));
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

    public Brush BorderBrush
    {
        get => GetValue<Brush>(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public Thickness BorderThickness
    {
        get => GetValue<Thickness>(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => GetValue<CornerRadius>(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public Thickness Padding
    {
        get => GetValue<Thickness>(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    public double FontSize
    {
        get => GetValue<double>(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
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

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Own-container tabs (authored <TabItem/>) get no PrepareContainer call, and the owner reflects selection only on
        // change - so a tab realized into the strip pulls its current state here, lighting up the initially-selected tab.
        if (this.GetVisualAncestors().OfType<TabControl>().FirstOrDefault() is { } owner)
        {
            IsSelected = owner.IsContainerSelected(this);
            // Pull the close-button config from the owner (authored + generated tabs alike) and follow later changes.
            _closeOwner = owner;
            _closeOwner.PropertyChanged += OnOwnerPropertyChanged;
            SyncCloseButton();
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
    }

    // Owner's close config changed at runtime (e.g. ShowCloseButton toggled) -> re-sync this tab.
    private void OnOwnerPropertyChanged(object sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (e.Property == TabControl.ShowCloseButtonProperty || e.Property == TabControl.CloseButtonTemplateProperty)
            SyncCloseButton();
    }

    // Effective button state = owner shows close buttons AND this tab is closable; the look comes from the owner.
    private void SyncCloseButton()
    {
        var owner = _closeOwner ?? Owner;
        ShowCloseButton = owner is { ShowCloseButton: true } && IsClosable;
        CloseButtonTemplate = owner?.CloseButtonTemplate;
    }

    private void OnCloseButtonClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;   // don't let the click fall through to tab selection / drag
        Owner?.RequestClose(this);
    }

    private const double DragThreshold = 4;   // px moved before a press becomes a reorder drag
    private Vector2 _pressPos;
    private bool _pressed;
    private bool _dragging;

    private TabControl Owner => this.GetVisualAncestors().OfType<TabControl>().FirstOrDefault();

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
        if (!_dragging && (e.GetPosition(this) - _pressPos).Length() > DragThreshold)
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
        if (IsMouseCaptured) ReleaseMouseCapture();
        _pressed = false;
        _dragging = false;
    }
}
