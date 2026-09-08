using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Adamantium.UI.Core.Collections;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// The half that makes filtering usable: an item that changes ITSELF leaves or re-enters the view, without the source
/// or the predicate having changed. Ordinary filtering re-runs on a new predicate or a new item; a row whose own
/// property stopped qualifying just stays on screen, and search-as-you-type, facets and "hide the finished ones" are
/// all that same case.
/// <para>It is opt-in because it subscribes per item, so the tests also pin that it is genuinely OFF until asked for
/// and genuinely released when it is not.</para>
/// </summary>
[TestFixture]
public class CollectionViewLiveShapingTests
{
    private sealed class Row(string name, bool done = false) : INotifyPropertyChanged
    {
        private string _name = name;
        private bool _done = done;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Name
        {
            get => _name;
            set { _name = value; Raise(nameof(Name)); }
        }

        public bool Done
        {
            get => _done;
            set { _done = value; Raise(nameof(Done)); }
        }

        public int Weight { get; set; }

        public void RaiseWeight() => Raise(nameof(Weight));

        private void Raise(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

        public override string ToString() => Name;
    }

    private sealed class ByName : IComparer
    {
        public int Compare(object x, object y) => string.CompareOrdinal(((Row)x).Name, ((Row)y).Name);
    }

    private static string[] Names(CollectionView view) => view.Cast<Row>().Select(r => r.Name).ToArray();

    private static List<NotifyCollectionChangedEventArgs> Recorded(CollectionView view)
    {
        var log = new List<NotifyCollectionChangedEventArgs>();
        view.CollectionChanged += (_, e) => log.Add(e);
        return log;
    }

    /// <summary>The default costs nothing: nobody is subscribed, so nothing reacts.</summary>
    [Test]
    public void WithoutLiveFiltering_AChangedItemStaysPut()
    {
        var row = new Row("a");
        var view = new CollectionView(new ObservableCollection<Row> { row, new("b") })
        {
            Filter = o => !((Row)o).Done
        };
        var log = Recorded(view);

        row.Done = true;

        Assert.That(Names(view), Is.EqualTo(new[] { "a", "b" }), "this is the WPF behaviour, and why live shaping exists");
        Assert.That(log, Is.Empty);
    }

    [Test]
    public void AnItemThatStopsQualifying_LeavesAtItsViewIndex()
    {
        var row = new Row("b");
        var view = new CollectionView(new ObservableCollection<Row> { new("a"), row, new("c") })
        {
            Filter = o => !((Row)o).Done,
            IsLiveFiltering = true
        };
        view.LiveFilteringProperties.Add(nameof(Row.Done));
        var log = Recorded(view);

        row.Done = true;

        Assert.That(Names(view), Is.EqualTo(new[] { "a", "c" }));
        Assert.That(log, Has.Count.EqualTo(1));
        Assert.That(log[0].Action, Is.EqualTo(NotifyCollectionChangedAction.Remove));
        Assert.That(log[0].OldStartingIndex, Is.EqualTo(1));
    }

    [Test]
    public void AnItemThatStartsQualifying_ComesBackInItsPlace()
    {
        var row = new Row("b", done: true);
        var view = new CollectionView(new ObservableCollection<Row> { new("a"), row, new("c") })
        {
            Filter = o => !((Row)o).Done,
            IsLiveFiltering = true
        };
        view.LiveFilteringProperties.Add(nameof(Row.Done));
        Assert.That(Names(view), Is.EqualTo(new[] { "a", "c" }));
        var log = Recorded(view);

        row.Done = false;

        Assert.That(Names(view), Is.EqualTo(new[] { "a", "b", "c" }), "back between its neighbours, not appended");
        Assert.That(log, Has.Count.EqualTo(1));
        Assert.That(log[0].Action, Is.EqualTo(NotifyCollectionChangedAction.Add));
        Assert.That(log[0].NewStartingIndex, Is.EqualTo(1));
    }

    /// <summary>Naming the properties is the whole point of the opt-in: an unrelated change must not run the predicate
    /// over anything.</summary>
    [Test]
    public void AChangeToAnUNNAMEDProperty_IsIgnored()
    {
        var row = new Row("a");
        var view = new CollectionView(new ObservableCollection<Row> { row })
        {
            Filter = o => !((Row)o).Done,
            IsLiveFiltering = true
        };
        view.LiveFilteringProperties.Add(nameof(Row.Done));
        var log = Recorded(view);

        row.Weight = 5;
        row.RaiseWeight();

        Assert.That(log, Is.Empty);
    }

