using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Primitives;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// What a two-bound control must never allow, and what it must survive. No template here - these are the value rules,
/// which is exactly where the control differs from a pair of Sliders.
/// </summary>
[TestFixture]
public class RangeSliderTests
{
    private static RangeSlider NewSlider() => new() { Minimum = 0, Maximum = 100, LowerValue = 20, UpperValue = 70 };

    [Test]
    public void TheBoundsCannotCross()
    {
        var slider = NewSlider();

        slider.LowerValue = 90;

        Assert.That(slider.LowerValue, Is.EqualTo(70), "the start cannot pass the end");
        Assert.That(slider.UpperValue, Is.EqualTo(70), "and the end stays where it was");
    }

    [Test]
    public void MinimumRangeKeepsTheSpanFromCollapsing()
    {
        var slider = NewSlider();
        slider.MinimumRange = 10;

        slider.LowerValue = 68;

        Assert.That(slider.LowerValue, Is.EqualTo(60), "kept ten units clear of the end");
    }

    /// <summary>
    /// Bindings arrive one at a time. A start that has to be squeezed because the end is still at its default must come
    /// back once the end arrives - otherwise a control bound to 20..70 settles on something else entirely, which is what
    /// it used to do (it read 1..70, because 20 was clamped against the default upper bound of 1 and then forgotten).
    /// </summary>
    [Test]
    public void AValueSqueezedByTheOtherBoundIsRestoredWhenThereIsRoom()
    {
        var slider = new RangeSlider();   // defaults: 0..1, so 20 does not fit yet

        slider.Maximum = 100;
        slider.LowerValue = 20;           // squeezed to 1 by the still-default UpperValue
        slider.UpperValue = 70;

        Assert.That(slider.LowerValue, Is.EqualTo(20));
        Assert.That(slider.UpperValue, Is.EqualTo(70));
    }

    [Test]
    public void MovingTheBoundsPullsTheSelectionInside()
    {
        var slider = NewSlider();

        slider.Maximum = 50;

        Assert.That(slider.UpperValue, Is.EqualTo(50), "the end follows the new ceiling");
        Assert.That(slider.LowerValue, Is.EqualTo(20), "the start was already inside");
    }

    [Test]
    public void RangeChangedFiresForEitherBound()
    {
        var slider = NewSlider();
        var fired = 0;
        slider.RangeChanged += (_, _) => fired++;

        slider.LowerValue = 30;
        slider.UpperValue = 60;

        Assert.That(fired, Is.EqualTo(2));
    }

    /// <summary>The whole point of splitting the base: bounds live one level up, so both kinds of range control share
    /// them and a Slider still gets its single Value.</summary>
    [Test]
    public void BothKindsOfRangeControlShareTheSameBounds()
    {
        Assert.That(typeof(RangeSlider), Is.InstanceOf<object>());
        Assert.That(typeof(RangeBoundsBase).IsAssignableFrom(typeof(RangeSlider)), Is.True);
        Assert.That(typeof(RangeBoundsBase).IsAssignableFrom(typeof(RangeBase)), Is.True);
        Assert.That(typeof(RangeBase).IsAssignableFrom(typeof(Slider)), Is.True);
        Assert.That(typeof(RangeBase).IsAssignableFrom(typeof(RingProgressBar)), Is.True);
    }
}
