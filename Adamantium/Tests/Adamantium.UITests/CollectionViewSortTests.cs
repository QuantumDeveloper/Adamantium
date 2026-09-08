using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Adamantium.UI.Core.Collections;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// Ordering, and what it costs the rest of the view. Sorted, a view position no longer rises with its source position,
/// so the two lookups the incremental path is built on - "where does this arrival belong" and "where did this departure
/// sit" - both change shape. A sort that only works on a full rebuild would pass a naive test and fail the moment
/// anything was inserted, so the incremental cases are the ones that matter here.
/// </summary>
[TestFixture]
public class CollectionViewSortTests
{
    private sealed class Row(string name, int rank)
    {
        public string Name { get; } = name;
        public int Rank { get; } = rank;
        public override string ToString() => Name;
    }

    private sealed class ByLength : IComparer
    {
        public int Compare(object x, object y) => ((string)x).Length.CompareTo(((string)y).Length);
    }

    private static string[] Names(CollectionView view) => view.Cast<Row>().Select(r => r.Name).ToArray();

    [Test]
    public void ADescription_OrdersTheView()
    {
        var view = new CollectionView(new[] { new Row("c", 3), new Row("a", 1), new Row("b", 2) });
        view.SortDescriptions.Add(new SortDescription(nameof(Row.Name)));

        Assert.That(Names(view), Is.EqualTo(new[] { "a", "b", "c" }));
        Assert.That(view.IsSorted, Is.True);
    }

    [Test]
    public void Descending_OrdersTheOtherWay()
    {
        var view = new CollectionView(new[] { new Row("a", 1), new Row("c", 3), new Row("b", 2) });
        view.SortDescriptions.Add(new SortDescription(nameof(Row.Name), SortDirection.Descending));

        Assert.That(Names(view), Is.EqualTo(new[] { "c", "b", "a" }));
    }

    /// <summary>The second level only decides ties in the first - the ordinary meaning of a multi-level sort.</summary>
    [Test]
    public void ASecondDescription_BreaksTiesInTheFirst()
    {
        var view = new CollectionView(new[]
        {
            new Row("b", 1), new Row("a", 2), new Row("a", 1), new Row("b", 2)
        });
        view.SortDescriptions.Add(new SortDescription(nameof(Row.Name)));
        view.SortDescriptions.Add(new SortDescription(nameof(Row.Rank), SortDirection.Descending));

        Assert.That(view.Cast<Row>().Select(r => $"{r.Name}{r.Rank}"),
            Is.EqualTo(new[] { "a2", "a1", "b2", "b1" }));
    }

    /// <summary>Items the comparer calls equal keep the order the source had them in - so a sort never shuffles what it
    /// was not asked about.</summary>
    [Test]
    public void EqualItems_KeepTheSourceOrder()
    {
        var view = new CollectionView(new[] { "bbb", "aaa", "cc", "dd" }) { CustomSort = new ByLength() };

        Assert.That(view.Cast<string>(), Is.EqualTo(new[] { "cc", "dd", "bbb", "aaa" }));
    }

    [Test]
    public void ACustomSort_WinsOverDescriptions()
    {
        var view = new CollectionView(new[] { "bbb", "cc", "a" });
        view.SortDescriptions.Add(new SortDescription("Length", SortDirection.Descending));
        view.CustomSort = new ByLength();

        Assert.That(view.Cast<string>(), Is.EqualTo(new[] { "a", "cc", "bbb" }));
    }

    /// <summary>Sorting runs AFTER filtering - the shown set is ordered, not the whole source.</summary>
    [Test]
    public void FilterRunsFirst_ThenSort()
    {
        var view = new CollectionView(new[] { 5, 2, 9, 4, 7, 1 })
        {
            Filter = o => (int)o % 2 == 1,
            CustomSort = Comparer<object>.Default as IComparer
        };

        Assert.That(view.Cast<int>(), Is.EqualTo(new[] { 1, 5, 7, 9 }),
            "the EVEN numbers must be gone, and what is left ordered - not the whole source ordered and then cut");
    }

    /// <summary>The incremental case: an arrival must land where the ORDER puts it, not where the source put it.</summary>
    [Test]
    public void AnAddedItem_LandsWhereTheOrderPutsIt()
    {
        var source = new ObservableCollection<Row> { new("a", 1), new("c", 3) };
        var view = new CollectionView(source);
        view.SortDescriptions.Add(new SortDescription(nameof(Row.Name)));

        var log = new List<NotifyCollectionChangedEventArgs>();
        view.CollectionChanged += (_, e) => log.Add(e);

        source.Add(new Row("b", 2));

        Assert.That(Names(view), Is.EqualTo(new[] { "a", "b", "c" }));
        Assert.That(log, Has.Count.EqualTo(1), "an arrival is an Add, not a re-sort of everything");
        Assert.That(log[0].Action, Is.EqualTo(NotifyCollectionChangedAction.Add));
        Assert.That(log[0].NewStartingIndex, Is.EqualTo(1), "appended to the source, but ordered into the middle");
    }

    /// <summary>...and a departure must be found by WHICH item it was, not by where the source index would have put it.</summary>
    [Test]
    public void ARemovedItem_LeavesFromWhereItWasSHOWN()
    {
        var source = new ObservableCollection<Row> { new("c", 3), new("a", 1), new("b", 2) };
        var view = new CollectionView(source);
        view.SortDescriptions.Add(new SortDescription(nameof(Row.Name)));

        var log = new List<NotifyCollectionChangedEventArgs>();
        view.CollectionChanged += (_, e) => log.Add(e);

        source.RemoveAt(0);

        Assert.That(Names(view), Is.EqualTo(new[] { "a", "b" }));
        Assert.That(log, Has.Count.EqualTo(1));
        Assert.That(log[0].Action, Is.EqualTo(NotifyCollectionChangedAction.Remove));
        Assert.That(log[0].OldStartingIndex, Is.EqualTo(2), "source index 0, but it was shown last");
    }

    /// <summary>The index map is maintained edit by edit, so a SEQUENCE is a different test from any one of them - this
    /// is where a stale map shows up.</summary>
    [Test]
    public void ASEQUENCEOfEdits_KeepsTheOrderCorrect()
    {
        var source = new ObservableCollection<Row> { new("d", 4), new("b", 2) };
        var view = new CollectionView(source) { Filter = o => ((Row)o).Rank != 3 };
        view.SortDescriptions.Add(new SortDescription(nameof(Row.Name)));

        source.Insert(0, new Row("f", 6));
        source.Add(new Row("c", 3));      // filtered out
        source.Insert(1, new Row("a", 1));
        source.RemoveAt(2);               // removes "d"
        source.Add(new Row("e", 5));

        Assert.That(Names(view), Is.EqualTo(new[] { "a", "b", "e", "f" }),
            "the incremental map drifted from what a fresh pass over the source says");
    }

    [Test]
    public void ClearingTheDescriptions_ReturnsToTheSourceOrder()
    {
        var view = new CollectionView(new[] { new Row("c", 3), new Row("a", 1) });
        view.SortDescriptions.Add(new SortDescription(nameof(Row.Name)));
        Assert.That(Names(view), Is.EqualTo(new[] { "a", "c" }));

        view.SortDescriptions.Clear();

        Assert.That(Names(view), Is.EqualTo(new[] { "c", "a" }));
        Assert.That(view.IsSorted, Is.False);
    }
}
