using System;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>Pulling a WIRE out of a socket and dropping it on another.
/// <para>A GESTURE and not a tool, because that is what every node editor does: in Unreal, Blender, Substance, Houdini
/// and the rest there is nothing to switch to - the socket itself is the offer, and you drag off it with whatever is
/// already in hand. A mode would be one more thing to enter and leave for an act performed twenty times a minute, and
/// leaving it forgotten is a press that draws a wire when it meant to pick something up.</para>
/// <para>Held by the tool that offers it the press first. It answers only when the press LANDS ON A SOCKET and says so;
/// anything else it refuses, and the tool carries on as though nothing had asked.</para></summary>
public class ConnectGesture
{
    private readonly WirePainter _painter = new();

    private ElementItem _fromItem;
    private CanvasNodePin _fromPin;
    private CanvasNode _opened;
    private Vector2 _at;

    /// <summary>Whether a wire is being pulled right now. The tool holding this asks before doing anything of its own.
    /// </summary>
    public bool IsBusy => _fromPin != null;

    /// <summary>How far off a socket a press may be and still count, in SCREEN pixels. A socket is about a dozen pixels
    /// across at 1:1 and smaller the further out the camera is, so hitting one exactly is not something a hand should
    /// be asked to do.</summary>
    public double Reach { get; set; } = 6;

    /// <summary>How thick a wire is drawn, in WORLD units.</summary>
    public double Thickness { get; set; } = 2;

    /// <summary>Takes the press if it landed on a socket. False means it did not, and the tool should go on as usual.
    /// </summary>
    public bool Press(InfiniteCanvas canvas, Vector2 world)
    {
        if (canvas?.Scene == null) return false;
        if (!Socket(canvas, world, out _fromItem, out _fromPin)) return false;

        // AN INPUT THAT IS ALREADY TAKEN hands its wire over instead of starting a second one: the wire comes off and
        // the gesture carries on from the far end of it, so the same drag that would have made a wire moves the one
        // that is there. Dropped on another socket it is re-routed, dropped on nothing it is gone - which is how a wire
        // is deleted, and why deleting one needs nothing of its own.
        //
        // Only an input. An output feeds as many wires as you like, so a press on one means a NEW wire and can mean
        // nothing else.
        if (_fromPin.IsInput && ConnectionItem.Into(canvas.Scene, _fromPin) is { } taken)
        {
            ConnectionItem.Cut(canvas.Scene, taken);

            _fromItem = taken.FromItem;
            _fromPin = taken.FromPin;
        }

        _at = world;
        canvas.CaptureMouse();
        return true;
    }

    /// <summary>Follows the pointer while a wire is out. False when there is none.</summary>
    public bool Move(InfiniteCanvas canvas, Vector2 world)
    {
        if (_fromPin == null) return false;

        _at = world;
        Spring(canvas, world);
        canvas.InvalidateRender(false);
        return true;
    }

    // A FOLDED node under a wire OPENS, and shuts again when the wire moves off it without being dropped. Folded, a
    // node has one stub a side and cannot say which socket is meant, so a wire let go over it read as a drop on
    // nothing - and offered to make a NEW node instead of wiring the one being pointed at. Opening it puts the sockets
    // back under the pointer and the drop means what it looks like.
    private void Spring(InfiniteCanvas canvas, Vector2 world)
    {
        var node = NodeAt(canvas, world);
        if (ReferenceEquals(node, _opened)) return;

        Shut();

        if (node is not { IsCollapsed: true }) return;

        node.SetCurrentValue(CanvasNode.IsCollapsedProperty, false);
        _opened = node;
    }

    private void Shut()
    {
        if (_opened == null) return;

        _opened.SetCurrentValue(CanvasNode.IsCollapsedProperty, true);
        _opened = null;
    }

    private static CanvasNode NodeAt(InfiniteCanvas canvas, Vector2 world)
    {
        if (canvas?.Scene == null) return null;

        foreach (var candidate in canvas.ItemsHere(new Rect(world.X, world.Y, 0, 0)))
        {
            if (candidate is ElementItem { Element: CanvasNode node } element && element.World.Contains(world))
            {
                return node;
            }
        }

        return null;
    }

