using System;
using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry.Shapes;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>A trail of ink: a run of points with a width and a color.
/// <para>The points are FLOATS, and they are offsets from the stroke's own origin rather than places in the world. A
/// stroke is a tight cluster a few hundred units across, so a float holds it to far better than a pixel - while the
/// same numbers written absolutely would lose precision the further the drawing went from zero, and take twice the
/// memory to do it. Moving a stroke is then one field, not a walk over its points.</para></summary>
public class StrokeItem : ICanvasItem
{
    private readonly List<StrokePoint> _points = new();
    private Vector2F _min = new(float.MaxValue, float.MaxValue);
    private Vector2F _max = new(float.MinValue, float.MinValue);

    // The stroke's points in the canvas's coordinates, and the parameter block that carries them to the ink pass. Both
    // are kept and re-pointed rather than made per frame - a stroke being drawn hands the same array over every frame
    // and simply says a higher count.
    private Vector2F[] _screen;
    private readonly InkBrush _ink = new();

    public StrokeItem(Vector2 origin, Brush brush, double thickness)
    {
        Origin = origin;
        Brush = brush;
        Thickness = thickness;
    }

    /// <summary>A second stroke just like this one - the same ink, laid down through the same door the pen uses, so a
    /// copy is put together exactly the way the original was.</summary>
    public ICanvasItem Copy()
    {
        var copy = new StrokeItem(Origin, Brush?.Copy(), Thickness);
        foreach (var point in _points) copy.Add(new Vector2(Origin.X + point.At.X, Origin.Y + point.At.Y), point.Pressure);

        return copy;
    }

    /// <summary>Where the stroke's own coordinates start, in the world.</summary>
    public Vector2 Origin { get; set; }

    public Brush Brush { get; set; }

    /// <summary>The colour of the ink - the stroke's brush, when it is a plain one.</summary>
    public Color? Paint => (Brush as SolidColorBrush)?.Color;

    public void PaintWith(Color color) => Brush = new SolidColorBrush(color);

    /// <summary>How wide the ink is, in WORLD units - so it grows with the zoom, the way ink on paper does. What must
    /// NOT scale is the grid and the handles; those are stated in screen pixels.</summary>
    public double Thickness { get; set; }

    public IReadOnlyList<StrokePoint> Points => _points;

    /// <summary>Where it is and how big, one number at a time - what an inspector line binds to, since a box cannot be
    /// written half at a time. Said in terms of <see cref="Bounds"/>, <see cref="Move"/> and <see cref="Resize"/>, so
    /// it is the same four lines for every kind of item and a new kind can copy them.</summary>
    public double X
    {
        get => Bounds.X;
        set => Move(new Vector2(value - Bounds.X, 0));
    }

    public double Y
    {
        get => Bounds.Y;
        set => Move(new Vector2(0, value - Bounds.Y));
    }

    public double Width
    {
        get => Bounds.Width;
        set => Resize(new Rect(Bounds.X, Bounds.Y, Math.Max(1e-9, value), Bounds.Height));
    }

    public double Height
    {
        get => Bounds.Height;
        set => Resize(new Rect(Bounds.X, Bounds.Y, Bounds.Width, Math.Max(1e-9, value)));
    }

    /// <summary>How long the stroke is, because one stroke looks like another in a list and the count is the only
    /// thing that tells them apart at a glance.</summary>
    public string Title => $"Stroke ({_points.Count})";

    public Rect Bounds
    {
        get
        {
            if (_points.Count == 0) return new Rect(Origin.X, Origin.Y, 0, 0);

            var half = Thickness / 2;
            return new Rect(Origin.X + _min.X - half, Origin.Y + _min.Y - half,
                _max.X - _min.X + Thickness, _max.Y - _min.Y + Thickness);
        }
    }

    /// <summary>Adds a point, in WORLD coordinates - the stroke works out the offset itself, so nothing outside it has
    /// to know that it keeps its points relative to anything.</summary>
    public void Add(Vector2 world, float pressure = 1f) =>
        Append(new Vector2F((float)(world.X - Origin.X), (float)(world.Y - Origin.Y)), pressure);

