using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>What the plain left button DOES. A swappable object, so that the canvas knows about a camera, a grid and a
/// scene and about no gesture at all: the pen, the selection frame and every shape are the same size of thing, and one
/// more of them is a new class rather than another branch in the control.
/// <para>The canvas keeps panning and zooming for itself - the middle button, space, the wheel, Home - because those
/// are how you LOOK at a drawing rather than how you change it, and a tool that could take them away would have to give
/// them back.</para></summary>
public interface ICanvasTool
{
    /// <summary>Whether a gesture is in progress that spans releases of the button - a line being drawn point to point.
    /// The canvas asks so that swapping tools, or losing the pointer, can finish it rather than leave it hanging.
    /// </summary>
    bool IsBusy { get; }

    void OnPressed(InfiniteCanvas canvas, CanvasPointerEventArgs e);

    void OnMoved(InfiniteCanvas canvas, CanvasPointerEventArgs e);

    void OnReleased(InfiniteCanvas canvas, CanvasPointerEventArgs e);

    /// <summary>A KEY, offered before the canvas reads it for itself. Empty by default: most tools answer the pointer
    /// only, and making every one of them carry an empty method would say nothing about them.
    /// <para>First refusal, and it matters: the canvas spends Delete on the selection, Escape on letting go and space on
    /// panning, and a tool with a caret in a word needs all three to mean what they mean while typing.</para></summary>
    void OnKey(InfiniteCanvas canvas, KeyEventArgs e)
    {
    }

    /// <summary>Characters, already turned into text by the keyboard layout - which is the only thing that knows what a
    /// key means on the user's own keyboard.</summary>
    void OnText(InfiniteCanvas canvas, TextInputEventArgs e)
    {
    }

    /// <summary>Draw what is being made but is not in the scene yet, and whatever the tool shows about itself. Called
    /// last, over the scene, in SCREEN coordinates.</summary>
    void Render(IDrawingSession session, InfiniteCanvas canvas);

    /// <summary>Finish or abandon whatever is in progress - the tool is being put down, or the pointer is gone.</summary>
    void Cancel(InfiniteCanvas canvas);
}
