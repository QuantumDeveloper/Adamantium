using Adamantium.UI.Controls.Primitives;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// A menu row follows its command, the way a button does. Before this it only asked the command at CLICK time, so a
/// row whose command said no looked perfectly ordinary and simply did nothing when picked - the one outcome a menu
/// must never have, because the person is left with no idea whether they missed or the application ignored them.
/// </summary>
[TestFixture]
public class MenuItemCommandTests
{
    [Test]
    public void ARowIsDisabledWhileItsCommandSaysNo()
    {
        var command = new SwitchableCommand();
        var item = new MenuItem { Command = command };

        Assert.That(item.IsEnabled, Is.False, "the command says no from the start");

        command.CanRun = true;
        Assert.That(item.IsEnabled, Is.True, "and the row follows it without being touched");

        command.CanRun = false;
        Assert.That(item.IsEnabled, Is.False, "both ways");
    }

    [Test]
    public void ChangingTheCommand_DropsTheOldOne()
    {
        var first = new SwitchableCommand { CanRun = true };
        var second = new SwitchableCommand();
        var item = new MenuItem { Command = first };

        item.Command = second;

        Assert.That(item.IsEnabled, Is.False, "the row answers to the command it has now");

        first.CanRun = false;   // the old one must no longer be able to move it
        second.CanRun = true;

        Assert.That(item.IsEnabled, Is.True, "and only to that one");
    }

    /// <summary>A PARENT row opens a submenu and runs nothing, so a command left on it must not grey it out - which
    /// would make a whole branch unreachable.</summary>
    [Test]
    public void AParentRowIsNotDisabledByACommand()
    {
        var item = new MenuItem { Command = new SwitchableCommand() };
        item.Items.Add(new MenuItem());

        Assert.That(item.IsEnabled, Is.True);
    }
}
