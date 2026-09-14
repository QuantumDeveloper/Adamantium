namespace Adamantium.UI.Controls;

/// <summary>How <see cref="InfiniteCanvas"/> draws the plane behind its content.
/// <para>The first three values all paint a PLANE and differ in what is on it. <see cref="Transparent"/> is the one that
/// is not like the others: there is no plane at all.</para></summary>
public enum CanvasGridStyle
{
    /// <summary>A plain plane: the ground is painted, the world's axes are marked, and there are no grid marks.</summary>
    None,

    /// <summary>A dot where the lines would cross. Quieter than lines, and the usual choice for drawing on.</summary>
    Dots,

    /// <summary>Full lines both ways - a drafting grid, for work that is measured rather than drawn.</summary>
    Lines,

    /// <summary>GLASS: no ground, no axes, no marks - nothing is painted behind the content at all, and whatever the
    /// canvas is laid over shows straight through it.
    /// <para>This is what turns the control into a sheet of annotation over something else - a whiteboard over a shared
    /// screen, a mark-up layer over a form. The other three are a surface you draw ON; this one is a surface you draw
    /// OVER, and the difference is the whole point of it.</para>
    /// <para>It also costs nothing: the ground is one quad covering the whole viewport with a shader on it, and here
    /// that quad is not drawn at all rather than drawn and discarded.</para>
    /// <para>Two things stay the canvas's own business and are worth setting deliberately with it: whether presses reach
    /// what is underneath (<c>IsHitTestVisible</c>) and whether the panel is shown at all
    /// (<see cref="InfiniteCanvas.IsOverlayVisible"/>) - an annotation layer is usually driven from somewhere else
    /// entirely.</para></summary>
    Transparent
}
