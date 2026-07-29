using System.Collections.Generic;
using Adamantium.UI.Controls.Panels;

namespace Adamantium.UI.Controls.Docking;

/// <summary>
/// A whole docking layout: a FOREST of roots (the main window plus every floating one), and the operations that change
/// it. Both the gestures and any code go through these same methods - there is deliberately no second path, because two
/// paths drift apart and then it matters which one a save reads.
/// </summary>
public class DockingLayout
{
    /// <summary>Format version, written to the saved file. A layout outlives the code that wrote it.</summary>
    public int Version { get; set; } = 1;

    public List<DockingRoot> Roots { get; } = new();

    public DockingRoot Main
    {
        get
        {
            foreach (var root in Roots)
            {
                if (root.IsMain) return root;
            }
            return null;
        }
    }

    /// <summary>
    /// Builds a layout from AUTHORED ZONES - what markup says. The centre is laid first and everything else is docked
    /// around it in declaration order, so the last edge declared is the outermost one, exactly as reading the markup
    /// top to bottom suggests.
    /// <para>This is why markup carries no fractions: the author says "inspector on the right, about 220 wide", and
    /// the tree - which split, in what order, holding what share - is derived here. A share written by hand would be a
    /// share of a split the author cannot see, and would stop being true the first time a divider is dragged.</para>
    /// </summary>
    public static DockingLayout FromZones(IEnumerable<ZoneDeclaration> declarations)
    {
        var layout = new DockingLayout();
        var root = new DockingRoot(null, isMain: true);
        layout.Roots.Add(root);

        foreach (var declaration in declarations)
        {
            var group = declaration.Group;

            if (root.Content == null)
            {
                // The first one takes everything - whether it called itself Center or not. A layout has to start
                // somewhere, and docking against nothing has no meaning.
                root.Content = group;
                continue;
            }

            if (declaration.Zone is DockZone.Center or DockZone.Floating)
            {
                // Center means "with the documents", not "split something": join the group that is already there.
                if (root.Content is PaneGroupNode centre)
                {
                    foreach (var pane in group.PaneIds) centre.Add(pane);
                    continue;
                }
            }

            layout.Split(root.Content, declaration.Zone is DockZone.Center or DockZone.Floating ? DockZone.Right : declaration.Zone, group);

            // AFTER the split, which hands both sides a share of what the target held - that is right for a pane being
            // dropped, and wrong for one the author sized. A number in markup is PIXELS along the zone's own axis ("the
            // inspector starts 240 wide"); saying nothing leaves it taking a share of whatever is left, like the centre.
            if (!double.IsNaN(declaration.Size)) group.Length = PaneLength.Pixels(declaration.Size);
        }

        layout.Normalize();
        return layout;
    }

    /// <summary>Cuts a length in two: the target keeps <paramref name="keep"/> of it, the arrival the rest. Whatever the
    /// pair is worth together is exactly what the one of them was worth, so the row around them does not move.</summary>
    private static PaneLength Halve(PaneLength length, double keep, out PaneLength arrivals)
    {
        arrivals = new PaneLength(length.Value * (1 - keep), length.Unit);
        return new PaneLength(length.Value * keep, length.Unit);
    }

