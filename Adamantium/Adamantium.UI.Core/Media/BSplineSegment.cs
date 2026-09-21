using Adamantium.Mathematics;
using Adamantium.UI.Core.Collections;

namespace Adamantium.UI.Core.Media;

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
        // Dense base, then EVEN arc-length resample (~3px) - smooth stroke, no sub-pixel bunching.
        var fine = MathHelper.GetBSpline2(points, 256);
        var count = System.Math.Clamp((int)(MathHelper.PolylineLength(fine) / 3.0), 8, 256);
        return MathHelper.ResampleByArcLength(fine, count);
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