using System;
using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry.Shapes;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls;

/// <summary>The eraser. Dragged over the drawing it rubs a hole in what it passes - or takes the whole stroke, if that
/// is the mode - and shows a ring so the hand can see how wide it is before it is put down.
/// <para>Only INK erases by point: half an ellipse is not an ellipse and half a button is not a button, so a shape, a
/// piece of text and a control go whole whichever mode is set. That is not a shortcut - it is the difference between
/// something made of a path and something made of a definition.</para></summary>
public class EraseTool : ICanvasTool
{
    private readonly List<StrokeItem> _pieces = new();

    private bool _erasing;
    private Vector2 _at;
    private bool _over;
    private Vector2 _last;

    public EraseTool(CanvasEraseMode mode = CanvasEraseMode.Point)
    {
        Mode = mode;

        var whole = mode == CanvasEraseMode.Stroke;
        Name = whole ? "Erase stroke" : "Erase";
        Icon = whole ? "ToolEraseStrokeIcon" : "ToolEraseIcon";
        Shortcut = whole ? Key.D : Key.E;
        Description = whole ? "takes the whole stroke it touches" : "rubs a hole where it is dragged";
    }

    /// <summary>How a rail shows this tool. Taken from the MODE by default - one class serves both erasers - and
    /// settable, like every other tool's.</summary>
    public string Name { get; set; }

    public string Icon { get; set; }

    public Key Shortcut { get; set; }

    public string Description { get; set; }

    /// <summary>NONE - the eraser draws its own ring, and a pointer on top of it would only hide the very thing that
    /// shows how wide the rubber is.</summary>
    public Cursor Cursor { get; set; } = Cursors.None;

    /// <summary>What this eraser takes. Set once, when the eraser is made: the two are separate TOOLS rather than one
    /// tool with a switch, so picking which one you want is the same act as picking any other tool.</summary>
    public CanvasEraseMode Mode { get; }

    public bool IsBusy => false;

    public void OnPressed(InfiniteCanvas canvas, CanvasPointerEventArgs e)
    {
        if (e.Button != MouseButtons.Left || canvas.Scene == null) return;

        _erasing = true;
        _last = e.Pointer;

        Rub(canvas, e.Pointer);

        canvas.CaptureMouse();
        e.Handled = true;
    }

    public void OnMoved(InfiniteCanvas canvas, CanvasPointerEventArgs e)
    {
        // The ring follows the pointer whether or not the button is down: how big the eraser is has to be visible
        // BEFORE it is used, or it can only be found out by rubbing something out.
        _at = e.Screen;
        _over = true;
        canvas.InvalidateRender(false);

        if (!_erasing) return;

        // Along the WHOLE way, not only where the pointer was reported. A pointer arrives a handful of times a frame,
        // and a quick sweep would otherwise erase a row of dots with untouched drawing between them.
        var step = canvas.ScreenToWorldLength(Math.Max(2, canvas.EraserSize / 3));
        var travel = (e.Pointer - _last).Length();
        var stops = Math.Max(1, (int)Math.Ceiling(travel / Math.Max(step, 1e-9)));

        for (var i = 1; i <= stops; i++) Rub(canvas, _last + (e.Pointer - _last) * ((double)i / stops));

        _last = e.Pointer;
        e.Handled = true;
    }

    public void OnReleased(InfiniteCanvas canvas, CanvasPointerEventArgs e)
    {
        if (!_erasing) return;

        _erasing = false;
        canvas.ReleaseMouseCapture();
        e.Handled = true;
    }

    public void Render(IDrawingSession session, InfiniteCanvas canvas)
    {
        if (!_over || canvas.SelectionBrush == null || canvas.EraserSize <= 0) return;

        var size = canvas.EraserSize;

        session.DrawEllipse(new Rect(_at.X - size / 2, _at.Y - size / 2, size, size),
            null, 0, 360, EllipseType.Sector, new Pen(canvas.SelectionBrush));
    }

    public void Cancel(InfiniteCanvas canvas)
    {
        if (_erasing) canvas.ReleaseMouseCapture();

        _erasing = false;
        _over = false;
    }

    private void Rub(InfiniteCanvas canvas, Vector2 world)
    {
        if (canvas.Scene is not { } scene) return;

        var radius = canvas.ScreenToWorldLength(canvas.EraserSize) / 2;
        var reach = new Rect(world.X - radius, world.Y - radius, radius * 2, radius * 2);

        // What the eraser touches, taken out of the scene FIRST: editing a scene while walking it is how a walk ends up
        // skipping half of what it was asked about.
        var touched = new List<ICanvasItem>();
        foreach (var item in scene.ItemsIn(reach))
        {
            if (item.HitTest(world, radius)) touched.Add(item);
        }

        if (touched.Count == 0) return;

        foreach (var item in touched)
        {
            if (Mode == CanvasEraseMode.Point && item is StrokeItem stroke)
            {
                _pieces.Clear();
                if (!stroke.Erase(world, radius, _pieces)) continue;

                // IN PLACE, not "remove then add": the pieces take the stroke's own place in paint order. Added at the
                // end instead, a stroke rubbed through rose above everything put on the canvas after it was drawn.
                scene.Replace(stroke, _pieces);

                continue;
            }

            scene.Remove(item);
        }

        canvas.ClearSelection();
        canvas.InvalidateRender(false);
    }
}
