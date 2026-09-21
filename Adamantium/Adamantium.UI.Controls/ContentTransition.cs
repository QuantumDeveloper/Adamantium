namespace Adamantium.UI.Controls;

/// <summary>How a <see cref="ContentControl"/> animates the swap from the old content to the new one.</summary>
public enum ContentTransition
{
    /// <summary>No animation - the new content replaces the old immediately.</summary>
    None,

    /// <summary>New content slides in from the right; the old content slides out to the left.</summary>
    SlideLeft,

    /// <summary>New content slides in from the left; the old content slides out to the right.</summary>
    SlideRight,

    /// <summary>New content slides in from below; the old content slides out upward.</summary>
    SlideUp,

    /// <summary>New content slides in from above; the old content slides out downward.</summary>
    SlideDown,
}