    /// <summary>Splits <paramref name="target"/>, putting <paramref name="inserted"/> on the given side of it and
    /// giving the newcomer <paramref name="fraction"/> of the space they now share.</summary>
    public void Split(PaneNode target, DockZone side, PaneNode inserted, double fraction = 0.5)
    {
        var vertical = side is DockZone.Top or DockZone.Bottom;
        var before = side is DockZone.Left or DockZone.Top;

        // Already splitting the right way? Then this is one more child, NOT a nested split - that is what keeps a
        // layout from growing a level every time something is dropped on the same side.
        if (target.Parent is { } parent && parent.Orientation == (vertical ? Orientation.Vertical : Orientation.Horizontal))
        {
            var at = parent.Children.IndexOf(target);

            // The two of them share what the TARGET had, and nobody else in the row moves - whatever kind of length it
            // was. Splitting the target's own length keeps the pair worth exactly what the one of them was worth: a
            // fixed 160 becomes 80 and 80, a weight of 2 becomes 1 and 1. Giving the arrival a share of the WHOLE row
            // instead made it take far more than the half it was dropped on, and squeezed the neighbours to pay for it.
            target.Length = Halve(target.Length, 1 - fraction, out var arrivals);
            inserted.Length = arrivals;

            parent.Insert(before ? at : at + 1, inserted);
            return;
        }

        var split = new PaneSplitNode { Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal };
        var host = target.Parent;

        // The new split stands exactly where the target stood, so it inherits the target's claim on the OUTER row -
        // "the console area is 160 tall" still holds when that area is two panes side by side. Inside it the pair simply
        // shares, in weights: a pixel number stated for one axis says nothing about the other, and spending it there
        // charged a height as a width.
        var kept = target.Length;
        target.Length = PaneLength.Stars(1 - fraction);
        inserted.Length = PaneLength.Stars(fraction);

        if (host != null)
        {
            host.Replace(target, split);
        }
        else
        {
            // target was a root's top node - the new split takes its place there.
            foreach (var root in Roots)
            {
                if (ReferenceEquals(root.Content, target)) root.Content = split;
            }
        }

        split.Length = kept;

        if (before)
        {
            split.Add(inserted);
            split.Add(target);
        }
        else
        {
            split.Add(target);
            split.Add(inserted);
        }
    }

    /// <summary>
    /// Moves a pane to <paramref name="target"/>: into it as another tab (<see cref="DockZone.Center"/>), or beside it
    /// by splitting. THE operation every gesture ends in - the compass, a dropped floating window and a view-model
    /// setting <c>Zone</c> all arrive here, so there is one place where a move can be got wrong and one place to fix it.
    /// <para>The target is any NODE, which is what makes an edge anchor - "along the whole left side of the area" -
    /// the same operation rather than a second one: aim it at the root instead of at a group. There is deliberately no
    /// second verb and no extra zone for it, because there is no second concept: the root is a node like any other, and
    /// splitting it is what spanning the whole side means. Only a group can be tabbed INTO, so a centre drop on
    /// anything else is refused rather than invented.</para>
    /// </summary>
    /// <param name="index">Where among the target's tabs it lands, or -1 for last. Only meaningful for the centre.</param>
    /// <param name="size">How much of the split the newcomer takes; null means half, which is what "beside this group"
    /// means. An EDGE anchor passes a band instead - a side panel is a couple of hundred pixels wide, and half the
    /// editor is not an anchor but a partition.</param>
    public bool MovePane(string paneId, PaneNode target, DockZone zone, int index = -1, PaneLength? size = null)
    {
        if (target == null) return false;

        var source = FindGroup(paneId);
        if (source == null) return false;

        var group = target as PaneGroupNode;
        if (zone is DockZone.Center or DockZone.Floating && group == null) return false;

        // Splitting a group off from itself when it holds nothing else: the target would be emptied by the removal and
        // the split would then be made against a node that is no longer in the tree. Nothing was being asked for anyway.
        if (ReferenceEquals(source, target) && source.PaneIds.Count == 1 && zone is not (DockZone.Center or DockZone.Floating)) return false;

        source.Remove(paneId);

        if (zone is DockZone.Center or DockZone.Floating)
        {
            if (index < 0) group.Add(paneId);
            else group.Insert(index, paneId);
        }
        else
        {
            var moved = new PaneGroupNode();
            moved.Add(paneId);
            Split(target, zone, moved);

            // A band was asked for: the newcomer takes exactly it and its neighbour goes back to taking what is left.
            // Both halves matter - a fixed newcomer beside a fixed neighbour would leave the row unable to absorb a
            // resize at all.
            if (size is { } band)
            {
                moved.Length = band;
                target.Length = PaneLength.Star;
            }
        }

        Normalize();
        return true;
    }

