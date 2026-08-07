using Adamantium.UI.Controls.Buttons;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// A button follows its command's availability - it greys itself out while the command says no, and lights up again
/// when the answer changes, without anything touching it. The mechanism lives in <c>ButtonBase</c>, so every control of
/// the family inherits it (Button, RepeatButton, ToggleButton and through it CheckBox / RadioButton / ToggleSwitch).
/// It was working and untested, which for the one behaviour that decides whether a control can be used at all is not a
/// state to leave it in.
/// </summary>
[TestFixture]
public class ButtonCommandTests
{
    [Test]
    public void AButtonIsDisabledWhileItsCommandSaysNo()
    {
        var command = new SwitchableCommand();
        var button = new Button { Command = command };

        Assert.That(button.IsEnabled, Is.False, "the command says no from the start");

        command.CanRun = true;
        Assert.That(button.IsEnabled, Is.True, "and the button follows it without being touched");

        command.CanRun = false;
        Assert.That(button.IsEnabled, Is.False, "both ways");
    }

    [Test]
    public void ChangingTheCommand_DropsTheOldOne()
    {
        var first = new SwitchableCommand { CanRun = true };
        var second = new SwitchableCommand();
        var button = new Button { Command = first };

        button.Command = second;
        Assert.That(button.IsEnabled, Is.False, "the button answers to the command it has now");

        first.CanRun = false;   // the old one must no longer be able to move it
        second.CanRun = true;

        Assert.That(button.IsEnabled, Is.True, "and only to that one");
    }

    /// <summary>The parameter is part of the question: a command that answers per-argument has to be re-asked when the
    /// argument changes, not only when the command does.</summary>
    [Test]
    public void ChangingTheParameter_ReAsksTheCommand()
    {
        var command = new SwitchableCommand { CanRun = true };
        var button = new Button { Command = command, CommandParameter = "a" };

        command.CanRun = false;
        button.CommandParameter = "b";

        Assert.That(button.IsEnabled, Is.False);
    }

    /// <summary>A disabled button does not run its command - the click path checks the same answer the look shows, so
    /// the two cannot disagree.</summary>
    [Test]
    public void ADisabledButtonDoesNotRun()
    {
        var command = new SwitchableCommand();
        var button = new Button { Command = command };

        button.PerformClick();
        Assert.That(command.Runs, Is.Zero);

        command.CanRun = true;
        button.PerformClick();
        Assert.That(command.Runs, Is.EqualTo(1));
    }
}