    [Test]
    public void AChangedSortKey_MovesTheItem()
    {
        var row = new Row("b");
        var view = new CollectionView(new ObservableCollection<Row> { new("a"), row, new("c") })
        {
            IsLiveSorting = true
        };
        view.SortDescriptions.Add(new SortDescription(nameof(Row.Name)));
        var log = Recorded(view);

        row.Name = "z";

        Assert.That(Names(view), Is.EqualTo(new[] { "a", "c", "z" }));
        Assert.That(log, Has.Count.EqualTo(1));
        Assert.That(log[0].Action, Is.EqualTo(NotifyCollectionChangedAction.Move), "a re-placed row MOVES, it is not a reset");
        Assert.That(log[0].OldStartingIndex, Is.EqualTo(1));
        Assert.That(log[0].NewStartingIndex, Is.EqualTo(2));
    }

    /// <summary>Sorting by a property IS saying that property matters, so nothing has to be named twice.</summary>
    [Test]
    public void SortDescriptions_ImplyTheirOwnLiveProperties()
    {
        var row = new Row("b");
        var view = new CollectionView(new ObservableCollection<Row> { new("a"), row }) { IsLiveSorting = true };
        view.SortDescriptions.Add(new SortDescription(nameof(Row.Name)));

        row.Name = "0";

        Assert.That(Names(view), Is.EqualTo(new[] { "0", "a" }),
            "LiveSortingProperties was never filled in - the descriptions answer for themselves");
    }

    /// <summary>A comparer cannot be asked what it reads, so with nothing named EVERY change re-places the item -
    /// correct, and the slow answer. Naming the properties is what narrows it, and the only reason to.</summary>
    [Test]
    public void ACustomSort_ReactsToEverythingUntilItsPropertiesAreNamed()
    {
        var row = new Row("b");
        var view = new CollectionView(new ObservableCollection<Row> { new("a"), row })
        {
            CustomSort = new ByName(),
            IsLiveSorting = true
        };

        row.Name = "0";
        Assert.That(Names(view), Is.EqualTo(new[] { "0", "a" }), "nothing named - so an opaque comparer reacts to all of it");

        var narrowed = new CollectionView(new ObservableCollection<Row> { new("a"), row })
        {
            CustomSort = new ByName(),
            IsLiveSorting = true
        };
        narrowed.LiveSortingProperties.Add(nameof(Row.Done));
        var log = Recorded(narrowed);

        row.Name = "!";

        Assert.That(log, Is.Empty, "Name was not named, so its change must not cost a re-place");
    }

    /// <summary>Turning it off has to actually let go - a subscription that outlives its purpose is how a view keeps a
    /// whole model alive.</summary>
    [Test]
    public void TurningLiveShapingOff_ReleasesTheItems()
    {
        var row = new Row("a");
        var view = new CollectionView(new ObservableCollection<Row> { row })
        {
            Filter = o => !((Row)o).Done,
            IsLiveFiltering = true
        };
        view.LiveFilteringProperties.Add(nameof(Row.Done));

        view.IsLiveFiltering = false;
        var log = Recorded(view);
        row.Done = true;

        Assert.That(log, Is.Empty);
        Assert.That(Names(view), Is.EqualTo(new[] { "a" }));
    }

    /// <summary>...and so does an item leaving the source.</summary>
    [Test]
    public void AnItemRemovedFromTheSource_IsReleased()
    {
        var row = new Row("b");
        var source = new ObservableCollection<Row> { new("a"), row };
        var view = new CollectionView(source)
        {
            Filter = o => !((Row)o).Done,
            IsLiveFiltering = true
        };
        view.LiveFilteringProperties.Add(nameof(Row.Done));

        source.Remove(row);
        var log = Recorded(view);
        row.Done = true;

        Assert.That(log, Is.Empty, "the view is still listening to an item it no longer holds");
    }

    /// <summary>Live filtering and live sorting on the same view: the item has to leave, not be re-placed.</summary>
    [Test]
    public void FilteringWins_WhenAChangeDoesBoth()
    {
        var row = new Row("b");
        var view = new CollectionView(new ObservableCollection<Row> { new("a"), row, new("c") })
        {
            Filter = o => !((Row)o).Done,
            IsLiveFiltering = true,
            IsLiveSorting = true
        };
        view.SortDescriptions.Add(new SortDescription(nameof(Row.Name)));
        view.LiveFilteringProperties.Add(nameof(Row.Done));
        var log = Recorded(view);

        row.Done = true;

        Assert.That(Names(view), Is.EqualTo(new[] { "a", "c" }));
        Assert.That(log.Select(e => e.Action), Is.EqualTo(new[] { NotifyCollectionChangedAction.Remove }));
    }
}
