namespace Adamantium.UI.Core.Input;

/// <summary>Where a drop lands relative to the item it is over - the hybrid tree-drop model. A flat list uses only
/// <see cref="None"/> (its position comes from <c>InsertBefore</c>/<c>InsertIndex</c>); a hierarchy adds sibling
/// (<see cref="Before"/>/<see cref="After"/>) vs child (<see cref="Into"/>) placement against <c>DropTarget</c>.</summary>
public enum DropPlacement
{
    /// <summary>No hierarchical placement - a plain list drop (use InsertBefore/InsertIndex).</summary>
    None,

    /// <summary>Insert as a sibling BEFORE the target item.</summary>
    Before,

    /// <summary>Insert as a sibling AFTER the target item.</summary>
    After,

    /// <summary>Insert as a CHILD of the target item.</summary>
    Into
}
