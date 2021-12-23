using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Engine.Graphics;

namespace Adamantium.UI.Media;

public class ArcSegment : PathSegment
{
    private int tessellationFactor = 5;

    public ArcSegment()
    {
        
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
            SweepDirection == SweepDirection.Clockwise, 
            tessellationFactor);
    }

    private Vector2[] GetArcPoints(Vector2 pt1, Vector2 pt2,
        double radiusX, double radiusY, double rotationAngle,
        bool isLargeArc, bool isClockwise,
        double tolerance)
    {
        if (radiusX == 0 || radiusY == 0) return new[] { pt1, pt2 };

        // Get info about chord that connects both points
        var midPoint = (pt1 + pt2) / 2;
        var direction = pt2 - pt1;
        double halfChord = direction.Length() / 2;

        if (radiusX < halfChord)
        {
            radiusY = radiusY / radiusX * halfChord;
            radiusX = halfChord;
        }

        var radius = new Vector2(radiusX, radiusY);
        
        // Get vector from chord to center
        Vector2 vectRotated;

        if (isLargeArc && isClockwise)
            vectRotated = new Vector2(direction.Y, -direction.X);
        else
            vectRotated = new Vector2(-direction.Y, direction.X);

        vectRotated.Normalize();
        
        direction.Normalize();

        // Distance from chord to center 
        double centerDistance = Math.Sqrt(radiusY * radiusY - halfChord * halfChord);

        Vector2 center = midPoint;
        
        // Calculate center point
        //center = midPoint + centerDistance * vectRotated;

        // Get angles from center to the two points
        double angle1 = Math.Atan2(pt1.Y - center.Y, pt1.X - center.X);
        double angle2 = Math.Atan2(pt2.Y - center.Y, pt2.X - center.X);

        if (isLargeArc && (Math.Abs(angle2 - angle1) <= Math.PI))
        {
            if (angle1 < angle2)
                angle1 += 2 * Math.PI;
            else
                angle2 += 2 * Math.PI;
        }

        // Calculate number of points for polyline approximation
        int max = (int)((4 * (radius.X + radius.Y) * Math.Abs(angle2 - angle1) / (2 * Math.PI)) / tolerance);

        var points = new List<Vector2>();
        for (int i = 0; i <= max; i++)
        {
            double angle = ((max - i) * angle1 + i * angle2) / max;
            double x = center.X + radius.X * Math.Cos(angle);
            double y = center.Y + radius.Y * Math.Sin(angle);
        
            // Transform the point back
            var pt = new Vector3(x, y, 0);
            //var pt = Vector3.TransformCoordinate(new Vector3(x, y, 0), matrix);
            points.Add(Vector2.Round((Vector2)pt, 2));
        }

        var rect = Rect.FromPoints(points);
        var matrix = Matrix4x4.Translation(new Vector3(-rect.X, -rect.Bottom , 0)) * Matrix4x4.RotationX(MathHelper.DegreesToRadians(-rotationAngle)) * Matrix4x4.Translation(new Vector3(rect.X, rect.Bottom , 0));
        for (int i = 0; i < points.Count; i++)
        {
            points[i] = (Vector2)Vector3.TransformCoordinate((Vector3)points[i], matrix);
        }

        // foreach (var point in result.Points)
        // {
        //     points.Add((Vector2)point);
        // }
        
        return points.ToArray();
    }
}