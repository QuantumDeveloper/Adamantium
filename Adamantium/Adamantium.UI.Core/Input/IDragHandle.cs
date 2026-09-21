namespace Adamantium.UI.Core.Input;

/// <summary>
/// A control that IS a drag grip: a source containing an active one starts a drag only from it, leaving the rest of the
/// row free to click and select. The same thing <c>DragDrop.IsDragHandle</c> does for an arbitrary element - this is the
/// form a real control takes, so the drag engine can recognise it without the control having to reach up to
/// <c>DragDrop</c> (which lives above the controls assembly).
/// </summary>
public interface IDragHandle
{
    /// <summary>False takes the grip out of the picture entirely: the source then has no handle and drags by its whole
    /// body again. That is what a "drag only by the handle" switch binds to.</summary>
    bool IsDragHandleActive { get; }
}
