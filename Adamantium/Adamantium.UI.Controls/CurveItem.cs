using System;
using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls;

/// <summary>A curve through a list of points: a Bezier, a B-spline or a NURBS.
/// <para>One item for all three because to everything around it they are the same thing - points and a line through
/// them - and what differs is one call to the maths. Which kind it is can be changed on a curve already drawn, which is
/// the whole reason it is one item and not three: choosing is what a person does AFTER seeing the line.</para>
/// <para>It is reshaped by its POINTS and not by a box: it offers only the body grip, so the frame moves it and the
/// points bend it, and the two gestures do not fight.</para></summary>
public class CurveItem : ICanvasItem, ICanvasPoints
{
    // The outline, rebuilt only when what it is made of changes - a pen is immutable-per-change, so one per frame per
    // curve would allocate for every curve on screen every frame.
    private Pen _pen;
    private double _penWidth = double.NaN;
    private Brush _penBrush;
    private StreamGeometry _path;

    private readonly List<Vector2> _points = new();

    private int _degree;

    public CurveItem(CanvasCurve kind, IEnumerable<Vector2> points, Brush stroke, double thickness)
    {
        Kind = kind;
        Stroke = stroke;
        Thickness = thickness;

        if (points != null) _points.AddRange(points);
    }

    /// <summary>Which curve is drawn through the points. Settable: it is chosen by looking at the line, not before
    /// drawing it.</summary>
    public CanvasCurve Kind { get; set; }

    public IReadOnlyList<Vector2> Points => _points;

    public Brush Stroke { get; set; }

    /// <summary>How wide the line is, in WORLD units - it belongs to the drawing, so it grows with the zoom.</summary>
    public double Thickness { get; set; }

    /// <summary>The degree of a <see cref="CanvasCurve.Nurbs"/>, or zero to let it follow the number of points. Means
    /// nothing to the other two.</summary>
    public int Degree
    {
        get => _degree;
        set => _degree = Math.Max(0, value);
    }

    /// <summary>Whether a NURBS has EVEN knots. Means nothing to the other two.</summary>
    public bool IsUniform { get; set; }

    /// <summary>How many points it is drawn through - a number an inspector can show and nothing else can.</summary>
    public int Count => _points.Count;

    public string Title => $"{Kind} ({_points.Count})";

    /// <summary>The BODY only: a curve is reshaped by its points, and a box with eight grips round it would offer a
    /// second way to do it that fights the first.</summary>
    public CanvasHandles Handles => CanvasHandles.Body;

    /// <summary>What it covers: the box round its POINTS, grown by half the line.
    /// <para>The points and not the curve itself. Every one of these three curves stays inside the hull of its points,
    /// so this is a box that certainly contains the line - a little larger than it needs to be where the curve pulls
    /// away from a point, and never smaller, which is the direction that matters for drawing and picking.</para></summary>
    public Rect Bounds
    {
        get
        {
            if (_points.Count == 0) return default;

            double left = _points[0].X, right = left, top = _points[0].Y, bottom = top;
            for (var i = 1; i < _points.Count; i++)
            {
                left = Math.Min(left, _points[i].X);
                right = Math.Max(right, _points[i].X);
                top = Math.Min(top, _points[i].Y);
                bottom = Math.Max(bottom, _points[i].Y);
            }

            var half = Math.Max(Thickness, 0) / 2;
            return new Rect(left - half, top - half, right - left + half * 2, bottom - top + half * 2);
        }
    }

    public void MovePoint(int index, Vector2 world)
    {
        if (index < 0 || index >= _points.Count) return;

        _points[index] = world;
    }

    /// <summary>Adds a point to the end - what the tool does while the curve is being drawn.</summary>
    public void Add(Vector2 world) => _points.Add(world);


    public void Move(Vector2 worldDelta)
    {
        for (var i = 0; i < _points.Count; i++) _points[i] += worldDelta;
    }

    public void Resize(Rect world)
    {
        var from = Bounds;
        if (world.Width <= 0 || world.Height <= 0 || from.Width <= 0 || from.Height <= 0) return;

        var sx = world.Width / from.Width;
        var sy = world.Height / from.Height;

        for (var i = 0; i < _points.Count; i++)
        {
            _points[i] = new Vector2(
                world.X + (_points[i].X - from.X) * sx,
                world.Y + (_points[i].Y - from.Y) * sy);
        }
    }

