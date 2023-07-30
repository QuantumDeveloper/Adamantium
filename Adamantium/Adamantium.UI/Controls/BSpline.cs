using System.Linq;
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
        var streamContext = StreamGeometry.Open();
        streamContext.BeginFigure(Points[0], true, true).BSplineTo(Points.Skip(1));
        
        context.DrawGeometry(Stroke, StreamGeometry, GetPen());
    }
}