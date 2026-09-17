using System;
using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>Picking things up: click to select, drag a band round several, drag them about, drag a grip to resize.
/// <para>What a press means is decided by WHERE it lands, in this order: a grip of the manipulation frame, then inside
/// the frame, then an item, then nothing. The order is the point - a grip sits on top of the thing it resizes, so
/// asking about items first would make a frame round a filled shape impossible to resize.</para></summary>
public class SelectTool : ICanvasTool
{
    private readonly List<Rect> _startBounds = new();

    private CanvasHandle _grip;
    private Rect _startFrame;
    private Vector2 _from;
    private Vector2 _fromPointer;
    private Vector2 _applied;
    private Rect? _band;
    private bool _bandExtends;

    /// <summary>A gesture here lives inside one press, so nothing outlives the button.</summary>
    public bool IsBusy => Wires.IsBusy;

    /// <summary>How a rail shows this tool. Settable, so an application that wants another name, another picture or
    /// another key says so on the tool it built rather than anywhere else.</summary>
    public string Name { get; set; } = "Select";

    public string Icon { get; set; } = "ToolSelectIcon";

    public Key Shortcut { get; set; } = Key.V;

    public string Description { get; set; } = "select, move and resize";

    /// <summary>Pulling a WIRE out of a socket, which this tool offers the press to FIRST.
    /// <para>Here rather than in a tool of its own because that is what a node editor is: nothing to switch to, the
    /// socket itself is the offer, and you drag off it with whatever is already in hand. The knowledge of what a socket
    /// is stays in the gesture - this tool only asks whether the press was one.</para></summary>
    public ConnectGesture Wires { get; } = new();

    public void OnPressed(InfiniteCanvas canvas, CanvasPointerEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;

        // A SOCKET first, and nothing else if it was one: a press on a socket means a wire, never a drag of the node
        // it belongs to. Picking would be the wrong answer to a gesture aimed at something a dozen pixels across.
        if (Wires.Press(canvas, e.Pointer))
        {
            e.Handled = true;
            return;
        }

        _from = e.World;
        _fromPointer = e.Pointer;
        _applied = Vector2.Zero;

        // No GRIP branch here any more: the frame belongs to the canvas, which answers presses on its grips before any
        // tool is asked - so resizing works under every tool and not only under this one. What is left for the tool is
        // what a press MEANS, which is its own business: pick something, drag what is picked, or open a band.
        var item = Topmost(canvas, e.Pointer);

        // INTO the group: a second click on a group that is already selected picks the thing inside it under the
        // pointer. A mode would be the other way to do it, and a mode has to be left again - this one needs nothing
        // remembered and nothing to get out of.
        if (e.ClickCount > 1 && item is GroupItem group && canvas.IsSelected(group) &&
            group.Pick(e.Pointer, canvas.ScreenToWorldLength(4)) is { } inside)
        {
            canvas.Select(inside, false);
            _grip = CanvasHandle.Body;
            Begin(canvas);
            canvas.CaptureMouse();
            e.Handled = true;
            return;
        }

        if (item != null)
        {
            // Already in the selection: the press starts a drag of the WHOLE selection and leaves it alone. Picking one
            // out of a group by pressing it is what the extend keys are for, and without this rule every attempt to drag
            // a group would collapse it to whichever item was under the pointer.
            if (!canvas.IsSelected(item)) canvas.Select(item, e.Extends);
            else if (e.Extends) canvas.Deselect(item);

            if (canvas.Selection.Count > 0)
            {
                _grip = CanvasHandle.Body;
                Begin(canvas);
                canvas.CaptureMouse();
            }

            e.Handled = true;
            return;
        }

        // Nothing under the pointer: a band. It does not clear the selection YET - a press that turns out to be a click
        // on empty space clears it on RELEASE, so that a band started by accident over a group does not lose the group
        // before it has selected anything.
        _bandExtends = e.Extends;
        _band = new Rect(e.Pointer.X, e.Pointer.Y, 0, 0);

        canvas.CaptureMouse();
        e.Handled = true;
    }

    public void OnMoved(InfiniteCanvas canvas, CanvasPointerEventArgs e)
    {
        if (Wires.Move(canvas, e.Pointer))
        {
            e.Handled = true;
            return;
        }

        if (_band != null)
        {
            // The band follows the POINTER and not the snap: it is a thing being aimed with, and one that jumped from
            // crossing to crossing could not be closed round something that sits between two of them.
            _band = new Rect(Math.Min(_fromPointer.X, e.Pointer.X), Math.Min(_fromPointer.Y, e.Pointer.Y),
                Math.Abs(e.Pointer.X - _fromPointer.X), Math.Abs(e.Pointer.Y - _fromPointer.Y));

            canvas.InvalidateRender(false);
            e.Handled = true;
            return;
        }

        if (_grip == CanvasHandle.None || canvas.Selection.Count == 0) return;

        var delta = e.World - _from;

        if (_grip == CanvasHandle.Body)
        {
            // By STEPS, against what has already been applied: an item is told to move by a distance, not to be at a
            // place, so handing it the whole drag every time would move it again from where it already is.
            var step = delta - _applied;
            _applied = delta;

            foreach (var item in canvas.Selection) item.Move(step);
        }
        else
        {
            var frame = Stretch(_startFrame, _grip, delta, canvas.ScreenToWorldLength(2));
            Apply(canvas, frame);
        }

        canvas.Scene?.Touch();
        canvas.InvalidateRender(false);
        e.Handled = true;
    }

