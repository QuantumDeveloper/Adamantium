using Adamantium.UI.Core;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// Two trigger setters fighting over ONE property of ONE element are resolved by how LOCAL the rule is first, and by
/// where it stands in the markup second.
/// <para>A CheckBox is BasedOn a ToggleButton, and the ToggleButton paints its label with the colour that belongs on an
/// accent-filled button. The CheckBox says otherwise - its accent fills the box, not the row - but both setters were
/// numbered within their own style, so the winner came down to which trigger happened to sit lower in ITS file. The
/// checkbox label came out white on a light panel.</para>
/// </summary>
[TestFixture]
public class TriggerStyleBandTests
{
    private static Setter Contribution(int band, int declarationOrder) =>
        new("Foreground", null) { StyleBand = band, DeclarationOrder = declarationOrder };

    [Test]
    public void TheMoreDerivedStyleWins_EvenWhenTheBaseIsDeclaredLower()
    {
        var container = new TriggerValueContainer();
        var baseSetter = Contribution(band: 0, declarationOrder: 9000);   // last trigger of the BASE style
        var derived = Contribution(band: 1, declarationOrder: 1000);      // first trigger of the DERIVED style

        container.Set(baseSetter, "base");
        container.Set(derived, "derived");

        Assert.That(container.EffectiveValue, Is.EqualTo("derived"),
            "the local rule wins: a derived style is not outranked by the base it is built on");
    }

    [Test]
    public void OrderOfApplicationDoesNotDecideIt()
    {
        var container = new TriggerValueContainer();
        var derived = Contribution(band: 1, declarationOrder: 1000);
        var baseSetter = Contribution(band: 0, declarationOrder: 9000);

        container.Set(derived, "derived");
        container.Set(baseSetter, "base");   // applied AFTER, and still loses

        Assert.That(container.EffectiveValue, Is.EqualTo("derived"));
    }

    [Test]
    public void WithinOneStyle_TheOneWrittenLowerStillWins()
    {
        var container = new TriggerValueContainer();
        container.Set(Contribution(band: 1, declarationOrder: 1000), "upper");
        container.Set(Contribution(band: 1, declarationOrder: 2000), "lower");

        Assert.That(container.EffectiveValue, Is.EqualTo("lower"),
            "banding must not disturb the rule inside a single style");
    }

    [Test]
    public void AnEmptyStack_FallsThroughBelowTrigger()
    {
        Assert.That(new TriggerValueContainer().EffectiveValue, Is.EqualTo(AdamantiumProperty.UnsetValue));
    }
}
