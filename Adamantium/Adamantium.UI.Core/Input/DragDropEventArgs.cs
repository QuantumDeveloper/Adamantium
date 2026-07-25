using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Input;

/// <summary>
/// The argument delivered to a drop target - as a <c>DropCommand</c> parameter (MVVM: no UI types in the VM) and, later,
/// as the routed Drag* events' payload. The target reads <see cref="Data"/> and sets <see cref="Effects"/> (which drives
/// the cursor). Named DragDrop* to avoid the existing <c>DragEventArgs</c> (a Thumb drag-delta).
/// </summary>
public class DragDropEventArgs
{
    public DragDropEventArgs(IDataPackage data, object source, Vector2 position)
    {
        Data = data;
        Source = source;
        Position = position;
    }

    /// <summary>The dragged payload.</summary>
    public IDataPackage Data { get; }

    /// <summary>The drag source element (the one that carried <c>DragDrop.DragData</c>).</summary>
    public object Source { get; }

    /// <summary>Pointer position in the drop target's coordinate space.</summary>
    public Vector2 Position { get; }

    /// <summary>What the target will do with the payload - set by the target; drives the cursor and the source's outcome.</summary>
    public DragDropEffects Effects { get; set; } = DragDropEffects.Move;
}
