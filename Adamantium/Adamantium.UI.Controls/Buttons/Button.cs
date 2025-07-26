using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls.Buttons;

public class Button : ContentControl
{
    public Button()
    {
        
    }
    
    public static readonly AdamantiumProperty BorderBrushProperty = AdamantiumProperty.Register(nameof(BorderBrush),
        typeof (Brush), typeof (Button),
        new PropertyMetadata(Brushes.Transparent, PropertyMetadataOptions.AffectsRender));
    
    public static readonly AdamantiumProperty CornerRadiusProperty = AdamantiumProperty.Register(nameof(CornerRadius),
        typeof (CornerRadius), typeof (Button),
        new PropertyMetadata(default(CornerRadius), PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty BorderThicknessProperty =
        AdamantiumProperty.Register(nameof(BorderThickness),
            typeof (Thickness), typeof (Button),
            new PropertyMetadata(default(Thickness), PropertyMetadataOptions.AffectsMeasure));

    public Brush BorderBrush
    {
        get => GetValue<Brush>(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => GetValue<CornerRadius>(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public Thickness BorderThickness
    {
        get => GetValue<Thickness>(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }
}