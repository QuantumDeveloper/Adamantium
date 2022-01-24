using System.Collections.Generic;

namespace Adamantium.UI.Media;

public interface IFigureSegments
{
    IFigureSegments LineTo(Vector2 point, bool isStroked);
    
    IFigureSegments LineTo(double x, double y, bool isStroked = true);

    IFigureSegments PolylineLineTo(IEnumerable<Vector2> points, bool isStroked);
      
    IFigureSegments ArcTo(Vector2 point, Size size, double rotationAngle, bool isLargeArc, SweepDirection sweepDirection, bool isStroked);
      
    IFigureSegments QuadraticBezierTo(Vector2 controlPoint, Vector2 point, bool isStroked);
      
    IFigureSegments CubicBezierTo(Vector2 controlPoint1, Vector2 controlPoint2, Vector2 point, bool isStroked);
      
    IFigureSegments PolyQuadraticBezierTo(IEnumerable<Vector2> points, bool isStroked);
      
    IFigureSegments PolyCubicBezierTo(IEnumerable<Vector2> points, bool isStroked);
      
    IFigureSegments BSplineTo(IEnumerable<Vector2> points, bool isStroked);
      
    IFigureSegments NurbsTo(IEnumerable<Vector2> points, bool isUniform, bool useCustomDegree, int degree, bool isStroked);
}