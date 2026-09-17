namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>Which kind of curve a <see cref="CurveItem"/> draws through its points. One item for all three, because to
/// everything around it they are the same thing - points and a line through them - and what differs is one call.</summary>
public enum CanvasCurve
{
    /// <summary>A cubic Bezier through the points, four at a time: the curve most people mean, and the one whose
    /// handles behave the way a drawing program has taught them to expect.</summary>
    Bezier,

    /// <summary>A B-spline: the curve does not pass through its points at all, it is pulled by them. Smoother to shape,
    /// and what a long flowing line wants.</summary>
    BSpline,

    /// <summary>A NURBS: a B-spline that can say more about itself - the degree, and whether the knots are even.</summary>
    Nurbs
}
