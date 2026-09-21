namespace Adamantium.UI.Controls;

/// <summary>How much room a ribbon command takes in its group. The author states a RANGE
/// (<see cref="Ribbon.MaxSizeProperty"/> / <see cref="Ribbon.MinSizeProperty"/>); the group decides the actual
/// <see cref="Ribbon.SizeProperty"/> within it.</summary>
public enum RibbonSize
{
    /// <summary>Big icon over the label - one per column.</summary>
    Large,

    /// <summary>Small icon with the label beside it - three per column.</summary>
    Medium,

    /// <summary>Icon only - three per column.</summary>
    Small
}
