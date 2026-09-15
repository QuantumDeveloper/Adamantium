using System;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls;

/// <summary>A WIRE between two sockets of two nodes - the thing a graph is actually made of.
/// <para>It is held by what it JOINS and not by where it is. There is nothing here to move and nothing to resize: both
/// ends are sockets, and a socket is somewhere because its node is somewhere. Drag either node and the wire follows
/// without being told, because it never knew a position to begin with - it asks, every frame, where those two sockets
/// are now.</para>
/// <para>Which is also why the nodes are asked rather than the pins: where a socket sits is a fact about the node's
/// TEMPLATE, and each theme answers it differently. A wire that worked it out for itself would be a copy of every
/// template's arithmetic, wrong the moment a theme changed a margin.</para></summary>
public class ConnectionItem : ICanvasItem
{
    private readonly ElementItem _from;
    private readonly ElementItem _to;
    private readonly WirePainter _painter = new();

    public ConnectionItem(ElementItem from, CanvasNodePin fromPin, ElementItem to, CanvasNodePin toPin)
    {
        _from = from;
        _to = to;
        FromPin = fromPin;
        ToPin = toPin;
    }

    /// <summary>The socket it leaves - an output, in a graph that runs one way.</summary>
    public CanvasNodePin FromPin { get; }

    /// <summary>The socket it arrives at.</summary>
    public CanvasNodePin ToPin { get; }

    /// <summary>The nodes at either end, so that removing a node can take its wires with it and a tool can ask whether
    /// a wire it is about to draw is already there.</summary>
    public ElementItem FromItem => _from;

    public ElementItem ToItem => _to;

    /// <summary>How thick the wire is, in WORLD units - it belongs to the drawing, so it grows with the zoom.</summary>
    public double Thickness { get; set; } = 2;

    /// <summary>What it is drawn in. Null takes the colour of the socket it LEAVES, which is what a graph editor does:
    /// a wire is the same colour as the thing flowing down it.</summary>
    public Brush Stroke { get; set; }

    public string Title => $"Wire {FromPin?.Name} - {ToPin?.Name}";

    /// <summary>NONE. Both ends are held by what they join, so there is nothing a frame could offer: a box round a wire
    /// would invite a drag that has no meaning and would fight the one that does - moving a node.</summary>
    public CanvasHandles Handles => CanvasHandles.None;

    /// <summary>A wire belongs to a GRAPH and to nothing else - there is no such thing as a wire in a drawing.</summary>
    public CanvasMode Mode => CanvasMode.Nodes;

    public Rect Bounds
    {
        get
        {
            if (Ends(out var from, out var to) is false) return new Rect();

            // The BEND is outside the straight line between the ends, so the box has to hold the control points too -
            // otherwise a wire drawn between two nodes side by side is culled the moment its ends leave the viewport
            // while its belly is still on screen.
            Bend(from, to, out var first, out var second);

            var lowX = Math.Min(Math.Min(from.X, to.X), Math.Min(first.X, second.X));
            var lowY = Math.Min(Math.Min(from.Y, to.Y), Math.Min(first.Y, second.Y));
            var highX = Math.Max(Math.Max(from.X, to.X), Math.Max(first.X, second.X));
            var highY = Math.Max(Math.Max(from.Y, to.Y), Math.Max(first.Y, second.Y));

            var half = Math.Max(Thickness, 0) / 2;

            return new Rect(lowX - half, lowY - half, highX - lowX + 2 * half, highY - lowY + 2 * half);
        }
    }

    /// <summary>Where the wire leaves and arrives, in WORLD units. False while either node has not been laid out - which
    /// is every frame before the first one, and any frame in which a node is off screen and was never arranged.</summary>
    public bool Ends(out Vector2 from, out Vector2 to)
    {
        from = to = Vector2.Zero;

        if (At(_from, FromPin) is not { } start || At(_to, ToPin) is not { } finish) return false;

        from = start;
        to = finish;
        return true;
    }

    public bool HitTest(Vector2 world, double tolerance)
    {
        if (!Ends(out var from, out var to)) return false;

        Bend(from, to, out var first, out var second);

        var reach = Math.Max(tolerance, Thickness / 2);
        var previous = from;

        // Walked rather than solved: the nearest point on a cubic is a quartic, and a wire is a few dozen pixels of
        // line that somebody is pointing at. Sixteen steps is under a pixel of error at any zoom a hand can aim at.
        for (var step = 1; step <= 16; step++)
        {
            var at = Along(from, first, second, to, step / 16.0);
            if (Distance(world, previous, at) <= reach) return true;

            previous = at;
        }

        return false;
    }

    /// <summary>Nothing. A wire is where its sockets are; moving it would mean moving them, and they belong to their
    /// nodes.</summary>
    public void Move(Vector2 worldDelta)
    {
    }

    public void Resize(Rect world)
    {
    }

    public void Render(IDrawingSession session, InfiniteCanvas canvas)
    {
        if (canvas == null || !Ends(out var from, out var to)) return;

        _painter.Draw(session, canvas, from, to, Stroke ?? FromPin?.Color ?? ToPin?.Color, Thickness);
    }

    // The two control points of the curve. A wire leaves an output SIDEWAYS and arrives at an input sideways, which is
    // what makes a graph readable: the first thing the eye follows out of a socket is the direction of flow, and a
    // straight line between two nodes one above the other says nothing about which way anything goes.
    internal static void Bend(Vector2 from, Vector2 to, out Vector2 first, out Vector2 second)
    {
        var reach = Math.Max(Math.Abs(to.X - from.X) * 0.5, 40);

        first = new Vector2(from.X + reach, from.Y);
        second = new Vector2(to.X - reach, to.Y);
    }

    // Where a socket is in WORLD units: the node is arranged into the item's own box, so a point in the node's
    // coordinates is that point from the box's corner.
    private static Vector2? At(ElementItem item, CanvasNodePin pin)
    {
        if (item?.Element is not CanvasNode node || node.Where(pin) is not { } local) return null;

        return new Vector2(item.World.X + local.X, item.World.Y + local.Y);
    }

    internal static Vector2 Along(Vector2 a, Vector2 b, Vector2 c, Vector2 d, double t)
    {
        var s = 1 - t;
        var x = s * s * s * a.X + 3 * s * s * t * b.X + 3 * s * t * t * c.X + t * t * t * d.X;
        var y = s * s * s * a.Y + 3 * s * s * t * b.Y + 3 * s * t * t * c.Y + t * t * t * d.Y;

        return new Vector2(x, y);
    }

    private static double Distance(Vector2 point, Vector2 a, Vector2 b)
    {
        var abx = b.X - a.X;
        var aby = b.Y - a.Y;
        var length = abx * abx + aby * aby;

        var t = length <= 1e-12 ? 0 : Math.Clamp(((point.X - a.X) * abx + (point.Y - a.Y) * aby) / length, 0, 1);
        var dx = point.X - (a.X + abx * t);
        var dy = point.Y - (a.Y + aby * t);

        return Math.Sqrt(dx * dx + dy * dy);
    }
}
