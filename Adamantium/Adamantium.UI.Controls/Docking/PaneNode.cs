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
    /// <summary>Share of the parent split this node takes, 0..1. Meaningless (and ignored) on a root.
    /// <para>The fraction lives on the CHILD rather than in a list on the parent so that it travels with the node it
    /// describes: moving a child around cannot leave it wearing its neighbour's size.</para></summary>
    public double Fraction { get; set; } = 1.0;

    /// <summary>The split this node hangs from, or null for a root's top node.</summary>
    public PaneSplitNode Parent { get; internal set; }

    /// <summary>Initial size in PIXELS, or NaN. Only a hint, and only for the first layout: the author says "the
    /// inspector starts about 220 wide" without knowing the window's size, and the first arrange turns it into a
    /// fraction. From then on the fraction is the truth - the pixel number would be a lie the moment the window
    /// resizes or the user drags a divider.</summary>
    public double DesiredSize { get; set; } = double.NaN;
}
