using System;
using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>A curve through a list of points: a Bezier, a B-spline or a NURBS.
/// <para>One item for all three because to everything around it they are the same thing - points and a line through
/// them - and what differs is one call to the maths. Which kind it is can be changed on a curve already drawn, which is
/// the whole reason it is one item and not three: choosing is what a person does AFTER seeing the line.</para>
/// <para>It is reshaped by its POINTS and not by a box: it offers only the body grip, so the frame moves it and the
/// points bend it, and the two gestures do not fight.</para></summary>
public class CurveItem : ICanvasItem, ICanvasPoints
{
    // What the curve is DRAWN with: a polyline handed to the ink pass as data. See Render for why it is not a geometry.
    private readonly InkBrush _ink = new();
    private Vector2F[] _screen;

    private readonly List<Vector2> _points = new();

    private CanvasCurve _kind;
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
    public CanvasCurve Kind
    {
        get => _kind;
        set => _kind = value;
    }

    /// <summary>A second curve just like this one.</summary>
    public ICanvasItem Copy() => new CurveItem(Kind, _points, Stroke, Thickness)
    {
        Degree = _degree,
        IsUniform = IsUniform
    };

    public IReadOnlyList<Vector2> Points => _points;

    public Brush Stroke { get; set; }

    /// <summary>How wide the line is, in WORLD units - it belongs to the drawing, so it grows with the zoom.</summary>
    public double Thickness { get; set; }

    /// <summary>The degree of the curve. A <see cref="CanvasCurve.Nurbs"/> is ASKED for one, and zero lets it follow
    /// the number of points; a <see cref="CanvasCurve.Bezier"/> only REPORTS one, because a Bezier through N+1 points
    /// is of degree N and can be nothing else. Means nothing to a B-spline.</summary>
    public int Degree
    {
        // A Bezier's degree is COUNTED, not chosen: a curve through N+1 points is of degree N, and there is no other
        // answer - one point fewer or more is a different curve. Choosing it would mean either putting anchors along
        // the line, which is not what a Bezier is, or quietly leaving points out.
        get => _kind == CanvasCurve.Bezier ? Math.Max(1, _points.Count - 1) : _degree;
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
        if (count < 2 || Stroke is not SolidColorBrush solid || Thickness <= 0) return;

        // Never thinner than a pixel: a line zoomed out should thin to a hair, not vanish.
        var width = Math.Max(Thickness * canvas.Scale, 1.0);

        // The control points in SCREEN coordinates - the curve is then walked in the same units, so how finely it is
        // sampled follows how big it actually is on screen rather than how big it is on the plane.
        var control = new List<Vector2>(count);
        for (var i = 0; i < _points.Count; i++) control.Add(canvas.WorldToScreen(_points[i]));
        if (extra.HasValue) control.Add(canvas.WorldToScreen(extra.Value));

        var along = Walk(control, Kind, Degree, IsUniform, StepsFor(control));
        if (along.Count < 2) return;

        // INK, and not a geometry, and this is the whole point of the class's drawing. Stroked as a path it cost the
        // frame and cost more the longer the application ran: a geometry is cached by its CONTENT, this one is built
        // in screen coordinates and so has different content after every pan and every turn of the wheel, and that
        // cache is never emptied and is walked once per frame. One curve on the plane was enough to bring the whole
        // thing to a crawl.
        //
        // The ink pass has none of that: the points ARE the parameter block, one instance draws the whole polyline,
        // and nothing is built, cached or left behind. It is the pass a pen stroke already goes through.
        if (_screen == null || _screen.Length < along.Count)
            Array.Resize(ref _screen, Math.Max(64, along.Count * 2));

        var lowX = double.MaxValue;
        var lowY = double.MaxValue;
        var highX = double.MinValue;
        var highY = double.MinValue;

        for (var i = 0; i < along.Count; i++)
        {
            _screen[i] = new Vector2F((float)along[i].X, (float)along[i].Y);

            if (along[i].X < lowX) lowX = along[i].X;
            if (along[i].Y < lowY) lowY = along[i].Y;
            if (along[i].X > highX) highX = along[i].X;
            if (along[i].Y > highY) highY = along[i].Y;
        }

        _ink.Points = _screen;
        _ink.Count = along.Count;
        _ink.Color = solid.Color;
        _ink.Thickness = width;

        // The CONTENTS of the borrowed array just changed and its reference did not, so the paint has to be told - see
        // InkBrush.Revision. Without it the curve stays baked wherever it first was.
        _ink.Revision++;

        // The rectangle is the curve's own box on screen: what the record measures overlap and clipping by. The pass
        // places the line from the points rather than from it.
        var half = width / 2 + 1;

        session.DrawRectangle(_ink, new Rect(lowX - half, lowY - half,
            highX - lowX + 2 * half, highY - lowY + 2 * half));
    }

    // How finely to sample, from how long the line through the control points is ON SCREEN: about a sample every four
    // pixels, floored so a short curve is still a curve and capped so a curve dragged across a wall of monitors does
    // not hand the pass a polyline nobody can see the detail of.
    private static int StepsFor(List<Vector2> control)
    {
        double reach = 0;
        for (var i = 1; i < control.Count; i++) reach += (control[i] - control[i - 1]).Length();

        return (int)Math.Clamp(reach / 4, 32, 512);
    }

    // The curve as a polyline - what is DRAWN and what hit-testing measures against, in whatever units it was handed.
    // One walk for both, so what is picked is exactly what is seen.
    private static List<Vector2> Walk(List<Vector2> points, CanvasCurve kind, int degree, bool uniform, int steps)
    {
        if (points.Count < 2) return points;

        switch (kind)
        {
            case CanvasCurve.BSpline:
                return MathHelper.GetBSpline2(points, (uint)steps);

            case CanvasCurve.Nurbs:
                return new List<Vector2>(MathHelper.GetNurbsCurve(points,
                    degree > 0 ? degree : points.Count - 1, uniform, 1.0 / steps));

            default:
                // ONE Bezier over ALL the points, and that is what makes it a Bezier: every point but the first and the
                // last PULLS the line without being on it.
                //
                // A CHAIN of spans stood here, with the degree of a span chosen in the inspector, and it was wrong on
                // exactly that point - the place where one span ends and the next begins lies ON the curve, so a curve
                // of three cubic spans had two of its interior points sitting on the line and a corner at each of them.
                // A Bezier has no interior anchors; a line that is anchored at points along its length is a spline, and
                // both of those are in the list beside this one.
                //
                // So the degree is not a choice: a Bezier through N+1 points is of degree N, and the way to ask for a
                // quadratic is to draw it with three points. See CurveItem.Degree.
                return MathHelper.GetBezier(points, (uint)steps);
        }
    }

    // The curve in WORLD units, sampled finely enough that picking it agrees with seeing it at any zoom.
    private List<Vector2> Walk() => Walk(_points, Kind, Degree, IsUniform, 128);

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
