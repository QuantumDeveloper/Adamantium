using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Adamantium.UI.Controls;
using Adamantium.UI.Core.Collections;
using Adamantium.UI.Core.Data;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// The pager's own job, without a template in sight: resolve what it was given into something pageable, drive it, and
/// report where it has got to. The buttons are the easy half - what is worth pinning is that ONE view is in play (the
/// list and the pager must not each make their own, or they turn different pages of the same data) and that an unknown
/// total is carried honestly rather than guessed at.
/// </summary>
[TestFixture]
public class DataPagerTests
{
    private static AsyncPagedSource Server(int?[] totals, params object[][] pages)
    {
        var index = 0;
        return new AsyncPagedSource((request, _) =>
        {
            var items = request.PageIndex < pages.Length ? pages[request.PageIndex] : [];
            var total = index < totals.Length ? totals[index] : null;
            index++;
            return Task.FromResult(new PageResult(items, total));
        }, 3);
    }

    [Test]
    public void APlainCollection_IsWrappedSoThereIsSomethingToPage()
    {
        var pager = new DataPager { PageSize = 2, Source = new[] { 1, 2, 3, 4, 5 } };

        Assert.That(pager.PagedSource, Is.InstanceOf<CollectionView>());
        Assert.That(pager.PageCount, Is.EqualTo(3));
        Assert.That(pager.TotalItemCount, Is.EqualTo(5));
        Assert.That(((CollectionView)pager.PagedSource).Cast<int>(), Is.EqualTo(new[] { 1, 2 }));
    }

    /// <summary>The list binds to PagedSource, so both are looking at the SAME object - the whole reason the pager
    /// owns the wrapping instead of leaving it to whoever writes the markup.</summary>
    [Test]
    public void ThePagedSource_IsTheSameObjectTheListWouldShow()
    {
        var pager = new DataPager { PageSize = 2, Source = new[] { 1, 2, 3, 4 } };

        pager.MoveToNextPage();

        Assert.That(((CollectionView)pager.PagedSource).Cast<int>(), Is.EqualTo(new[] { 3, 4 }),
            "the object handed to the list turned the page with the pager");
    }

    /// <summary>ANY collection - the pager must not be fussy about which kind, because the model layer will not be.</summary>
    [TestCaseSource(nameof(EveryKindOfCollection))]
    public void AnyKindOfCollection_CanBePaged(object source)
    {
        var pager = new DataPager { PageSize = 2, Source = source };

        Assert.That(pager.TotalItemCount, Is.EqualTo(4));
        Assert.That(pager.PageCount, Is.EqualTo(2));
        Assert.That(((CollectionView)pager.PagedSource).Cast<object>().Select(o => o.ToString()),
            Is.EqualTo(new[] { "1", "2" }));
    }

    private static IEnumerable<object> EveryKindOfCollection()
    {
        yield return new[] { 1, 2, 3, 4 };
        yield return new List<int> { 1, 2, 3, 4 };
        yield return new ObservableCollection<int> { 1, 2, 3, 4 };
        yield return new System.Collections.ArrayList { 1, 2, 3, 4 };
        yield return Enumerable.Range(1, 4);                        // lazy, never materialised by the caller
        yield return new[] { "1", "2", "3", "4" };
    }

    /// <summary>...and something that is not a collection at all is a mistake, said out loud. An empty pager would
    /// look like an empty collection, and the author would go looking in the wrong place.</summary>
    [Test]
    public void SomethingThatIsNotACollection_SaysSoRatherThanShowingNothing()
    {
        Assert.Throws<System.ArgumentException>(() => _ = new DataPager { Source = 42 });
    }

    [Test]
    public void AnAlreadyPageableSource_IsDrivenAsItIs()
    {
        var view = new CollectionView(new[] { 1, 2, 3, 4 });
        var pager = new DataPager { PageSize = 2, Source = view };

        Assert.That(pager.PagedSource, Is.SameAs(view), "nothing is wrapped around something that already pages");
    }