    /// <summary>Moves the LAST point, in WORLD coordinates. This is what a line drawn point to point needs: between two
    /// clicks there is one vertex that follows the pointer, and it is moved rather than recorded - otherwise everywhere
    /// the pointer passed on the way would end up in the stroke.</summary>
    public void MoveLast(Vector2 world)
    {
        if (_points.Count == 0) return;

        _points[^1] = new StrokePoint(new Vector2F((float)(world.X - Origin.X), (float)(world.Y - Origin.Y)),
            _points[^1].Pressure);

        Rebuild();
    }

    /// <summary>Moves the whole stroke. ONE field: the points are offsets from the origin, which is the reason they are
    /// kept that way.</summary>
    public void Move(Vector2 worldDelta) => Origin += worldDelta;

    /// <summary>Scales the stroke into a new box, ink and all - a stroke made twice the size is drawn with a pen twice
    /// as wide, which is what happens to a drawing that is enlarged.</summary>
    public void Resize(Rect world)
    {
        if (_points.Count == 0 || world.Width <= 0 || world.Height <= 0) return;

        var from = Bounds;
        if (from.Width <= 0 || from.Height <= 0) return;

        // The thickness first, from how much the whole box changed - then the box the POINTS have to land in is the
        // wanted one minus that new thickness. Done the other way round the ink would be scaled by a box it had already
        // changed, and every resize would drift by half a nib.
        Thickness *= (world.Width / from.Width + world.Height / from.Height) / 2;

        var half = Thickness / 2;
        var inner = new Rect(from.X + half, from.Y + half,
            Math.Max(0, from.Width - Thickness), Math.Max(0, from.Height - Thickness));
        var target = new Rect(world.X + half, world.Y + half,
            Math.Max(0, world.Width - Thickness), Math.Max(0, world.Height - Thickness));

        // A stroke with no extent in one direction - a horizontal line - has nothing to scale there, only somewhere to
        // be put. Scaling it by a ratio of zeroes would send every point to NaN.
        var sx = inner.Width > 1e-9 ? target.Width / inner.Width : 0;
        var sy = inner.Height > 1e-9 ? target.Height / inner.Height : 0;

        for (var i = 0; i < _points.Count; i++)
        {
            var at = _points[i].At;
            var x = inner.Width > 1e-9 ? target.X + (Origin.X + at.X - inner.X) * sx : target.X;
            var y = inner.Height > 1e-9 ? target.Y + (Origin.Y + at.Y - inner.Y) * sy : target.Y;

            _points[i] = new StrokePoint(new Vector2F((float)(x - Origin.X), (float)(y - Origin.Y)),
                _points[i].Pressure);
        }

        Rebuild();
    }

