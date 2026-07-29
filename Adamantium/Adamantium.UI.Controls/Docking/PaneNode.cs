namespace Adamantium.UI.Controls.Docking;

/// <summary>
/// A node of a docking layout. Exactly two kinds exist - a split and a group - and a third must not be added: every
/// operation here is written as "one or the other", and a third kind turns each of them into a guessing game.
/// <para>These are DATA, not controls. A layout can therefore be built, changed and asserted on in a test without a
/// window, which is the whole reason the model is separate: a layout that can only be produced by dragging cannot be
/// tested, and so will not be.</para>
/// </summary>
public abstract class PaneNode
{
    /// <summary>How much of its parent split this node takes - so many pixels, or a weight in what is left over.
    /// Meaningless (and ignored) on a root, which takes everything.
    /// <para>ONE number, deliberately. It used to be two - a fraction AND a pixel hint - and every bug in this layout
    /// came from the seam between them: they had to be kept in step across splits, moves, rebuilds and divider drags,
    /// and each of those was a place they drifted. A pane whose fraction said half while its hint said 160 looked
    /// correct right up until the hint stopped applying, and then jumped to half the window.</para>
    /// <para>It lives on the CHILD rather than in a list on the parent so it travels with the node it describes: moving
    /// a child around cannot leave it wearing its neighbour's size.</para></summary>
    public Panels.PaneLength Length { get; set; } = Panels.PaneLength.Star;

    /// <summary>The split this node hangs from, or null for a root's top node.</summary>
    public PaneSplitNode Parent { get; internal set; }
}
