namespace Adamantium.UI.Controls;

/// <summary>How many nodes a <see cref="TreeView"/> may have selected at once.</summary>
public enum TreeViewSelectionMode
{
    /// <summary>One node at a time - selecting another clears the previous (the default).</summary>
    Single,

    /// <summary>Any number of nodes - each click toggles that node on/off, leaving the others as they are.</summary>
    Multiple,

    /// <summary>Like a list box: a plain click selects one node, Ctrl+click toggles a node into/out of the selection, and
    /// Shift+click selects the visible range from the anchor (the last plain/Ctrl click) to the clicked node.</summary>
    Extended
}
