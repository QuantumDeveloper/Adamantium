using Adamantium.Core.Collections;
using NUnit.Framework;

namespace Adamantium.CoreTests;

// Regression: AdamantiumCollection.Insert(index, item) stored the item at the END (currentIndex) instead of at `index`,
// so an insert-in-the-middle appended it and duplicated the element that had been at `index`. It surfaced as a tab
// dragged to a middle slot "flying to the end" (the panel's Children.Insert during the reorder appended the moved tab).
[TestFixture]
public class AdamantiumCollectionTests
{
    private static TrackingCollection<string> Of(params string[] items)
    {
        var c = new TrackingCollection<string>();
        foreach (var i in items) c.Add(i);
        return c;
    }

    [Test]
    public void Insert_InTheMiddle_PutsItemAtIndex_NotAtTheEnd()
    {
        var c = Of("A", "B", "C", "D");

        c.Insert(2, "X");

        Assert.That(c.Count, Is.EqualTo(5));
        Assert.That(new[] { c[0], c[1], c[2], c[3], c[4] }, Is.EqualTo(new[] { "A", "B", "X", "C", "D" }));
    }

    [Test]
    public void Insert_AtStart_ShiftsEverythingRight()
    {
        var c = Of("A", "B");
        c.Insert(0, "X");
        Assert.That(new[] { c[0], c[1], c[2] }, Is.EqualTo(new[] { "X", "A", "B" }));
    }

    [Test]
    public void Insert_AtEnd_Appends()
    {
        var c = Of("A", "B");
        c.Insert(2, "C");
        Assert.That(new[] { c[0], c[1], c[2] }, Is.EqualTo(new[] { "A", "B", "C" }));
    }

    // The exact move-reorder pattern (RemoveAt + Insert), as a tab drag commits it.
    [Test]
    public void RemoveThenInsert_ReordersToTheTargetSlot()
    {
        var c = Of("A", "B", "C", "D", "E");
        var moved = c[1];       // "B"
        c.RemoveAt(1);          // A C D E
        c.Insert(3, moved);     // A C D B E
        Assert.That(new[] { c[0], c[1], c[2], c[3], c[4] }, Is.EqualTo(new[] { "A", "C", "D", "B", "E" }));
    }

    // Regression: RemoveRange removed every OTHER element (RemoveInternal(i) for i in [index, index+count), but each
    // remove shifts the rest down) and could run off the end.
    [Test]
    public void RemoveRange_RemovesTheConsecutiveBlock()
    {
        var c = Of("A", "B", "C", "D", "E");
        c.RemoveRange(1, 2);   // drop B, C
        Assert.That(c.Count, Is.EqualTo(3));
        Assert.That(new[] { c[0], c[1], c[2] }, Is.EqualTo(new[] { "A", "D", "E" }));
    }

    [Test]
    public void RemoveRange_ClampsCountToWhatRemains()
    {
        var c = Of("A", "B", "C");
        c.RemoveRange(1, 10);
        Assert.That(c.Count, Is.EqualTo(1));
        Assert.That(c[0], Is.EqualTo("A"));
    }
}
