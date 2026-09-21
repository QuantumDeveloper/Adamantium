using Adamantium.Mathematics;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// A trigger that paints a part through <c>{ThemeResource}</c> / <c>{ObservableResource}</c> must take its colour back
/// with it when it leaves - even while ANOTHER trigger still owns the same part property.
/// <para>The symptom this was written for: tick a checkbox and untick it, and the box keeps the accent fill in every
/// theme. The tick itself (a plain Opacity setter) cleared correctly, which is what pointed at the marker path rather
/// than at the trigger machinery: two accent triggers share <c>Box.Background</c> - checked, and checked+pressed - and
/// the first to leave found the second still registered on the slot, refreshed IT, and returned without clearing its
/// own contribution. That value then had no owner and nothing ever took it off the stack.</para>
/// </summary>
[TestFixture]
public class TriggerResourceRemovalTests
{
    private static readonly SolidColorBrush Checked = new(Colors.RoyalBlue);
    private static readonly SolidColorBrush Pressed = new(Colors.DarkBlue);

    // The order is the one a real click produces: the pressed trigger goes first (the button comes up), then the checked
    // one (the state flips). Removing them the other way round never showed the defect, which is why it survived.
    [Test]
    public void AThemeResourceTriggerTakesItsColourWithIt()
    {
        var part = new Border();
        var property = part.GetProperty(nameof(Border.Background));
        object checkedToken = new(), pressedToken = new();
        var resource = new ThemeResource("AccentFillColorDefault");

        resource.Apply(part, nameof(Border.Background), ValuePriority.Trigger, checkedToken);
        part.SetTriggerValue(property, Checked, checkedToken);
        resource.Apply(part, nameof(Border.Background), ValuePriority.Trigger, pressedToken);
        part.SetTriggerValue(property, Pressed, pressedToken);
        Assert.That(part.Background, Is.SameAs(Pressed), "the last trigger to apply is the one on top");

        ThemeResource.Remove(part, nameof(Border.Background), ValuePriority.Trigger, pressedToken);
        Assert.That(part.Background, Is.SameAs(Checked),
            "the pressed trigger left, so the checked one underneath it shows again");

        ThemeResource.Remove(part, nameof(Border.Background), ValuePriority.Trigger, checkedToken);
        // Not "is null": an un-triggered Border carries its own default brush. What matters is that NEITHER trigger's
        // colour is still on the part.
        Assert.That(part.Background, Is.Not.SameAs(Checked).And.Not.SameAs(Pressed),
            "both triggers have left - nothing of theirs may stay on the part");
    }

    // The same seam, the same shape, the same defect: the label's Foreground is an {ObservableResource} in the very
    // triggers this bug was found in.
    [Test]
    public void AnObservableResourceTriggerTakesItsColourWithIt()
    {
        var part = new Border();
        var property = part.GetProperty(nameof(Border.Background));
        object checkedToken = new(), pressedToken = new();
        var resource = new ObservableResource("TextFillColorPrimary");

        resource.Apply(part, nameof(Border.Background), ValuePriority.Trigger, checkedToken);
        part.SetTriggerValue(property, Checked, checkedToken);
        resource.Apply(part, nameof(Border.Background), ValuePriority.Trigger, pressedToken);
        part.SetTriggerValue(property, Pressed, pressedToken);

        ObservableResource.Remove(part, nameof(Border.Background), ValuePriority.Trigger, pressedToken);
        Assert.That(part.Background, Is.SameAs(Checked),
            "the pressed trigger left, so the checked one underneath it shows again");

        ObservableResource.Remove(part, nameof(Border.Background), ValuePriority.Trigger, checkedToken);
        // Not "is null": an un-triggered Border carries its own default brush. What matters is that NEITHER trigger's
        // colour is still on the part.
        Assert.That(part.Background, Is.Not.SameAs(Checked).And.Not.SameAs(Pressed),
            "both triggers have left - nothing of theirs may stay on the part");
    }
}