    /// <summary>The PAGER's size wins, including over a source that arrived with one of its own: the size is what the
    /// markup asked for, and a source cannot know what it is being shown in.</summary>
    [Test]
    public void ThePageSize_ReachesTheSourceAndOverridesIt()
    {
        var view = new CollectionView(new[] { 1, 2, 3, 4, 5, 6 }) { PageSize = 2 };
        var pager = new DataPager { PageSize = 3, Source = view };

        Assert.That(view.PageSize, Is.EqualTo(3));
        Assert.That(pager.PageCount, Is.EqualTo(2));
    }

    [Test]
    public void TheArrows_FollowWhereThereIsSomewhereToGo()
    {
        var pager = new DataPager { PageSize = 2, Source = new[] { 1, 2, 3, 4, 5 } };

        Assert.That(pager.CanGoBack, Is.False, "nothing before the first page");
        Assert.That(pager.CanGoForward, Is.True);

        pager.MoveToLastPage();

        Assert.That(pager.PageIndex, Is.EqualTo(2));
        Assert.That(pager.CanGoBack, Is.True);
        Assert.That(pager.CanGoForward, Is.False, "nothing after the last one");
    }

    [Test]
    public void SteppingBackAndForward_MovesOnePageEachWay()
    {
        var pager = new DataPager { PageSize = 2, Source = new[] { 1, 2, 3, 4, 5, 6 } };

        pager.MoveToNextPage();
        Assert.That(pager.PageIndex, Is.EqualTo(1));

        pager.MoveToPreviousPage();
        Assert.That(pager.PageIndex, Is.Zero);

        pager.MoveToPreviousPage();
        Assert.That(pager.PageIndex, Is.Zero, "there is nowhere further back to go");
    }

    [Test]
    public void SettingThePageIndex_TurnsThePage()
    {
        var pager = new DataPager { PageSize = 2, Source = new[] { 1, 2, 3, 4, 5, 6 } };

        pager.PageIndex = 2;

        Assert.That(((CollectionView)pager.PagedSource).Cast<int>(), Is.EqualTo(new[] { 5, 6 }));
    }

    // ---- an unknown total is a state, not a missing number -------------------------------------------------------

    /// <summary>Over a source that cannot count, the end is not known: no page numbers can be drawn and "last" has
    /// nowhere to go - but stepping FORWARD is still offered, because there may well be more.</summary>
    [Test]
    public async Task WithAnUnknownTotal_ForwardIsOfferedAndLastIsNot()
    {
        var server = Server([null, null], ["a", "b", "c"], ["d", "e", "f"]);
        var pager = new DataPager { PageSize = 3, Source = server };
        await server.MoveToPageAsync(0);

        Assert.That(pager.IsEndKnown, Is.False);
        Assert.That(pager.PageCount, Is.EqualTo(-1), "not zero - zero would read as 'there are no pages'");
        Assert.That(pager.TotalItemCount, Is.EqualTo(-1));
        Assert.That(pager.CanGoForward, Is.True);

        pager.MoveToLastPage();

        Assert.That(pager.PageIndex, Is.Zero, "there is no last page to go to yet");
    }

    /// <summary>...and when a short page reveals the end, the pager grows into it: the count appears and forward
    /// closes off. That transition is the one a pager over a server spends its life doing.</summary>
    [Test]
    public async Task WhenAShortPageRevealsTheEnd_ThePagerGrowsIntoIt()
    {
        var server = Server([null, null], ["a", "b", "c"], ["d"]);
        var pager = new DataPager { PageSize = 3, Source = server };

        await server.MoveToPageAsync(0);
        Assert.That(pager.IsEndKnown, Is.False);

        await server.MoveToPageAsync(1);

        Assert.That(pager.IsEndKnown, Is.True);
        Assert.That(pager.PageCount, Is.EqualTo(2));
        Assert.That(pager.TotalItemCount, Is.EqualTo(4));
        Assert.That(pager.CanGoForward, Is.False);
        Assert.That(pager.CanGoBack, Is.True);
    }

    [Test]
    public void WithNoSource_ThePagerIsQuietRatherThanBroken()
    {
        var pager = new DataPager();

        Assert.That(pager.PagedSource, Is.Null);
        Assert.That(pager.CanGoBack, Is.False);
        Assert.That(pager.CanGoForward, Is.False);
        pager.MoveToNextPage();
        Assert.That(pager.PageIndex, Is.Zero);
    }

