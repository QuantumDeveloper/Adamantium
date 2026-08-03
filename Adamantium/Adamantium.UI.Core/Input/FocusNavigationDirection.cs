namespace Adamantium.UI.Core.Input;

/// <summary>Which way keyboard navigation is asking to move. <see cref="Next"/>/<see cref="Previous"/> are Tab and
/// Shift+Tab - an order, not a geometry; the four others are the arrow keys, which a panel answers from its own
/// layout.</summary>
public enum FocusNavigationDirection
{
    Next,
    Previous,
    Up,
    Down,
    Left,
    Right
}
