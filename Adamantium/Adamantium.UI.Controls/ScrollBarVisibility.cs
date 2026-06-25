namespace Adamantium.UI.Controls;

/// <summary>When a <see cref="ScrollViewer"/> shows a scrollbar on a given axis. Mirrors WPF's ScrollBarVisibility.</summary>
public enum ScrollBarVisibility
{
    /// <summary>No scrolling on this axis: the content is constrained to the viewport and the bar never appears.</summary>
    Disabled,

    /// <summary>The bar appears only while the content is larger than the viewport on this axis.</summary>
    Auto,

    /// <summary>The bar never appears, but scrolling (wheel / programmatic) is still allowed.</summary>
    Hidden,

    /// <summary>The bar is always shown.</summary>
    Visible
}
