using Adamantium.Mathematics;
using Adamantium.UI.Core.Input;

namespace Adamantium.UI.Controls;

/// <summary>What a tool is told about the pointer. In WORLD coordinates, because a tool works on the drawing and not on
/// the viewport - the screen position is here too, but only for the things that are honestly screen-sized: how big a
/// grip is, how far a drag has to go before it counts.</summary>
public sealed class CanvasPointerEventArgs
{
    /// <summary>Where the pointer is, in the world - already pulled to the grid when the canvas pulled it, so a tool
    /// never has to know that snapping exists. This is what a tool PLACES things at.</summary>
    public Vector2 World { get; init; }

    /// <summary>Where the pointer actually is, with no snap applied. This is what a tool PICKS with: a snap says where a
    /// new thing goes, not where an existing one is, and hit-testing the pulled point means aiming at a line and
    /// selecting whatever happens to be at the nearest grid crossing instead.</summary>
    public Vector2 Pointer { get; init; }

    /// <summary>Where it is on screen, in pixels.</summary>
    public Vector2 Screen { get; init; }

    /// <summary>Whether <see cref="World"/> was pulled to a grid mark rather than being where the pointer actually is.
    /// </summary>
    public bool IsSnapped { get; init; }

    public MouseButtons Button { get; init; }

    public int ClickCount { get; init; }

    public InputModifiers Modifiers { get; init; }

    /// <summary>Set by the tool when it has taken the event: the canvas then keeps it away from panning and marks the
    /// routed event handled.</summary>
    public bool Handled { get; set; }

    /// <summary>Whether a key that ADDS to a selection is down rather than replacing it - Ctrl or Shift, both, because
    /// both are what people already press.</summary>
    public bool Extends =>
        (Modifiers & (InputModifiers.LeftControl | InputModifiers.RightControl |
                      InputModifiers.LeftShift | InputModifiers.RightShift)) != 0;
}
