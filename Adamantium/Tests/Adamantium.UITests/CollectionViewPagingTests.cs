using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Adamantium.UI.Core.Collections;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// A page is a WINDOW over the shaped set, not a snapshot of it. That is the whole of this file: the window is a fixed
/// size, so an item leaving in the middle of it has to pull the next one in from beyond the page boundary, and an item
/// arriving before it has to push the last one out. Those two are what a paging implementation that only re-slices on
/// MoveToPage gets wrong - it shows 24 rows on a page of 25 and nobody notices until they count.
/// </summary>
[TestFixture]
public class CollectionViewPagingTests
{
    private static int[] Shown(CollectionView view) => view.Cast<int>().ToArray();

    private static List<NotifyCollectionChangedEventArgs> Recorded(CollectionView view)
    {
        var log = new List<NotifyCollectionChangedEventArgs>();
        view.CollectionChanged += (_, e) => log.Add(e);
        return log;
    }

    private static CollectionView Paged(ObservableCollection<int> source, int size, int index = 0) =>
        new(source) { PageSize = size, PageIndex = index };

    [Test]
    public void WithoutAPageSize_EverythingIsShown()
    {
        var view = new CollectionView(new[] { 1, 2, 3 });

        Assert.That(view.IsPaged, Is.False);
        Assert.That(Shown(view), Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(view.PageCount, Is.EqualTo(1));
    }

    [Test]
    public void APageSize_ShowsOneWindow()
    {
        var view = Paged(new ObservableCollection<int> { 1, 2, 3, 4, 5 }, 2);

        Assert.That(Shown(view), Is.EqualTo(new[] { 1, 2 }));
        Assert.That(view.Count, Is.EqualTo(2), "Count is the PAGE");
        Assert.That(view.TotalItemCount, Is.EqualTo(5), "...and TotalItemCount what there is to page through");
        Assert.That(view.PageCount, Is.EqualTo(3), "5 over 2 is three pages, the last one short");
    }

    [Test]
    public void TheLastPage_MayBeShort()
    {
        var view = Paged(new ObservableCollection<int> { 1, 2, 3, 4, 5 }, 2, 2);

        Assert.That(Shown(view), Is.EqualTo(new[] { 5 }));
    }

    [Test]
    public void APageIndexPastTheEnd_IsClamped()
    {
        var view = Paged(new ObservableCollection<int> { 1, 2, 3 }, 2, 99);

        Assert.That(view.PageIndex, Is.EqualTo(1));
        Assert.That(Shown(view), Is.EqualTo(new[] { 3 }));
    }

    /// <summary>Paging runs LAST: the window is cut out of what the filter and the sort left, not out of the source.</summary>
    [Test]
    public void PagingComesAfterFilterAndSort()
    {
        var view = new CollectionView(new[] { 5, 2, 9, 4, 7, 1, 3 })
        {
            Filter = o => (int)o % 2 == 1,
            PageSize = 2
        };
        view.SortDescriptions.Add(new SortDescription(null));

        Assert.That(view.TotalItemCount, Is.EqualTo(5), "the five odd ones - the evens are on no page at all");
        Assert.That(Shown(view), Is.EqualTo(new[] { 5, 9 }), "the first two of what the filter left, in source order");
    }

    // ---- the window's edges: the two cases a re-slicing implementation gets wrong --------------------------------

    /// <summary>An item removed from the middle of the window pulls the next one in from BEYOND the page - the page
    /// must not go one short.</summary>
    [Test]
    public void RemovingInsideTheWindow_PullsTheNextOneIn()
    {
        var source = new ObservableCollection<int> { 1, 2, 3, 4, 5, 6 };
        var view = Paged(source, 3);
        var log = Recorded(view);

        source.Remove(2);

        Assert.That(Shown(view), Is.EqualTo(new[] { 1, 3, 4 }), "4 was on page two and has been pulled in");
        Assert.That(view.Count, Is.EqualTo(3), "a page of three stays a page of three while there is more to show");
        Assert.That(log.Select(e => e.Action),
            Is.EqualTo(new[] { NotifyCollectionChangedAction.Remove, NotifyCollectionChangedAction.Add }));
        Assert.That(log[0].OldStartingIndex, Is.EqualTo(1));
        Assert.That(log[1].NewStartingIndex, Is.EqualTo(2), "the arrival lands at the END of the window");
    }

    /// <summary>An item added inside the window pushes the last one OUT onto the next page.</summary>
    [Test]
    public void AddingInsideTheWindow_PushesTheLastOneOut()
    {
        var source = new ObservableCollection<int> { 1, 2, 3, 4 };
        var view = Paged(source, 3);
        var log = Recorded(view);

        source.Insert(1, 9);

        Assert.That(Shown(view), Is.EqualTo(new[] { 1, 9, 2 }));
        Assert.That(log.Select(e => e.Action),
            Is.EqualTo(new[] { NotifyCollectionChangedAction.Add, NotifyCollectionChangedAction.Remove }));
        Assert.That(log[0].NewStartingIndex, Is.EqualTo(1));
        Assert.That(log[1].OldItems[0], Is.EqualTo(3), "3 was pushed off the end of the page");
        Assert.That(log[1].OldStartingIndex, Is.EqualTo(3),
            "index 3, not 2: the consumer has already applied the Add, so its list is four long at that moment");
    }

    /// <summary>A removal BEFORE the window shifts everything back: the first row leaves and a new one arrives at the
    /// end, without the page number changing. This is the case nobody writes a test for.</summary>
    [Test]
    public void RemovingBEFORETheWindow_ShiftsItAlong()
    {
        var source = new ObservableCollection<int> { 1, 2, 3, 4, 5, 6, 7 };
        var view = Paged(source, 3, 1);
        Assert.That(Shown(view), Is.EqualTo(new[] { 4, 5, 6 }));
        var log = Recorded(view);

        source.Remove(1);

        Assert.That(Shown(view), Is.EqualTo(new[] { 5, 6, 7 }));
        Assert.That(view.PageIndex, Is.EqualTo(1), "the page did not turn - its contents moved");
        Assert.That(log.Select(e => e.Action),
            Is.EqualTo(new[] { NotifyCollectionChangedAction.Remove, NotifyCollectionChangedAction.Add }));
    }

    /// <summary>...and an insertion before it, the other way round.</summary>
    [Test]
    public void AddingBEFORETheWindow_ShiftsItAlong()
    {
        var source = new ObservableCollection<int> { 1, 2, 3, 4, 5, 6 };
        var view = Paged(source, 3, 1);
        Assert.That(Shown(view), Is.EqualTo(new[] { 4, 5, 6 }));

        source.Insert(0, 0);

        Assert.That(Shown(view), Is.EqualTo(new[] { 3, 4, 5 }));
        Assert.That(view.PageIndex, Is.EqualTo(1));
    }

    /// <summary>A change past the last page is invisible - but the totals still move, because a pager is showing them.</summary>
    [Test]
    public void AChangeBeyondTheWindow_MovesOnlyTheTotals()
    {
        var source = new ObservableCollection<int> { 1, 2, 3 };
        var view = Paged(source, 2);
        var log = Recorded(view);

        source.Add(4);

        Assert.That(log, Is.Empty, "nothing on screen changed");
        Assert.That(view.TotalItemCount, Is.EqualTo(4));
        Assert.That(view.PageCount, Is.EqualTo(2));
    }

    /// <summary>Emptied out from under the reader, the view lands on the last page that exists rather than showing
    /// nothing.</summary>
    [Test]
    public void WhenThePageStopsExisting_TheViewFallsBackToTheLastOne()
    {
        var source = new ObservableCollection<int> { 1, 2, 3, 4, 5, 6, 7 };
        var view = Paged(source, 3, 2);
        Assert.That(Shown(view), Is.EqualTo(new[] { 7 }));

        for (var i = 7; i > 2; i--) source.Remove(i);

        Assert.That(view.PageIndex, Is.EqualTo(0));
        Assert.That(Shown(view), Is.EqualTo(new[] { 1, 2 }));
    }

    /// <summary>Changing the size renumbers the pages AROUND the reader instead of dropping them at the beginning.</summary>
    [Test]
    public void ChangingThePageSize_KeepsTheReaderWhereTheyWere()
    {
        var source = new ObservableCollection<int>(Enumerable.Range(1, 100));
        var view = Paged(source, 10, 4);
        Assert.That(Shown(view).First(), Is.EqualTo(41));

        view.PageSize = 20;

        Assert.That(view.PageIndex, Is.EqualTo(2), "item 41 lives on page 2 when pages hold 20");
        Assert.That(Shown(view).First(), Is.EqualTo(41), "and it is still the first thing on screen");
    }

    [Test]
    public void TurningPagingOff_ShowsEverythingAgain()
    {
        var view = Paged(new ObservableCollection<int> { 1, 2, 3, 4 }, 2, 1);

        view.PageSize = 0;

        Assert.That(view.IsPaged, Is.False);
        Assert.That(Shown(view), Is.EqualTo(new[] { 1, 2, 3, 4 }));
        Assert.That(view.PageIndex, Is.Zero);
    }

    /// <summary>Live filtering under a page: the row leaves AND the page refills, both.</summary>
    [Test]
    public void LiveFilteringUnderAPage_RefillsTheWindow()
    {
        var source = new ObservableCollection<Flagged>(Enumerable.Range(1, 6).Select(i => new Flagged(i)));
        var view = new CollectionView(source)
        {
            Filter = o => !((Flagged)o).Hidden,
            IsLiveFiltering = true,
            PageSize = 3
        };
        view.LiveFilteringProperties.Add(nameof(Flagged.Hidden));

        source[1].Hidden = true;

        Assert.That(view.Cast<Flagged>().Select(f => f.Id), Is.EqualTo(new[] { 1, 3, 4 }));
        Assert.That(view.TotalItemCount, Is.EqualTo(5));
    }

    private sealed class Flagged(int id) : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _hidden;

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        public int Id { get; } = id;

        public bool Hidden
        {
            get => _hidden;
            set
            {
                _hidden = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Hidden)));
            }
        }
    }
}
