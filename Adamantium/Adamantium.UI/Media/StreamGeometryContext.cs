using System.Collections.Generic;

namespace Adamantium.UI.Media;

public class StreamGeometryContext : IFigureSegments
{
    private PathFigure figure;
      
    public void BeginFigure(Vector2 startPoint, bool isFilled, bool isClosed)
    {
        figure = new PathFigure();
        figure.StartPoint = startPoint;
        figure.IsFilled = isFilled;
        figure.IsClosed = isClosed;
        figure.Segments = new PathSegmentCollection();
    }

    public IFigureSegments LineTo(Vector2 point, bool isStroked)
    {
        figure.Segments.Add(new LineSegment(point, isStroked));
        return this;
    }

    public IFigureSegments PolylineLineTo(IEnumerable<Vector2> points, bool isStroked)
    {
        figure.Segments.Add(new PolylineSegment(points, isStroked));
        return this;
    }

    public IFigureSegments ArcTo(Vector2 point, Size size, double rotationAngle, bool isLargeArc, SweepDirection sweepDirection,
        bool isStroked)
    {
        figure.Segments.Add(new ArcSegment(point, size, rotationAngle, isLargeArc, sweepDirection, isStroked));
        return this;
    }

    public IFigureSegments QuadraticBezierTo(Vector2 controlPoint, Vector2 point, bool isStroked)
    {
        figure.Segments.Add(new QuadraticBezierSegment(controlPoint, point, isStroked));
        return this;
    }

    public IFigureSegments CubicBezierTo(Vector2 controlPoint1, Vector2 controlPoint2, Vector2 point, bool isStroked)
    {
        figure.Segments.Add(new CubicBezierSegment(controlPoint1, controlPoint2, point, isStroked));
        return this;
    }

    public IFigureSegments PolyQuadraticBezierTo(IEnumerable<Vector2> points, bool isStroked)
    {
        figure.Segments.Add(new PolyQuadraticBezierSegment(points, isStroked));
        return this;
    }

    public IFigureSegments PolyCubicBezierTo(IEnumerable<Vector2> points, bool isStroked)
    {
        figure.Segments.Add(new PolyCubicBezierSegment(points, isStroked));
        return this;
    }

    public IFigureSegments BSplineTo(IEnumerable<Vector2> points, bool isStroked)
    {
        figure.Segments.Add(new BSplineSegment(points, isStroked));
        return this;
    }

    public IFigureSegments NurbsTo(IEnumerable<Vector2> points, bool isUniform, bool useCustomDegree, int degree, bool isStroked)
    {
        figure.Segments.Add(new NurbsSegment(points, isUniform, useCustomDegree, degree, isStroked));
        return this;
    }

    internal void ProcessFigure()
    {
        figure?.ProcessSegments();
    }

    internal Vector2[] GetPoints()
    {
        return figure.Points.ToArray();
    }
}