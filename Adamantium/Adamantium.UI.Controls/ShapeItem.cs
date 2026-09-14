using System;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.ProceduralGeometry.Shapes;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls;

/// <summary>A rectangle, an ellipse or a line on the plane: a box in WORLD units with a fill and an outline.
/// <para>One type for all three because they are the same thing to everything around them - something with a box that
/// can be moved, resized, hit and drawn. Which is also why a resize here is one field and not a walk: unlike a stroke,
/// a shape IS its box.</para></summary>
public class ShapeItem : ICanvasItem
{
    // The outline, kept and rebuilt only when what it is made of changes. A pen is immutable-per-change, so building one
    // per frame per shape would allocate for every shape on screen every frame - and the width DOES change on every step
    // of the zoom, which is exactly why the cached one is keyed on the width it was built for.
    private Pen _pen;
    private double _penWidth = double.NaN;
    private Brush _penBrush;
    private Rect _world;
    private int _sides = 5;

    // Where a polygon's first corner sits. Straight UP: a polygon inscribed from angle zero starts at the right-hand
    // side, so a triangle comes out lying on its side - which is not what anybody drawing a triangle means.
    private const double StartAngle = -90;

    public ShapeItem(CanvasShape shape, Rect world, Brush stroke, double thickness, Brush fill = null)
    {
        Shape = shape;
        World = Normalise(world);
        Stroke = stroke;
        Thickness = thickness;
        Fill = fill;
    }

    public CanvasShape Shape { get; }

    /// <summary>The box it occupies, in WORLD units - the shape itself, without its outline. Always the right way round:
    /// a box dragged out upwards and to the left is the same box as one dragged down and to the right, and everything
    /// downstream may assume it.</summary>
    public Rect World
    {
        get => _world;
        set => _world = Normalise(value);
    }

    /// <summary>Which way a <see cref="CanvasShape.Line"/> crosses its box. The box is normalised, so without this a
    /// line dragged up and to the right would come back leaning the other way.</summary>
    public bool Flipped { get; set; }

    public Brush Stroke { get; set; }

    public Brush Fill { get; set; }

    /// <summary>How wide the outline is, in WORLD units - it belongs to the drawing, so it grows with the zoom. The
    /// tool states it in screen pixels and converts, the same division the pen makes.</summary>
    public double Thickness { get; set; }

    /// <summary>How round a <see cref="CanvasShape.Rectangle"/>'s corners are, in WORLD units - so a rounded box keeps
    /// its shape through a zoom like everything else drawn on the plane. Nothing to the others.
    /// <para>All FOUR of them: a card with two rounded corners and two square ones is an ordinary shape, and the
    /// renderer has always drawn four independent radii - it was this that could only say one number.</para></summary>
    public CornerRadius Corner { get; set; }

    /// <summary>One corner at a time. A radius is a struct and a struct cannot be written half at a time, so an
    /// inspector line editing the top left has nothing else to bind to.</summary>
    public double CornerTopLeft
    {
        get => Corner.TopLeft;
        set => Corner = new CornerRadius(value, Corner.TopRight, Corner.BottomRight, Corner.BottomLeft);
    }

    public double CornerTopRight
    {
        get => Corner.TopRight;
        set => Corner = new CornerRadius(Corner.TopLeft, value, Corner.BottomRight, Corner.BottomLeft);
    }

    public double CornerBottomRight
    {
        get => Corner.BottomRight;
        set => Corner = new CornerRadius(Corner.TopLeft, Corner.TopRight, value, Corner.BottomLeft);
    }

    public double CornerBottomLeft
    {
        get => Corner.BottomLeft;
        set => Corner = new CornerRadius(Corner.TopLeft, Corner.TopRight, Corner.BottomRight, value);
    }

    /// <summary>How many sides a <see cref="CanvasShape.Polygon"/> has. Three at the least - two sides are a line, and
    /// a polygon that can be told to have one would draw nothing at all.</summary>
    public int Sides
    {
        get => _sides;
        set => _sides = Math.Max(3, value);
    }

    /// <summary>Where it is and how big, one number at a time - a box cannot be written half at a time, and an
    /// inspector line editing only the width has nothing else to bind to.</summary>
    public double X
    {
        get => _world.X;
        set => _world = new Rect(value, _world.Y, _world.Width, _world.Height);
    }

    public double Y
    {
        get => _world.Y;
        set => _world = new Rect(_world.X, value, _world.Width, _world.Height);
    }

    public double Width
    {
        get => _world.Width;
        set => _world = new Rect(_world.X, _world.Y, Math.Max(1e-9, value), _world.Height);
    }

    public double Height
    {
        get => _world.Height;
        set => _world = new Rect(_world.X, _world.Y, _world.Width, Math.Max(1e-9, value));
    }

