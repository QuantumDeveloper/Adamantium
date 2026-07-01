using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls;

/// <summary>
/// The themed card that hosts hover-tooltip content (a string or any UI element), shown by <see cref="ToolTipService"/>.
/// Everything visible - background / border / corner / padding / text colour and size - is driven by the active theme's
/// <c>ToolTip</c> style through {ResourceReference}/{ThemeResource}, so it restyles live on a theme or accent change
/// instead of being hard-coded. A string is rendered by the template's ContentPresenter (which template-binds Foreground
/// and FontSize); a UI element is shown as-is.
/// </summary>
public class ToolTip : ContentControl
{
    public static readonly AdamantiumProperty BorderBrushProperty = AdamantiumProperty.Register(nameof(BorderBrush),
        typeof(Brush), typeof(ToolTip), new PropertyMetadata(Brushes.Transparent, PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty BorderThicknessProperty = AdamantiumProperty.Register(nameof(BorderThickness),
        typeof(Thickness), typeof(ToolTip), new PropertyMetadata(default(Thickness), PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty CornerRadiusProperty = AdamantiumProperty.Register(nameof(CornerRadius),
        typeof(CornerRadius), typeof(ToolTip), new PropertyMetadata(default(CornerRadius), PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty PaddingProperty = AdamantiumProperty.Register(nameof(Padding),
        typeof(Thickness), typeof(ToolTip),
        new PropertyMetadata(default(Thickness), PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));

    public static readonly AdamantiumProperty FontSizeProperty = AdamantiumProperty.Register(nameof(FontSize),
        typeof(double), typeof(ToolTip), new PropertyMetadata(12.0, PropertyMetadataOptions.AffectsMeasure));

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
}
