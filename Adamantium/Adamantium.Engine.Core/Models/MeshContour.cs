using System.Collections.Generic;
using System.Linq;
using Adamantium.Core;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Core.Models;

public class MeshContour
{
    public Vector3[] Points { get; set; }
    
    public LineSegment2D[] Segments { get; set; }
    
    public bool IsGeometryClosed { get; set; }

    public MeshContour()
    {
        
    }
    
    public MeshContour(Vector3[] points, bool isGeometryClosed, bool generateSegments = true)
    {
        Points = points;
        IsGeometryClosed = isGeometryClosed;
        if (generateSegments)
        {
            SplitOnSegments();
        }
    }
    
    public void SetOrUpdatePoints(IEnumerable<Vector2> points, bool generateSegments = true)
    {
        SetPoints(Utilities.ToVector3(points), generateSegments);
    }

    public void SetPoints(IEnumerable<Vector3> points, bool generateSegments = true)
    {
        Points = points.ToArray();
        if (generateSegments)
        {
            SplitOnSegments();
        }
    }

    public void SplitOnSegments()
    {
        Segments = PolygonHelper.SplitOnSegments(Points, IsGeometryClosed);
    }
}