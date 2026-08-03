using System.Globalization;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Core.Templates;
using Adamantium.UI.Extensions;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// The value side of the NumericUpDown: what a step does, what the box says the value is, and where both stop. Built on
/// a hand-made template - the theme decides where the buttons SIT, which is a layout question and not one of these.
/// </summary>
[TestFixture]
public class NumericUpDownTests
{
    private static ControlTemplate Template() => new(() =>
    {
        var grid = new Grid();
        var text = new TextBox();
        var increase = new RepeatButton();
        var decrease = new RepeatButton();
        grid.Children.Add(text);
        grid.Children.Add(increase);
        grid.Children.Add(decrease);

        var result = new TemplateResult { RootComponent = grid };
        result.RegisterName("PART_TextBox", text);
        result.RegisterName("PART_Increase", increase);
        result.RegisterName("PART_Decrease", decrease);
        return result;
    });

    // Through the real layout pass, like the Slider tests: that is what applies the template and hands the control its
    // parts - there is no shortcut that also wires them.
    private static NumericUpDown NewBox(double value = 5, double min = 0, double max = 10)
    {
        var box = new NumericUpDown { Minimum = min, Maximum = max, Value = value, SmallChange = 1 };
        box.Template = Template();
        var root = new Border { Width = 200, Height = 32, Child = box };
        for (var i = 0; i < 5; i++) WindowExtension.UpdateTree(root);
        return box;
    }

    private static TextBox TextOf(NumericUpDown box) => (TextBox)box.GetTemplateChild("PART_TextBox");

    private static string Selected(TextBox text) => text.Text.Substring(text.SelectionStart, text.SelectionLength);

    // A press on a RepeatButton is what raises Click; raising it directly is the same thing minus the mouse.
    private static void Press(NumericUpDown box, string part)
    {
        var button = (RepeatButton)box.GetTemplateChild(part);
        button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button) { RoutedEvent = ButtonBase.ClickEvent });
    }

    [Test]
    public void AStepMovesTheValueBySmallChange()
    {
        var box = NewBox();

        Press(box, "PART_Increase");
        Assert.That(box.Value, Is.EqualTo(6));

        Press(box, "PART_Decrease");
        Press(box, "PART_Decrease");
        Assert.That(box.Value, Is.EqualTo(4));
    }

    [Test]
    public void TheValueStopsAtTheLimits()
    {
        var box = NewBox(value: 9);
        var reachedMaximum = 0;
        box.MaximumReached += (_, _) => reachedMaximum++;

        Press(box, "PART_Increase");
        Press(box, "PART_Increase");
        Press(box, "PART_Increase");

        Assert.Multiple(() =>
        {
            Assert.That(box.Value, Is.EqualTo(10), "the ceiling holds");
            Assert.That(reachedMaximum, Is.GreaterThan(0), "and says so");
        });
    }

    /// <summary>A button that cannot move the value any further is disabled, so a click that would do nothing is not
    /// offered in the first place.</summary>
    [Test]
    public void AButtonAtItsLimitIsDisabled()
    {
        var box = NewBox(value: 10);

        Assert.Multiple(() =>
        {
            Assert.That(((RepeatButton)box.GetTemplateChild("PART_Increase")).IsEnabled, Is.False);
            Assert.That(((RepeatButton)box.GetTemplateChild("PART_Decrease")).IsEnabled, Is.True);
        });
    }

    [Test]
    public void TheBoxShowsTheValueInTheGivenFormat()
    {
        var box = NewBox(value: 2.5, max: 100);
        box.Culture = CultureInfo.InvariantCulture;
        box.StringFormat = "N2";

        Assert.That(TextOf(box).Text, Is.EqualTo("2.50"));

        box.Value = 7;
        Assert.That(TextOf(box).Text, Is.EqualTo("7.00"), "and follows it");
    }

    /// <summary>A composite format is the other shape MahApps accepts, so a unit can be carried along with the number.</summary>
    [Test]
    public void ACompositeFormatIsAccepted()
    {
        var box = NewBox(value: 3, max: 100);
        box.Culture = CultureInfo.InvariantCulture;
        box.StringFormat = "{0:N1} kg";

        Assert.That(TextOf(box).Text, Is.EqualTo("3.0 kg"));
    }

    [Test]
    public void SnappingRoundsToWholeSteps()
    {
        var box = NewBox(value: 0.1, max: 10);
        box.SmallChange = 0.25;
        box.SnapsToSmallChange = true;

        Press(box, "PART_Increase");

        Assert.That(box.Value, Is.EqualTo(0.25).Within(1e-9), "0.1 + 0.25 lands on the nearest quarter, not on 0.35");
    }

    [Test]
    public void AReadOnlyBoxIgnoresItsButtons()
    {
        var box = NewBox();
        box.IsReadOnly = true;

        Press(box, "PART_Increase");

        Assert.That(box.Value, Is.EqualTo(5));
    }

    /// <summary>Unbounded by default: a spinner that could only count from zero to one would be useless, and every
    /// limit here is opt-in - the same as MahApps.</summary>
    [Test]
    public void ItIsUnboundedUntilToldOtherwise()
    {
        var box = new NumericUpDown();

        Assert.Multiple(() =>
        {
            Assert.That(box.Minimum, Is.EqualTo(double.MinValue));
            Assert.That(box.Maximum, Is.EqualTo(double.MaxValue));
        });
    }

    /// <summary>An empty box is EMPTY, not a box holding zero - which is why the value is nullable and why this control
    /// sits on the limits rather than on RangeBase. Zero would be a lie about a field nobody has filled in.</summary>
    [Test]
    public void NoValueIsAStateOfItsOwn()
    {
        var box = NewBox();

        box.Value = null;

        Assert.Multiple(() =>
        {
            Assert.That(box.Value, Is.Null);
            Assert.That(TextOf(box).Text, Is.Empty, "and the box shows nothing, not a zero");
        });
    }

    [Test]
    public void SteppingAnEmptyBoxStartsFromInsideTheRange()
    {
        var box = NewBox(min: 5, max: 20);
        box.Value = null;

        Press(box, "PART_Increase");

        Assert.That(box.Value, Is.EqualTo(6), "zero is below this range, so the step starts at its floor");
    }

    /// <summary>Double-click takes the word, triple takes the lot - the compensation for a box whose drag is spoken for
    /// by the value scrub. Driven through the editor's own entry points, since a mouse is not available here.</summary>
    [Test]
    public void RepeatClicksSelectWordThenEverything()
    {
        var box = NewBox(max: 100);
        var text = TextOf(box);
        text.Text = "12 34";

        text.SelectWordAt(0);
        Assert.That(Selected(text), Is.EqualTo("12"), "a double-click takes the word under it");

        text.SelectWordAt(4);
        Assert.That(Selected(text), Is.EqualTo("34"), "and the word it actually landed on, not the one before it");

        text.SelectAll();
        Assert.That(Selected(text), Is.EqualTo("12 34"), "a triple-click takes everything");
    }

    [Test]
    public void BothButtonsWorkWhileTheBoxIsEmpty()
    {
        var box = NewBox();
        box.Value = null;

        Assert.Multiple(() =>
        {
            Assert.That(((RepeatButton)box.GetTemplateChild("PART_Increase")).IsEnabled, Is.True);
            Assert.That(((RepeatButton)box.GetTemplateChild("PART_Decrease")).IsEnabled, Is.True);
        });
    }
}