    /// <summary>Rubs a round hole in the stroke and hands back what is LEFT - none, one piece, or two when the hole was
    /// punched through the middle.
    /// <para>By POINT and not by stroke, the way an ink surface has always erased: a line drawn across a page is rubbed
    /// where the eraser went, not deleted because it was touched. Which is why this returns pieces - a stroke cut in the
    /// middle is two strokes, and there is no other honest answer.</para>
    /// <para>Cut at the CROSSINGS rather than by dropping points inside the circle: the points of a finished stroke are
    /// far apart (they were thinned when the pen lifted), so dropping whole points would take out far more than the
    /// eraser covered and leave the cut ends wherever the thinning happened to have put them.</para>
    /// <returns>Whether anything was rubbed out at all.</returns></summary>
    public bool Erase(Vector2 center, double radius, List<StrokeItem> pieces)
    {
        if (_points.Count == 0 || radius <= 0) return false;

        // The radius asked for is the hole in the PAINTED stroke, and the cut is made in its centerline - so the cut has
        // to go back by half the ink's width as well. The pieces left behind are drawn with ROUND ENDS, and each of
        // those reaches half a thickness back toward the middle: cutting at the bare radius let the two caps close the
        // hole again, and once the eraser was thinner than the line they closed it completely - the thinner the eraser,
        // the less it appeared to do, until it did nothing at all.
        var reach = radius + Thickness / 2;

        var runs = new List<List<Vector2>>();
        List<Vector2> run = null;
        var cut = false;

        // A stroke of ONE point is a dot: it is either under the eraser or it is not. Its own reach is what makes it
        // visible, so that is what the eraser has to touch.
        if (_points.Count == 1)
        {
            if ((World(0) - center).Length() > reach) return false;

            cut = true;
        }

        for (var i = 1; i < _points.Count; i++)
        {
            var from = World(i - 1);
            var to = World(i);

            // Where this segment is INSIDE the circle, as the piece [enter, leave] of its length. Nothing means the
            // whole segment survives.
            var inside = Inside(from, to, center, reach);
            if (inside == null)
            {
                run ??= new List<Vector2> { from };
                run.Add(to);
                continue;
            }

            cut = true;
            var (enter, leave) = inside.Value;

            // The part BEFORE the hole closes off the run that was being built.
            if (enter > 0)
            {
                run ??= new List<Vector2> { from };
                run.Add(At(from, to, enter));
            }

            if (run is { Count: > 1 }) runs.Add(run);
            run = null;

            // ...and the part AFTER it opens a new one.
            if (leave < 1) run = new List<Vector2> { At(from, to, leave), to };
        }

        if (run is { Count: > 1 }) runs.Add(run);
        if (!cut) return false;

        foreach (var kept in runs)
        {
            var piece = new StrokeItem(kept[0], Brush, Thickness);
            foreach (var point in kept) piece.Add(point);

            pieces.Add(piece);
        }

        return true;
    }

    // The piece of a segment that falls inside a circle, as a pair of fractions of its length, or nothing when none of
    // it does. Solving |A + t(B-A) - C| = R for t, and keeping only what lies on the segment itself.
    private static (double Enter, double Leave)? Inside(Vector2 from, Vector2 to, Vector2 center, double radius)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var a = dx * dx + dy * dy;

        // A segment of no length is a point: inside or out, with nothing to cut.
        if (a <= 1e-12)
            return (from - center).Length() <= radius ? (0.0, 1.0) : null;

        var fx = from.X - center.X;
        var fy = from.Y - center.Y;
        var b = 2 * (fx * dx + fy * dy);
        var c = fx * fx + fy * fy - radius * radius;

        var discriminant = b * b - 4 * a * c;
        if (discriminant <= 0) return null;

        var root = Math.Sqrt(discriminant);
        var enter = Math.Max(0, (-b - root) / (2 * a));
        var leave = Math.Min(1, (-b + root) / (2 * a));

