using Adamantium.UI.Media;

namespace Adamantium.UI.Controls;

public class BSpline : Polyline
{
    public BSpline()
    {
        
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
    
    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        var rate = CalculatePointsLength(Points);
        var result = MathHelper.GetBSpline2(Points, (uint)rate).ToArray();
        SplineGeometry.Points = new PointsCollection(result);
            
        context.BeginDraw(this);
        
        context.DrawGeometry(Stroke, SplineGeometry, GetPen());
        context.EndDraw(this);
    }
}