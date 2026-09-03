using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;
using Adamantium.UI.Themes.MacOsTheme;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>The chosen span of a range slider runs from one handle's CENTRE to the other's - under both of them.
/// <para>It used to run BETWEEN them, edge to edge, which measures as touching and looks like falling short: a handle
/// is round, so where its box ends the circle has narrowed to a point and the band ends against nothing. Rounding the
/// placement onto whole units (the vertical demo landed on halves, the horizontal one on wholes) made the edge crisp
/// and did not close it - what settled it was that a handle's shadow is translucent, so a band running underneath would
/// show through it, and none did.</para>
/// </summary>
[TestFixture]
public class MacOsRangeBandTests
{
    /// <summary>The numbers the THEME supplies, stated here because this fixture stands the style set up without one:
    /// every <c>{ResourceReference}</c> in it resolves to nothing, so the band would keep the control's own default
    /// thickness and the control would keep no minimum size at all - which is not the geometry on screen, and measuring
    /// it proves nothing about the geometry that is.</summary>
    private const double ThemeBandThickness = 4;   // SliderTrackThickness
    private const double ThemeCrossSize = 28;      // ControlHeight

    /// <summary>The control AS THE SANDBOX BUILDS IT (Views/RangesView.auml), not an approximation of it. Two earlier
    /// versions of this fixture measured a slider of their own invention and reported "no gap" while the screen plainly
    /// had one - the vertical demo also sets MinimumRangeWidth, which goes straight into the band's arithmetic, and its
    /// own BandThickness, which outranks the theme's.</summary>
    private static (Rect Band, Rect Lower, Rect Upper, Rect Track) Place(Orientation orientation,
        double minimumRange = 0, double bandThickness = ThemeBandThickness)
    {
        var slider = new RangeSlider
        {
            Orientation = orientation,
            Minimum = 0, Maximum = 100, LowerValue = 25, UpperValue = 75,
            MinimumRangeWidth = minimumRange,
            BandThickness = bandThickness
        };
        if (orientation == Orientation.Vertical)
        {
            slider.Height = 150;
            slider.MinWidth = ThemeCrossSize;
        }
        else
        {
            slider.Width = 200;
            slider.MinHeight = ThemeCrossSize;
        }

        var set = new MacOsRangeSliderStyleSet();
        set.Initialize(null);
        foreach (var style in set.Styles) style.Attach(slider);

        var root = new Border { Width = 400, Height = 400, Child = slider };
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(root);
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(root);

        var track = (RangeTrack)slider.GetTemplateChild("PART_Track");
        return (((IUIComponent)track.CenterThumb).Bounds,
                ((IUIComponent)track.LowerThumb).Bounds,
                ((IUIComponent)track.UpperThumb).Bounds,
                ((IUIComponent)track).Bounds);
    }

    private static double CentreY(Rect r) => r.Y + r.Height / 2;
    private static double CentreX(Rect r) => r.X + r.Width / 2;

    [Test]
    public void TheBandReachesBothThumbCentres_Horizontally()
    {
        var (band, lower, upper, track) = Place(Orientation.Horizontal);
        TestContext.WriteLine($"track={track} band={band} lower={lower} upper={upper}");

        Assert.Multiple(() =>
        {
            Assert.That(band.X, Is.EqualTo(CentreX(lower)).Within(0.51), "the band starts at the lower thumb's centre");
            Assert.That(band.Right, Is.EqualTo(CentreX(upper)).Within(0.51), "...and ends at the upper one's");
        });
    }

    /// <summary>The sandbox's own vertical range slider, number for number - the one the gap was seen on.</summary>
    [Test]
    public void TheBandReachesBothThumbCentres_Vertically_AsTheSandboxBuildsIt()
    {
        var (band, lower, upper, track) = Place(Orientation.Vertical, minimumRange: 16, bandThickness: 8);
        TestContext.WriteLine($"track={track} band={band} lower={lower} upper={upper}");
        TestContext.WriteLine($"band reaches {CentreY(upper) - band.Y} past the upper centre, " +
                              $"{band.Bottom - CentreY(lower)} past the lower one");

        Assert.Multiple(() =>
        {
            Assert.That(band.Y, Is.EqualTo(CentreY(upper)).Within(0.51), "the band starts at the upper thumb's centre");
            Assert.That(band.Bottom, Is.EqualTo(CentreY(lower)).Within(0.51), "...and ends at the lower one's");
        });
    }