    /// <summary>A live source under a page: removing items shrinks the page count under the reader, and the pager
    /// follows rather than pointing at a page that no longer exists.</summary>
    [Test]
    public void WhenTheSourceShrinks_ThePagerFollows()
    {
        var source = new ObservableCollection<int>(Enumerable.Range(1, 10));
        var view = new CollectionView(source);
        var pager = new DataPager { Source = view, PageSize = 3 };
        pager.MoveToLastPage();
        Assert.That(pager.PageIndex, Is.EqualTo(3));

        for (var i = 10; i > 4; i--) source.Remove(i);

        Assert.That(pager.PageCount, Is.EqualTo(2));
        Assert.That(pager.PageIndex, Is.EqualTo(1), "the page it was on is gone; it sits on the last one that exists");
    }

    // ---- the row of numbers is a WINDOW, not a list ---------------------------------------------------------------

    private static DataPager Pager(int pages, int pageIndex, int slots = 5)
    {
        var pager = new DataPager
        {
            PageSize = 1,
            PageButtonCount = slots,
            Source = new CollectionView(Enumerable.Range(1, pages).ToList())
        };
        pager.MoveToPage(pageIndex);
        return pager;
    }

    private static string Row(DataPager pager) => string.Join(" ", pager.PageItems.Select(i => i.Text));

    /// <summary>Fewer pages than the row holds: every one of them is shown, and there is nothing to stand in for.</summary>
    [Test]
    public void WithFewerPagesThanTheRowHolds_TheWholeRunIsShownWithNoGaps()
    {
        Assert.That(Row(Pager(2, 0)), Is.EqualTo("1 2"));
        Assert.That(Pager(2, 0).PageItems.All(i => i.IsEnabled), Is.True);
    }

    /// <summary>In the middle of a long run: the current page with a neighbour each side, and a gap at each end.</summary>
    [Test]
    public void InTheMiddleOfALongRun_TheWindowIsCentredWithAGapEachSide()
    {
        Assert.That(Row(Pager(10000, 10)), Is.EqualTo("… 10 11 12 …"));
    }

    /// <summary>At the ends there is nothing to leave out on one side, so that place is spent on ONE MORE PAGE rather
    /// than left empty - the row keeps its length instead of losing an entry as the reader reaches the edge.</summary>
    [Test]
    public void AtEitherEnd_TheFreedPlaceGoesToAnExtraPage()
    {
        Assert.That(Row(Pager(10000, 0)), Is.EqualTo("1 2 3 4 …"), "no gap before the first page");
        Assert.That(Row(Pager(10000, 9999)), Is.EqualTo("… 9997 9998 9999 10000"), "no gap after the last");
    }

    /// <summary>The head block holds until the reader steps off the end of it: on page 4 the row still starts at 1, and
    /// page 5 is the first that has anything hidden before it.</summary>
    [Test]
    public void TheEllipsisAppearsOnlyOnceAPageIsActuallyHidden()
    {
        Assert.That(Row(Pager(10000, 3)), Is.EqualTo("1 2 3 4 …"), "page 4 - nothing is hidden before it yet");
        Assert.That(Row(Pager(10000, 4)), Is.EqualTo("… 4 5 6 …"), "page 5 - page 1 is now behind the gap");
    }

    /// <summary>THE POINT OF THE WHOLE SHAPE: the row is the same length at every page and every page count. Walked
    /// end to end rather than sampled, because the two places a row like this changes length are the two ends.</summary>
    [Test]
    public void TheRowIsTheSameLength_AtEveryPageAndEveryPageCount()
    {
        foreach (var pages in new[] { 6, 10, 999, 10000 })
        {
            var pager = Pager(pages, 0);
            for (var page = 0; page < Math.Min(pages, 40); page++)
            {
                pager.MoveToPage(page);
                Assert.That(pager.PageItems.Count, Is.EqualTo(5), $"{pages} pages, page {page + 1}: {Row(pager)}");
            }

            for (var page = Math.Max(0, pages - 40); page < pages; page++)
            {
                pager.MoveToPage(page);
                Assert.That(pager.PageItems.Count, Is.EqualTo(5), $"{pages} pages, page {page + 1}: {Row(pager)}");
            }
        }
    }

