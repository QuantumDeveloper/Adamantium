using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core.Input;
using NUnit.Framework;

namespace Adamantium.UITests;

// A menu row is an ItemsControl, not a button - it may hold a submenu - so it inherits none of the button's key
// handling. Without its own, a menu the keyboard had walked into could be looked at but never used.
[TestFixture]
public class MenuItemKeyboardTests
{
    private static KeyEventArgs Press(Key key) =>
        new(KeyboardDevice.CurrentDevice, key, InputModifiers.None, 0) { RoutedEvent = Keyboard.KeyDownEvent };

    [Test]
    public void EnterRunsALeafRow()
    {
        var item = new MenuItem { Header = "Paste" };
        var clicked = 0;
        item.Click += (_, _) => clicked++;

        item.RaiseEvent(Press(Key.Enter));

        Assert.That(clicked, Is.EqualTo(1));
    }

    [Test]
    public void SpaceRunsItToo()
    {
        var item = new MenuItem { Header = "Paste" };
        var clicked = 0;
        item.Click += (_, _) => clicked++;

        item.RaiseEvent(Press(Key.Space));

        Assert.That(clicked, Is.EqualTo(1));
    }

    [Test]
    public void EnterOnAParentRowOpensItsSubmenuInstead()
    {
        var parent = new MenuItem { Header = "Recent" };
        parent.Items.Add(new MenuItem { Header = "city.scene" });
        var clicked = 0;
        parent.Click += (_, _) => clicked++;

        parent.RaiseEvent(Press(Key.Enter));

        Assert.Multiple(() =>
        {
            Assert.That(parent.IsSubmenuOpen, Is.True);
            Assert.That(clicked, Is.Zero, "a parent row is not a command - choosing it opens what it holds");
        });
    }

    [Test]
    public void ADisabledRowDoesNothing()
    {
        var item = new MenuItem { Header = "Paste", IsEnabled = false };
        var clicked = 0;
        item.Click += (_, _) => clicked++;

        item.RaiseEvent(Press(Key.Enter));

        Assert.That(clicked, Is.Zero);
    }
}
