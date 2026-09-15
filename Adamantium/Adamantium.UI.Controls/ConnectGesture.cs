using System;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls;

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

        _at = world;
        canvas.CaptureMouse();
        return true;
    }

    /// <summary>Follows the pointer while a wire is out. False when there is none.</summary>
    public bool Move(InfiniteCanvas canvas, Vector2 world)
    {
        if (_fromPin == null) return false;

        _at = world;
        canvas.InvalidateRender(false);
        return true;
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

        if (!Socket(canvas, world, out var toItem, out var toPin)) return true;
        if (!Joinable(item, pin, toItem, toPin)) return true;

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

    public void Draw(IDrawingSession session, InfiniteCanvas canvas)
    {
        if (_fromPin == null || canvas == null) return;
        if (Where(_fromItem, _fromPin) is not { } from) return;

        // The SAME curve the finished wire is drawn with, through the same painter: one that looked one way while it
        // was being pulled out and another once it was let go would be two answers to one question.
        _painter.Draw(session, canvas, from, _at, _fromPin.Color, Thickness);
    }

    public void Cancel(InfiniteCanvas canvas)
    {
        if (_fromPin == null) return;

        _fromItem = null;
        _fromPin = null;
        canvas.ReleaseMouseCapture();
        canvas.InvalidateRender(false);
    }

    /// <summary>Whether a wire may run between these two sockets. Stated here rather than inside the gesture so that a
    /// list beside the canvas, or a test, can ask the same question without pressing anything.</summary>
    public static bool Joinable(ElementItem fromItem, CanvasNodePin from, ElementItem toItem, CanvasNodePin to)
    {
        if (from == null || to == null || fromItem == null || toItem == null) return false;
        if (ReferenceEquals(from, to)) return false;
        if (ReferenceEquals(fromItem, toItem)) return false;

        return from.IsInput != to.IsInput;
    }

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
