using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Adamantium.UI.Controls;

/// <summary>Projects a tree - root items + a per-node children getter + each node's expand state - into a FLAT,
/// display-ordered <see cref="Rows"/> list: the source a VirtualizingPanel consumes. Expanding a row splices its child
/// run in right after it (as ONE range edit, so the cost is O(viewport realized), not O(children)); collapsing removes
/// its whole visible subtree. A level of thousands of siblings therefore costs thousands of cheap <see cref="TreeRow"/>
/// wrappers, not thousands of controls, and only the viewport is realized. Each expanded node's (and the roots')
/// children collection is observed, so a lazily- or dynamically-filled branch reconciles into the flat list in place.</summary>
internal sealed class TreeFlattener
{
    private readonly Func<object, IEnumerable> _childrenOf;
    private readonly Func<object, bool> _isExpanded;
    private readonly Func<object, bool> _isSelected;
    private readonly Dictionary<TreeRow, Subscription> _subs = new();
    private IEnumerable _roots;
    private Subscription _rootsSub;

    // isExpanded / isSelected let a (re)build RESTORE a node's persisted state (a node the view-model still marks
    // expanded/selected, e.g. after a tab switch recreated the view): an expanded node's row is built already-open with its
    // subtree spliced in, a selected node's row built already-selected. Null probes = never restore (build starts blank).
    public TreeFlattener(Func<object, IEnumerable> childrenOf, Func<object, bool> isExpanded = null, Func<object, bool> isSelected = null)
    {
        _childrenOf = childrenOf;
        _isExpanded = isExpanded ?? (static _ => false);
        _isSelected = isSelected ?? (static _ => false);
    }

    /// <summary>The visible rows in display order - the virtualized list binds to this.</summary>
    public FlatRowCollection Rows { get; } = [];

    /// <summary>Rebuild the flat list from these roots (all collapsed), observing the roots collection for live changes.</summary>
    public void SetRoots(IEnumerable roots)
    {
        UnsubscribeAllNodes();
        Unsubscribe(null);
        _roots = roots;
        Subscribe(null, roots);
        Rows.ResetTo(BuildRows(roots, 0));
    }

    /// <summary>Detach every subscription (roots + expanded nodes) - called before this flattener is discarded so its
    /// handlers don't keep the old collections alive or fire into a stale list. The Rows collection is dropped with it.</summary>
    public void Clear()
    {
        UnsubscribeAllNodes();
        Unsubscribe(null);
        _roots = null;
    }

    public void Toggle(TreeRow row)
    {
        if (row.IsExpanded)
        {
            Collapse(row);
        }
        else
        {
            Expand(row);
        }
    }

    /// <summary>Open a branch: splice its direct children in right after it (collapsed - one level per expand) and start
    /// observing its children collection.</summary>
    public void Expand(TreeRow row)
    {
        if (row.IsExpanded)
        {
            return;
        }

        // Re-evaluate children LIVE rather than trusting the cached flag: a node that was a leaf when its row was built may
        // have gained children since (a drop into it, a lazy load). Refresh the row's flag too, so a former leaf now shows
        // an expander and can open - a stale HasChildren=false would silently swallow the new child.
        var children = _childrenOf(row.Node);
        row.HasChildren = HasAny(children);
        if (!row.HasChildren)
        {
            return;
        }

        row.IsExpanded = true;
        var index = Rows.IndexOf(row);
        if (index < 0)
        {
            return;
        }

        Subscribe(row, children);
        Rows.InsertMany(index + 1, BuildRows(children, row.Depth + 1));
    }

    /// <summary>Close a branch: drop the contiguous run of deeper rows that follows it - its whole VISIBLE subtree -
    /// unsubscribing any expanded descendants along the way, and stop observing its own children.</summary>
    public void Collapse(TreeRow row)
    {
        if (!row.IsExpanded)
        {
            return;
        }

        row.IsExpanded = false;
        Unsubscribe(row);
        var index = Rows.IndexOf(row);
        if (index < 0)
        {
            return;
        }

        var end = SubtreeEnd(index, row.Depth);
        UnsubscribeRange(index + 1, end);
        Rows.RemoveMany(index + 1, end - index - 1);
    }

    // ---- Children observability ---------------------------------------------------------------------------------