    /// <summary>Removes a pane wherever it is. Empty groups and the levels they leave behind go with it.</summary>
    public bool RemovePane(string paneId)
    {
        foreach (var root in Roots)
        {
            if (!RemoveFrom(root.Content, paneId)) continue;
            Normalize();
            return true;
        }
        return false;
    }

    private static bool RemoveFrom(PaneNode node, string paneId)
    {
        switch (node)
        {
            case PaneGroupNode group:
                return group.Remove(paneId);
            case PaneSplitNode split:
                foreach (var child in split.Children)
                {
                    if (RemoveFrom(child, paneId)) return true;
                }
                return false;
            default:
                return false;
        }
    }

    /// <summary>Finds the group holding a pane, or null.</summary>
    public PaneGroupNode FindGroup(string paneId)
    {
        foreach (var root in Roots)
        {
            var found = FindIn(root.Content, paneId);
            if (found != null) return found;
        }
        return null;
    }

    private static PaneGroupNode FindIn(PaneNode node, string paneId)
    {
        switch (node)
        {
            case PaneGroupNode group:
                return group.PaneIds.Contains(paneId) ? group : null;
            case PaneSplitNode split:
                foreach (var child in split.Children)
                {
                    var found = FindIn(child, paneId);
                    if (found != null) return found;
                }
                return null;
            default:
                return null;
        }
    }

    /// <summary>
    /// Tidies every root. WITHOUT this the tree grows depth for nothing and keeps levels that divide a single child:
    /// - an EMPTY group is dropped (closing the last pane must not leave a hole that still takes space);
    /// - a split with ONE child collapses into that child (the level divides nothing);
    /// - a split nested in a split of the SAME orientation is flattened into it, so "drop left, then drop left again"
    ///   yields one split of three shares instead of two nested splits that merely look like one;
    /// - shares are rescaled to sum to 1.
    /// A root left with nothing is removed - unless it is the main one, which stays (an application without its main
    /// window is not a layout state we want to be able to represent).
    /// </summary>
    public void Normalize()
    {
        for (var i = Roots.Count - 1; i >= 0; i--)
        {
            var root = Roots[i];
            root.Content = NormalizeNode(root.Content);
            if (root.Content == null && !root.IsMain) Roots.RemoveAt(i);
        }
    }

    private static PaneNode NormalizeNode(PaneNode node)
    {
        switch (node)
        {
            case PaneGroupNode group:
                return group.IsEmpty ? null : group;

            case PaneSplitNode split:
            {
                var kept = new List<PaneNode>();
                foreach (var child in split.Children)
                {
                    var normalized = NormalizeNode(child);
                    if (normalized == null) continue;

                    // Same orientation -> take its children as our own instead of keeping the level.
                    if (normalized is PaneSplitNode inner && inner.Orientation == split.Orientation)
                    {
                        // Flattened INTO this row, so each grandchild's share of the inner row becomes its share of this
                        // one. Only weights compose that way; a fixed length is already an answer in pixels and stays
                        // exactly what it was.
                        foreach (var grandChild in inner.Children)
                        {
                            if (grandChild.Length.IsStar && normalized.Length.IsStar)
                                grandChild.Length = PaneLength.Stars(grandChild.Length.Value * normalized.Length.Value);
                            kept.Add(grandChild);
                        }
                        continue;
                    }

                    kept.Add(normalized);
                }

                split.Children.Clear();
                foreach (var child in kept) split.Add(child);

                if (split.Children.Count == 0) return null;
                if (split.Children.Count == 1)
                {
                    var only = split.Children[0];
                    only.Length = split.Length;   // the survivor stands in the space the split held
                    only.Parent = split.Parent;
                    return only;
                }

                split.NormalizeLengths();
                return split;
            }

            default:
                return node;
        }
    }
}
