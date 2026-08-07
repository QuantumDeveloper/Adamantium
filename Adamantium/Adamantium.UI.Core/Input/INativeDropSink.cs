using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Input;

/// <summary>
/// What a platform's native drop target calls back into - implemented by the drag-drop engine, so an OS-driven drag
/// (files from Explorer/Finder, text from another app) drives exactly the same targeting, highlight, insertion cue and
/// <c>DropCommand</c> delivery as an in-app drag (docs/DRAG_DROP_PLAN.md phase 5).
/// <para>
/// THREADING: these run on the platform's window/message thread, INSIDE the drag source's modal loop, and must return
/// promptly - the engine posts the tree work onto the UI loop thread and answers with the effect it settled on last move.
/// </para>
/// </summary>
public interface INativeDropSink
{
    /// <summary>The pointer entered <paramref name="window"/> carrying <paramref name="data"/> (already read out of the
    /// OS payload into a managed package). Returns the effect to show.</summary>
    DragDropEffects DragEnter(IWindow window, IDataPackage data, PixelPoint screenPoint, InputModifiers modifiers, DragDropEffects allowed);

    /// <summary>The pointer moved inside <paramref name="window"/> during a native drag. Returns the effect to show.</summary>
    DragDropEffects DragOver(IWindow window, PixelPoint screenPoint, InputModifiers modifiers, DragDropEffects allowed);

    /// <summary>The pointer left <paramref name="window"/>, or the native drag was cancelled over it.</summary>
    void DragLeave(IWindow window);

    /// <summary>The payload was released over <paramref name="window"/>. Returns the effect actually applied - the OS
    /// reports it back to the drag source (a Move tells the source app to delete the original, so never answer Move
    /// unless the drop really consumed it).</summary>
    DragDropEffects Drop(IWindow window, IDataPackage data, PixelPoint screenPoint, InputModifiers modifiers, DragDropEffects allowed);
}
