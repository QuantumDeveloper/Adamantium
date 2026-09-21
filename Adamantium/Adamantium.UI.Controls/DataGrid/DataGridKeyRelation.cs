using System.Collections;
using System.Collections.Generic;

namespace Adamantium.UI.Controls.DataGrid;

/// <summary>A tree worked out from a FLAT list and two members: the key a record is known by, and the key of the record
/// it belongs to. That is the shape data arrives in from a database or a service - rows with an id and a parent id -
/// and it was the one shape of tree this table could not be told about in markup, only in code.
/// <para>Built ONCE per rebuild and asked afterwards. Working a record's children out by scanning the list would be a
/// scan per branch opened, which on ten thousand rows is the table stopping every time someone opens one.</para>
/// </summary>
internal sealed class DataGridKeyRelation
{
    private static readonly object[] None = System.Array.Empty<object>();

    private readonly Dictionary<object, List<object>> _children;
    private readonly List<object> _roots;

    private DataGridKeyRelation(Dictionary<object, List<object>> children, List<object> roots)
    {
        _children = children;
        _roots = roots;
    }

    /// <summary>The records nothing else claims - what the table shows at the top.</summary>
    public IReadOnlyList<object> Roots => _roots;

    /// <summary>What belongs to one record, or nothing.</summary>
    public IEnumerable ChildrenOf(object node) =>
        node != null && _children.TryGetValue(node, out var found) ? found : None;

    public static DataGridKeyRelation Build(IEnumerable source, string keyPath, string parentKeyPath)
    {
        var readKey = TreeChildResolver.ForValuePath(keyPath);
        var readParentKey = TreeChildResolver.ForValuePath(parentKeyPath);

        var items = new List<object>();
        foreach (var item in source)
        {
            if (item != null) items.Add(item);
        }

        // The FIRST record with a key owns it. Two records claiming one key is the data contradicting itself, and
        // hanging the children off both would put the same rows in the table twice - the one outcome that is certainly
        // wrong. The duplicate is still a record and still placed by its own parent key.
        var byKey = new Dictionary<object, object>();
        foreach (var item in items)
        {
            if (readKey(item) is { } key && !byKey.ContainsKey(key)) byKey[key] = item;
        }

        var children = new Dictionary<object, List<object>>();
        var parentOf = new Dictionary<object, object>();

        foreach (var item in items)
        {
            // NOT a child: no parent named, a parent nobody in the list answers to, or itself. The middle one is an
            // ORPHAN, and it becomes a root rather than disappearing - a record the table cannot reach is data silently
            // lost, which is worse than a record shown at the top.
            if (readParentKey(item) is not { } parentKey) continue;
            if (!byKey.TryGetValue(parentKey, out var parent) || ReferenceEquals(parent, item)) continue;

            parentOf[item] = parent;
            if (!children.TryGetValue(parent, out var list)) children[parent] = list = new List<object>();
            list.Add(item);
        }

        var roots = new List<object>();
        foreach (var item in items)
        {
            if (!parentOf.ContainsKey(item)) roots.Add(item);
        }

        // A CYCLE reaches nothing: A belongs to B and B to A, so neither is a root and neither is under one, and both
        // would simply vanish. Whatever the descent from the roots does not reach is broken open at its first record in
        // the source's own order - shown, rather than dropped and never explained.
        var reached = new HashSet<object>();
        var pending = new Stack<object>(roots);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (!reached.Add(node) || !children.TryGetValue(node, out var kids)) continue;

            foreach (var kid in kids) pending.Push(kid);
        }

        foreach (var item in items)
        {
            if (reached.Contains(item)) continue;

            // The record it pointed at has to LET GO of it, or it shows twice: once at the top, and once under the
            // record inside the cycle. Its former parent is already known - searching the lists for it would be a scan
            // per broken cycle over every child in the table.
            if (parentOf.TryGetValue(item, out var was) && children.TryGetValue(was, out var list)) list.Remove(item);

            parentOf.Remove(item);
            roots.Add(item);

            // ...and its own descent is reached now, so the rest of the same cycle is not opened a second time.
            pending.Push(item);
            while (pending.Count > 0)
            {
                var node = pending.Pop();
                if (!reached.Add(node) || !children.TryGetValue(node, out var kids)) continue;

                foreach (var kid in kids) pending.Push(kid);
            }
        }

        return new DataGridKeyRelation(children, roots);
    }
}
