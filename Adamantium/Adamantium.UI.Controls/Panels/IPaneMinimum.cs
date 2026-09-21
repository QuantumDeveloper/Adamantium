namespace Adamantium.UI.Controls.Panels;

/// <summary>
/// Something that can say how small it may become along an axis. A splitter asks its neighbours this before squeezing
/// them, so the answer comes from whoever actually knows - a group knows what its panes declared and how tall its own
/// tab strip is; a nested host knows because squeezing it squeezes its children.
/// <para>An interface rather than a check for concrete types: this lives in the panels layer, and the docking controls
/// sit above it. The panel must not have to know what a pane group is in order to ask it a question.</para>
/// </summary>
public interface IPaneMinimum
{
    /// <summary>Smallest extent in pixels along <paramref name="orientation"/>, or 0 for "no opinion".</summary>
    double MinimumExtent(Orientation orientation);
}