    /// <summary>A gap always has a page behind it - it is never an ellipsis standing for nothing.</summary>
    [Test]
    public void AGap_AlwaysHasAtLeastOnePageBehindIt()
    {
        var pager = Pager(20, 0);
        for (var page = 0; page < 20; page++)
        {
            pager.MoveToPage(page);
            var numbers = pager.PageItems.Where(i => i.Text != "…").Select(i => int.Parse(i.Text)).ToList();

            if (pager.PageItems[0].Text == "…") Assert.That(numbers[0], Is.GreaterThan(1), Row(pager));
            else Assert.That(numbers[0], Is.EqualTo(1), Row(pager));

            if (pager.PageItems[^1].Text == "…") Assert.That(numbers[^1], Is.LessThan(20), Row(pager));
            else Assert.That(numbers[^1], Is.EqualTo(20), Row(pager));
        }
    }

    /// <summary>The ellipsis is LIVE, and steps one page the way it is pointing - a dead button in the middle of a row
    /// of live ones is a place the pointer goes to be refused.</summary>
    [Test]
    public void TheEllipsis_StepsOnePageTowardsTheSideItIsOn()
    {
        var pager = Pager(10000, 10);
        Assert.That(Row(pager), Is.EqualTo("… 10 11 12 …"));

        pager.PageItems[0].Command.Execute();
        Assert.That(pager.PageIndex, Is.EqualTo(9), "the leading ellipsis goes back one");

        pager.PageItems[^1].Command.Execute();
        Assert.That(pager.PageIndex, Is.EqualTo(10), "and the trailing one goes forward again");
    }

    /// <summary>Neither ellipsis is ever a dead button.</summary>
    [Test]
    public void EveryEntryInTheRow_IsSomethingToClick()
    {
        var pager = Pager(10000, 0);
        for (var page = 0; page < 12; page++)
        {
            pager.MoveToPage(page);
            Assert.That(pager.PageItems.All(i => i.IsEnabled && i.Command != null), Is.True, Row(pager));
        }
    }

    /// <summary>Exactly one entry is marked, and it is the page being shown.</summary>
    [Test]
    public void TheCurrentPage_IsTheOnlyOneMarked()
    {
        var items = Pager(10000, 10).PageItems;

        Assert.That(items.Count(i => i.IsCurrent), Is.EqualTo(1));
        Assert.That(items.Single(i => i.IsCurrent).Text, Is.EqualTo("11"));
    }

    /// <summary>Clicking a number in the row turns to that page.</summary>
    [Test]
    public void ANumberInTheRow_TurnsToItsPage()
    {
        var pager = Pager(10000, 10);

        pager.PageItems.Single(i => i.Text == "12").Command.Execute();

        Assert.That(pager.PageIndex, Is.EqualTo(11));
    }

    /// <summary>Over a source that cannot count there are no numbers to draw - the arrows and the box are the whole
    /// pager until a short page reveals the end.</summary>
    [Test]
    public async Task WithAnUnknownTotal_TheRowIsEmpty()
    {
        var server = Server([null, null], ["a", "b", "c"], ["d", "e", "f"]);
        var pager = new DataPager { PageSize = 3, Source = server };
        await server.MoveToPageAsync(0);

        Assert.That(pager.PageItems, Is.Empty);
    }

    // ---- the progression of page sizes ----------------------------------------------------------------------------

    /// <summary>A pager offers a progression out of the box, so a picker in a template has something to show without
    /// anyone declaring one.</summary>
    [Test]
    public void APager_OffersAProgressionOutOfTheBox()
    {
        Assert.That(new DataPager().PageSizes, Is.EqualTo(new[] { 10, 25, 50, 100 }));
    }

    /// <summary>...and the application owns it: the progression is a property, not a constant inside the control.</summary>
    [Test]
    public void TheProgression_IsTheApplicationsToReplace()
    {
        var pager = Pager(100, 0);

        pager.PageSizes = new[] { 15, 30, 60 };

        Assert.That(pager.PageSizes, Is.EqualTo(new[] { 15, 30, 60 }));
    }