    [Test]
    public void TheBandReachesBothThumbCentres_Vertically()
    {
        var (band, lower, upper, track) = Place(Orientation.Vertical);
        TestContext.WriteLine($"track={track} band={band} lower={lower} upper={upper}");

        // Reversed: the UPPER value is at the top, so the band runs from the upper thumb's centre down to the lower
        // thumb's.
        Assert.Multiple(() =>
        {
            Assert.That(band.Y, Is.EqualTo(CentreY(upper)).Within(0.51), "the band starts at the upper thumb's centre");
            Assert.That(band.Bottom, Is.EqualTo(CentreY(lower)).Within(0.51), "...and ends at the lower one's");
        });
    }

    /// <summary>A press aimed at an end handle must grab THAT handle, though the band now runs underneath it.
    /// <para>Asked of the decision itself, not of a stand-in for it. The first version of this test checked the order
    /// the parts sit in the track's Children and called that "the thumbs are on top" - which is true of PAINTING and
    /// says nothing about the press: the slider picks the handle by span, in its own order, and while the three could
    /// not overlap that order did not matter. It does now, and asking the children list would have gone on passing
    /// while every press on a handle dragged the whole span.</para></summary>
    [TestCase(Orientation.Horizontal)]
    [TestCase(Orientation.Vertical)]
    public void APressOnAnEndHandle_GrabsThatHandle_NotTheBand(Orientation orientation)
    {
        var slider = new RangeSlider
        {
            Orientation = orientation,
            Minimum = 0, Maximum = 100, LowerValue = 25, UpperValue = 75
        };
        if (orientation == Orientation.Vertical) { slider.Height = 150; slider.MinWidth = ThemeCrossSize; }
        else { slider.Width = 200; slider.MinHeight = ThemeCrossSize; }

        var set = new MacOsRangeSliderStyleSet();
        set.Initialize(null);
        foreach (var style in set.Styles) style.Attach(slider);

        var root = new Border { Width = 400, Height = 400, Child = slider };
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(root);
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(root);

        var track = (RangeTrack)slider.GetTemplateChild("PART_Track");
        var vertical = orientation == Orientation.Vertical;
        var lowerBounds = ((IUIComponent)track.LowerThumb).Bounds;
        var upperBounds = ((IUIComponent)track.UpperThumb).Bounds;
        var atLower = vertical ? CentreY(lowerBounds) : CentreX(lowerBounds);
        var atUpper = vertical ? CentreY(upperBounds) : CentreX(upperBounds);

        var grabbedAtLower = slider.CoveringHandle(atLower, vertical);
        var grabbedAtUpper = slider.CoveringHandle(atUpper, vertical);
        TestContext.WriteLine($"at the lower centre ({atLower}): " +
                              $"{(ReferenceEquals(grabbedAtLower, track.CenterThumb) ? "band" : "thumb")}; " +
                              $"at the upper centre ({atUpper}): " +
                              $"{(ReferenceEquals(grabbedAtUpper, track.CenterThumb) ? "band" : "thumb")}");

        Assert.Multiple(() =>
        {
            Assert.That(grabbedAtLower, Is.SameAs(track.LowerThumb), "a press on the lower handle must grab it");
            Assert.That(grabbedAtUpper, Is.SameAs(track.UpperThumb), "...and on the upper one, that one");
        });
    }

    /// <summary>...and the band is still grabbable where it is the only thing there - halfway between the handles.
    /// Stated because the fix is an ORDER, and an order put right for one case can be put wrong for the other.</summary>
    [Test]
    public void APressBetweenTheHandles_GrabsTheBand()
    {
        var slider = new RangeSlider
        {
            Orientation = Orientation.Horizontal, Width = 200, MinHeight = ThemeCrossSize,
            Minimum = 0, Maximum = 100, LowerValue = 25, UpperValue = 75
        };
        var set = new MacOsRangeSliderStyleSet();
        set.Initialize(null);
        foreach (var style in set.Styles) style.Attach(slider);

        var root = new Border { Width = 400, Height = 400, Child = slider };
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(root);
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(root);

        var track = (RangeTrack)slider.GetTemplateChild("PART_Track");
        var band = ((IUIComponent)track.CenterThumb).Bounds;
        Assert.That(slider.CoveringHandle(CentreX(band), vertical: false), Is.SameAs(track.CenterThumb));
    }
}
