using System;
using System.Collections.Generic;

namespace Adamantium.UI.Media;

public class ArcSegment : PathSegment
{
    private int tessellationFactor = 200;

    public ArcSegment()
    {
        
    }

    public ArcSegment(
        Vector2 point, 
        Size size, 
        double rotationAngle, 
        bool isLargeArc, 
        SweepDirection sweepDirection,
        bool isStroked)
    {
        Point = point;
        Size = size;
        RotationAngle = rotationAngle;
        IsLargeArc = isLargeArc;
        SweepDirection = sweepDirection;
        IsStroked = isStroked;
    }
    
    public static readonly AdamantiumProperty PointProperty =
        AdamantiumProperty.Register(nameof(Point), typeof(Vector2), typeof(ArcSegment),
            new PropertyMetadata(Vector2.Zero, PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty IsLargeArcProperty =
        AdamantiumProperty.Register(nameof(IsLargeArc), typeof(bool), typeof(ArcSegment),
            new PropertyMetadata(false, PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty RotationAngleProperty =
        AdamantiumProperty.Register(nameof(RotationAngle), typeof(Double), typeof(ArcSegment),
            new PropertyMetadata(0d, PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty SweepDirectionProperty =
        AdamantiumProperty.Register(nameof(SweepDirection), typeof(SweepDirection), typeof(ArcSegment),
            new PropertyMetadata(SweepDirection.Clockwise, PropertyMetadataOptions.AffectsMeasure));
    
    public static readonly AdamantiumProperty SizeProperty =
        AdamantiumProperty.Register(nameof(Size), typeof(Size), typeof(ArcSegment),
            new PropertyMetadata(new Size(0, 0), PropertyMetadataOptions.AffectsMeasure));

    public Vector2 Point
    {
        get => GetValue<Vector2>(PointProperty);
        set => SetValue(PointProperty, value);
    }

    public bool IsLargeArc
    {
        get => GetValue<bool>(IsLargeArcProperty);
        set => SetValue(IsLargeArcProperty, value);
    }

    public Double RotationAngle
    {
        get => GetValue<Double>(RotationAngleProperty);
        set => SetValue(RotationAngleProperty, value);
    }

    public SweepDirection SweepDirection
    {
        get => GetValue<SweepDirection>(SweepDirectionProperty);
        set => SetValue(SweepDirectionProperty, value);
    }

    public Size Size
    {
        get => GetValue<Size>(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    internal override Vector2[] ProcessSegment(Vector2 currentPoint)
    {
        return GetArcPoints(
            currentPoint, 
            Point, 
            Size.Width, 
            Size.Height, 
            RotationAngle, 
            IsLargeArc,
            SweepDirection == SweepDirection.CounterClockwise, 
            tessellationFactor);
    }

    private Vector2[] GetArcPoints(Vector2 pt1, Vector2 pt2,
        double radiusX, double radiusY, double rotationAngle,
        bool isLargeArc, bool isCounterclockwise,
        int tolerance)
    {
        var points = new List<Vector2>();

        // Adjust for different radii and rotation angle
        var rotation = Matrix3x2.Rotation(-MathHelper.DegreesToRadians(rotationAngle));
        var scale = Matrix3x2.Scaling(radiusY / radiusX, 1);
        var transform = rotation * scale;
        pt1 = Matrix3x2.TransformPoint(transform, pt1);
        pt2 = Matrix3x2.TransformPoint(transform, pt2);

        // Get info about chord that connects both points
        var midPoint = new Vector2((pt1.X + pt2.X) / 2.0, (pt1.Y + pt2.Y) / 2.0);
        var chord = pt2 - pt1;
        var halfChord = chord.Length() / 2.0;

        if (radiusY < halfChord)
        {
          var radiusRatio = radiusY / radiusX;
          radiusY = halfChord;
          radiusX = radiusY * radiusRatio;
        }

        // Get vector from chord to center
        var chordNormal = isLargeArc == isCounterclockwise ? new Vector2(-chord.Y, chord.X) : new Vector2(chord.Y, -chord.X);

        chordNormal.Normalize();

        // Distance from chord to center 
        var centerDistance = Math.Sqrt(radiusY * radiusY - halfChord * halfChord);

        // Calculate center point
        var center = midPoint + centerDistance * chordNormal;

        // Get angles from center to the two points
        var angle1 = Math.Atan2(pt1.Y - center.Y, pt1.X - center.X);
        var angle2 = Math.Atan2(pt2.Y - center.Y, pt2.X - center.X);

        if (Math.Abs(angle2 - angle1) < Math.PI)
        {
            if (isLargeArc)
            {
                if (angle1 < angle2) angle1 += 2 * Math.PI;
                else angle2 += 2 * Math.PI;
            }
        }

        // Invert matrix for final point calculation
        transform.Invert();

        var startAngle = MathHelper.RadiansToDegrees(angle1);
        var stopAngle = MathHelper.RadiansToDegrees(angle2);
        var range = Math.Abs(startAngle - stopAngle);
        if (!isLargeArc && range > 180)
        {
            range = 360 - range;
        }
        
        float sign = 1;
        if (isCounterclockwise)
        {
            sign = -1;
        }
        
        var segmentsCount = (int)((4 * (radiusX + radiusY) * MathHelper.DegreesToRadians(range) / (2 * Math.PI)));
        if (segmentsCount > tolerance) segmentsCount = tolerance;
        
        var angle = (range / segmentsCount) * sign;
        var currentAngle = startAngle;
        
        for (var i = 0; i <= segmentsCount; i++)
        {
            var angleItem = MathHelper.DegreesToRadians(currentAngle);
            var x = center.X + (radiusY * Math.Cos(angleItem));
            var y = center.Y + (radiusY * Math.Sin(angleItem));
            
            var pt = Matrix3x2.TransformPoint(transform, new Vector2(x, y));
            
            points.Add(Vector2.Round(pt, 4));

            currentAngle += angle;
        }
        
         
        return points.ToArray();
    }
}