    /// <summary>AND A BINDING WINS OVER THE DEFAULT, which is not the same statement. The default used to be written by
    /// the constructor, and a plain setter there takes the LOCAL slot - which outranks a binding for the life of the
    /// object. So <c>PageSizes="{Binding Steps}"</c> was accepted, reported nothing, and did nothing: the picker went on
    /// offering 10/25/50/100 while the application's own progression sat unused.</summary>
    [Test]
    public void ABinding_BeatsTheDefaultProgression()
    {
        var pager = new DataPager();
        var source = new StepsHolder { Steps = [15, 30, 60, 120] };

        pager.DataContext = source;
        pager.SetBinding(DataPager.PageSizesProperty, new Binding(nameof(StepsHolder.Steps)));
        BindingUpdateQueue.Flush();

        Assert.That(pager.PageSizes, Is.EqualTo(new[] { 15, 30, 60, 120 }),
            "the bound progression never reached the control");
    }

    private sealed class StepsHolder
    {
        public ObservableCollection<int> Steps { get; init; }
    }

    /// <summary>A size that the new progression does not offer moves to the NEAREST one that it does - a picker showing
    /// a value nobody can choose again is a picker lying about its choices.</summary>
    [Test]
    public void ASizeTheProgressionDoesNotOffer_MovesToTheNearestItDoes()
    {
        var pager = new DataPager { PageSize = 40, Source = Enumerable.Range(1, 500).ToList() };

        pager.PageSizes = new[] { 10, 25, 50, 100 };

        Assert.That(pager.PageSize, Is.EqualTo(50), "40 is closer to 50 than to 25");
    }

    /// <summary>A size the progression already offers is left exactly where it is.</summary>
    [Test]
    public void ASizeTheProgressionOffers_IsLeftAlone()
    {
        var pager = new DataPager { PageSize = 25, Source = Enumerable.Range(1, 500).ToList() };

        pager.PageSizes = new[] { 10, 25, 50 };

        Assert.That(pager.PageSize, Is.EqualTo(25));
    }

    /// <summary>THE PROGRESSION EDITED IN PLACE, which is the ordinary case: an application that lets the reader add a
    /// step hands over the same collection and adds to it. Growing it offers more and disturbs nothing.</summary>
    [Test]
    public void AStepAddedToTheProgression_ChangesNothingElse()
    {
        var steps = new ObservableCollection<int> { 10, 25, 50 };
        var pager = new DataPager { PageSize = 25, PageSizes = steps, Source = Enumerable.Range(1, 500).ToList() };

        steps.Add(200);

        Assert.That(pager.PageSizes, Is.EqualTo(new[] { 10, 25, 50, 200 }));
        Assert.That(pager.PageSize, Is.EqualTo(25), "adding a choice does not move the reader off the one they are on");
    }

    /// <summary>...and taking away the step it is ON moves it, live. Without listening to the COLLECTION - only to the
    /// property - the picker would be left showing a number that had just stopped being one of the choices.</summary>
    [Test]
    public void TheStepInUseRemovedFromTheProgression_MovesToTheNearestLeft()
    {
        var steps = new ObservableCollection<int> { 10, 25, 50 };
        var pager = new DataPager { PageSize = 25, PageSizes = steps, Source = Enumerable.Range(1, 500).ToList() };

        steps.Remove(25);

        Assert.That(pager.PageSize, Is.EqualTo(10).Or.EqualTo(50));
        Assert.That(steps, Does.Contain(pager.PageSize), "whatever it moved to has to be a step that still exists");
    }

    /// <summary>And the page really is re-cut - the move is a page size, not a number on a label.</summary>
    [Test]
    public void MovingToAnotherStep_ReCutsThePages()
    {
        var steps = new ObservableCollection<int> { 10, 50 };
        var pager = new DataPager { PageSize = 10, PageSizes = steps, Source = Enumerable.Range(1, 100).ToList() };
        Assert.That(pager.PageCount, Is.EqualTo(10));

        steps.Remove(10);

        Assert.That(pager.PageSize, Is.EqualTo(50));
        Assert.That(pager.PageCount, Is.EqualTo(2), "100 rows in pages of 50");
    }