    public void OnReleased(InfiniteCanvas canvas, CanvasPointerEventArgs e)
    {
        if (Wires.Release(canvas, e.Pointer))
        {
            e.Handled = true;
            return;
        }

        canvas.ReleaseMouseCapture();
        Drop(canvas);

        if (_band is { } band)
        {
            _band = null;

            // A band that never opened is a click on empty space, and that is what clears a selection.
            var least = canvas.ScreenToWorldLength(3);
            if (band.Width < least && band.Height < least)
            {
                if (!_bandExtends) canvas.ClearSelection();
            }
            else
            {
                canvas.SelectMany(Inside(canvas, band), _bandExtends);
            }

            canvas.InvalidateRender(false);
            e.Handled = true;
            return;
        }

        if (_grip == CanvasHandle.None) return;

        _grip = CanvasHandle.None;
        _startBounds.Clear();

        canvas.Scene?.Touch();
        e.Handled = true;
    }

    public void Render(IDrawingSession session, InfiniteCanvas canvas)
    {
        Wires.Draw(session, canvas);

        if (_band is not { } band || canvas.RubberBandBrush == null && canvas.SelectionBrush == null) return;

        var topLeft = canvas.WorldToScreen(new Vector2(band.X, band.Y));
        var bottomRight = canvas.WorldToScreen(new Vector2(band.X + band.Width, band.Y + band.Height));

        session.DrawRectangle(canvas.RubberBandBrush,
            new Rect(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y),
            canvas.SelectionBrush == null ? null : new Pen(canvas.SelectionBrush));
    }

    public void Cancel(InfiniteCanvas canvas)
    {
        Wires.Cancel(canvas);

        _band = null;
        _grip = CanvasHandle.None;
        _startBounds.Clear();

        canvas.ReleaseMouseCapture();
    }

    private void Begin(InfiniteCanvas canvas)
    {
        _startFrame = canvas.SelectionBounds ?? new Rect(0, 0, 0, 0);
        _startBounds.Clear();

        foreach (var item in canvas.Selection) _startBounds.Add(item.Bounds);

        // A COMMENT FRAME picks up what is standing on it, ONCE, here. Asked again on every move it would collect
        // whatever it was pushed over on the way, and a node dragged out of a frame would be dragged back in by the
        // frame catching up with it.
        foreach (var item in canvas.Selection)
        {
            if (item is CanvasFrameItem frame) frame.Catch(canvas.ItemsHere());
        }
    }

    // ...and lets go when the drag is over, so a frame standing still owns nothing.
    private static void Drop(InfiniteCanvas canvas)
    {
        foreach (var item in canvas.Selection)
        {
            if (item is CanvasFrameItem frame) frame.Release();
        }
    }

    // Every selected item put through the same change of box the frame went through. Proportional rather than each item
    // being resized to the frame: a group keeps its arrangement when it is scaled, which is the whole difference between
    // resizing a group and resizing everything in it.
    private void Apply(InfiniteCanvas canvas, Rect frame)
    {
        if (_startFrame.Width <= 1e-9 && _startFrame.Height <= 1e-9) return;

        var sx = _startFrame.Width > 1e-9 ? frame.Width / _startFrame.Width : 1;
        var sy = _startFrame.Height > 1e-9 ? frame.Height / _startFrame.Height : 1;

        var index = 0;
        foreach (var item in canvas.Selection)
        {
            if (index >= _startBounds.Count) break;

            var was = _startBounds[index++];
            item.Resize(new Rect(
                frame.X + (was.X - _startFrame.X) * sx,
                frame.Y + (was.Y - _startFrame.Y) * sy,
                Math.Max(1e-9, was.Width * sx),
                Math.Max(1e-9, was.Height * sy)));
        }
    }

    // Which edges a grip moves. Never allowed to turn inside out: an edge dragged past its opposite stops there, because
    // a frame that flipped would swap which grip the hand is holding halfway through the drag.
    private static Rect Stretch(Rect from, CanvasHandle grip, Vector2 delta, double least)
    {
        var left = from.X;
        var top = from.Y;
        var right = from.X + from.Width;
        var bottom = from.Y + from.Height;

        if (grip is CanvasHandle.TopLeft or CanvasHandle.Left or CanvasHandle.BottomLeft)
            left = Math.Min(left + delta.X, right - least);
        if (grip is CanvasHandle.TopRight or CanvasHandle.Right or CanvasHandle.BottomRight)
            right = Math.Max(right + delta.X, left + least);
        if (grip is CanvasHandle.TopLeft or CanvasHandle.Top or CanvasHandle.TopRight)
            top = Math.Min(top + delta.Y, bottom - least);
        if (grip is CanvasHandle.BottomLeft or CanvasHandle.Bottom or CanvasHandle.BottomRight)
            bottom = Math.Max(bottom + delta.Y, top + least);

        return new Rect(left, top, right - left, bottom - top);
    }

    // The scene yields in PAINT order, so the last one that answers is the one on top - which is the one a press meant.
    private static ICanvasItem Topmost(InfiniteCanvas canvas, Vector2 world) => canvas.ItemAt(world);

    // Everything the band TOUCHES, not everything it swallows whole. A band that only counted what it contained entirely
    // would refuse to take a long stroke however carefully it was drawn round it.
    private static IEnumerable<ICanvasItem> Inside(InfiniteCanvas canvas, Rect band)
    {
        if (canvas.Scene == null) yield break;

        foreach (var item in canvas.ItemsHere(band)) yield return item;
    }
}
