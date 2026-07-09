using System.Collections.Generic;
using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Input;

/// <summary>
/// Implemented by a container that can resolve which of its (many) visual children a hit-test point could possibly hit,
/// so the hit-test recursion visits only those instead of every child. A virtualizing panel lays its tiles at absolute,
/// non-overlapping slots, so a point maps to ONE slot by arithmetic - turning an O(realized-tiles) walk (thousands of
/// nodes, the mouse-move freeze) into O(1). Return <c>null</c> to fall back to the default "recurse all children" walk.
/// </summary>
public interface IHitTestChildren
{
    /// <summary>The children (in the element's local space) the point <paramref name="localPoint"/> could hit, or null
    /// to let the caller walk all visual children.</summary>
    IReadOnlyList<IUIComponent> GetHitTestChildren(Vector2 localPoint);
}
