namespace Adamantium.UI.Core.Input;

/// <summary>
/// An element that "springs open" when a drag hovers (dwells) over it - a <c>TabItem</c> activates, a TreeView node
/// expands, an expander opens - so you can drag INTO content that is not visible yet. The drag engine runs a dwell timer:
/// hold the drag over the element for a moment and it calls <see cref="SpringLoad"/>. One contract, many consumers.
/// </summary>
public interface ISpringLoadable
{
    /// <summary>Activate / expand this element (the drag has dwelled over it). Idempotent.</summary>
    void SpringLoad();
}
