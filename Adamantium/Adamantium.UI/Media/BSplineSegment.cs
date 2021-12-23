using System;

namespace Adamantium.UI.Media;

public class BSplineSegment : PolylineSegment
{
    public BSplineSegment()
    {
        
    }
    
    public static readonly AdamantiumProperty SampleRateProperty =
        AdamantiumProperty.Register(nameof(SampleRate), typeof(UInt32), typeof(BSplineSegment),
            new PropertyMetadata((uint)20, PropertyMetadataOptions.AffectsMeasure));

    public UInt32 SampleRate
    {
        get => GetValue<UInt32>(SampleRateProperty);
        set => SetValue(SampleRateProperty, value);
    }
    
    internal override Vector2[] ProcessSegment(Vector2 currentPoint)
    {
        var points = new PointsCollection { currentPoint };
        points.AddRange(Points);
        return MathHelper.GetBSpline2(points, SampleRate).ToArray();
    }
}