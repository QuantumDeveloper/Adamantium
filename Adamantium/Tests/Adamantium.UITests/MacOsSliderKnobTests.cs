using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Themes.MacOsTheme;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>The slider knob glows while it is dragged, and the glow is switched by a trigger that addresses the Aura
/// BY NAME. That only works if a named NON-VISUAL object inside a template is registered in the template's name scope -
/// which is an assumption about the engine, not about the theme, so it is measured here rather than believed.
/// <para>The first attempt handed the trigger a finished Aura through <c>Setter.Value</c>. That object belongs to no
/// tree, so its <c>{ResourceReference}</c> had no scope to resolve against and it kept Aura's default colour - white -
/// and only came into being at the moment of the drag, so it also arrived late.</para>
/// </summary>
[TestFixture]
public class MacOsSliderKnobTests
{
    private static Thumb Knob()
    {
        var thumb = new Thumb { Width = 20, Height = 20 };
        thumb.ClassNames.Add("SliderKnob");

        var set = new MacOsSliderKnobStyleSet();
        set.Initialize(null);
        foreach (var style in set.Styles) style.Attach(thumb);

        var root = new Border { Width = 200, Height = 100, Child = thumb };
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(root);
        return thumb;
    }

    [Test]
    public void TheTemplateRegistersBothTheKnobAndItsAura()
    {
        var thumb = Knob();

        var knob = thumb.GetTemplateChild("Knob");
        var aura = thumb.GetTemplateChild("KnobAura");

        TestContext.WriteLine($"Knob     -> {knob?.GetType().Name ?? "<null>"}");
        TestContext.WriteLine($"KnobAura -> {aura?.GetType().Name ?? "<null>"}");
        if (knob is Border border)
        {
            TestContext.WriteLine($"border.Aura   = {border.Aura?.GetType().Name ?? "<null>"}");
            TestContext.WriteLine($"border.Shadow = {border.Shadow?.GetType().Name ?? "<null>"}");
            // The colour is printed for the record and is NOT asserted: this fixture stands up the style set with no
            // theme, so there is no dictionary for the colour keys to resolve against and white here means nothing.
            // What the colour resolves to is a question for the running app.
            if (border.Aura != null)
                TestContext.WriteLine($"aura: enabled={border.Aura.IsEnabled} colour={border.Aura.Color} (no theme) " +
                                      $"spread={border.Aura.Spread} radius={border.Aura.Radius}");
        }

        Assert.Multiple(() =>
        {
            Assert.That(knob, Is.TypeOf<Border>(), "the visual part is found by name, as always");
            Assert.That(aura, Is.TypeOf<Aura>(),
                "and so is the Aura - without this the IsDragging setter has nothing to address and does nothing, silently");
        });
    }

    /// <summary>The glow is OFF at rest and ON while dragging. Being FOUND by name and being WRITTEN to are two
    /// different claims, and only the first was measured at first - so this drives the state itself.
    /// <para><c>IsDragging</c> is read-only, which stops a transition being attached to it and nothing else:
    /// <c>SetValue</c> writes it, so the state a real drag produces can be produced here.</para>
    /// </summary>
    [Test]
    public void TheGlowFollowsTheDrag()
    {
        var thumb = Knob();
        var aura = (Aura)thumb.GetTemplateChild("KnobAura");

        TestContext.WriteLine($"at rest:  IsDragging={thumb.IsDragging} aura.IsEnabled={aura.IsEnabled}");
        Assert.That(aura.IsEnabled, Is.False, "a resting knob does not glow");

        var knob = (Border)thumb.GetTemplateChild("Knob");
        var atRest = knob.Background;

        thumb.SetValue(Thumb.IsDraggingProperty, true);
        TestContext.WriteLine($"dragging: IsDragging={thumb.IsDragging} aura.IsEnabled={aura.IsEnabled} " +
                              $"fillChanged={!ReferenceEquals(knob.Background, atRest)}");
        Assert.That(aura.IsEnabled, Is.True, "the trigger reaches the Aura by name and switches it on");

        thumb.SetValue(Thumb.IsDraggingProperty, false);
        TestContext.WriteLine($"released: IsDragging={thumb.IsDragging} aura.IsEnabled={aura.IsEnabled}");
        Assert.That(aura.IsEnabled, Is.False, "and lets go of it again");
        Assert.That(knob.Background, Is.SameAs(atRest), "and the knob's fill goes back with it");
    }
}
