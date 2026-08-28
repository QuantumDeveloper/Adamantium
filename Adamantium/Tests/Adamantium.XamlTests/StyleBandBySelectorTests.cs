using Adamantium.Core.DependencyInjection;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.Resources.Triggers;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// How LOCAL a style's rules are is decided by the TYPE ITS SELECTOR NAMES, not by where the style landed in a
/// BasedOn chain.
/// <para>A control's rules are spread over several style blocks - one concern each, per the small-styles convention -
/// while the base it is built on arrives through the ONE block that says <c>BasedOn</c>. Banding by position in the
/// collected chain therefore ranked a BASE style ABOVE a derived one whose block declared no BasedOn of its own, and a
/// trigger written to un-inherit a base rule lives in exactly such a block: ToggleButton contributes three style blocks,
/// so its checked-label rule sat on band 2 while the ToggleSwitch rule meant to overrule it sat on band 0. On screen the
/// label of a checked switch, checkbox and radio button came out white on a light panel.</para>
/// </summary>
[TestFixture]
public class StyleBandBySelectorTests
{
    [OneTimeSetUp]
    public void EnsureAppContext()
    {
        // A Theme reaches through UIAppContext.Current for its resource manager; a minimal context is enough.
        UIAppContext.Initialize(new FakeApp(new AdamantiumDependencyContainer()), null);
    }

    private static Style TriggerStyle(System.Type selects, string value, StyleSelector basedOn = null)
    {
        var setter = new Setter("Foreground", value);
        var trigger = new PropertyTrigger { Property = "IsChecked", Value = "true" };
        trigger.Add(setter);

        var style = new Style { Selector = new StyleSelector { Types = { selects } }, BasedOn = basedOn };
        style.Triggers.Add(trigger);
        return style;
    }

    private static Setter OnlySetterOf(Style style) => (Setter)style.Triggers[0].Setters[0];

    private static StyleSelector On(System.Type type) => new() { Types = { type } };

    [Test]
    public void AStyleOnTheControlsOwnType_OutranksOneOnTheTypeItDerivesFrom()
    {
        var theme = new Theme("bands");
        var set = new StyleSet();

        // The base contributes SEVERAL blocks, exactly as ToggleButton does - that is what used to hand it a high band.
        var basePadding = TriggerStyle(typeof(ToggleButton), "base-1");
        var baseChrome = TriggerStyle(typeof(ToggleButton), "base-2");
        var baseLabel = TriggerStyle(typeof(ToggleButton), "base-label");

        // The derived control opts into the base look in ONE block...
        var derivedBrushes = TriggerStyle(typeof(ToggleSwitch), "derived-brushes", basedOn: On(typeof(ToggleButton)));
        // ...and un-inherits the base's label rule from ANOTHER, which says no BasedOn of its own.
        var derivedLabel = TriggerStyle(typeof(ToggleSwitch), "derived-label");

        foreach (var style in new[] { basePadding, baseChrome, baseLabel, derivedBrushes, derivedLabel }) set.Add(style);
        theme.AddStyleSet(set);

        var control = new ToggleSwitch();
        derivedBrushes.Attach(control);
        derivedLabel.Attach(control);

        Assert.That(OnlySetterOf(derivedLabel).StyleBand, Is.GreaterThan(OnlySetterOf(baseLabel).StyleBand),
            "the rule written on the control's own type has to outrank the one it derives from, whichever of the " +
            "control's blocks happens to carry it");
    }

    [Test]
    public void EveryBlockOfOneControl_SharesOneBand()
    {
        var theme = new Theme("bands");
        var set = new StyleSet();

        var withBasedOn = TriggerStyle(typeof(ToggleSwitch), "a", basedOn: On(typeof(ToggleButton)));
        var withoutBasedOn = TriggerStyle(typeof(ToggleSwitch), "b");
        var baseStyle = TriggerStyle(typeof(ToggleButton), "base");

        foreach (var style in new[] { baseStyle, withBasedOn, withoutBasedOn }) set.Add(style);
        theme.AddStyleSet(set);

        var control = new ToggleSwitch();
        withBasedOn.Attach(control);
        withoutBasedOn.Attach(control);

        // Declaring BasedOn is how a control opts into a LOOK; it must not also change how its own rules rank against
        // each other, or which block a rule is written in would silently decide who wins.
        Assert.That(OnlySetterOf(withoutBasedOn).StyleBand, Is.EqualTo(OnlySetterOf(withBasedOn).StyleBand));
    }
}
