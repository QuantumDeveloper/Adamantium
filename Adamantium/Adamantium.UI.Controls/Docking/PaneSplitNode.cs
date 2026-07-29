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

        newChild.Fraction = oldChild.Fraction;   // the replacement stands in the same space
        newChild.Parent = this;
        oldChild.Parent = null;
        Children[index] = newChild;
    }

    /// <summary>Scales the children's shares so they add up to 1. Every operation that adds or removes a child ends
    /// here: shares that do not sum to one leave the layout with a gap or an overlap, and the error accumulates.</summary>
    public void NormalizeFractions()
    {
        if (Children.Count == 0) return;

        var total = 0.0;
        foreach (var child in Children) total += child.Fraction;

        if (total <= 0)
        {
            var even = 1.0 / Children.Count;
            foreach (var child in Children) child.Fraction = even;
            return;
        }

        foreach (var child in Children) child.Fraction /= total;
    }
}
