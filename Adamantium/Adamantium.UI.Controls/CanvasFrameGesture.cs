using System;
using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;

namespace Adamantium.UI.Controls;

/// <summary>Dragging the manipulation frame - a grip to resize what is selected, or the body to move it.
/// <para>Its own type because the frame belongs to the CANVAS and not to any one tool: the canvas draws it, so the
/// canvas answers presses on it, and a person who has just dragged out a rectangle can pull its corner straight away
/// instead of putting the shape tool down first. The select tool uses the same gesture for the body, so there is one
/// implementation of "what a drag of the frame does" rather than one per tool.</para></summary>
public sealed class CanvasFrameGesture
{
    private readonly List<Rect> _startBounds = new();

    private Rect _startFrame;
    private Vector2 _from;
    private Vector2 _applied;

    /// <summary>Which part of the frame is being dragged, or <see cref="CanvasHandle.None"/> when none is.</summary>
    public CanvasHandle Handle { get; private set; }

    public bool IsActive => Handle != CanvasHandle.None;

    /// <summary>Take hold. Remembers the frame and every item's box as they are NOW: a drag is measured from where it
    /// started, so that running the pointer back and forth leaves the drawing where it began.</summary>
    public void Begin(InfiniteCanvas canvas, CanvasHandle handle, Vector2 world)
    {
        Handle = handle;
        _from = world;
        _applied = Vector2.Zero;
        _startFrame = canvas.SelectionBounds ?? new Rect(0, 0, 0, 0);

        _startBounds.Clear();
        foreach (var item in canvas.Selection) _startBounds.Add(item.Bounds);
    }

    public void MoveTo(InfiniteCanvas canvas, Vector2 world)
    {
        if (!IsActive || canvas.Selection.Count == 0) return;

        var delta = world - _from;

        if (Handle == CanvasHandle.Body)
        {
            // By STEPS, against what has already been applied: an item is told to move by a distance, not to be at a
            // place, so handing it the whole drag every time would move it again from where it already is.
            var step = delta - _applied;
            _applied = delta;

            foreach (var item in canvas.Selection) item.Move(step);
        }
        else
        {
            Apply(canvas, Stretch(_startFrame, Handle, delta, canvas.ScreenToWorldLength(2)));
        }

        canvas.Scene?.Touch();
        canvas.InvalidateRender(false);
    }

    public void End() => Handle = CanvasHandle.None;

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

    /// <summary>The cursor that says what a press on this part of the frame would do, or null where the frame has
    /// nothing to say and the TOOL's own pointer should stand.</summary>
    public static Cursor CursorFor(CanvasHandle handle) => handle switch
    {
        CanvasHandle.TopLeft or CanvasHandle.BottomRight => Cursors.SizeNWSE,
        CanvasHandle.TopRight or CanvasHandle.BottomLeft => Cursors.SizeNESW,
        CanvasHandle.Top or CanvasHandle.Bottom => Cursors.SizeNS,
        CanvasHandle.Left or CanvasHandle.Right => Cursors.SizeEWE,
        _ => null
    };
}
