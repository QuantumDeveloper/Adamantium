namespace Adamantium.Mathematics;

public readonly struct QuadraticBezierPoint
{
    public QuadraticBezierPoint(Vector2 point1, Vector2 point2, Vector2 controlPoint)
    {
        Point1 = point1;
        Point2 = point2;
        ControlPoint = controlPoint;
    }
    
    public Vector2 ControlPoint { get; }

    public Vector2 Point1 { get; }

    public Vector2 Point2 { get; }
}