using System.Linq;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;

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

    public TabItem()
    {
        Focusable = true;
    }

    /// <summary>The tab-strip label - a string or any UI content. Distinct from <see cref="ContentControl.Content"/>,
    /// which is the body shown when the tab is selected.</summary>
    public object Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
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

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Own-container tabs (authored <TabItem/>) get no PrepareContainer call, and the owner reflects selection only on
        // change - so a tab realized into the strip pulls its current state here, lighting up the initially-selected tab.
        if (this.GetVisualAncestors().OfType<TabControl>().FirstOrDefault() is { } owner)
            IsSelected = owner.IsContainerSelected(this);
    }

    private const double DragThreshold = 4;   // px moved before a press becomes a reorder drag
    private Vector2 _pressPos;
    private bool _dragging;

    private TabControl Owner => this.GetVisualAncestors().OfType<TabControl>().FirstOrDefault();

    protected override void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(sender, e);
        if (!IsEnabled) return;
        e.Handled = true;
        Focus();
        Owner?.SelectTab(this);
        _pressPos = e.GetPosition(this);
        _dragging = false;
        CaptureMouse();   // so the drag keeps tracking even as the pointer leaves this tab
    }

    protected override void OnMouseMove(object sender, MouseEventArgs e)
    {
        base.OnMouseMove(sender, e);
        if (!IsMouseCaptured) return;
        if (!_dragging && (e.GetPosition(this) - _pressPos).Length() > DragThreshold)
        {
            _dragging = true;
            Owner?.BeginDrag(this, e);
        }
        if (_dragging) Owner?.UpdateDrag(this, e);
    }

    protected override void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(sender, e);
        if (_dragging) Owner?.EndDrag(this);
        if (IsMouseCaptured) ReleaseMouseCapture();
        _dragging = false;
    }
}
