namespace Adamantium.UI.Controls;

/// <summary>
/// Where a <see cref="Popup"/> positions its child relative to its <see cref="Popup.PlacementTarget"/>. The popup
/// re-evaluates this every frame, so it follows a moving target (e.g. a tooltip riding a slider thumb).
/// </summary>
public enum PlacementMode
{
    /// <summary>Below the target, left edges aligned.</summary>
    Bottom,

    /// <summary>Above the target, left edges aligned.</summary>
    Top,

    /// <summary>To the left of the target, top edges aligned.</summary>
    Left,

    /// <summary>To the right of the target, top edges aligned.</summary>
    Right,

    /// <summary>Centered over the target.</summary>
    Center,

    /// <summary>At the target's top-left corner (then offset) - the "exact position" mode.</summary>
    Relative
}
