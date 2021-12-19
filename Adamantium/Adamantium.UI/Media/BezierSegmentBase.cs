using System;

namespace Adamantium.UI.Media;

public abstract class BezierSegmentBase : PathSegment
{
    public static readonly AdamantiumProperty PointProperty =
        AdamantiumProperty.Register(nameof(Point), typeof(Vector2), typeof(BezierSegmentBase),
            new PropertyMetadata(Vector2.Zero, PropertyMetadataOptions.AffectsMeasure));
    
    public static readonly AdamantiumProperty SampleRateProperty =
        AdamantiumProperty.Register(nameof(SampleRate), typeof(UInt32), typeof(BezierSegmentBase),
            new PropertyMetadata((uint)20, PropertyMetadataOptions.AffectsMeasure));
    
    public Vector2 Point
    {
        get => GetValue<Vector2>(PointProperty);
        set => SetValue(PointProperty, value);
    }
    
    public UInt32 SampleRate
    {
        get => GetValue<UInt32>(SampleRateProperty);
        set => SetValue(SampleRateProperty, value);
    }
}