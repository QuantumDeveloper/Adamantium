using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Input;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>Puts a NODE on the plane: a press asks which kind, and the pick puts it there.
/// <para>Asks rather than places, because which kind is the one thing a tool cannot know - the canvas carries the
/// list, the application wrote it, and the same list answers a wire let go over nothing. One question, one list, one
/// way onto the graph.</para></summary>
public class NodeTool : ICanvasTool
{
    public string Name { get; set; } = "Node";

    public string Icon { get; set; } = "ToolNodeIcon";

    public string Description { get; set; } = "put a node on the plane";

    public string Group { get; set; } = string.Empty;

    public Key Shortcut { get; set; } = Key.None;

    public Cursor Cursor { get; set; } = Cursors.Crosshair;

    // The tool this GESTURE was handed to, when the press landed on something already there. Held for as long as the
    // gesture lasts: a press that means "take hold of this node" is answered by a drag and a release, and half a
    // gesture delivered somewhere else is a node that jumps.
    private ICanvasTool _through;

    /// <summary>Busy while the gesture it handed on is. Nothing is ever half-made HERE: the press asks, and the list
    /// answers or it does not.</summary>
    public bool IsBusy => _through is { IsBusy: true };

    /// <summary>A tool of the GRAPH: a node has no business in a drawing any more than a pen has in a graph.</summary>
    public bool WorksIn(CanvasMode mode) => mode == CanvasMode.Nodes;

    public void OnPressed(InfiniteCanvas canvas, CanvasPointerEventArgs e)
    {
        if (e.Button != MouseButtons.Left || canvas == null) return;

        // SOMETHING IS ALREADY THERE, so the press is about that and not about making another. Handed to the tool the
        // canvas rests on - which is what a press on a node means anywhere: take hold of it. Putting a new node on top
        // of the one that was clicked is the one thing it cannot mean.
        if (canvas.ItemAt(e.World) != null)
        {
            _through = canvas.DefaultTool;
            _through?.OnPressed(canvas, e);
            return;
        }

        canvas.AskForNode(e.World);
        e.Handled = true;
    }

    public void OnMoved(InfiniteCanvas canvas, CanvasPointerEventArgs e) => _through?.OnMoved(canvas, e);

    public void OnReleased(InfiniteCanvas canvas, CanvasPointerEventArgs e)
    {
        _through?.OnReleased(canvas, e);
        _through = null;
    }

    public void Render(IDrawingSession session, InfiniteCanvas canvas) => _through?.Render(session, canvas);

    public void Cancel(InfiniteCanvas canvas)
    {
        _through?.Cancel(canvas);
        _through = null;
    }
}
