namespace Adamantium.UI.Controls.Docking;

/// <summary>
/// The window a floating root lives in. Inside it is an ordinary <see cref="DockingArea"/> showing that root, which is
/// what makes a floating panel a full participant: its own tab strip, its own compass, and every gesture the main
/// window has.
/// <para>A TYPE of its own rather than a plain <see cref="Window"/> so that everything about a floating panel's window -
/// its chrome, how big it starts, whether it may be maximised, how a theme dresses it - has ONE place to be said and
/// one selector to be styled by. Configured at the point where a window is opened, those answers would be scattered
/// across the two gestures that open one, and a theme could not reach them at all.</para>
/// <para>It carries no behaviour of its own on purpose: the layout is the truth, and this is a frame around a view of
/// it. Anything it did for itself would be a second opinion about a root.</para>
/// </summary>
public class DockingWindow : Window
{
    /// <summary>The area showing this window's root. Held so the docking system can find its way from a window back to
    /// the root it stands for - the window is what the platform hands back from a move, and the root is what the model
    /// knows.</summary>
    public DockingArea Area { get; internal set; }
}
