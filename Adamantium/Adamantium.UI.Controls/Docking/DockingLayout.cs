using System.Collections.Generic;
using System.Linq;
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

    /// <summary>
    /// The DOCUMENT WELL: the group that is home to what is being edited. Everything inside it is a document, everything
    /// outside it is a tool - which is what makes a tool dropped into the centre behave like a document, and is the whole
    /// content of rule 1 in DOCKING_PLAN's rules.
    /// <para>Why a place and not a per-pane flag: "centre" has to mean something to the LAYOUT, or the centre stops
    /// existing the moment its last document is closed and the next one opens wherever it likes. So the well is a node,
    /// it cannot be collapsed or torn off, and it survives being emptied as a piece of empty space.</para>
    /// </summary>
    public PaneGroupNode DocumentWell { get; set; }

    private bool IsWell(PaneNode node) => DocumentWell != null && ReferenceEquals(node, DocumentWell);

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

        // The centre column: what the documents occupy, and what a top/bottom band divides. It starts as the first group
        // and grows into the split node holding the bands - never into the sides, which is what keeps them full height.
        PaneNode centre = null;

        foreach (var declaration in declarations)
        {
            var group = declaration.Group;

            if (root.Content == null)
            {
                // The first one takes everything - whether it called itself Center or not. A layout has to start
                // somewhere, and docking against nothing has no meaning.
                root.Content = group;
                centre = group;

                // The first group declared is where the documents live - whether it called itself Center or not, since a
                // layout has to start somewhere. From here on it is a PLACE, and what lands in it becomes a document.
                layout.DocumentWell = group;
                continue;
            }

            if (declaration.Zone is DockZone.Center or DockZone.Floating)
            {
                // Center means "with the documents", not "split something": join the group that is already there.
                if (root.Content is PaneGroupNode documents)
                {
                    foreach (var pane in group.PaneIds) documents.Add(pane);
                    continue;
                }
            }

            var zone = declaration.Zone is DockZone.Center or DockZone.Floating ? DockZone.Right : declaration.Zone;

            // WHAT gets split is not always the whole layout. A SIDE takes the full height of the window, so it splits the
            // root; a TOP/BOTTOM band belongs under the documents and must not run beneath the sides, so it splits the
            // CENTRE COLUMN instead. That ordering is the layout every editor uses - sides first, the band gets what is
            // left - and stating it here makes it independent of the order the author happens to declare the zones in.
            var target = zone is DockZone.Top or DockZone.Bottom ? centre : root.Content;
            layout.Split(target, zone, group);

            // The band is now part of the centre column, so the NEXT band splits that column, not the group inside it.
            if (zone is DockZone.Top or DockZone.Bottom) centre = group.Parent ?? centre;

            // AFTER the split, which hands both sides a share of what the target held - that is right for a pane being
            // dropped, and wrong for one the author sized. A number in markup is PIXELS along the zone's own axis ("the
            // inspector starts 240 wide"); saying nothing leaves it taking a share of whatever is left, like the centre.
            if (!double.IsNaN(declaration.Size)) group.Length = PaneLength.Pixels(declaration.Size);
        }

        layout.Normalize();
        return layout;
    }

    /// <summary>
    /// Splits <paramref name="target"/> and gives the arrival a length OF ITS OWN, leaving the target exactly as it was.
    /// <para>Splitting normally halves the target's length, because the pair then has to be worth what the one of them
    /// was. That is right when the arrival takes a share of the target, and wrong when it states its own size: nothing
    /// is being taken FROM the target, and the stars in the row absorb the difference. Halving it anyway meant every
    /// restore from an edge strip cut the rest of the layout in two - measured, the whole upper layout fell from half
    /// the window to 22 pixels after three of them.</para>
    /// </summary>
    private void SplitWithLength(PaneNode target, DockZone side, PaneNode inserted, PaneLength length)
    {
        var kept = target.Length;
        Split(target, side, inserted);
        inserted.Length = length;
        target.Length = kept;
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

            // A band was asked for (an edge anchor): the newcomer takes exactly it and the target keeps what it had -
            // see SplitWithLength. Otherwise the two share what the target held, which is what "beside this" means.
            if (size is { } band) SplitWithLength(target, zone, moved, band);
            else Split(target, zone, moved);
        }

        Normalize();
        return true;
    }

    /// <summary>
    /// Collapses a group to its own tab strip: it stays exactly where it is and gives up everything but the strip, so
    /// the tabs remain in place and clicking one brings the panel straight back. What that leaves it is not a number
    /// anyone states - it is what the strip measures (see <see cref="PaneUnit.Auto"/>).
    /// <para>Refused for the last group in a root: collapsing the only thing in an editor leaves a window of strips.</para>
    /// </summary>
    public bool CollapseGroup(PaneGroupNode group)
    {
        if (group is not { State: PaneGroupState.Docked } || group.IsEmpty) return false;
        if (group.Parent == null) return false;   // the only group in its root
        if (IsWell(group)) return false;          // the centre folds to nothing: there is no edge for it to fold against

        // And neither does anything else that is not ON an edge. Folding is a statement about one: the strip lives on
        // the edge and its labels turn to face out of it. Measured on a panel with another docked outside it - it folded
        // where it stood, so its caption and body went and its strip stayed horizontal, leaving a wide empty box with
        // tabs along the bottom. See Unfold for the same rule applied after the fact.
        if (EdgeOf(group) == DockZone.None) return false;

        group.RestoreLength = group.Length;
        group.Length = PaneLength.Auto;
        group.State = PaneGroupState.Collapsed;
        return true;
    }

    /// <summary>Shows a put-away group's body WITHOUT pinning it back: the strip stays against the edge with its labels
    /// still turned, and only the body comes into view. The panel is not docked - it is being looked at - so its tabs stay
    /// on the edge until it is pinned.
    /// <para>The body is given the room the panel had (<see cref="PaneGroupNode.RestoreLength"/>), which means the
    /// neighbours move aside for it. Drawing it OVER them instead - the flyout every editor uses - is a layer above the
    /// split tree and is not built yet.</para></summary>
    public bool RevealGroup(PaneGroupNode group)
    {
        if (group is not { State: PaneGroupState.Collapsed }) return false;

        // The LENGTH does not change: in the tree a revealed panel is still just its strip. Its body is shown OVER the
        // neighbours (a flyout), not by pushing them aside - see rule 3.10. Giving it back its docked length here is what
        // made it shove the layout about every time anyone glanced at a tool.
        group.State = PaneGroupState.Revealed;
        return true;
    }

    /// <summary>Puts a revealed group's body away again, leaving the strip. The mirror of <see cref="RevealGroup"/>.</summary>
    public bool HideGroup(PaneGroupNode group)
    {
        if (group is not { State: PaneGroupState.Revealed }) return false;

        // Only the state: the length has been Auto throughout, and RestoreLength still holds what the panel is worth
        // docked. Copying the current length into it here would overwrite that with Auto.
        group.State = PaneGroupState.Collapsed;
        return true;
    }

    /// <summary>Which side of the layout a node sits against, or <see cref="DockZone.None"/> when it is not against one:
    /// it is the first or last child of a row, and the row's orientation says which pair of sides that means.
    /// <para>READ from the tree rather than remembered. A group's authored zone says where it STARTED; after a drag or a
    /// drop it may be somewhere else entirely, and a collapsed panel has to fold towards the edge it is actually on.</para>
    /// </summary>
    public static DockZone EdgeOf(PaneNode node)
    {
        if (node?.Parent is not { } row) return DockZone.None;

        var horizontal = row.Orientation == Orientation.Horizontal;
        if (ReferenceEquals(row.Children[0], node)) return horizontal ? DockZone.Left : DockZone.Top;
        if (ReferenceEquals(row.Children[^1], node)) return horizontal ? DockZone.Right : DockZone.Bottom;
        return DockZone.None;
    }

    /// <summary>Pins a folded group back into the layout, giving it the room it had - from either folded state, since
    /// pinning is what a revealed panel is waiting for. Its place never changed, so there is nowhere to put it back,
    /// only a size to return.</summary>
    public bool ExpandGroup(PaneGroupNode group)
    {
        if (group == null || group.State == PaneGroupState.Docked) return false;

        group.Length = group.RestoreLength;
        group.State = PaneGroupState.Docked;
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

    /// <summary>Takes a WHOLE group out of its layout and gives it a root of its own - what dragging a tool panel by its
    /// caption means. The panes travel together, in their order and with their selection, because the thing being moved is
    /// the panel, not the tabs in it.
    /// <para>Refused for a group that is already a root's entire content: it is a floating panel, and tearing a window off
    /// itself means nothing.</para></summary>
    public DockingRoot TearOffGroup(PaneGroupNode group)
    {
        if (group is not { IsEmpty: false }) return null;
        if (group.Parent == null) return null;
        if (IsWell(group)) return null;   // the centre does not leave the window it is the centre of

        var parent = group.Parent;
        parent.Children.Remove(group);
        group.Parent = null;

        // Folded state belongs to the edge it was folded against, and it has just left that edge.
        group.State = PaneGroupState.Docked;
        group.Length = group.RestoreLength;

        var root = new DockingRoot(group, isMain: false);
        Roots.Add(root);

        Normalize();
        return root;
    }

    /// <summary>
    /// Moves a WHOLE node - what dropping a floating WINDOW onto the compass means. The window may hold a single group
    /// or a whole split of them by then (things can be docked INTO a floating window), and either way it is that node
    /// which lands: <see cref="DockZone.Center"/> tabs its panes into the target group, any other zone splits the target
    /// and puts the node beside it.
    /// <para>The counterpart of <see cref="MovePane"/>, not a generalisation of it: moving one pane out of a group is a
    /// different move from moving the group, and merging the two behind an "is it the whole group?" test would make
    /// which one happened depend on how many tabs happened to be open.</para>
    /// </summary>
    public bool MoveNode(PaneNode node, PaneNode target, DockZone zone, PaneLength? size = null)
    {
        if (node == null || target == null || ReferenceEquals(node, target)) return false;
        if (IsWell(node)) return false;   // the centre stays where it is; things move around it

        // Into itself: the target would leave the tree along with the node, and the split would then be made against
        // something nothing points at any more.
        if (IsWithin(target, node)) return false;

        var tabbed = zone is DockZone.Center or DockZone.Floating;
        if (tabbed && (node is not PaneGroupNode || target is not PaneGroupNode)) return false;

        // Everything that could refuse has refused; only now is the node taken out of where it is.
        if (node.Parent is { } row)
        {
            row.Children.Remove(node);
        }
        else
        {
            // A whole root - the floating window case. The main one is not something that can be docked into another.
            var owner = Roots.FirstOrDefault(r => ReferenceEquals(r.Content, node));
            if (owner == null || owner.IsMain) return false;
            Roots.Remove(owner);
        }

        node.Parent = null;

        // A fold describes the edge the group was folded against, and it has just left that edge.
        if (node is PaneGroupNode group)
        {
            group.State = PaneGroupState.Docked;
            group.Length = group.RestoreLength;
        }

        if (tabbed)
        {
            var into = (PaneGroupNode)target;
            foreach (var pane in ((PaneGroupNode)node).PaneIds) into.Add(pane);
        }
        else if (size is { } band)
        {
            SplitWithLength(target, zone, node, band);
        }
        else
        {
            Split(target, zone, node);
        }

        Normalize();
        return true;
    }

    /// <summary>Whether <paramref name="node"/> is <paramref name="ancestor"/> or sits somewhere under it.</summary>
    private static bool IsWithin(PaneNode node, PaneNode ancestor)
    {
        for (var n = node; n != null; n = n.Parent)
        {
            if (ReferenceEquals(n, ancestor)) return true;
        }
        return false;
    }

    /// <summary>Every pane under a node, in tree order. What is being dragged may be one pane, a panel, or a whole split
    /// built up inside a floating window, and several questions - what it is allowed to do, what to call its window -
    /// have to be asked of all of them.</summary>
    public static IEnumerable<string> PanesIn(PaneNode node)
    {
        switch (node)
        {
            case PaneGroupNode group:
                foreach (var pane in group.PaneIds) yield return pane;
                break;

            case PaneSplitNode split:
                foreach (var child in split.Children)
                {
                    foreach (var pane in PanesIn(child)) yield return pane;
                }
                break;
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

        foreach (var root in Roots) Unfold(root.Content);
    }

    /// <summary>
    /// A folded panel that is no longer against an EDGE unfolds. Being put away is a statement about an edge - the strip
    /// lives on it and the labels turn to face out of it - so a panel that something has been docked outside of has
    /// nothing left to be folded against.
    /// <para>Measured: dropping a pane on the right-hand edge anchor put a new group outside a panel that was put away
    /// there. It stayed Collapsed, so its caption and body were still hidden, while its strip - no longer on an edge -
    /// turned back horizontal. What was left was an empty box with tabs along the bottom and no title.</para>
    /// </summary>
    private void Unfold(PaneNode node)
    {
        switch (node)
        {
            case PaneGroupNode group:
                if (group.State == PaneGroupState.Docked || EdgeOf(group) != DockZone.None) return;

                group.Length = group.RestoreLength;
                group.State = PaneGroupState.Docked;
                return;

            case PaneSplitNode split:
                foreach (var child in split.Children) Unfold(child);
                return;
        }
    }

    private PaneNode NormalizeNode(PaneNode node)
    {
        switch (node)
        {
            case PaneGroupNode group:
                // The well survives being emptied - closing the last document must not take the centre of the layout with
                // it, or the next document opens wherever it likes and the editing area moves under the user.
                if (!group.IsEmpty || IsWell(group)) return group;

                // Dropped means dropped: a node that is out of the tree must not go on pointing at a live parent, or
                // anything that reads its position - EdgeOf, MoveNode - gets a confident answer about where it is.
                group.Parent = null;
                return null;

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

                if (split.Children.Count == 0)
                {
                    split.Parent = null;
                    return null;
                }
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