    /// <summary>The box it occupies - the box itself, outline included, because the outline is drawn INSIDE it.
    /// <para>It used to be the box grown by half the thickness, on the reasoning that a pen straddles the path it
    /// follows. True of a pen and wrong for a shape somebody sized: making the outline thicker then made the shape
    /// BIGGER, and a rectangle dragged out to a size quietly outgrew it. A shape IS its box; a fatter outline eats
    /// inwards.</para></summary>
    public Rect Bounds => World;

    public void Move(Vector2 worldDelta) =>
        _world = new Rect(_world.X + worldDelta.X, _world.Y + worldDelta.Y, _world.Width, _world.Height);

    public void Resize(Rect world)
    {
        if (world.Width <= 0 || world.Height <= 0) return;

        // The box that comes in IS the shape's box - the outline lives inside it, so there is nothing to take off.
        World = Normalise(world);
    }

    public bool HitTest(Vector2 world, double tolerance)
    {
        // OUT to the tolerance and IN by the whole outline: the box is the outer edge now, so there is nothing of the
        // shape outside it, and everything of the outline is in the band just inside.
        var reach = tolerance;
        var band = Thickness + tolerance;

        switch (Shape)
        {
            case CanvasShape.Line:
            {
                var from = Flipped ? new Vector2(World.X + World.Width, World.Y) : new Vector2(World.X, World.Y);
                var to = Flipped
                    ? new Vector2(World.X, World.Y + World.Height)
                    : new Vector2(World.X + World.Width, World.Y + World.Height);

                return Distance(world, from, to) <= reach;
            }

            case CanvasShape.Ellipse:
            {
                var rx = World.Width / 2;
                var ry = World.Height / 2;
                if (rx <= 0 || ry <= 0) return false;

                var nx = (world.X - (World.X + rx)) / (rx + reach);
                var ny = (world.Y - (World.Y + ry)) / (ry + reach);
                if (nx * nx + ny * ny > 1) return false;

                // Filled, anywhere inside counts. Hollow, only the ring does - otherwise an outline drawn round half the
                // drawing would swallow every press inside it.
                if (Fill != null) return true;

                var ix = (world.X - (World.X + rx)) / Math.Max(1e-9, rx - band);
                var iy = (world.Y - (World.Y + ry)) / Math.Max(1e-9, ry - band);
                return ix * ix + iy * iy >= 1;
            }

            case CanvasShape.Polygon:
            {
                // The corners themselves, not the box: half a triangle's box is empty, and a press in that half would
                // otherwise pick it up.
                var fitted = Fit(World, Sides);
                var inside = false;
                var nearest = double.MaxValue;
                var previous = Vertex(fitted, Sides, Sides - 1);

                for (var i = 0; i < Sides; i++)
                {
                    var current = Vertex(fitted, Sides, i);

                    if (current.Y > world.Y != previous.Y > world.Y &&
                        world.X < (previous.X - current.X) * (world.Y - current.Y) /
                        (previous.Y - current.Y) + current.X)
                        inside = !inside;

                    nearest = Math.Min(nearest, Distance(world, previous, current));
                    previous = current;
                }

                if (Fill != null) return inside || nearest <= reach;
                return nearest <= (inside ? band : reach);
            }

            default:
            {
                var outer = new Rect(World.X - reach, World.Y - reach,
                    World.Width + reach * 2, World.Height + reach * 2);
                if (!Contains(outer, world)) return false;
                if (Fill != null) return true;

                var inner = new Rect(World.X + band, World.Y + band,
                    Math.Max(0, World.Width - band * 2), Math.Max(0, World.Height - band * 2));
                return !Contains(inner, world);
            }
        }
    }

