using Adamantium.UI.Controls;

namespace Adamantium.UI.Media;

public abstract class PathSegment : AdamantiumComponent
{
    public static readonly AdamantiumProperty IsStrokedProperty =
        AdamantiumProperty.Register(nameof(IsStroked), typeof(bool), typeof(PathSegment),
            new PropertyMetadata(true, PropertyMetadataOptions.AffectsRender));

    public bool IsStroked
    {
        get => GetValue<bool>(IsStrokedProperty);
        set => SetValue(IsStrokedProperty, value);
    }
    
    internal abstract Vector2[] ProcessSegment(Vector2 currentPoint);
}