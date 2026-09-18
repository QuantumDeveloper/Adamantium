using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Input;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>Places the points of a curve: a click puts one down, moving shows where the line would go, a double click
/// ends it.
/// <para>Point by point and never by dragging: all three of these curves are defined BY their points, and a drag would
/// have to invent them from a path - which is what the pen already does, and what a curve is chosen instead of.</para>
/// <para>ONE tool for the three kinds, and the kind is a setting: which curve suits a line is decided by looking at it,
/// so it can be changed afterwards in the panel. An application that wants a button per kind builds three of these.</para>
/// </summary>
public class CurveTool : ICanvasTool
{
    private CurveItem _making;
    private Vector2 _pointer;

    public CurveTool(CanvasCurve kind = CanvasCurve.Bezier)
    {
        Kind = kind;
        Name = kind.ToString();
    }

    /// <summary>Which curve the next one is. Changing it changes nothing already drawn - that is the panel's to do.</summary>
    public CanvasCurve Kind { get; set; }

    /// <summary>A curve is placed over several clicks, so it outlives the button and the canvas has to be told.</summary>
    public bool IsBusy => _making != null;

    public string Name { get; set; }

    public string Icon { get; set; } = "ToolCurveIcon";

    public Key Shortcut { get; set; } = Key.B;

    public string Description { get; set; } = "click to place points, double click to finish";

    public Cursor Cursor { get; set; } = Cursors.Crosshair;

    /// <summary>A curve drawn by hand is part of a drawing. The curve between two sockets is a WIRE, and it is drawn by
    /// the gesture that joins them rather than by a tool of its own.</summary>
    public bool WorksIn(CanvasMode mode) => mode == CanvasMode.Drawing;

    public void OnPressed(InfiniteCanvas canvas, CanvasPointerEventArgs e)
    {
        if (e.Button != MouseButtons.Left || canvas.Scene == null) return;

        // The second click ENDS it, and is taken here so that it does not also place a point - a curve that gained a
        // duplicate of its last point every time it was finished would bend oddly at the end for no visible reason.
        if (e.ClickCount >= 2 && _making != null)
        {
            Finish(canvas);
            e.Handled = true;
            return;
        }

        if (_making == null)
        {
            _making = new CurveItem(Kind, null, canvas.Ink?.Copy(),
                canvas.ScreenToWorldLength(canvas.InkThickness));
        }

        _making.Add(e.World);
        _pointer = e.World;

        canvas.InvalidateRender(false);
        e.Handled = true;
    }

    public void OnMoved(InfiniteCanvas canvas, CanvasPointerEventArgs e)
    {
        if (_making == null) return;

        _pointer = e.World;
        canvas.InvalidateRender(false);
        e.Handled = true;
    }

    /// <summary>Nothing: the button going up is not what places a point here, the button going DOWN is.</summary>
    public void OnReleased(InfiniteCanvas canvas, CanvasPointerEventArgs e)
    {
    }

    public void OnKey(InfiniteCanvas canvas, KeyEventArgs e)
    {
        if (_making == null) return;

        // Enter finishes it too. A double click is the gesture, and a key is what the hand already on the keyboard
        // reaches for - Escape is the canvas's "give up", which Cancel below is.
        if (e.Key != Key.Enter) return;

        Finish(canvas);
        e.Handled = true;
    }

    public void Render(IDrawingSession session, InfiniteCanvas canvas)
    {
        // The curve so far PLUS where the pointer is: without that last point the line stops at the last click and the
        // hand is drawing blind. HANDED to the item rather than added to it - drawing runs on its own thread, and a
        // tool that added a point, drew and took it off again raced every click.
        _making?.Render(session, canvas, _pointer);
    }

    public void Cancel(InfiniteCanvas canvas)
    {
        if (_making == null) return;

        _making = null;
        canvas.InvalidateRender(false);
    }

    private void Finish(InfiniteCanvas canvas)
    {
        var made = _making;
        _making = null;

        // Two points at the least: one is a dot nobody asked for, and a curve through one point draws nothing at all.
        if (made != null && made.Count >= 2)
        {
            canvas.Scene?.Add(made);
            canvas.Select(made, false);
        }

        canvas.InvalidateRender(false);
    }
}
