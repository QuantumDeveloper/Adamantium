using System.Collections.ObjectModel;
using Adamantium.MVVM;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>A node of the in-memory demo tree on the Drag &amp; Drop tab: a plain <see cref="Title"/> plus optional
/// <see cref="Children"/>, so drag-drop (ghost, insertion caret, spring-load auto-expand) can be watched on a TreeView.
/// <see cref="IsExpanded"/>/<see cref="IsSelected"/> are bound two-way to the container via the tree's ItemContainerStyle.</summary>
[ViewModel]
public partial class DragTreeNode : AdamantiumViewModel
{
    public DragTreeNode(string title, params DragTreeNode[] children)
    {
        Title = title;
        foreach (var child in children) { child.Parent = this; Children.Add(child); }
    }

    /// <summary>The node's label - also the drag payload when a node is dragged out.</summary>
    public string Title { get; }

    /// <summary>The owning node, or null for a root node - so a sibling (before/after) drop knows which collection to
    /// insert into. Maintained by whoever adds the node.</summary>
    public DragTreeNode Parent { get; set; }

    /// <summary>Child nodes (empty = a leaf).</summary>
    public ObservableCollection<DragTreeNode> Children { get; } = [];

    [Bindable] private bool _isExpanded;
    [Bindable] private bool _isSelected;

    public override string ToString() => Title;
}
