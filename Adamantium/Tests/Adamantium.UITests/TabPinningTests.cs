using System.Linq;
using Adamantium.UI.Controls;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// Pinned tabs live in a row of their OWN, so the strip keeps two sources rather than one list it has to sort. These
/// cover the split itself - which tab is in which - because everything the strip draws is built on top of it.
/// </summary>
[TestFixture]
public class TabPinningTests
{
    private static TabControl Strip(params string[] headers)
    {
        var tabs = new TabControl();
        foreach (var header in headers) tabs.Items.Add(new TabItem { Header = header });
        return tabs;
    }

    private static string[] HeadersOf(System.Collections.Generic.IEnumerable<object> items) =>
        items.OfType<TabItem>().Select(tab => (string)tab.Header).ToArray();

    [Test]
    public void WithNothingPinned_EveryTabIsInTheOrdinaryRow()
    {
        var tabs = Strip("one", "two");

        Assert.Multiple(() =>
        {
            Assert.That(tabs.PinnedItems, Is.Empty);
            Assert.That(HeadersOf(tabs.UnpinnedItems), Is.EqualTo(new[] { "one", "two" }));
        });
    }

    [Test]
    public void PinningATab_MovesItToThePinnedRow()
    {
        var tabs = Strip("one", "two", "three");

        ((TabItem)tabs.Items[1]).IsPinned = true;

        Assert.Multiple(() =>
        {
            Assert.That(HeadersOf(tabs.PinnedItems), Is.EqualTo(new[] { "two" }));
            Assert.That(HeadersOf(tabs.UnpinnedItems), Is.EqualTo(new[] { "one", "three" }));
        });
    }

    /// <summary>Unpinning puts it back among the others, in the strip's own order - not at the end, which would make
    /// pinning and unpinning a way of shuffling the tabs.</summary>
    [Test]
    public void UnpinningATab_PutsItBackInPlace()
    {
        var tabs = Strip("one", "two", "three");
        var second = (TabItem)tabs.Items[1];

        second.IsPinned = true;
        second.IsPinned = false;

        Assert.That(HeadersOf(tabs.UnpinnedItems), Is.EqualTo(new[] { "one", "two", "three" }));
    }

    [Test]
    public void ATabAddedLater_LandsInTheRowItsFlagSays()
    {
        var tabs = Strip("one");

        tabs.Items.Add(new TabItem { Header = "pinned", IsPinned = true });
        tabs.Items.Add(new TabItem { Header = "plain" });

        Assert.Multiple(() =>
        {
            Assert.That(HeadersOf(tabs.PinnedItems), Is.EqualTo(new[] { "pinned" }));
            Assert.That(HeadersOf(tabs.UnpinnedItems), Is.EqualTo(new[] { "one", "plain" }));
        });
    }

    [Test]
    public void ARemovedTab_LeavesBothRows()
    {
        var tabs = Strip("one", "two");
        ((TabItem)tabs.Items[0]).IsPinned = true;

        tabs.Items.RemoveAt(0);

        Assert.Multiple(() =>
        {
            Assert.That(tabs.PinnedItems, Is.Empty, "the pinned row let it go");
            Assert.That(HeadersOf(tabs.UnpinnedItems), Is.EqualTo(new[] { "two" }));
        });
    }

    /// <summary>The default is a row of their own - the reason for splitting at all. Stated as a test because it is a
    /// decision, not an accident: sharing one row is what costs the ordinary tabs their space.</summary>
    [Test]
    public void ByDefault_PinnedTabsGetASeparateRow()
    {
        Assert.That(new TabControl().PinnedTabsPlacement, Is.EqualTo(PinnedTabsPlacement.SeparateRow));
    }
}
