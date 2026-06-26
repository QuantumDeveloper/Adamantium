using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Media;

public interface IFigureSegments
{
    IFigureSegments LineTo(Vector2 point, bool isStroked = true);
    
    IFigureSegments LineTo(double x, double y, bool isStroked = true);

    IFigureSegments PolylineLineTo(IEnumerable<Vector2> points, bool isStroked = true);
      
    IFigureSegments ArcTo(Vector2 point, Size size, double rotationAngle, bool isLargeArc, SweepDirection sweepDirection, bool isStroked = true);
      
    IFigureSegments QuadraticBezierTo(Vector2 controlPoint, Vector2 point, bool isStroked = true);
      
    IFigureSegments CubicBezierTo(Vector2 controlPoint1, Vector2 controlPoint2, Vector2 point, bool isStroked = true);
      
    IFigureSegments PolyQuadraticBezierTo(IEnumerable<Vector2> points, bool isStroked = true);
      
    IFigureSegments PolyCubicBezierTo(IEnumerable<Vector2> points, bool isStroked = true);
      
    IFigureSegments BSplineTo(IEnumerable<Vector2> points, bool isStroked = true);
      
    IFigureSegments NurbsTo(IEnumerable<Vector2> points, bool isUniform, bool useCustomDegree, int degree, bool isStroked = true);

    /// <summary>Marks the current figure CLOSED (the SVG/XAML 'Z' command): its outline connects the last point back to
    /// the start. Without it a figure stays OPEN - a stroke is a polyline with end caps, not a ring - so "M..L..L" with
    /// no Z renders as a check mark / arc, not a filled-style triangle.</summary>
    IFigureSegments CloseFigure();
}