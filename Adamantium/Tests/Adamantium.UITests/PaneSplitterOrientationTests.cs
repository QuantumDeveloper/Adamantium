using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// A splitter's orientation is the THEME's business - the grip it draws runs across the gap, so which way it runs is
/// decided by this. It used to be a plain CLR property over a field: the setter announced nothing, so a
/// <c>PropertyTrigger</c> keyed on it never fired and a horizontal splitter went on drawing the vertical grip - a
/// sliver as tall as the gap instead of a line along it.
/// </summary>
[TestFixture]
public class PaneSplitterOrientationTests
{
    /// <summary>The property system has to SEE the change, or nothing a theme writes about it can ever run.</summary>
    [Test]
    public void ChangingTheOrientation_IsAnnouncedToThePropertySystem()
    {
        var splitter = new PaneSplitter { Orientation = Orientation.Horizontal };

        var announced = false;
        splitter.PropertyChanged += (_, e) =>
        {
            if (e.Property == PaneSplitter.OrientationProperty) announced = true;
        };

        splitter.Orientation = Orientation.Vertical;

        Assert.Multiple(() =>
        {
            Assert.That(announced, Is.True, "a trigger can only watch what the property system announces");
            Assert.That(splitter.Orientation, Is.EqualTo(Orientation.Vertical));
        });
    }

    /// <summary>...and the cursor still follows it: that side effect used to live in the setter, and moving the value
    /// into the property system is exactly the kind of change that silently drops it.</summary>
    [Test]
    public void TheResizeCursor_StillFollowsTheOrientation()
    {
        var splitter = new PaneSplitter { Orientation = Orientation.Horizontal };
        var acrossCursor = splitter.Cursor;

        splitter.Orientation = Orientation.Vertical;

        Assert.That(splitter.Cursor, Is.Not.EqualTo(acrossCursor),
            "without the arrows nothing tells the user this strip can be dragged");
    }

    /// <summary>The case a value-changed callback CANNOT cover, and the one that broke: the host writes every
    /// splitter's orientation, and writing the value it already has is not a change - so a splitter left at the
    /// default never ran the callback and came up with no resize arrows at all. A plain setter ran on every write and
    /// hid that; the property system does not.</summary>
    [Test]
    public void ASplitterAtItsDEFAULTOrientation_AlreadyHasTheArrows()
    {
        var untouched = new PaneSplitter();
        var turned = new PaneSplitter { Orientation = Orientation.Vertical };

        Assert.Multiple(() =>
        {
            Assert.That(untouched.Cursor, Is.Not.Null, "the default orientation is a state too, not an absence of one");
            Assert.That(untouched.Cursor, Is.Not.EqualTo(turned.Cursor), "...and it is the one for the OTHER axis");
        });
    }
}
