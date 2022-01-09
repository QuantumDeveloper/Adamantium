using System.Collections.Generic;

namespace Adamantium.UI.Media;

public class BSplineSegment : PolylineSegment
{
    public BSplineSegment()
    {
        
    }

    public BSplineSegment(IEnumerable<Vector2> points, bool isStroked)
    {
        Points = new PointsCollection(points);
        IsStroked = isStroked;
    }
    
    internal override Vector2[] ProcessSegment(Vector2 currentPoint)
    {
        var points = new PointsCollection { currentPoint };
        points.AddRange(Points);
        var rate = CalculatePointsLength(points);
        return MathHelper.GetBSpline2(points, (uint)rate).ToArray();
    }
    
    protected double CalculatePointsLength(PointsCollection points)
    {
        double cumulativeLength = 0;
        for (int i = 0; i < points.Count - 1; i++)
        {
            var vector = points[i + 1] - points[i];
            cumulativeLength += vector.Length();
        }

        return cumulativeLength / points.Count;
    }
}