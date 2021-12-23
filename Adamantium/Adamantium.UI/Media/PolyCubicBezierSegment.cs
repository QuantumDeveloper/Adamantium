using System.Collections.Generic;
using System.Collections.Specialized;
using Adamantium.UI.Controls;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI.Media;

public class PolyCubicBezierSegment : PathSegment
{
    public static readonly AdamantiumProperty PointsProperty =
        AdamantiumProperty.Register(nameof(Points), typeof(PointsCollection), typeof(PolylineSegment),
            new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure, PointsChangedCallback));

    private static void PointsChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is PolyCubicBezierSegment segment)
        {
            if (e.OldValue is PointsCollection collection1) collection1.CollectionChanged -= segment.PointsOnCollectionChanged;
            
            if (e.NewValue is PointsCollection collection2) collection2.CollectionChanged += segment.PointsOnCollectionChanged;
        }
    }
    
    public PointsCollection Points
    {
        get => GetValue<PointsCollection>(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    private void PointsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RaiseComponentUpdated();
    }
    
    internal override Vector2[] ProcessSegment(Vector2 currentPoint)
    {
        var bezierPoints = new List<CubicBezierPoint>();
        for (int i = 0; i < Points.Count; i+=3)
        {
            var c1 = Points[i];
            var c2 = Points[i + 1];
            var p3 = Points[i + 2];
            bezierPoints.Add(new CubicBezierPoint(currentPoint, p3, c1, c2));
            currentPoint = p3;
        }

        var points = new List<Vector2>();
        foreach (var bezierPoint in bezierPoints)
        {
            var result = MathHelper.GetCubicBezier(bezierPoint.Point1, bezierPoint.ControlPoint1, bezierPoint.ControlPoint2, bezierPoint.Point2, 20);
            points.AddRange(result);
        }

        return points.ToArray();
    }
}