    // ---- DisplayMode, spread into one boolean per part ------------------------------------------------------------

    /// <summary>The whole pager by default - a control that hid half of itself until asked would read as broken.</summary>
    [Test]
    public void ByDefault_EveryPartIsShown()
    {
        var pager = Pager(10, 0);

        Assert.Multiple(() =>
        {
            Assert.That(pager.ShowsFirstLast, Is.True);
            Assert.That(pager.ShowsPreviousNext, Is.True);
            Assert.That(pager.ShowsNumbers, Is.True);
            Assert.That(pager.ShowsPageSizeSelector, Is.True);
            Assert.That(pager.ShowsPageInput, Is.True);
        });
    }

    /// <summary>Each flag reaches its own part and nothing else. A template cannot test a flags enum - the engine ships
    /// no converters and a trigger matches a WHOLE value - so these booleans are the only thing markup can read, and
    /// they have to be right one at a time.</summary>
    [Test]
    public void EachFlag_ReachesItsOwnPartAndNoOther()
    {
        var pager = Pager(10, 0);

        pager.DisplayMode = PagerDisplayMode.PreviousNext;

        Assert.Multiple(() =>
        {
            Assert.That(pager.ShowsPreviousNext, Is.True);
            Assert.That(pager.ShowsFirstLast, Is.False);
            Assert.That(pager.ShowsNumbers, Is.False);
            Assert.That(pager.ShowsPageSizeSelector, Is.False);
            Assert.That(pager.ShowsPageInput, Is.False);
        });
    }

    /// <summary>Flags COMPOSE - that is what a flags enum is for, and the whole reason one boolean per part exists.</summary>
    [Test]
    public void FlagsCompose()
    {
        var pager = Pager(10, 0);

        pager.DisplayMode = PagerDisplayMode.FirstLast | PagerDisplayMode.PageInput;

        Assert.Multiple(() =>
        {
            Assert.That(pager.ShowsFirstLast, Is.True);
            Assert.That(pager.ShowsPageInput, Is.True);
            Assert.That(pager.ShowsPreviousNext, Is.False);
            Assert.That(pager.ShowsNumbers, Is.False);
        });
    }

    /// <summary>The numbers need a total as well as permission: over a source that cannot count there is nothing to
    /// number, and the row waits for the end to be discovered rather than appearing and vanishing.</summary>
    [Test]
    public async Task TheNumbersWaitForATotal_EvenWhenTheyAreAskedFor()
    {
        var server = Server([null, null], ["a", "b", "c"], ["d"]);
        var pager = new DataPager { PageSize = 3, Source = server };

        await server.MoveToPageAsync(0);
        Assert.That(pager.ShowsNumbers, Is.False, "no total yet - nothing to number");

        await server.MoveToPageAsync(1);
        Assert.That(pager.ShowsNumbers, Is.True, "a short page revealed the end");
    }

    // ---- "Page [n] of N" ------------------------------------------------------------------------------------------

    /// <summary>The box's value and the tail beside it are separate from the sentence, because one of them is edited.</summary>
    [Test]
    public void ThePageBoxAndItsTail_ReportWhereTheReaderIs()
    {
        var pager = Pager(10000, 10);

        Assert.That(pager.PageNumberText, Is.EqualTo("11"));
        Assert.That(pager.PageCountText, Is.EqualTo("of 10000"));
        Assert.That(pager.PageText, Is.EqualTo("Page 11 of 10000"));
    }

    /// <summary>With the end still unknown the tail says so rather than naming a number the pager does not have.</summary>
    [Test]
    public async Task WithAnUnknownTotal_TheTailSaysSoRatherThanNamingAZero()
    {
        var server = Server([null, null], ["a", "b", "c"], ["d", "e", "f"]);
        var pager = new DataPager { PageSize = 3, Source = server };
        await server.MoveToPageAsync(0);

        Assert.That(pager.PageNumberText, Is.EqualTo("1"));
        Assert.That(pager.PageCountText, Is.EqualTo("of ?"));
    }
}
