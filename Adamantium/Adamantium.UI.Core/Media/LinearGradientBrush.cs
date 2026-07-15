using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Media;

/// <summary>A gradient painted along a straight axis from <see cref="StartPoint"/> (offset 0) to <see cref="EndPoint"/>
/// (offset 1). Points are RELATIVE to the filled bounds (0..1), so (0,0)->(1,1) is a corner-to-corner diagonal and
/// (0,0)->(1,0) a left-to-right horizontal.</summary>
public sealed class LinearGradientBrush : GradientBrush
{
    public LinearGradientBrush() { }

    public LinearGradientBrush(GradientStopCollection stops) : base(stops) { }

    // PAINT: the axis is RELATIVE to the filled bounds, so turning it re-colours the same pixels (see Brush.Opacity).
    public static readonly AdamantiumProperty StartPointProperty = AdamantiumProperty.Register(nameof(StartPoint),
        typeof(Vector2), typeof(LinearGradientBrush), new PropertyMetadata(new Vector2(0, 0), PropertyMetadataOptions.AffectsPaint));

    public static readonly AdamantiumProperty EndPointProperty = AdamantiumProperty.Register(nameof(EndPoint),
        typeof(Vector2), typeof(LinearGradientBrush), new PropertyMetadata(new Vector2(1, 1), PropertyMetadataOptions.AffectsPaint));

    public Vector2 StartPoint
    {
        get => GetValue<Vector2>(StartPointProperty);
        set { if (IsFrozen) return; SetValue(StartPointProperty, value); }
    }

    public Vector2 EndPoint
    {
        get => GetValue<Vector2>(EndPointProperty);
        set { if (IsFrozen) return; SetValue(EndPointProperty, value); }
    }

    protected override Brush CreateClone() =>
        new LinearGradientBrush(CopyStops())
        {
            StartPoint = StartPoint,
            EndPoint = EndPoint,
            SpreadMethod = SpreadMethod,
            Opacity = Opacity
        };
}
