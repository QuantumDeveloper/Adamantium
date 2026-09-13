namespace Adamantium.UI.Controls;

/// <summary>How <see cref="InfiniteCanvas"/> draws the plane behind its content.</summary>
public enum CanvasGridStyle
{
    /// <summary>Nothing behind the content.</summary>
    None,

    /// <summary>A dot where the lines would cross. Quieter than lines, and the usual choice for drawing on.</summary>
    Dots,

    /// <summary>Full lines both ways - a drafting grid, for work that is measured rather than drawn.</summary>
    Lines
}
