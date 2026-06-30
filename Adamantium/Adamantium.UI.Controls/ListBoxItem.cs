using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls;

/// <summary>
/// A selectable item container in a <see cref="ListBox"/>. Carries the selectable chrome (border / corner / padding) and
/// the hover/pressed/selected brushes the default template's triggers project, plus its <see cref="IsSelected"/> /
/// <see cref="IsPressed"/> visual state. Pressing it selects it in the owning ListBox. Like all item containers it is
/// recycled, so its selected state is driven BY the ListBox (set in PrepareContainer / on selection change), never
/// stored only here.
/// </summary>
public class ListBoxItem : ContentControl
{
    public static readonly AdamantiumProperty IsSelectedProperty = AdamantiumProperty.Register(nameof(IsSelected),
        typeof(bool), typeof(ListBoxItem), new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty IsPressedProperty = AdamantiumProperty.Register(nameof(IsPressed),
        typeof(bool), typeof(ListBoxItem), new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty BorderBrushProperty = AdamantiumProperty.Register(nameof(BorderBrush),
        typeof(Brush), typeof(ListBoxItem), new PropertyMetadata(Brushes.Transparent, PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty BorderThicknessProperty = AdamantiumProperty.Register(nameof(BorderThickness),
        typeof(Thickness), typeof(ListBoxItem), new PropertyMetadata(default(Thickness), PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty CornerRadiusProperty = AdamantiumProperty.Register(nameof(CornerRadius),
        typeof(CornerRadius), typeof(ListBoxItem), new PropertyMetadata(default(CornerRadius), PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty PaddingProperty = AdamantiumProperty.Register(nameof(Padding),
        typeof(Thickness), typeof(ListBoxItem),
        new PropertyMetadata(default(Thickness), PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));

    // State brushes the default template's triggers project onto the chrome (hover / pressed / selected). Exposing them
    // as properties lets ONE template serve every variant - the theme just sets these. Null = no change in that state.
    public static readonly AdamantiumProperty BackgroundPointerOverProperty = AdamantiumProperty.Register(
        nameof(BackgroundPointerOver), typeof(Brush), typeof(ListBoxItem), new PropertyMetadata(default(Brush)));

    public static readonly AdamantiumProperty BackgroundPressedProperty = AdamantiumProperty.Register(
        nameof(BackgroundPressed), typeof(Brush), typeof(ListBoxItem), new PropertyMetadata(default(Brush)));

    public static readonly AdamantiumProperty BackgroundSelectedProperty = AdamantiumProperty.Register(
        nameof(BackgroundSelected), typeof(Brush), typeof(ListBoxItem), new PropertyMetadata(default(Brush)));

    public static readonly AdamantiumProperty ForegroundSelectedProperty = AdamantiumProperty.Register(
        nameof(ForegroundSelected), typeof(Brush), typeof(ListBoxItem), new PropertyMetadata(default(Brush)));

    static ListBoxItem()
    {
        // A list item has NO background by default - only the hover/pressed/selected states paint one (via the theme
        // triggers). So a rest item creates no fill render unit and no analytic-AA fringe: ~60 transparent-fill fringes
        // in a scrolled wrap list were exhausting GPU memory. Hit-testing is bounds-based, so the whole row stays
        // clickable without a background brush. (Control's default is Brushes.Transparent - a SolidColorBrush, which
        // would build a fringe.)
        BackgroundProperty.OverrideMetadata(typeof(ListBoxItem),
            new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));
    }

    public ListBoxItem()
    {
        Focusable = true;
    }

    public bool IsSelected
    {
        get => GetValue<bool>(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public bool IsPressed
    {
        get => GetValue<bool>(IsPressedProperty);
        private set => SetValue(IsPressedProperty, value);
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

    public Brush BackgroundPointerOver
    {
        get => GetValue<Brush>(BackgroundPointerOverProperty);
        set => SetValue(BackgroundPointerOverProperty, value);
    }

    public Brush BackgroundPressed
    {
        get => GetValue<Brush>(BackgroundPressedProperty);
        set => SetValue(BackgroundPressedProperty, value);
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

    protected override void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(sender, e);
        if (!IsEnabled) return;
        e.Handled = true;
        Focus();
        IsPressed = true;
        // Select on press (WPF semantics). Reach the owner by walking up - works for both generated containers and a
        // ListBoxItem authored directly in markup (an "item is its own container"), without a back-reference to maintain.
        // Use the modifiers carried BY THE EVENT (captured on the OS message thread when it was raised) - input handlers
        // run later on the loop thread, so re-reading Keyboard.Modifiers there misses the Ctrl/Shift that was held.
        this.GetVisualAncestors().OfType<ListBox>().FirstOrDefault()?.SelectFromContainer(this, e.Modifiers);
    }

    protected override void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(sender, e);
        e.Handled = true;
        IsPressed = false;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        IsPressed = false;
    }
}
