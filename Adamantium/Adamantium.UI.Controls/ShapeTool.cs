using System;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Input;

namespace Adamantium.UI.Controls;

/// <summary>Drags out a rectangle, an ellipse or a line. One tool for all three: what changes between them is a single
/// field on the item it makes, and three classes that differ by a constructor argument would be three places to fix the
/// next thing that goes wrong with dragging out a box.</summary>
public class ShapeTool : ICanvasTool
{
    private ShapeItem _making;
    private Vector2 _from;

    public ShapeTool(CanvasShape shape)
    {
        Shape = shape;
        Name = shape.ToString();
        Icon = "Tool" + shape + "Icon";
        Shortcut = shape switch
        {
            CanvasShape.Rectangle => Key.R,
            CanvasShape.Ellipse => Key.O,
            CanvasShape.Polygon => Key.G,
            _ => Key.L
        };
    }

    public CanvasShape Shape { get; }

    /// <summary>A shape is one drag from press to release, so nothing here ever outlives the button.</summary>
    public bool IsBusy => false;

    /// <summary>How a rail shows this tool. Taken from the SHAPE by default - one class serves three tools, and each
    /// of the three is called and drawn after the shape it makes - and settable, like every other tool's.</summary>
    public string Name { get; set; }

    public string Icon { get; set; }

    public Key Shortcut { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>A crosshair - a shape is dragged out from an exact corner.</summary>
    public Cursor Cursor { get; set; } = Cursors.Crosshair;

    /// <summary>How many sides the next POLYGON gets. On the tool rather than on the canvas, because it is a fact about
    /// what this tool makes - the same place the shape itself is stated.</summary>
    public int Sides { get; set; } = 5;

    public void OnPressed(InfiniteCanvas canvas, CanvasPointerEventArgs e)
    {
        if (e.Button != MouseButtons.Left || canvas.Scene == null) return;

        _from = e.World;
        _making = new ShapeItem(Shape, new Rect(_from.X, _from.Y, 0, 0), canvas.Ink,
            canvas.ScreenToWorldLength(canvas.InkThickness), Shape == CanvasShape.Line ? null : canvas.ShapeFill)
        {
            Sides = Sides
        };

        canvas.CaptureMouse();
        canvas.InvalidateRender(false);
        e.Handled = true;
    }

    public void OnMoved(InfiniteCanvas canvas, CanvasPointerEventArgs e)
    {
        if (_making == null) return;

        Stretch(e.World);
        canvas.InvalidateRender(false);
        e.Handled = true;
    }

    public void OnReleased(InfiniteCanvas canvas, CanvasPointerEventArgs e)
    {
        if (_making == null) return;

        var made = _making;
        _making = null;
        canvas.ReleaseMouseCapture();

        Stretch(e.World, made);

        // A press that never moved is a click, not a shape. Measured in SCREEN pixels, because what counts as "did not
        // move" is about the hand and not about how far out the camera is.
        var least = canvas.ScreenToWorldLength(3);
        if (made.World.Width >= least || made.World.Height >= least)
        {
            canvas.Scene?.Add(made);
            canvas.Select(made, false);
        }

        canvas.InvalidateRender(false);
        e.Handled = true;
    }

    public void Render(IDrawingSession session, InfiniteCanvas canvas) => _making?.Render(session, canvas);

    public void Cancel(InfiniteCanvas canvas)
    {
        if (_making == null) return;

        _making = null;
        canvas.ReleaseMouseCapture();
        canvas.InvalidateRender(false);
    }

    private void Stretch(Vector2 to, ShapeItem shape = null)
    {
        shape ??= _making;
        if (shape == null) return;

        // Which way the drag went, kept before the box is squared up: a line leans the other way when it is dragged up
        // and to the right, and a normalised box cannot say that on its own.
        shape.Flipped = (to.X - _from.X) * (to.Y - _from.Y) < 0;

        // The box itself, not the bounds: while a shape is being dragged out, the box IS what the drag made, and going
        // through Resize would take the outline's width off it on every single move.
        shape.World = new Rect(Math.Min(_from.X, to.X), Math.Min(_from.Y, to.Y),
            Math.Abs(to.X - _from.X), Math.Abs(to.Y - _from.Y));
    }
}
