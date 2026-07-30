namespace Adamantium.UI.Controls.Docking;

/// <summary>
/// What a pane IS, which is what decides how its group is dressed. The two look different in every editor, and for a
/// reason rather than for taste.
/// </summary>
public enum PaneKind
{
    /// <summary>A document: tabs across the TOP, no header. There are many of them, they come and go with the work, and
    /// the tab is the whole of their chrome - a caption bar per document would be a caption bar per file.</summary>
    Document,

    /// <summary>A tool: a HEADER on top carrying its name and its buttons, tabs along the bottom. There are few of them,
    /// they are part of the workspace rather than of the work, and the header is where "pin" and "close" live - the two
    /// things you do to a tool and never to a document.</summary>
    Tool
}
