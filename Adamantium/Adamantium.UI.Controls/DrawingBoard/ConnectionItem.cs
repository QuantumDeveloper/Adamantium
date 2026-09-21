using System;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls.DrawingBoard;

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

    /// <summary>NO PLACE OF ITS OWN: both ends are sockets, and a wire is wherever they are. Nothing that arranges the
    /// plane may move it, nor count the room it covers as taken.</summary>
    public bool IsPlaced => false;

    /// <summary>Where it stands in paint order - stamped by the scene. See ICanvasItem.Order.</summary>
    public int Order { get; set; }

    public Rect Bounds
    {
        get
        {
            if (Ends(out var from, out var to) is false) return new Rect();

            // The BEND is outside the straight line between the ends, so the box has to hold the control points too -
            // otherwise a wire drawn between two nodes side by side is culled the moment its ends leave the viewport
            // while its belly is still on screen. The bend it has actually TAKEN, which may be a detour round something
            // in the way.
            Taken(from, to, out var first, out var second);

            var box = Box(from, to, first, second);
            var half = Math.Max(Thickness, 0) / 2;

            return new Rect(box.X - half, box.Y - half, box.Width + 2 * half, box.Height + 2 * half);
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

    // The bend the wire has ACTUALLY taken - the detour it worked out when it was last drawn, or the plain one before
    // the first frame. Asked by the box and the hit test, which have no canvas to work a detour out from and must in
    // any case agree with what is on the screen.
    private void Taken(Vector2 from, Vector2 to, out Vector2 first, out Vector2 second)
    {
        if (_routed)
        {
            first = _first;
            second = _second;
            return;
        }

        Bend(from, to, out first, out second);
    }

    public bool HitTest(Vector2 world, double tolerance)
    {
        if (!Ends(out var from, out var to)) return false;

        Taken(from, to, out var first, out var second);

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

        // WORKED OUT HERE and remembered: drawing is the one moment a wire has a canvas to look at, and the box and the
        // hit test have to agree with the line that was actually put on the screen.
        Route(canvas, from, to, out _first, out _second);
        _routed = true;

        _painter.Draw(session, canvas, from, _first, _second, to, Stroke ?? FromPin?.Color ?? ToPin?.Color, Thickness);
    }

    // The two control points of the curve. A wire leaves an output SIDEWAYS and arrives at an input sideways, which is
    // what makes a graph readable: the first thing the eye follows out of a socket is the direction of flow, and a
    // straight line between two nodes one above the other says nothing about which way anything goes.
    /// <summary>Whether this wire ends on a socket - either end of it.</summary>
    public bool Holds(CanvasNodePin pin) => pin != null && (ReferenceEquals(FromPin, pin) || ReferenceEquals(ToPin, pin));

    /// <summary>Whether this wire is fastened to a node - either end of it.</summary>
    public bool Holds(ElementItem item) => item != null && (ReferenceEquals(_from, item) || ReferenceEquals(_to, item));

    /// <summary>The FIRST wire fastened to a socket, or null - the oldest of them, since the scene keeps what was put
    /// on it in order. What pulling a wire back off a socket asks for, and what a full socket loses.</summary>
    public static ConnectionItem Into(ICanvasScene scene, CanvasNodePin pin)
    {
        if (scene == null || pin == null) return null;

        foreach (var item in scene.ItemsIn(Everywhere))
        {
            if (item is ConnectionItem wire && wire.Holds(pin)) return wire;
        }

        return null;
    }

    /// <summary>How many wires are fastened to it - what says whether it is full.</summary>
    public static int CountInto(ICanvasScene scene, CanvasNodePin pin)
    {
        if (scene == null || pin == null) return 0;

        var count = 0;
        foreach (var item in scene.ItemsIn(Everywhere))
        {
            if (item is ConnectionItem wire && wire.Holds(pin)) count++;
        }

        return count;
    }

    /// <summary>Makes room on a socket for one more wire: while it holds as many as it takes, the oldest goes. Nothing
    /// happens to a socket that takes as many as come.</summary>
    public static void MakeRoom(ICanvasScene scene, CanvasNodePin pin)
    {
        if (scene == null || pin == null || pin.Capacity <= 0) return;

        while (CountInto(scene, pin) >= pin.Capacity && Into(scene, pin) is { } oldest)
        {
            Cut(scene, oldest);
        }
    }

    /// <summary>Takes a wire out of the scene and says so on the sockets it was fastened to.
    /// <para>ASKED OF THE SCENE and not remembered on the socket: a socket is "connected" when something is joined to
    /// it, and the only thing that knows is the list of wires. A flag kept by hand went stale the first time a wire was
    /// removed some other way - by deleting the node at its far end - and left a socket drawn as taken with nothing in
    /// it.</para></summary>
    public static void Cut(ICanvasScene scene, ConnectionItem wire)
    {
        if (scene == null || wire == null) return;

        scene.Remove(wire);
        Retie(scene, wire.FromPin);
        Retie(scene, wire.ToPin);
    }

    /// <summary>Says on a socket whether anything is still joined to it.</summary>
    public static void Retie(ICanvasScene scene, CanvasNodePin pin)
    {
        if (pin == null) return;

        pin.IsConnected = scene != null && Into(scene, pin) != null;
    }

    // The whole plane. The scene answers by region and a wire may be anywhere - including off screen, which is exactly
    // where the far end of the one being cut usually is.
    private static Rect Everywhere =>
        new(Double.MinValue / 4, Double.MinValue / 4, Double.MaxValue / 2, Double.MaxValue / 2);

    internal static void Bend(Vector2 from, Vector2 to, out Vector2 first, out Vector2 second)
    {
        var reach = Math.Max(Math.Abs(to.X - from.X) * 0.5, 40);

        first = new Vector2(from.X + reach, from.Y);
        second = new Vector2(to.X - reach, to.Y);
    }

    /// <summary>Whether this wire goes ROUND the nodes in its way rather than through them. On, and here to be turned
    /// off: a graph laid out so that nothing is ever in the way pays nothing for this, and a person who prefers the
    /// honest straight bend can say so.</summary>
    public bool Routes { get; set; } = true;

    // Where the bend actually went last time it was worked out. Remembered because the BOX and the HIT TEST have to
    // agree with what is drawn, and they are asked without a canvas to work it out from.
    private Vector2 _first;
    private Vector2 _second;
    private bool _routed;

    // THE BEND, pushed clear of whatever is standing in the way.
    //
    // Not a path-finder. A wire is a curve between two sockets and the thing in its way is almost always one node, so
    // the answer is the cheap one: take the plain bend, and if it crosses something, lift the belly of the curve until
    // it does not - up or down, whichever clears first. Two nodes deep it gives up and draws straight through, which is
    // honest and is what every editor does all the time anyway.
    private void Route(InfiniteCanvas canvas, Vector2 from, Vector2 to, out Vector2 first, out Vector2 second)
    {
        Bend(from, to, out first, out second);
        if (!Routes || canvas == null) return;

        var blockers = new List<Rect>();
        var reach = Box(from, to, first, second);

        foreach (var item in canvas.ItemsHere(reach))
        {
            if (item is not ElementItem element || ReferenceEquals(element, _from) || ReferenceEquals(element, _to))
                continue;

            blockers.Add(element.World);
        }

        if (blockers.Count == 0 || Clear(from, first, second, to, blockers)) return;

        // A step big enough to make a difference at the size of what is in the way: creeping round a node a pixel at a
        // time would take a hundred tries and look like a wire that cannot make up its mind.
        var step = 0d;
        foreach (var box in blockers) step = Math.Max(step, box.Height);
        step = Math.Max(step / 2 + 16, 24);

        for (var push = 1; push <= 3; push++)
        {
            foreach (var sign in Sides)
            {
                var lift = sign * step * push;
                var a = new Vector2(first.X, first.Y + lift);
                var b = new Vector2(second.X, second.Y + lift);

                if (!Clear(from, a, b, to, blockers)) continue;

                first = a;
                second = b;
                return;
            }
        }
    }

    private static readonly int[] Sides = { -1, 1 };

    // Whether the curve misses everything in the list. Walked, like the hit test: the crossing of a cubic and a box has
    // no answer worth writing down, and a few dozen samples is under a pixel at any zoom.
    private static bool Clear(Vector2 from, Vector2 first, Vector2 second, Vector2 to, List<Rect> blockers)
    {
        for (var step = 1; step < 24; step++)
        {
            var at = Along(from, first, second, to, step / 24.0);

            foreach (var box in blockers)
            {
                if (at.X >= box.X && at.X <= box.X + box.Width && at.Y >= box.Y && at.Y <= box.Y + box.Height)
                    return false;
            }
        }

        return true;
    }

    private static Rect Box(Vector2 from, Vector2 to, Vector2 first, Vector2 second)
    {
        var lowX = Math.Min(Math.Min(from.X, to.X), Math.Min(first.X, second.X));
        var lowY = Math.Min(Math.Min(from.Y, to.Y), Math.Min(first.Y, second.Y));
        var highX = Math.Max(Math.Max(from.X, to.X), Math.Max(first.X, second.X));
        var highY = Math.Max(Math.Max(from.Y, to.Y), Math.Max(first.Y, second.Y));

        return new Rect(lowX, lowY, highX - lowX, highY - lowY);
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
