using Adamantium.Mathematics;
using Adamantium.UI.Controls.Docking;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// The compass decides where a dropped pane goes, and it decides it by arithmetic over a rectangle - so it is tested
/// as arithmetic. This is the calculation both the preview and the drop ask, which is the reason it is one function
/// rather than a rule in the renderer and a matching rule in the gesture.
/// </summary>
[TestFixture]
public class DockCompassTests
{
    private const double Size = 34;
    private const double Gap = 6;

    private static readonly Rect Target = new(100, 200, 400, 300);

    private static Vector2 Centre => new(Target.X + Target.Width / 2, Target.Y + Target.Height / 2);

    [Test]
    public void TheCentreIndicator_MeansJoinTheseTabs()
    {
        Assert.That(DockCompass.ZoneAt(Target, Centre, Size, Gap), Is.EqualTo(DockZone.Center));
    }

    [Test]
    public void EachSideIndicator_SitsOneStepOutAlongItsOwnAxis()
    {
        var step = Size + Gap;

        Assert.Multiple(() =>
        {
            Assert.That(DockCompass.ZoneAt(Target, new Vector2(Centre.X - step, Centre.Y), Size, Gap), Is.EqualTo(DockZone.Left));
            Assert.That(DockCompass.ZoneAt(Target, new Vector2(Centre.X + step, Centre.Y), Size, Gap), Is.EqualTo(DockZone.Right));
            Assert.That(DockCompass.ZoneAt(Target, new Vector2(Centre.X, Centre.Y - step), Size, Gap), Is.EqualTo(DockZone.Top));
            Assert.That(DockCompass.ZoneAt(Target, new Vector2(Centre.X, Centre.Y + step), Size, Gap), Is.EqualTo(DockZone.Bottom));
        });
    }

    /// <summary>Between and around the indicators there is NOTHING. A drop there must do nothing at all - the whole
    /// point of aiming at an indicator is that the areas which do something have visible edges.</summary>
    [Test]
    public void AwayFromEveryIndicator_ThereIsNoTarget()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DockCompass.ZoneAt(Target, new Vector2(Target.X + 4, Target.Y + 4), Size, Gap), Is.EqualTo(DockZone.None),
                "a corner of the group is not a target");
            Assert.That(DockCompass.ZoneAt(Target, new Vector2(Centre.X + Size / 2 + Gap / 2, Centre.Y), Size, Gap),
                Is.EqualTo(DockZone.None), "the gap between two indicators is not a target");
        });
    }

    [Test]
    public void ASidePreview_TakesHalfTheGroupOnThatSide()
    {
        Assert.Multiple(() =>
        {
            var right = DockCompass.PreviewOf(Target, DockZone.Right);
            Assert.That(right.X, Is.EqualTo(300).Within(1e-9));
            Assert.That(right.Width, Is.EqualTo(200).Within(1e-9));
            Assert.That(right.Height, Is.EqualTo(300).Within(1e-9), "a left/right split spans the full height");

            var bottom = DockCompass.PreviewOf(Target, DockZone.Bottom);
            Assert.That(bottom.Y, Is.EqualTo(350).Within(1e-9));
            Assert.That(bottom.Height, Is.EqualTo(150).Within(1e-9));
        });
    }

    // The compass covers the whole docking area; the group it aims at is a rectangle INSIDE that. So it is laid out big
    // and aimed at a part of itself - the right-hand group of a two-group area.
    private static DockCompass AimedAt(Rect group, DockZone armed)
    {
        var compass = new DockCompass { IndicatorSize = Size, IndicatorGap = Gap };
        compass.AimAt(group, armed);

        compass.Measure(new Size(800, 300));
        compass.Arrange(new Rect(0, 0, 800, 300));
        return compass;
    }

    /// <summary>The plate covers the part of the GROUP a drop would take - in the compass's own coordinates, since it
    /// spans the whole area. Drawn and hit are one arithmetic (<see cref="DockCompass.PreviewOf"/>), so the plate must
    /// land exactly where that function says.</summary>
    [Test]
    public void ThePlate_CoversWhatTheDropWouldTake_WithinTheGroup()
    {
        var group = new Rect(400, 0, 400, 300);   // the right-hand group of the area
        var plate = AimedAt(group, DockZone.Bottom).Children[0];
        var expected = DockCompass.PreviewOf(group, DockZone.Bottom);

        Assert.Multiple(() =>
        {
            Assert.That(plate.Bounds.X, Is.EqualTo(expected.X).Within(0.5), "the plate stays inside the group, not the whole area");
            Assert.That(plate.Bounds.Width, Is.EqualTo(expected.Width).Within(0.5));
            Assert.That(plate.Bounds.Y, Is.EqualTo(expected.Y).Within(0.5), "a bottom drop takes the lower half of THAT group");
            Assert.That(plate.Bounds.Height, Is.EqualTo(expected.Height).Within(0.5));
        });
    }

    /// <summary>The cross sits at the centre of the group aimed at - the same centre <see cref="DockCompass.ZoneAt"/>
    /// measures from. Drawn and hit have to be one arrangement, or the indicator the pointer lights up is not the one it
    /// is over.</summary>
    [Test]
    public void TheCross_SitsAtTheCentreOfTheGroupAimedAt()
    {
        var group = new Rect(400, 0, 400, 300);
        var centre = AimedAt(group, DockZone.Center).Children[1];   // [preview, Center, Left, Top, Right, Bottom]

        Assert.Multiple(() =>
        {
            Assert.That(centre.Bounds.X + centre.Bounds.Width / 2, Is.EqualTo(600).Within(0.5));
            Assert.That(centre.Bounds.Y + centre.Bounds.Height / 2, Is.EqualTo(150).Within(0.5));
        });
    }

    /// <summary>Joining the tabs does not carve the group up, so the preview is the whole of it.</summary>
    [Test]
    public void TheCentrePreview_IsTheWholeGroup()
    {
        var preview = DockCompass.PreviewOf(Target, DockZone.Center);

        Assert.Multiple(() =>
        {
            Assert.That(preview.X, Is.EqualTo(Target.X).Within(1e-9));
            Assert.That(preview.Width, Is.EqualTo(Target.Width).Within(1e-9));
            Assert.That(preview.Height, Is.EqualTo(Target.Height).Within(1e-9));
        });
    }
}
