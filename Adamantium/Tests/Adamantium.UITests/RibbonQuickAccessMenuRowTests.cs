using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.Resources.Triggers;
using NUnit.Framework;

namespace Adamantium.UITests;

// The row of a command's context menu says the OPPOSITE thing once the command is already in the bar. The words live in
// the theme: a plain setter states "add", a trigger on the row's own state states "remove".
[TestFixture]
public class RibbonQuickAccessMenuRowTests
{
    private const string Add = "Add to quick access";
    private const string Remove = "Remove from quick access";

    // How the theme states it: the default in ONE style, the exception in ANOTHER - two blocks, one aspect each.
    private static Style Selecting()
    {
        var style = new Style();
        style.Selector.Types.Add(typeof(RibbonQuickAccessMenuItem));
        return style;
    }

    private static RibbonQuickAccessMenuItem Themed()
    {
        var row = new RibbonQuickAccessMenuItem();

        var words = Selecting();
        words.Setters.Add(new Setter(nameof(MenuItem.Header), Add));
        words.Attach(row);

        var whenIn = Selecting();
        var trigger = new PropertyTrigger
        {
            Property = nameof(RibbonQuickAccessMenuItem.IsInQuickAccess),
            Value = true
        };
        trigger.Add(new Setter(nameof(MenuItem.Header), Remove));
        whenIn.Triggers.Add(trigger);
        whenIn.Attach(row);

        return row;
    }

    [Test]
    public void ARowForACommandNotInTheBarSaysAdd()
    {
        var row = Themed();

        Assert.That(row.Header, Is.EqualTo(Add));
    }

    [Test]
    public void ARowForACommandAlreadyInTheBarSaysRemove()
    {
        var row = Themed();

        row.SetValue(RibbonQuickAccessMenuItem.IsInQuickAccessProperty, true);

        Assert.That(row.Header, Is.EqualTo(Remove));
    }
}