    /// <summary>Lets the wire go. It becomes a real one if it landed on a socket that may be joined to the one it came
    /// from, and vanishes otherwise - a wire dropped on nothing is a gesture abandoned, not an error.</summary>
    public bool Release(InfiniteCanvas canvas, Vector2 world)
    {
        if (_fromPin == null) return false;

        var item = _fromItem;
        var pin = _fromPin;

        _fromItem = null;
        _fromPin = null;
        canvas.ReleaseMouseCapture();
        canvas.InvalidateRender(false);

        // LET GO OVER NOTHING. Offered to the application before it is thrown away: this is the gesture a graph is
        // actually built with - drop the wire where there is nothing and pick the node that goes there, already wired.
        // See CanvasWireDroppedEventArgs. Nobody answering is the ordinary case, and then it is simply abandoned.
        if (!Socket(canvas, world, out var toItem, out var toPin))
        {
            Shut();
            canvas.OfferWire(item, pin, world);
            return true;
        }

        if (!Joinable(canvas.Scene, item, pin, toItem, toPin))
        {
            Shut();
            return true;
        }

        // The wire landed ON the node the drag opened, so it stays open: shutting it again would hide the socket the
        // wire was just put on.
        if (ReferenceEquals(toItem.Element, _opened)) _opened = null;
        Shut();

        // AS MANY AS THE SOCKET SAYS, which is usually one: a value arrives from one place. Full, it loses its oldest
        // wire rather than refusing the new one - refusing would be the other honest answer and is the worse one, since
        // the hand has already said what it wants. A socket that takes several simply has room.
        var input = pin.IsInput ? pin : toPin;
        ConnectionItem.MakeRoom(canvas.Scene, input);

        // The OUTPUT first, whichever end it was drawn from: a wire runs one way, and a graph read backwards is a graph
        // nobody can read. Pulling one out of an input and dropping it on an output is an ordinary thing to do.
        var wire = pin.IsInput
            ? new ConnectionItem(toItem, toPin, item, pin) { Thickness = Thickness }
            : new ConnectionItem(item, pin, toItem, toPin) { Thickness = Thickness };

        canvas.Scene?.Add(wire);

        pin.IsConnected = true;
        toPin.IsConnected = true;

        return true;
    }

    /// <summary>What a socket under the pointer offers, for a tool deciding which cursor to wear. Null when there is
    /// none there.</summary>
    public CanvasNodePin Under(InfiniteCanvas canvas, Vector2 world) =>
        canvas?.Scene != null && Socket(canvas, world, out _, out var pin) ? pin : null;

    /// <summary>What the wire is drawn in while it is over a socket that will not take it. Null draws it in the source
    /// socket's colour like any other, which is what an application that has not said otherwise gets.</summary>
    public Brush RefusedStroke { get; set; }

    /// <summary>Whether the socket under the pointer right now would take this wire. False while nothing is being
    /// pulled, and false over empty plane - which is not a refusal, just nothing to say.</summary>
    public bool OverRefusal(InfiniteCanvas canvas, Vector2 world)
    {
        if (_fromPin == null || canvas?.Scene == null) return false;
        if (!Socket(canvas, world, out var toItem, out var toPin)) return false;

        return !Joinable(canvas.Scene, _fromItem, _fromPin, toItem, toPin);
    }

    public void Draw(IDrawingSession session, InfiniteCanvas canvas)
    {
        if (_fromPin == null || canvas == null) return;
        if (Where(_fromItem, _fromPin) is not { } from) return;

        // SAID WHILE THE HAND IS STILL MOVING. A wire that looks willing all the way and is then quietly dropped tells
        // you nothing about why; over a socket that will not take it, it is drawn in the refusing colour and the answer
        // arrives before the button does.
        var stroke = RefusedStroke != null && OverRefusal(canvas, _at) ? RefusedStroke : _fromPin.Color;

        // The SAME curve the finished wire is drawn with, through the same painter: one that looked one way while it
        // was being pulled out and another once it was let go would be two answers to one question.
        _painter.Draw(session, canvas, from, _at, stroke, Thickness);
    }

