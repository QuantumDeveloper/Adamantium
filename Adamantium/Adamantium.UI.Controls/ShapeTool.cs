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
            CanvasShape.Arrow => Key.A,
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

    /// <summary>A shape is part of a drawing. A graph is made of nodes and wires and of nothing else.</summary>
    public bool WorksIn(CanvasMode mode) => mode == CanvasMode.Drawing;

    /// <summary>How many sides the next POLYGON gets. On the tool rather than on the canvas, because it is a fact about
    /// what this tool makes - the same place the shape itself is stated.</summary>
    public int Sides { get; set; } = 5;

    /// <summary>What the next ARROW wears on each end. On the tool for the same reason <see cref="Sides"/> is: an
    /// application offering a plain arrow and a double-headed one builds two of this class.</summary>
    public CanvasArrowHead StartHead { get; set; } = CanvasArrowHead.None;

    public CanvasArrowHead EndHead { get; set; } = CanvasArrowHead.Triangle;

    public void OnPressed(InfiniteCanvas canvas, CanvasPointerEventArgs e)
    {
        if (e.Button != MouseButtons.Left || canvas.Scene == null) return;

        _from = e.World;
        _making = new ShapeItem(Shape, new Rect(_from.X, _from.Y, 0, 0), canvas.Ink,
            canvas.ScreenToWorldLength(canvas.InkThickness),
            Shape is CanvasShape.Line or CanvasShape.Arrow ? null : canvas.ShapeFill)
        {
            Sides = Sides,
            StartHead = StartHead,
            EndHead = EndHead
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

        // The two points of the drag, handed to the shape whole. A normalised box cannot say which way the drag went,
        // so the shape keeps that as two bits beside it - which way the line leans, and which end of it the hand is at
        // - and it is the SHAPE that works both out. Set here by hand, the lean was right and the direction was never
        // written at all: an arrow dragged up and to the left put its head back at the start of the drag, pointing at
        // the hand rather than away from it.
        //
        // Not Resize: while a shape is being dragged out, the box IS what the drag made, and going through Resize
        // would take the outline's width off it on every single move.
        shape.SetEnds(_from, to);
    }
}
