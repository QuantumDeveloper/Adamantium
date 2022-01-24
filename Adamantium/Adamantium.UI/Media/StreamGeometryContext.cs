using System.Collections.Generic;

namespace Adamantium.UI.Media;

public class StreamGeometryContext : IFigureSegments
{
    private PathFigure figure;
    private List<PathFigure> figures = new List<PathFigure>();
    private List<MeshContour> contours = new List<MeshContour>();

    public IFigureSegments BeginFigure(Vector2 startPoint, bool isFilled, bool isClosed)
    {
        figure = new PathFigure();
        figure.StartPoint = startPoint;
        figure.IsFilled = isFilled;
        figure.IsClosed = isClosed;
        figure.Segments = new PathSegmentCollection();
        figures.Add(figure);
        return this;
    }

    IFigureSegments IFigureSegments.LineTo(Vector2 point, bool isStroked)
    {
        figure.Segments.Add(new LineSegment(point, isStroked));
        return this;
    }

    public IFigureSegments LineTo(double x, double y, bool isStroked = true)
    {
        figure.Segments.Add(new LineSegment(new Vector2(x, y), isStroked));
        return this;
    }

    IFigureSegments IFigureSegments.PolylineLineTo(IEnumerable<Vector2> points, bool isStroked)
    {
        figure.Segments.Add(new PolylineSegment(points, isStroked));
        return this;
    }

    IFigureSegments IFigureSegments.ArcTo(Vector2 point, Size size, double rotationAngle, bool isLargeArc, SweepDirection sweepDirection,
        bool isStroked)
    {
        figure.Segments.Add(new ArcSegment(point, size, rotationAngle, isLargeArc, sweepDirection, isStroked));
        return this;
    }

    IFigureSegments IFigureSegments.QuadraticBezierTo(Vector2 controlPoint, Vector2 point, bool isStroked)
    {
        figure.Segments.Add(new QuadraticBezierSegment(controlPoint, point, isStroked));
        return this;
    }

    IFigureSegments IFigureSegments.CubicBezierTo(Vector2 controlPoint1, Vector2 controlPoint2, Vector2 point, bool isStroked)
    {
        figure.Segments.Add(new CubicBezierSegment(controlPoint1, controlPoint2, point, isStroked));
        return this;
    }

    IFigureSegments IFigureSegments.PolyQuadraticBezierTo(IEnumerable<Vector2> points, bool isStroked)
    {
        figure.Segments.Add(new PolyQuadraticBezierSegment(points, isStroked));
        return this;
    }

    IFigureSegments IFigureSegments.PolyCubicBezierTo(IEnumerable<Vector2> points, bool isStroked)
    {
        figure.Segments.Add(new PolyCubicBezierSegment(points, isStroked));
        return this;
    }

    IFigureSegments IFigureSegments.BSplineTo(IEnumerable<Vector2> points, bool isStroked)
    {
        figure.Segments.Add(new BSplineSegment(points, isStroked));
        return this;
    }

    IFigureSegments IFigureSegments.NurbsTo(IEnumerable<Vector2> points, bool isUniform, bool useCustomDegree, int degree, bool isStroked)
    {
        figure.Segments.Add(new NurbsSegment(points, isUniform, useCustomDegree, degree, isStroked));
        return this;
    }

    internal void ProcessFigures()
    {
        foreach (var pathFigure in figures)
        {
            pathFigure?.ProcessSegments();
        }
        
    }

    internal MeshContour[] GetContours()
    {
        contours.Clear();
        foreach (var pathFigure in figures)
        {
            var contour = new MeshContour(pathFigure.Points);
            contours.Add(contour);
        }

        return contours.ToArray();
    }
}