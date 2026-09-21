using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using Adamantium.UI.Themes.MacOsTheme;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// The pager's row is meant to be built from cells that never go under a floor, so that a one-digit page and an
/// ellipsis occupy the same box and nothing beside them shifts as the reader pages. On the stand they kept hugging
/// their content instead, and the two possible reasons look identical from the outside: either the floor never reaches
/// the button, or layout does not honour it once it is there.
/// <para>This fixture separates them. The floor is written DIRECTLY here - no style, no resource - so a failure can only
/// be the layout pass. Whether a style setter delivers the value is a different question, asked below.</para>
/// </summary>
[TestFixture]
public class PagerButtonFloorTests
{
    private static Size Measure(MeasurableUIComponent element)
    {
        var root = new Border { Width = 400, Height = 100, Child = element };
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(root);
        return element.DesiredSize;
    }

    /// <summary>A button narrower than its floor is widened to it. If this fails, no amount of moving the value between
    /// a template and a style setter will ever make the row even.</summary>
    [Test]
    public void AButtonNarrowerThanItsFloor_IsWidenedToIt()
    {
        var button = new RadioButton { Content = "1", MinWidth = 30, Height = 28, Padding = new Thickness(0) };

        var desired = Measure(button);

        TestContext.WriteLine($"desired={desired} MinWidth={button.MinWidth} ActualWidth={button.ActualWidth}");
        Assert.That(desired.Width, Is.GreaterThanOrEqualTo(30));
    }

    /// <summary>...and one wider than its floor keeps its own width - the floor is a minimum, not a size.</summary>
    [Test]
    public void AButtonWiderThanItsFloor_KeepsItsOwnWidth()
    {
        var narrow = new RadioButton { Content = "1", MinWidth = 30, Height = 28, Padding = new Thickness(0) };
        var wide = new RadioButton { Content = "10000", MinWidth = 30, Height = 28, Padding = new Thickness(0) };

        var narrowSize = Measure(narrow);
        var wideSize = Measure(wide);

        TestContext.WriteLine($"narrow={narrowSize} wide={wideSize}");
        Assert.That(wideSize.Width, Is.GreaterThanOrEqualTo(narrowSize.Width));
    }

    /// <summary>The same for the arrows, which are plain buttons holding a glyph.</summary>
    [Test]
    public void AnArrowNarrowerThanItsFloor_IsWidenedToIt()
    {
        var button = new Button { MinWidth = 30, Height = 28, Padding = new Thickness(0) };

        var desired = Measure(button);

        TestContext.WriteLine($"desired={desired}");
        Assert.That(desired.Width, Is.GreaterThanOrEqualTo(30));
    }

    /// <summary>THE OTHER HALF: does a style SETTER deliver a plain number onto the control at all? Written as a literal
    /// so no resource lookup is involved - if the value does not arrive even from a literal, the setter path is the
    /// suspect; if it does, the suspect is what the setter's value was resolved FROM.</summary>
    [Test]
    public void AStyleSetter_DeliversAFloorOntoTheButton()
    {
        var button = new RadioButton { Content = "1" };
        button.ClassNames.Add("PagerPage");

        var set = new MacOsRadioButtonStyleSet();
        set.Initialize(null);
        foreach (var style in set.Styles) style.Attach(button);

        Measure(button);

        TestContext.WriteLine($"MinWidth={button.MinWidth} Height={button.Height} Padding={button.Padding} " +
                              $"desired={button.DesiredSize}");
        Assert.That(button.Padding, Is.EqualTo(new Thickness(0)),
            "the literal setter in the same block - if this arrives, the setter path itself works");
    }

    /// <summary>THE DEFECT THIS FIXTURE WAS WRITTEN FOR, and it is not the pager's. A control that reports its CONTENT
    /// size rather than the slot - a radio, a check box, a switch, whose glyph cannot stretch - was dropping its own
    /// Width/Height/Min* on the way, so it desired one size in measure and then drew at another. Anything restyled onto
    /// such a control (a radio wearing a toggle template, which is what a pager's page buttons are) collapsed back onto
    /// its text however wide the theme said it should be.
    /// <para>Desired was always right, which is why this took a probe in the running app to see: the wrong number is the
    /// ARRANGED one.</para></summary>
    [Test]
    public void AControlThatReportsItsContentSize_StillKeepsItsOwnFloor()
    {
        var radio = new RadioButton { Content = "1", MinWidth = 30, Height = 28, Padding = new Thickness(0) };

        Measure(radio);

        TestContext.WriteLine($"desired={radio.DesiredSize} actual={radio.ActualWidth}x{radio.ActualHeight}");
        Assert.Multiple(() =>
        {
            Assert.That(radio.ActualWidth, Is.GreaterThanOrEqualTo(30), "the floor it was given");
            Assert.That(radio.ActualHeight, Is.EqualTo(28), "and the height it was given");
        });
    }

    /// <summary>...and one with no size of its own still shrinks to its content, which is what these controls report the
    /// content size FOR. The fix must not turn them into stretching buttons.</summary>
    [Test]
    public void AControlWithNoSizeOfItsOwn_StillShrinksToItsContent()
    {
        var radio = new RadioButton { Content = "1" };

        var root = new Border { Width = 400, Height = 100, Child = radio };
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(root);

        TestContext.WriteLine($"desired={radio.DesiredSize} actual={radio.ActualWidth}x{radio.ActualHeight}");
        Assert.That(radio.ActualWidth, Is.LessThan(400), "it must not have grown into the slot it was handed");
    }
}
