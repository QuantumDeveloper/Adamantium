using System;
using Adamantium.Mathematics;
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

    public Rect Bounds
    {
        get
        {
            var half = Thickness / 2;
            return new Rect(World.X - half, World.Y - half, World.Width + Thickness, World.Height + Thickness);
        }
    }

    public void Move(Vector2 worldDelta) =>
        _world = new Rect(_world.X + worldDelta.X, _world.Y + worldDelta.Y, _world.Width, _world.Height);

    public void Resize(Rect world)
    {
        if (world.Width <= 0 || world.Height <= 0) return;

        // The box that comes in is the BOUNDS - outline included, because that is what a grip was dragged around. What
        // the shape keeps is the box inside it, or a shape with a fat outline would creep outwards on every resize.
        var half = Thickness / 2;
        World = Normalise(new Rect(world.X + half, world.Y + half,
            Math.Max(0, world.Width - Thickness), Math.Max(0, world.Height - Thickness)));
    }

    public bool HitTest(Vector2 world, double tolerance)
    {
        var reach = tolerance + Thickness / 2;

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

                var ix = (world.X - (World.X + rx)) / Math.Max(1e-9, rx - reach);
                var iy = (world.Y - (World.Y + ry)) / Math.Max(1e-9, ry - reach);
                return ix * ix + iy * iy >= 1;
            }

            default:
            {
                var outer = new Rect(World.X - reach, World.Y - reach,
                    World.Width + reach * 2, World.Height + reach * 2);
                if (!Contains(outer, world)) return false;
                if (Fill != null) return true;

                var inner = new Rect(World.X + reach, World.Y + reach,
                    Math.Max(0, World.Width - reach * 2), Math.Max(0, World.Height - reach * 2));
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

        // Never thinner than a pixel: an outline zoomed out should thin to a hair, not vanish.
        var pen = PenFor(Math.Max(Thickness * canvas.Scale, 1.0));

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
                session.DrawEllipse(box, Fill, 0, 360, EllipseType.Sector, pen);
                break;

            default:
                session.DrawRectangle(Fill, box, pen);
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