    /// <summary>Whether a point is on the LINE - not in the box round it. The box of a curve is mostly empty, and a
    /// curve picked up by its emptiness would swallow everything under it.</summary>
    public bool HitTest(Vector2 world, double tolerance)
    {
        var along = Walk();
        if (along.Count < 2) return along.Count == 1 && Distance(world, along[0], along[0]) <= tolerance;

        var reach = tolerance + Math.Max(Thickness, 0) / 2;
        for (var i = 1; i < along.Count; i++)
        {
            if (Distance(world, along[i - 1], along[i]) <= reach) return true;
        }

        return false;
    }

    public void Render(IDrawingSession session, InfiniteCanvas canvas) => Render(session, canvas, null);

    /// <summary>Draws the curve with ONE MORE point on the end that the item does not hold - where the pointer is,
    /// while the curve is still being placed.
    /// <para>Given rather than added, because drawing runs on its own thread: a tool that added a point, drew and took
    /// it off again was racing every click, and a curve placed with six clicks came out with twenty-one points.</para>
    /// </summary>
    public void Render(IDrawingSession session, InfiniteCanvas canvas, Vector2? extra)
    {
        if (canvas == null) return;

        var count = _points.Count + (extra.HasValue ? 1 : 0);
        if (count < 2) return;

        // Never thinner than a pixel: a line zoomed out should thin to a hair, not vanish.
        var width = Math.Max(Thickness * canvas.Scale, 1.0);
        var pen = PenFor(width);
        if (pen == null) return;

        // In SCREEN coordinates, and the curve itself is built by the DRAWING rather than by the walk below: the
        // engine resamples a segment at device resolution, so the line stays smooth at any zoom, while the walk is
        // there to answer where the line IS - a question the drawing cannot be asked.
        var screen = new Vector2[count];
        for (var i = 0; i < _points.Count; i++) screen[i] = canvas.WorldToScreen(_points[i]);
        if (extra.HasValue) screen[count - 1] = canvas.WorldToScreen(extra.Value);

        _path ??= new StreamGeometry();
        var figure = _path.Open().BeginFigure(screen[0], false, false);
        var rest = new Vector2[screen.Length - 1];
        Array.Copy(screen, 1, rest, 0, rest.Length);

        switch (Kind)
        {
            case CanvasCurve.BSpline:
                figure.BSplineTo(rest);
                break;

            case CanvasCurve.Nurbs:
                figure.NurbsTo(rest, IsUniform, Degree > 0, Degree);
                break;

            default:
                // A cubic takes THREE points per span. What is left over at the end - one or two - is joined straight:
                // dropping it would make the line end somewhere the last point is not, which reads as a bug in the
                // drawing rather than as arithmetic.
                var spans = rest.Length / 3 * 3;
                if (spans > 0)
                {
                    var curved = new Vector2[spans];
                    Array.Copy(rest, curved, spans);
                    figure.PolyCubicBezierTo(curved);
                }

                for (var i = spans; i < rest.Length; i++) figure.LineTo(rest[i]);
                break;
        }

        session.DrawGeometry(null, _path, pen);
    }

    // The curve as a polyline in WORLD units - what hit-testing measures against, so what is picked is what is seen.
    private List<Vector2> Walk()
    {
        if (_points.Count < 2) return _points;

        switch (Kind)
        {
            case CanvasCurve.BSpline:
                return MathHelper.GetBSpline2(_points, 128);

            case CanvasCurve.Nurbs:
                return new List<Vector2>(MathHelper.GetNurbsCurve(_points,
                    Degree > 0 ? Degree : _points.Count - 1, IsUniform, 1.0 / 128.0));

            default:
                var walked = new List<Vector2> { _points[0] };
                var at = 1;

                while (at + 2 < _points.Count)
                {
                    walked.AddRange(MathHelper.GetCubicBezier(_points[at - 1], _points[at], _points[at + 1],
                        _points[at + 2], 32));
                    at += 3;
                }

                for (; at < _points.Count; at++) walked.Add(_points[at]);
                return walked;
        }
    }

    private Pen PenFor(double width)
    {
        if (Stroke == null || Thickness <= 0) return null;
        if (_pen != null && ReferenceEquals(_penBrush, Stroke) && Math.Abs(_penWidth - width) < 1e-6) return _pen;

        _penBrush = Stroke;
        _penWidth = width;
        _pen = new Pen(Stroke, width, penLineJoin: PenLineJoin.Round);

        return _pen;
    }

    private static double Distance(Vector2 point, Vector2 from, Vector2 to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var lengthSquared = dx * dx + dy * dy;

        var t = lengthSquared <= double.Epsilon
            ? 0
            : Math.Clamp(((point.X - from.X) * dx + (point.Y - from.Y) * dy) / lengthSquared, 0, 1);

        var px = point.X - (from.X + t * dx);
        var py = point.Y - (from.Y + t * dy);

        return Math.Sqrt(px * px + py * py);
    }
}