    // A node's (or the roots') children changed while it is expanded/visible: reconcile just that node's direct-child
    // region of the flat list. parent == null means the roots collection.
    private void OnChildrenChanged(TreeRow parent, NotifyCollectionChangedEventArgs e)
    {
        if (parent != null && Rows.IndexOf(parent) < 0)
        {
            Unsubscribe(parent);   // parent scrolled out of existence
            return;
        }

        var baseDepth = parent == null ? 0 : parent.Depth + 1;

        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
            {
                var pos = FlatIndexForChild(parent, e.NewStartingIndex);
                var rows = new List<TreeRow>(e.NewItems.Count);
                foreach (var item in e.NewItems)
                {
                    rows.Add(new TreeRow(item, baseDepth, HasChildren(item)));
                }

                Rows.InsertMany(pos, rows);
                break;
            }
            case NotifyCollectionChangedAction.Remove:
            {
                foreach (var item in e.OldItems)
                {
                    var idx = FindDirectChildRow(parent, item);
                    if (idx < 0)
                    {
                        continue;
                    }

                    var end = SubtreeEnd(idx, Rows[idx].Depth);
                    UnsubscribeRange(idx, end);
                    Rows.RemoveMany(idx, end - idx);
                }

                break;
            }
            default:   // Reset / Replace / Move: rebuild this parent's visible child region.
                RebuildChildrenRegion(parent);
                break;
        }
    }

    private void RebuildChildrenRegion(TreeRow parent)
    {
        if (parent == null)
        {
            UnsubscribeAllNodes();
            Rows.ResetTo(BuildRows(_roots, 0));
            return;
        }

        var index = Rows.IndexOf(parent);
        if (index < 0)
        {
            return;
        }

        var end = SubtreeEnd(index, parent.Depth);
        UnsubscribeRange(index + 1, end);
        Rows.RemoveMany(index + 1, end - index - 1);
        Rows.InsertMany(index + 1, BuildRows(_childrenOf(parent.Node), parent.Depth + 1));
    }

    // ---- Geometry over the flat list ----------------------------------------------------------------------------

    // First index after `rowIndex` whose Depth is <= `depth` (i.e. one past the row's visible subtree), or Rows.Count.
    private int SubtreeEnd(int rowIndex, int depth)
    {
        var i = rowIndex + 1;
        while (i < Rows.Count && Rows[i].Depth > depth)
        {
            i++;
        }

        return i;
    }

    // The flat index at which the child at source position `childIndex` of `parent` (null = roots) belongs - i.e. before
    // the current childIndex-th DIRECT child, skipping the descendant runs of earlier siblings; end of region if past all.
    private int FlatIndexForChild(TreeRow parent, int childIndex)
    {
        var baseDepth = parent == null ? 0 : parent.Depth + 1;
        var i = parent == null ? 0 : Rows.IndexOf(parent) + 1;
        var seen = 0;
        for (; i < Rows.Count; i++)
        {
            var depth = Rows[i].Depth;
            if (parent != null && depth <= parent.Depth)
            {
                break;   // left the parent's subtree
            }

            if (depth != baseDepth)
            {
                continue;   // a deeper descendant of an earlier sibling
            }

            if (seen == childIndex)
            {
                return i;
            }

            seen++;
        }

        return i;
    }

    // Flat index of `parent`'s direct child row whose Node equals `item`, or -1.
    private int FindDirectChildRow(TreeRow parent, object item)
    {
        var baseDepth = parent == null ? 0 : parent.Depth + 1;
        var i = parent == null ? 0 : Rows.IndexOf(parent) + 1;
        for (; i < Rows.Count; i++)
        {
            var depth = Rows[i].Depth;
            if (parent != null && depth <= parent.Depth)
            {
                break;
            }

            if (depth == baseDepth && Equals(Rows[i].Node, item))
            {
                return i;
            }
        }

        return -1;
    }

    private List<TreeRow> BuildRows(IEnumerable items, int depth)
    {
        var list = new List<TreeRow>();
        AppendRows(list, items, depth);
        return list;
    }

    // Flatten items at `depth`, RESTORING each node that the view-model still marks expanded (its row is built open and its
    // subtree appended recursively + observed) - so rebuilding the tree (a recreated view over a persisted view-model)
    // comes back with the same branches open, not all collapsed.
    private void AppendRows(List<TreeRow> list, IEnumerable items, int depth)
    {
        if (items == null)
        {
            return;
        }

        foreach (var item in items)
        {
            var hasChildren = HasChildren(item);
            var row = new TreeRow(item, depth, hasChildren) { IsSelected = _isSelected(item) };
            list.Add(row);
            if (hasChildren && _isExpanded(item))
            {
                row.IsExpanded = true;
                var children = _childrenOf(item);
                Subscribe(row, children);
                AppendRows(list, children, depth + 1);
            }
        }
    }

    private bool HasChildren(object node) => HasAny(_childrenOf(node));

    private static bool HasAny(IEnumerable children)
    {
        if (children == null)
        {
            return false;
        }

        foreach (var _ in children)
        {
            return true;   // any child?
        }

        return false;
    }

    // ---- Subscriptions ------------------------------------------------------------------------------------------

    private void Subscribe(TreeRow parent, IEnumerable children)
    {
        if (children is not INotifyCollectionChanged incc)
        {
            return;
        }

        NotifyCollectionChangedEventHandler handler = (_, e) => OnChildrenChanged(parent, e);
        incc.CollectionChanged += handler;
        var sub = new Subscription(incc, handler);
        if (parent == null)
        {
            _rootsSub = sub;
        }
        else
        {
            _subs[parent] = sub;
        }
    }

    private void Unsubscribe(TreeRow parent)
    {
        if (parent == null)
        {
            _rootsSub?.Detach();
            _rootsSub = null;
        }
        else if (_subs.Remove(parent, out var sub))
        {
            sub.Detach();
        }
    }

    // Unsubscribe every expanded row in the flat range [start, end) that is about to be removed.
    private void UnsubscribeRange(int start, int end)
    {
        for (var i = start; i < end && i < Rows.Count; i++)
        {
            if (_subs.Remove(Rows[i], out var sub))
            {
                sub.Detach();
            }
        }
    }

    private void UnsubscribeAllNodes()
    {
        foreach (var sub in _subs.Values)
        {
            sub.Detach();
        }

        _subs.Clear();
    }

    private sealed class Subscription(INotifyCollectionChanged collection, NotifyCollectionChangedEventHandler handler)
    {
        public void Detach() => collection.CollectionChanged -= handler;
    }
}
