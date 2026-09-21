using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Adamantium.UI.Core.Collections;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// The core of the collection view: WHICH items are shown, and - just as much the point - HOW that is reported. Hiding a
/// row on the fly has to reach the list as a row leaving it, at its own index, or nothing downstream can animate it and
/// every realized container is thrown away on every keystroke of a search box.
/// <para>So each test asserts both halves: the contents afterwards, and the notification that carried the change.</para>
/// </summary>
[TestFixture]
public class CollectionViewFilterTests
{
    private static List<NotifyCollectionChangedEventArgs> Recorded(CollectionView view)
    {
        var log = new List<NotifyCollectionChangedEventArgs>();
        view.CollectionChanged += (_, e) => log.Add(e);
        return log;
    }

    private static object[] Items(CollectionView view) => view.Cast<object>().ToArray();

    [Test]
    public void WithNoFilter_TheViewIsTheSource()
    {
        var view = new CollectionView(new[] { "a", "b", "c" });

        Assert.That(Items(view), Is.EqualTo(new object[] { "a", "b", "c" }));
        Assert.That(view.Count, Is.EqualTo(3));
        Assert.That(view.SourceCount, Is.EqualTo(3));
    }

    [Test]
    public void AFilter_ShowsOnlyWhatPasses()
    {
        var view = new CollectionView(new[] { 1, 2, 3, 4, 5 }) { Filter = o => (int)o % 2 == 0 };

        Assert.That(Items(view), Is.EqualTo(new object[] { 2, 4 }));
        Assert.That(view.Count, Is.EqualTo(2), "Count is what is SHOWN");
        Assert.That(view.SourceCount, Is.EqualTo(5), "...and SourceCount what there is");
    }

    /// <summary>A new predicate agrees with the old one about nothing in particular, so Reset is the honest report.</summary>
    [Test]
    public void ChangingTheFilter_IsAReset()
    {
        var view = new CollectionView(new[] { 1, 2, 3, 4 });
        var log = Recorded(view);

        view.Filter = o => (int)o > 2;

        Assert.That(Items(view), Is.EqualTo(new object[] { 3, 4 }));
        Assert.That(log.Select(e => e.Action), Is.EqualTo(new[] { NotifyCollectionChangedAction.Reset }));
    }

    /// <summary>An item added to the source that PASSES arrives as an Add at its place in the VIEW - which is not its
    /// place in the source once anything ahead of it is filtered out.</summary>
    [Test]
    public void AnAddedItemThatPasses_ArrivesAtItsVIEWIndex()
    {
        var source = new ObservableCollection<int> { 1, 2, 3, 4 };
        var view = new CollectionView(source) { Filter = o => (int)o % 2 == 0 };
        var log = Recorded(view);

        source.Insert(3, 10);

        Assert.That(Items(view), Is.EqualTo(new object[] { 2, 10, 4 }));
        Assert.That(log, Has.Count.EqualTo(1));
        Assert.That(log[0].Action, Is.EqualTo(NotifyCollectionChangedAction.Add));
        Assert.That(log[0].NewStartingIndex, Is.EqualTo(1), "source index 3, but view index 1");
        Assert.That(log[0].NewItems[0], Is.EqualTo(10));
    }

    /// <summary>...and one that does not pass is not reported at all: nothing downstream changed.</summary>
    [Test]
    public void AnAddedItemThatDoesNotPass_IsSilent()
    {
        var source = new ObservableCollection<int> { 2, 4 };
        var view = new CollectionView(source) { Filter = o => (int)o % 2 == 0 };
        var log = Recorded(view);

        source.Add(7);

        Assert.That(Items(view), Is.EqualTo(new object[] { 2, 4 }));
        Assert.That(log, Is.Empty, "an item nobody can see must not disturb the list");
        Assert.That(view.SourceCount, Is.EqualTo(3), "the source still grew");
    }

    [Test]
    public void ARemovedItemThatWasShown_LeavesAtItsVIEWIndex()
    {
        var source = new ObservableCollection<int> { 1, 2, 3, 4 };
        var view = new CollectionView(source) { Filter = o => (int)o % 2 == 0 };
        var log = Recorded(view);

        source.Remove(4);

        Assert.That(Items(view), Is.EqualTo(new object[] { 2 }));
        Assert.That(log, Has.Count.EqualTo(1));
        Assert.That(log[0].Action, Is.EqualTo(NotifyCollectionChangedAction.Remove));
        Assert.That(log[0].OldStartingIndex, Is.EqualTo(1), "source index 3, view index 1");
    }

    [Test]
    public void ARemovedItemThatWasHidden_IsSilent()
    {
        var source = new ObservableCollection<int> { 1, 2, 3, 4 };
        var view = new CollectionView(source) { Filter = o => (int)o % 2 == 0 };
        var log = Recorded(view);

        source.Remove(3);

        Assert.That(Items(view), Is.EqualTo(new object[] { 2, 4 }));
        Assert.That(log, Is.Empty);
    }

    /// <summary>The map from view positions back to source positions is the thing that can silently rot: it is
    /// maintained incrementally, so a sequence of edits is a different test from any single one of them.</summary>
    [Test]
    public void ASEQUENCEOfEdits_KeepsTheViewCorrect()
    {
        var source = new ObservableCollection<int> { 1, 2, 3, 4, 5, 6 };
        var view = new CollectionView(source) { Filter = o => (int)o % 2 == 0 };

        source.Insert(0, 8);          // passes, at the front
        source.RemoveAt(3);           // was 2 - shown
        source.Add(10);               // passes, at the back
        source.Insert(2, 7);          // hidden
        source.RemoveAt(0);           // was 8 - shown

        Assert.That(Items(view), Is.EqualTo(source.Where(i => i % 2 == 0).Cast<object>().ToArray()),
            "the incremental map drifted from what a fresh pass over the source says");
    }

    /// <summary>A source that re-orders itself says nothing about which item went where, so the view re-reads it.</summary>
    [Test]
    public void AMovedSource_IsAReset()
    {
        var source = new ObservableCollection<int> { 1, 2, 3, 4 };
        var view = new CollectionView(source);
        var log = Recorded(view);

        source.Move(0, 3);

        Assert.That(Items(view), Is.EqualTo(new object[] { 2, 3, 4, 1 }));
        Assert.That(log.Select(e => e.Action), Is.EqualTo(new[] { NotifyCollectionChangedAction.Reset }));
    }

    [Test]
    public void ReplacingTheSource_DropsTheOldSubscription()
    {
        var first = new ObservableCollection<int> { 1, 2 };
        var view = new CollectionView(first) { Source = new ObservableCollection<int> { 9 } };
        var log = Recorded(view);

        first.Add(3);

        Assert.That(Items(view), Is.EqualTo(new object[] { 9 }));
        Assert.That(log, Is.Empty, "the view is still listening to the collection it no longer shows");
    }

    [Test]
    public void ANullSource_IsEmptyRatherThanAFailure()
    {
        var view = new CollectionView();

        Assert.That(view.Count, Is.Zero);
        Assert.That(Items(view), Is.Empty);
    }
}
