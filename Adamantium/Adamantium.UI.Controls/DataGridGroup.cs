using System.Collections.Generic;

namespace Adamantium.UI.Controls;

/// <summary>One group of rows: the value they share, and what is under it - either the rows themselves or the next
/// level of groups.
/// <para>A group is a NODE of the same tree the rows already live in, not a second kind of list. That is what lets
/// grouping cost nothing new: the flattener splices a group open exactly as it splices a branch, the virtualizer
/// realizes a group header exactly as it realizes a row, and a grouped table of a million rows still builds only what
/// is on screen.</para></summary>
public sealed class DataGridGroup
{
    internal DataGridGroup(DataGridColumn column, object key, int level, string path)
    {
        Column = column;
        Key = key;
        Level = level;
        Path = path;
    }

    // What this group IS, across rebuilds: the chain of key texts from the outermost group down to it. A sort or a
    // filter builds every group afresh, so "which groups are open" cannot be remembered by object - only by this.
    internal string Path { get; }

    /// <summary>The column the rows were grouped by.</summary>
    public DataGridColumn Column { get; }

    /// <summary>The value they share - what the group's header shows.</summary>
    public object Key { get; }

    /// <summary>How deep this group sits, from 0 for the outermost.</summary>
    public int Level { get; }

    /// <summary>What is directly under it: rows, or the next level of groups.</summary>
    public IReadOnlyList<object> Children => _children;

    /// <summary>Every row under it, however many GROUP levels down - what the header counts and what its totals are
    /// worked out over. Kept apart from <see cref="Children"/>, which is only what the next level shows. A row's own
    /// tree children are not here: they belong to that row, and counting them would make this group's total disagree
    /// with the table's.</summary>
    public IReadOnlyList<object> Items => _items;

    /// <summary>How many data rows are under it. "Iberia (240)" is the useful number in an outer group's header,
    /// "Iberia (4 sub-groups)" is not.</summary>
    public int Count => _items.Count;

    private readonly List<object> _children = [];
    private readonly List<object> _items = [];

    // A data row joins BOTH lists: what the next level shows, and what this group is made of. The first is replaced by
    // sub-groups when there is another level; the second is what the count and the totals read, at every level.
    internal void Add(object item)
    {
        _children.Add(item);
        _items.Add(item);
    }

    internal void Replace(List<object> children)
    {
        _children.Clear();
        _children.AddRange(children);
    }

    // Worked out once per group and kept: a header scrolled out and back must not re-walk its rows, and a group of a
    // hundred thousand would do it per realization.
    private Dictionary<DataGridColumn, object> _totals;

    internal object TotalFor(DataGridColumn column, System.Func<DataGridColumn, DataGridGroup, object> compute)
    {
        _totals ??= new Dictionary<DataGridColumn, object>();
        if (_totals.TryGetValue(column, out var total)) return total;

        total = compute(column, this);
        _totals[column] = total;
        return total;
    }

    internal void ForgetTotals() => _totals = null;
}
