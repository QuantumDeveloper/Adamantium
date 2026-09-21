using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// What <c>x:Shared="False"</c> buys, at the end the user sees it: two elements under ONE style each get their own
/// value. Without it a setter hands out the same reference, so anything that belongs to the element it sits on - a
/// ContextMenu (one PlacementTarget), a Popup, a Transform - could not be stated in a theme at all.
/// </summary>
[TestFixture]
public class PerTargetSetterValueTests
{
    private static (Button first, Button second) TwoButtonsUnder(Setter setter)
    {
        var style = new Style();
        style.Selector.Types.Add(typeof(Button));
        style.Setters.Add(setter);

        var first = new Button();
        var second = new Button();
        style.Attach(first);
        style.Attach(second);

        return (first, second);
    }

    // The control arm: this is the behaviour x:Shared="False" exists to change, and it has to keep working - sharing is
    // right for a brush and stays the default.
    [Test]
    public void APlainSetterValueIsSharedBetweenTargets()
    {
        var shared = new ContextMenu();

        var (first, second) = TwoButtonsUnder(new Setter("ContextMenu", shared));

        Assert.That(first.ContextMenu, Is.SameAs(shared));
        Assert.That(second.ContextMenu, Is.SameAs(shared));
    }

    [Test]
    public void APerTargetSetterValueGivesEachTargetItsOwn()
    {
        var setter = new Setter("ContextMenu", new PerTargetValue(() => new ContextMenu()));

        var (first, second) = TwoButtonsUnder(setter);

        Assert.That(first.ContextMenu, Is.Not.Null, "the factory has to be called, not stored as the value");
        Assert.That(second.ContextMenu, Is.Not.Null);
        Assert.That(first.ContextMenu, Is.Not.SameAs(second.ContextMenu),
            "a menu belongs to the element it sits on - sharing one is the bug this fixes");
    }
}
