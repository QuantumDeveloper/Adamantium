namespace Adamantium.Mathematics;

public readonly struct CubicBezierPoint
{
    public CubicBezierPoint(Vector2 point1, Vector2 point2, Vector2 controlPoint1, Vector2 controlPoint2)
    {
        Point1 = point1;
        Point2 = point2;
        ControlPoint1 = controlPoint1;
        ControlPoint2 = controlPoint2;
    }
    
    public Vector2 ControlPoint1 { get; }

    public Vector2 ControlPoint2 { get; }

    public Vector2 Point1 { get; }

    public Vector2 Point2 { get; }
}