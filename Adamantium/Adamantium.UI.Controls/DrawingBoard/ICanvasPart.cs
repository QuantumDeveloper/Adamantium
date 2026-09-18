namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>A PIECE OF A CANVAS'S CHROME - the tool rail, the view bar, the map, the inspector: a control that only
/// means anything pointed at an <see cref="InfiniteCanvas"/>.
/// <para>Said as an interface so the <see cref="CanvasPane"/> a piece rides in can simply hand it the canvas. A binding
/// would not do: a pane that is not on screen yet - a bar that waits for a selection, a list that waits to be asked -
/// has no template applied and therefore no tree for a binding to resolve through, so the piece inside it would learn
/// which canvas it belongs to only after it was first shown. It needs to know BEFORE that, because knowing is how it
/// decides whether to be shown at all.</para></summary>
public interface ICanvasPart
{
    /// <summary>The plane this piece is for.</summary>
    InfiniteCanvas Canvas { get; set; }
}
