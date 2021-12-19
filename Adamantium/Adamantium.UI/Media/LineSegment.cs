namespace Adamantium.UI.Media;

public class LineSegment : PathSegment
{
    public static readonly AdamantiumProperty PointProperty =
        AdamantiumProperty.Register(nameof(Point), typeof(Vector2), typeof(LineSegment),
            new PropertyMetadata(Vector2.Zero, PropertyMetadataOptions.AffectsMeasure));

    public Vector2 Point
    {
        get => GetValue<Vector2>(PointProperty);
        set => SetValue(PointProperty, value);
    }

    internal override Vector2[] ProcessSegment(Vector2 currentPoint)
    {
        return new[] { Point };
    }
}