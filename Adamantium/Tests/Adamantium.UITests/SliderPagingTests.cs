using Adamantium.UI.Controls;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// Clicking a slider's track pages toward the click - and must stop THERE, not sail past it. The page button cannot
/// notice on its own: the pointer does not move, the AREA moves out from under it, and enter/leave are raised from
/// pointer movement, so the button never hears that it was left and repeats all the way to the end. The scrollbar was
/// fixed the same way; the slider was left behind, and it has one complication the scrollbar does not - TICKS.
/// <para>These test the decision, not the pointer: reading the mouse is the one part that cannot be handed a value, so
/// the limit is passed in exactly as Track.PageLimitFromPoint would return it (that mapping has its own tests).</para>
/// </summary>
[TestFixture]
public class SliderPagingTests
{
    private static Slider Plain() => new() { Minimum = 0, Maximum = 100, LargeChange = 25 };

    private static Slider Snapping(double frequency) => new()
    {
        Minimum = 0, Maximum = 100, LargeChange = 25, IsSnapToTickEnabled = true, TickFrequency = frequency
    };

    /// <summary>The whole point: a step that would overshoot is cut short at the cursor.</summary>
    [Test]
    public void APageStepStopsAtTheCursorInsteadOfOvershooting()
    {
        var slider = Plain();

        Assert.Multiple(() =>
        {
            Assert.That(slider.PageTarget(stepped: 25, limit: 12, increasing: true), Is.EqualTo(12),
                "stepping right past the cursor must stop on it");
            Assert.That(slider.PageTarget(stepped: -25, limit: -12, increasing: false), Is.EqualTo(-12),
                "and stepping left, likewise");
        });
    }

    /// <summary>A step that falls SHORT of the cursor is left alone - the limit is a ceiling, not a destination, or a
    /// single click would jump the whole way like move-to-point does.</summary>
    [Test]
    public void AStepThatFallsShortIsNotStretchedToTheCursor()
    {
        var slider = Plain();

        Assert.Multiple(() =>
        {
            Assert.That(slider.PageTarget(stepped: 25, limit: 80, increasing: true), Is.EqualTo(25));
            Assert.That(slider.PageTarget(stepped: -25, limit: -80, increasing: false), Is.EqualTo(-25));
        });
    }

    /// <summary>The complication the scrollbar never had. Snapping rounds to the NEAREST tick, and half the time the
    /// nearest one lies PAST the cursor - which would undo the very limit that stopped there. The tick has to be taken
    /// on the near side.</summary>
    [Test]
    public void SnappingDoesNotRoundBackOverTheCursor()
    {
        var slider = Snapping(10);

        Assert.Multiple(() =>
        {
            // 47 rounds to 50 - past the cursor. It must land on 40.
            Assert.That(slider.PageTarget(stepped: 100, limit: 47, increasing: true), Is.EqualTo(40),
                "the nearest tick was past the cursor; the one before it is the answer");
            // 53 rounds to 50 - past the cursor going the other way. It must land on 60.
            Assert.That(slider.PageTarget(stepped: 0, limit: 53, increasing: false), Is.EqualTo(60),
                "and going left, the tick after it");
        });
    }

    /// <summary>An ordinary step - one the cursor never cut short - keeps rounding to the NEAREST tick. The directional
    /// rule is for the clamped case only; applying it everywhere would quietly turn every page step into a floor.</summary>
    [Test]
    public void AnUnclampedStepStillRoundsToTheNearestTick()
    {
        var slider = Snapping(10);

        Assert.That(slider.PageTarget(stepped: 27, limit: 90, increasing: true), Is.EqualTo(30),
            "27 is nearest 30, and nothing here says otherwise");
    }

    /// <summary>Exactly on the cursor is not past it: the limit must not be treated as an overshoot and re-snapped.</summary>
    [Test]
    public void LandingExactlyOnTheCursorIsNotAnOvershoot()
    {
        var slider = Snapping(10);

        Assert.That(slider.PageTarget(stepped: 40, limit: 40, increasing: true), Is.EqualTo(40));
    }
}
