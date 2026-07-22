namespace Adamantium.UI.Controls;

/// <summary>One row of the flattened, virtualization-ready view of a tree: a data <see cref="Node"/> at a given
/// <see cref="Depth"/> (indent level), visible because all its ancestors are expanded. A realized row container mirrors
/// this row's state (indent, expander glyph, selection) - the row is the model, the container the view, so the state
/// survives recycling. Cheap by design (a plain wrapper, not a container), so a level of thousands of siblings costs
/// thousands of these, not thousands of controls.</summary>
internal sealed class TreeRow
{
    public TreeRow(object node, int depth, bool hasChildren)
    {
        Node = node;
        Depth = depth;
        HasChildren = hasChildren;
    }

    /// <summary>The data item this row shows.</summary>
    public object Node { get; }

    /// <summary>Indent level: 0 for a root, +1 per nesting step.</summary>
    public int Depth { get; }

    /// <summary>Whether the node can expand (has children) - drives the expander's visibility. Settable: a lazily-loaded
    /// branch can turn out to have (or not have) children once read.</summary>
    public bool HasChildren { get; set; }

    /// <summary>Whether this branch is open (its children are spliced into the flat list). Owned by the flattener.</summary>
    public bool IsExpanded { get; set; }

    /// <summary>Whether this row is selected. Owned by the TreeView's selection policy; survives virtualization here so a
    /// selected row scrolled out and back is still highlighted.</summary>
    public bool IsSelected { get; set; }
}