    public void Cancel(InfiniteCanvas canvas)
    {
        if (_fromPin == null) return;

        Shut();

        _fromItem = null;
        _fromPin = null;
        canvas.ReleaseMouseCapture();
        canvas.InvalidateRender(false);
    }

    /// <summary>Whether a wire may run between these two sockets. Stated here rather than inside the gesture so that a
    /// list beside the canvas, or a test, can ask the same question without pressing anything.
    /// <para>Without a SCENE this is what can be told from the two sockets alone: one in and one out, on two different
    /// nodes, agreeing about what flows. Whether the wire would close a LOOP cannot be told that way - see the overload
    /// that is given the scene.</para></summary>
    public static bool Joinable(ElementItem fromItem, CanvasNodePin from, ElementItem toItem, CanvasNodePin to)
    {
        if (from == null || to == null || fromItem == null || toItem == null) return false;
        if (ReferenceEquals(from, to)) return false;
        if (ReferenceEquals(fromItem, toItem)) return false;
        if (from.IsInput == to.IsInput) return false;

        return from.Fits(to);
    }

    /// <summary>...and whether it would close a LOOP. A graph that lets you run a wire back round into its own source
    /// and then says nothing about it is a graph that breaks the first time anybody tries to read it - and the person
    /// who drew the loop is the only one who could have known not to.</summary>
    public static bool Joinable(ICanvasScene scene, ElementItem fromItem, CanvasNodePin from,
        ElementItem toItem, CanvasNodePin to)
    {
        if (!Joinable(fromItem, from, toItem, to)) return false;
        if (scene == null) return true;

        // Which way round the wire would actually run: out of the OUTPUT and into the input, whichever end the hand
        // started from.
        var source = from.IsInput ? toItem : fromItem;
        var target = from.IsInput ? fromItem : toItem;

        // A loop is exactly this: the node the wire arrives at can already reach the node it leaves.
        return !Reaches(scene, target, source);
    }

    // Whether anything downstream of `from` is `to`, following wires the way they run. Walked breadth-first with a seen
    // set, because a graph that already has a loop in it - loaded from a file somebody else wrote - must not send this
    // round for ever.
    private static bool Reaches(ICanvasScene scene, ElementItem from, ElementItem to)
    {
        var seen = new HashSet<ElementItem> { from };
        var queue = new Queue<ElementItem>();
        queue.Enqueue(from);

        while (queue.Count > 0)
        {
            var at = queue.Dequeue();
            if (ReferenceEquals(at, to)) return true;

            foreach (var item in scene.ItemsIn(Everywhere))
            {
                if (item is not ConnectionItem wire || !ReferenceEquals(wire.FromItem, at)) continue;
                if (wire.ToItem != null && seen.Add(wire.ToItem)) queue.Enqueue(wire.ToItem);
            }
        }

        return false;
    }

    private static Rect Everywhere =>
        new(Double.MinValue / 4, Double.MinValue / 4, Double.MaxValue / 2, Double.MaxValue / 2);

    // WHICH socket a world point is on, and the node carrying it. The item's box is where the node was arranged, so a
    // world point becomes a node point once the box's corner is taken off it.
    private bool Socket(InfiniteCanvas canvas, Vector2 world, out ElementItem item, out CanvasNodePin pin)
    {
        item = null;
        pin = null;

        var reach = canvas.ScreenToWorldLength(Math.Max(Reach, 0));
        var around = new Rect(world.X - reach, world.Y - reach, reach * 2, reach * 2);

        foreach (var candidate in canvas.ItemsHere(around))
        {
            if (candidate is not ElementItem element || element.Element is not CanvasNode node) continue;

            var local = new Vector2(world.X - element.World.X, world.Y - element.World.Y);
            if (node.PinAt(local, reach) is not { } found) continue;

            item = element;
            pin = found;
            return true;
        }

        return false;
    }

    private static Vector2? Where(ElementItem item, CanvasNodePin pin)
    {
        if (item?.Element is not CanvasNode node || node.Where(pin) is not { } local) return null;

        return new Vector2(item.World.X + local.X, item.World.Y + local.Y);
    }
}
