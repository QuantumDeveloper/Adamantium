namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>WHAT A NODE IS, as against the shell it is held in.
/// <para>The shell is the same for every node - a title, a place, sockets, a fold. Everything that differs between a
/// Multiply and a Texture lives here: the sockets that kind has, and the state a person edits in its body. Changing a
/// node's kind swaps this and leaves the node itself alone, so the selection, the undo step and every wire that
/// survives the new shape go on pointing at the same object.</para>
/// <para>It is also the node's CONTENT: the body template is chosen for its type and binds to its properties. There is
/// no separate payload beside it - that would be a second name for the same thing.</para></summary>
public interface ICanvasNodeSpecialization
{
    /// <summary>Puts this kind's sockets on the node. Called when it is installed, with the node's own lists to fill -
    /// they live on the node, because an inspector edits them and a wire sits on them.</summary>
    void Shape(ICanvasNode node);
}