using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Adamantium.UI.Controls;
using Adamantium.UI.Core.Collections;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// A source EDITED in place, rather than replaced. An items control subscribes to its source's collection changes, so a
/// row added to a live list has to appear without anything being re-assigned - which is the ordinary case for a list an
/// application lets the reader add to.
/// <para>Written because a page-size picker in a pager did not: the same collection, shown twice, moved in one place and
/// not in the other. Asked of the CONTROL rather than of the screen, so the answer says which of the two is wrong.</para>
/// </summary>
[TestFixture]
public class ItemsSourceLiveEditTests
{
    [Test]
    public void AnItemsControl_FollowsAnAddToItsSource()
    {
        var source = new ObservableCollection<int> { 10, 25 };
        var control = new ItemsControl { ItemsSource = source };

        source.Add(50);

        Assert.That(control.Items.Count, Is.EqualTo(3));
    }

    [Test]
    public void AnItemsControl_FollowsARemoveFromItsSource()
    {
        var source = new ObservableCollection<int> { 10, 25, 50 };
        var control = new ItemsControl { ItemsSource = source };

        source.Remove(25);

        Assert.That(control.Items.Count, Is.EqualTo(2));
        Assert.That(control.Items, Does.Not.Contain(25));
    }

    /// <summary>...and a DROP-DOWN is an items control, so it has to do the same. A picker that shows the choices it had
    /// when it was built is a picker that stops offering what the application started offering.</summary>
    [Test]
    public void ADropDown_FollowsAnAddToItsSource()
    {
        var source = new ObservableCollection<int> { 10, 25 };
        var drop = new DropDown { ItemsSource = source };

        source.Add(50);

        Assert.That(drop.Items.Count, Is.EqualTo(3));
        Assert.That(drop.Items, Does.Contain(50));
    }

    /// <summary>A PAGED SOURCE reaches the list the same way, and this is asked through the LIST rather than through the
    /// source's event on purpose. The event was always raised; what was missing was the interface that declares it, and
    /// a list subscribes by asking `source is INotifyCollectionChanged` - never by looking for an event of that name.
    /// So every page a server returned landed in the source and was never shown: the request completed, every state
    /// property updated, and the rows on screen stayed as they were. A test on the event alone would have passed.</summary>
    [Test]
    public async Task AnItemsControl_ShowsThePageAPagedSourceFetches()
    {
        var source = new AsyncPagedSource((request, _) => Task.FromResult(
            new PageResult(Enumerable.Range(request.PageIndex * request.PageSize, request.PageSize)
                .Select(object (i) => $"row {i}").ToList())), 5);
        var control = new ItemsControl { ItemsSource = source };

        await source.MoveToPageAsync(0);
        Assert.That(control.Items.Count, Is.EqualTo(5), "the first page never reached the list");
        Assert.That(control.Items[0], Is.EqualTo("row 0"));

        await source.MoveToPageAsync(1);
        Assert.That(control.Items[0], Is.EqualTo("row 5"), "the list still shows the page before");
    }

    /// <summary>...and a paged source declares the interface, which is what the list actually asks.</summary>
    [Test]
    public void APagedSource_DeclaresThatItNotifies()
    {
        Assert.That(new AsyncPagedSource((_, _) => Task.FromResult(new PageResult([]))),
            Is.InstanceOf<INotifyCollectionChanged>());
        Assert.That(new CollectionView(new[] { 1, 2, 3 }), Is.InstanceOf<INotifyCollectionChanged>());
    }

    [Test]
    public void ADropDown_FollowsARemoveFromItsSource()
    {
        var source = new ObservableCollection<int> { 10, 25, 50 };
        var drop = new DropDown { ItemsSource = source, SelectedItem = 10 };

        source.Remove(50);

        Assert.That(drop.Items.Count, Is.EqualTo(2));
        Assert.That(drop.Items, Does.Not.Contain(50));
    }
}
