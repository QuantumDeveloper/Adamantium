namespace Adamantium.Multiverse;

/// <summary>
/// Whether an output is on screen, and if not, why.
/// </summary>
public enum OutputState
{
    /// <summary>On screen: rendered and presented.</summary>
    Shown,

    /// <summary>Hidden in the UI on purpose.</summary>
    Hidden,

    /// <summary>Not in view - taken off the visual tree, e.g. its tab is not the selected one.</summary>
    OutOfView,

    /// <summary>Its window is minimized.</summary>
    Minimized
}
