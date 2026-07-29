using System.Collections.Generic;
using Adamantium.UI.Controls.Panels;

namespace Adamantium.UI.Controls.Docking;

/// <summary>
/// A split: children laid one after another along <see cref="Orientation"/>, each taking its own share. Splits nest
/// inside splits - that recursion IS the layout tree.
/// </summary>
public class PaneSplitNode : PaneNode
{
    public Orientation Orientation { get; set; } = Orientation.Horizontal;

    public List<PaneNode> Children { get; } = new();

    public void Add(PaneNode child)
    {
        child.Parent = this;
        Children.Add(child);
    }

    public void Insert(int index, PaneNode child)
    {
        if (index < 0) index = 0;
        if (index > Children.Count) index = Children.Count;
        child.Parent = this;
        Children.Insert(index, child);
    }

    public bool Remove(PaneNode child)
    {
        if (!Children.Remove(child)) return false;
        child.Parent = null;
        return true;
    }

    public void Replace(PaneNode oldChild, PaneNode newChild)
    {
        var index = Children.IndexOf(oldChild);
        if (index < 0) return;

        newChild.Length = oldChild.Length;   // the replacement stands in the same space
        newChild.Parent = this;
        oldChild.Parent = null;
        Children[index] = newChild;
    }

    /// <summary>Gives every child that has nothing to say a share of its own. Lengths do NOT have to add up to anything -
    /// fixed ones take their pixels and the starred ones divide what is left, which is what makes a row survive a pane
    /// arriving or leaving without everyone shuffling. The only thing to repair is a child left with no length at all.
    /// </summary>
    public void NormalizeLengths()
    {
        foreach (var child in Children)
        {
            if (child.Length.Value <= 0) child.Length = PaneLength.Star;
        }
    }
}