    public void Render(IDrawingSession session, InfiniteCanvas canvas)
    {
        if (canvas == null) return;

        var topLeft = canvas.WorldToScreen(new Vector2(World.X, World.Y));
        var bottomRight = canvas.WorldToScreen(new Vector2(World.X + World.Width, World.Y + World.Height));
        var box = new Rect(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);

        // Never thinner than a pixel: an outline zoomed out should thin to a hair, not vanish. NO outline at all is a
        // different statement, and one somebody makes on purpose - a filled shape with no edge - so a thickness of zero
        // passes straight through instead of being floored to a hairline.
        var width = Thickness > 0 ? Math.Max(Thickness * canvas.Scale, 1.0) : 0;
        var pen = PenFor(width);

        // INSIDE the box. A pen straddles the path it is given, so the path is pulled in by half the width and the
        // outline's outer edge lands exactly on the box - which is what keeps a shape the size it was drawn at however
        // thick its outline is later made. A line has no inside, a box too small to inset keeps what it has, and with
        // no pen there is nothing to make room for.
        var half = width / 2;
        var path = pen == null || Shape == CanvasShape.Line || box.Width <= width || box.Height <= width
            ? box
            : new Rect(box.X + half, box.Y + half, box.Width - width, box.Height - width);

        switch (Shape)
        {
            case CanvasShape.Line:
                if (pen == null) return;

                session.DrawLine(
                    Flipped ? new Vector2(box.X + box.Width, box.Y) : new Vector2(box.X, box.Y),
                    Flipped ? new Vector2(box.X, box.Y + box.Height)
                        : new Vector2(box.X + box.Width, box.Y + box.Height),
                    pen);
                break;

            case CanvasShape.Ellipse:
                session.DrawEllipse(path, Fill, 0, 360, EllipseType.Sector, pen);
                break;

            case CanvasShape.Polygon:
                // Fit, not inscribe. The session puts the corners on the ELLIPSE the box holds, so a triangle asked for
                // in a square touches the top edge at one point and leaves the bottom quarter and both sides empty -
                // the shape then sits loose inside the frame that was dragged out for it. Fit hands over the bigger box
                // in which the polygon's OWN bounds land on the one that was asked for.
                session.DrawRegularPolygon(Fill, Fit(path, Sides), Sides, pen, 0, StartAngle);
                break;

            default:
                // Every radius travels with the zoom like the box does, and none is ever more than half the shorter
                // side - past that a "rounder" corner only eats the straight edges and the shape stops changing.
                var limit = Math.Min(path.Width, path.Height) / 2;
                var corners = new CornerRadius(
                    Math.Min(Corner.TopLeft * canvas.Scale, limit),
                    Math.Min(Corner.TopRight * canvas.Scale, limit),
                    Math.Min(Corner.BottomRight * canvas.Scale, limit),
                    Math.Min(Corner.BottomLeft * canvas.Scale, limit));

                if (corners.TopLeft > 0.5 || corners.TopRight > 0.5 ||
                    corners.BottomRight > 0.5 || corners.BottomLeft > 0.5)
                    session.DrawRectangle(Fill, path, corners, pen);
                else session.DrawRectangle(Fill, path, pen);
                break;
        }
    }

    private Pen PenFor(double width)
    {
        if (Stroke == null || Thickness <= 0) return null;
        if (_pen != null && ReferenceEquals(_penBrush, Stroke) && Math.Abs(_penWidth - width) < 1e-6) return _pen;

        _penBrush = Stroke;
        _penWidth = width;
        _pen = new Pen(Stroke, width, penLineJoin: PenLineJoin.Miter);

        return _pen;
    }

    // The box to hand the session so a polygon's OWN bounds land on `box`. The corners sit on the ellipse the box holds,
    // and only a shape with a corner in every quadrant fills it; the rest need a bigger ellipse, off-centre by however
    // lopsided they are. Written as the box that carries that ellipse, because that is what the session takes.
    private static Rect Fit(Rect box, int sides)
    {
        double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
        for (var i = 0; i < sides; i++)
        {
            var angle = (StartAngle + i * 360.0 / sides) * Math.PI / 180;
            var x = Math.Cos(angle);
            var y = Math.Sin(angle);

            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y);
            maxY = Math.Max(maxY, y);
        }

        var rx = box.Width / Math.Max(1e-9, maxX - minX);
        var ry = box.Height / Math.Max(1e-9, maxY - minY);
        var cx = box.X + box.Width / 2 - rx * (minX + maxX) / 2;
        var cy = box.Y + box.Height / 2 - ry * (minY + maxY) / 2;

        return new Rect(cx - rx, cy - ry, rx * 2, ry * 2);
    }

    private static Vector2 Vertex(Rect fitted, int sides, int index)
    {
        var rx = fitted.Width / 2;
        var ry = fitted.Height / 2;
        var angle = (StartAngle + index * 360.0 / sides) * Math.PI / 180;

        return new Vector2(fitted.X + rx + rx * Math.Cos(angle), fitted.Y + ry + ry * Math.Sin(angle));
    }

    // A box dragged up and to the left has a negative width, and everything that reads a box - hit tests, grips, the
    // renderer - would have to cope with that everywhere instead of here once.
    private static Rect Normalise(Rect rect) =>
        new(Math.Min(rect.X, rect.X + rect.Width), Math.Min(rect.Y, rect.Y + rect.Height),
            Math.Abs(rect.Width), Math.Abs(rect.Height));

    private static bool Contains(Rect rect, Vector2 point) =>
        point.X >= rect.X && point.X <= rect.X + rect.Width &&
        point.Y >= rect.Y && point.Y <= rect.Y + rect.Height;

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
