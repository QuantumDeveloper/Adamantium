using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Input;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>Ink. Free, the button draws a stroke that is smoothed when it is lifted; snapped to the grid it places
/// VERTICES and the line runs straight from one to the next until a double click ends it.
/// <para>Those really are two gestures and not one with a setting, because a snap says where the points ARE: everything
/// a freehand drag would record between two marks is exactly what the snap says is not there. So under a snap there is
/// nothing to smooth either - every vertex is somewhere that was aimed at.</para></summary>
public class PenTool : ICanvasTool
{
    // How far the pointer must move, in SCREEN pixels, before a freehand stroke takes another point. A pointer reports
    // whole pixels, so a hand holding still still reports movement - and every one of those would be a point.
    private const double MinimumStep = 2.5;

    private StrokeItem _drawing;
    private Vector2 _from;
    private bool _chaining;

    public bool IsBusy => _chaining;

    /// <summary>How a rail shows this tool - see <see cref="SelectTool.Name"/>.</summary>
    public string Name { get; set; } = "Pen";

    public string Icon { get; set; } = "ToolPenIcon";

    public Key Shortcut { get; set; } = Key.P;

    public string Description { get; set; } = "draw freehand, or point to point under a grid";

    /// <summary>A crosshair: what a pen leaves starts exactly under the point, and an arrow's tip is not where a person
    /// reads it as being.</summary>
    public Cursor Cursor { get; set; } = Cursors.Crosshair;

    /// <summary>INK, and ink is a drawing. There is no such thing as a pen stroke in a graph.</summary>
    public bool WorksIn(CanvasMode mode) => mode == CanvasMode.Drawing;

    public void OnPressed(InfiniteCanvas canvas, CanvasPointerEventArgs e)
    {
        if (e.Button != MouseButtons.Left || canvas.Scene == null || canvas.Ink == null) return;

        // The second click on the last vertex finishes the line. Read from the click COUNT, which is where a double
        // click lives in this engine, and taken here so the press does not also place another vertex.
        if (e.ClickCount >= 2 && _chaining)
        {
            Finish(canvas);
            e.Handled = true;
            return;
        }

        _from = e.Screen;

        if (canvas.SnapToGrid)
        {
            if (_drawing == null)
            {
                _drawing = new StrokeItem(e.World, canvas.Ink.Copy(), canvas.ScreenToWorldLength(canvas.InkThickness));
                _drawing.Add(e.World);
                _chaining = true;
            }

            // One more point every press: the one just fixed keeps where it was clicked, and this is the next vertex,
            // which follows the pointer until the click that fixes it in turn.
            _drawing.Add(e.World);

            canvas.Repaint();
            e.Handled = true;
            return;
        }

        _drawing = new StrokeItem(e.World, canvas.Ink.Copy(), canvas.ScreenToWorldLength(canvas.InkThickness));
        _drawing.Add(e.World);

        canvas.CaptureMouse();
        canvas.Repaint();
        e.Handled = true;
    }

    public void OnMoved(InfiniteCanvas canvas, CanvasPointerEventArgs e)
    {
        if (_drawing == null) return;

        // Point to point: between two clicks there is ONE vertex that follows the pointer, and a move moves it rather
        // than recording anything.
        if (_chaining)
        {
            _drawing.MoveLast(e.World);
            canvas.Repaint();
            e.Handled = true;
            return;
        }

        // Points closer together than this are not a shape, they are the pointer reporting whole pixels while the hand
        // holds still. Kept OUT of the stroke rather than smoothed away afterwards: a point that was never recorded
        // costs nothing to draw, to hit-test or to save.
        if ((e.Screen - _from).Length() < MinimumStep) return;

        _from = e.Screen;
        _drawing.Add(e.World);

        canvas.Repaint();
        e.Handled = true;
    }

    public void OnReleased(InfiniteCanvas canvas, CanvasPointerEventArgs e)
    {
        // A line drawn point to point spans presses, so a LIFT ends nothing - only the double click does.
        if (_chaining)
        {
            e.Handled = true;
            return;
        }

        if (_drawing == null) return;

        var finished = _drawing;
        _drawing = null;

        // Smoothed on the LIFT, which is the first moment it is known where the stroke went. While it was being drawn it
        // showed the points as they arrived, and that difference is honest: the two really are different amounts of
        // information about the same movement. Both numbers are SCREEN distances turned into world ones - what may be
        // moved without anyone seeing it, and how far apart the points of a finished stroke need to be to read as smooth.
        finished.Smooth(canvas.ScreenToWorldLength(0.6), canvas.ScreenToWorldLength(4));

        canvas.ReleaseMouseCapture();
        canvas.Place(finished);
        canvas.Repaint();

        e.Handled = true;
    }

    public void Render(IDrawingSession session, InfiniteCanvas canvas) => _drawing?.Render(session, canvas);

    public void Cancel(InfiniteCanvas canvas)
    {
        if (_chaining) Finish(canvas);

        _drawing = null;
        _chaining = false;
    }

    private void Finish(InfiniteCanvas canvas)
    {
        var finished = _drawing;
        _drawing = null;
        _chaining = false;

        if (finished == null) return;

        // The vertex that was still following the pointer never became one.
        finished.RemoveLast();

        if (finished.Points.Count >= 2) canvas.Place(finished);
        canvas.Repaint();
    }
}