        return enter >= leave ? null : (enter, leave);
    }

    private static Vector2 At(Vector2 from, Vector2 to, double t) =>
        new(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t);

    /// <summary>Drops the last point - the vertex that was still following the pointer when the line was finished.
    /// </summary>
    public void RemoveLast()
    {
        if (_points.Count == 0) return;

        _points.RemoveAt(_points.Count - 1);
        Rebuild();
    }

    private void Append(Vector2F at, float pressure)
    {
        _points.Add(new StrokePoint(at, pressure));
        _min = new Vector2F(Math.Min(_min.X, at.X), Math.Min(_min.Y, at.Y));
        _max = new Vector2F(Math.Max(_max.X, at.X), Math.Max(_max.Y, at.Y));
    }

    // The box only ever GREW as points were appended, so a point that moves or leaves has to be measured again from
    // scratch: a box kept from where the pointer used to be is a box that covers where the stroke no longer is.
    private void Rebuild()
    {
        _min = new Vector2F(float.MaxValue, float.MaxValue);
        _max = new Vector2F(float.MinValue, float.MinValue);

        foreach (var point in _points)
        {
            _min = new Vector2F(Math.Min(_min.X, point.At.X), Math.Min(_min.Y, point.At.Y));
            _max = new Vector2F(Math.Max(_max.X, point.At.X), Math.Max(_max.Y, point.At.Y));
        }
    }


    /// <summary>Called when the pen is LIFTED: the recorded points are replaced by a smooth run through them.
    /// <para>Why then and not as they arrive: a hand is still moving, so a stroke can only be smoothed once it is known
    /// where it went. This is what an ink surface has always done - rough while it is being drawn, soft the moment it is
    /// finished - and it is honest about it, because the two really are different amounts of information.</para>
    /// <para>The curve goes THROUGH the recorded points (Catmull-Rom), it does not merely approach them: ink that
    /// drifted off where the pen actually went would be a different stroke drawn softly rather than the same one.</para>
    /// </summary>
    /// <summary>Called when the pen is LIFTED. <paramref name="tolerance"/> is how far the line may be moved without
    /// anyone seeing it, and <paramref name="step"/> how far apart the points of the finished stroke are - both in WORLD
    /// units, worked out by the canvas from screen ones, because both are questions about what the eye can resolve.
    /// </summary>
    public void Smooth(double tolerance, double step)
    {
        if (_points.Count < 3) return;

        var eased = Ease(_points);
        var kept = Simplify(eased, Math.Max(tolerance, 1e-6));
        var round = Round(kept, Math.Max(step, 1e-6));

        _points.Clear();
        _min = new Vector2F(float.MaxValue, float.MaxValue);
        _max = new Vector2F(float.MinValue, float.MinValue);

        foreach (var point in round) Append(point.At, point.Pressure);
    }

    // LAST, and only now: a curve through what is left. Doing this to the raw points would keep every wobble and merely
    // round it - which is the whole reason the tremor is eased and the redundant points dropped FIRST. Here there is
    // nothing left to round but the shape itself, and a long segment gets more samples than a short one, so a straight
    // run stays two points.
    private static List<StrokePoint> Round(List<StrokePoint> source, double step)
    {
        if (source.Count < 3) return source;

        var result = new List<StrokePoint>(source.Count * 3);

        for (var i = 0; i < source.Count - 1; i++)
        {
            var p0 = source[Math.Max(i - 1, 0)].At;
            var p1 = source[i].At;
            var p2 = source[i + 1].At;
            var p3 = source[Math.Min(i + 2, source.Count - 1)].At;

            var length = Math.Sqrt((p2.X - p1.X) * (p2.X - p1.X) + (p2.Y - p1.Y) * (p2.Y - p1.Y));
            var samples = Math.Clamp((int)Math.Round(length / step), 1, 24);

            for (var s = 0; s < samples; s++)
            {
                result.Add(new StrokePoint(CatmullRom(p0, p1, p2, p3, (float)s / samples), source[i].Pressure));
            }
        }

        result.Add(source[^1]);
        return result;
    }

    // Catmull-Rom: the curve passes THROUGH p1 and p2, bent by the points either side of them - so the finished stroke
    // still goes where the pen went.
    private static Vector2F CatmullRom(Vector2F p0, Vector2F p1, Vector2F p2, Vector2F p3, float t)
    {
        var t2 = t * t;
        var t3 = t2 * t;

        return new Vector2F(
            0.5f * (2 * p1.X + (-p0.X + p2.X) * t + (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * t2 +
                    (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * t3),
            0.5f * (2 * p1.Y + (-p0.Y + p2.Y) * t + (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * t2 +
                    (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * t3));
    }

    // TREMOR first: each point pulled toward the mean of its neighbours, twice. Not a curve through the points - a curve
    // through them keeps every wobble and merely rounds it. The ends stay exactly where the pen started and stopped.
    private static StrokePoint[] Ease(List<StrokePoint> source)
    {
        var current = source.ToArray();

        for (var pass = 0; pass < 2; pass++)
        {
            var next = (StrokePoint[])current.Clone();
            for (var i = 1; i < current.Length - 1; i++)
            {
                var a = current[i - 1].At;
                var b = current[i].At;
                var c = current[i + 1].At;

                next[i] = new StrokePoint(new Vector2F((a.X + 2 * b.X + c.X) * 0.25f, (a.Y + 2 * b.Y + c.Y) * 0.25f),
                    current[i].Pressure);
            }

            current = next;
        }

        return current;
    }

    // Then THIN: keep only the points the shape would miss. The furthest point from the line between the ends either
    // survives or the whole run between them is one straight piece - recursively, which is what keeps a corner sharp
    // while a gentle curve loses nine points in ten.
    private static List<StrokePoint> Simplify(StrokePoint[] source, double tolerance)
    {
        var kept = new List<StrokePoint> { source[0] };
        Walk(source, 0, source.Length - 1, tolerance, kept);
        kept.Add(source[^1]);

        return kept;
    }

    private static void Walk(StrokePoint[] source, int first, int last, double tolerance, List<StrokePoint> kept)
    {
        if (last <= first + 1) return;

        var from = new Vector2(source[first].At.X, source[first].At.Y);
        var to = new Vector2(source[last].At.X, source[last].At.Y);

        var worst = 0.0;
        var at = first;

        for (var i = first + 1; i < last; i++)
        {
            var distance = Distance(new Vector2(source[i].At.X, source[i].At.Y), from, to);
            if (distance <= worst) continue;

            worst = distance;
            at = i;
        }

        if (worst <= tolerance) return;

        Walk(source, first, at, tolerance, kept);
        kept.Add(source[at]);
        Walk(source, at, last, tolerance, kept);
    }

    public bool HitTest(Vector2 world, double tolerance)
    {
        if (_points.Count == 0) return false;

        var reach = tolerance + Thickness / 2;
        var local = new Vector2(world.X - Origin.X, world.Y - Origin.Y);

        if (_points.Count == 1) return Distance(local, At(0), At(0)) <= reach;

        for (var i = 1; i < _points.Count; i++)
        {
            if (Distance(local, At(i - 1), At(i)) <= reach) return true;
        }

        return false;
    }

    public void Render(IDrawingSession session, InfiniteCanvas canvas)
    {
        if (_points.Count == 0 || Brush == null || canvas == null) return;

        // ONE rectangle, and the ink pass turns it into a capsule per segment. What stood here built a path of
        // quadratic Beziers and stroked it, and that was wrong three times over - see the note at the top of
        // InkEffect.fx. The points are handed over in the canvas's own coordinates, into a BORROWED array: a stroke
        // being drawn hands the same one over every frame and simply says a higher count.
        if (_screen == null || _screen.Length < _points.Count) Array.Resize(ref _screen, Math.Max(64, _points.Count * 2));

        for (var i = 0; i < _points.Count; i++)
        {
            var at = canvas.WorldToScreen(World(i));
            _screen[i] = new Vector2F((float)at.X, (float)at.Y);
        }

        _ink.Points = _screen;
        _ink.Count = _points.Count;
        _ink.Color = ColorOf(Brush);
        // Never thinner than a pixel: ink zoomed out should thin to a hair, not disappear.
        _ink.Thickness = Math.Max(Thickness * canvas.Scale, 1.0);
        // The CONTENTS of the borrowed array just changed and its reference did not, so the paint has to be told - see
        // InkBrush.Revision. Without it a stroke stays baked as whatever it was when first recorded: a single dot.
        _ink.Revision++;

        // The rectangle is the stroke's own box on screen: it is what the record measures overlap and clipping by, and
        // the pass places each capsule from the points rather than from it.
        var box = Bounds;
        var topLeft = canvas.WorldToScreen(new Vector2(box.X, box.Y));
        var bottomRight = canvas.WorldToScreen(new Vector2(box.X + box.Width, box.Y + box.Height));

        session.DrawRectangle(_ink,
            new Rect(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y));
    }

    // What the ink pass paints with - a plain color, because a capsule is shaded by a color and not by a fill. A
    // gradient or a picture would say nothing about where the ink is, so anything else comes out as nothing rather than
    // being approximated into something nobody asked for.
    private static Color ColorOf(Brush brush) =>
        brush is SolidColorBrush solid ? solid.Color : new Color(0, 0, 0, 0);


    private Vector2 At(int index) => new(_points[index].At.X, _points[index].At.Y);

    private Vector2 World(int index) =>
        new(Origin.X + _points[index].At.X, Origin.Y + _points[index].At.Y);

    // Distance from a point to a segment. The degenerate case (both ends the same) falls out of the clamp, so a single
    // tap does not need a branch of its own.
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
