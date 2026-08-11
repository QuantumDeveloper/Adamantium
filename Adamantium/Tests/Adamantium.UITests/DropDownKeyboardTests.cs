using Adamantium.UI.Controls;
using Adamantium.UI.Core.Input;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>What a drop-down does with the keyboard while it is CLOSED. In this engine the arrows are how the keyboard
/// moves between controls, so a closed list that answered one would both open itself unasked and swallow the key -
/// trapping the walk on it.</summary>
[TestFixture]
public class DropDownKeyboardTests
{
    private static KeyEventArgs Press(Key key, object on) =>
        new(KeyboardDevice.CurrentDevice, key, InputModifiers.None, 0)
        {
            RoutedEvent = Keyboard.KeyDownEvent,
            OriginalSource = on
        };

    private static DropDown Closed()
    {
        var drop = new DropDown();
        drop.Items.Add(new DropDownItem { Content = "One" });
        drop.Items.Add(new DropDownItem { Content = "Two" });
        return drop;
    }

    [Test]
    public void AnArrowPassesOverItUntouched()
    {
        var drop = Closed();

        var down = Press(Key.DownArrow, drop);
        drop.RaiseEvent(down);

        Assert.Multiple(() =>
        {
            Assert.That(drop.IsDropDownOpen, Is.False, "walking past a list is not asking for it");
            Assert.That(down.Handled, Is.False, "and the key has to reach the navigation");
        });
    }

    [Test]
    public void EnterOpensIt()
    {
        var drop = Closed();

        drop.RaiseEvent(Press(Key.Enter, drop));

        Assert.That(drop.IsDropDownOpen, Is.True);
    }

    [Test]
    public void SpaceOpensItToo()
    {
        var drop = Closed();

        drop.RaiseEvent(Press(Key.Space, drop));

        Assert.That(drop.IsDropDownOpen, Is.True);
    }

    // Once OPEN the arrows are the list's own: they walk the rows, and nothing else should see them.
    [Test]
    public void OpenTheArrowsWalkTheRows()
    {
        var drop = Closed();
        drop.RaiseEvent(Press(Key.Enter, drop));

        var down = Press(Key.DownArrow, drop);
        drop.RaiseEvent(down);

        Assert.Multiple(() =>
        {
            Assert.That(drop.IsDropDownOpen, Is.True);
            Assert.That(down.Handled, Is.True);
        });
    }

    [Test]
    public void EscapePutsItAway()
    {
        var drop = Closed();
        drop.RaiseEvent(Press(Key.Enter, drop));

        drop.RaiseEvent(Press(Key.Escape, drop));

        Assert.That(drop.IsDropDownOpen, Is.False);
    }
}
