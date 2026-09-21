using Adamantium.Mathematics;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using Adamantium.UI.Rendering;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>A frozen layout snapshot is published as a DELTA, and the applier reads any entry in it as "the layout moved
/// under the recorded stream" and refuses to replay that stream. So the equality below decides two things at once: a real
/// move must never be swallowed (that is the stale-clip flicker), and an unchanged re-freeze must never be published (that
/// refused the replay on every frame, and cost the 60k view its retained path - 28 fps instead of ~320).</summary>
[TestFixture]
public class LayoutSnapshotTests
{
    private static readonly Border Parent = new();

    private static LayoutSnapshot Snapshot(Matrix4x4F transform, Size size, double opacity = 1)
        => new(transform, size, false, false, Parent, (float)opacity);

    [Test]
    public void TheSameStateFrozenTwiceIsEqual()
    {
        var a = Snapshot(Matrix4x4F.Translation(10, 20, 0), new Size(100, 50));
        var b = Snapshot(Matrix4x4F.Translation(10, 20, 0), new Size(100, 50));

        Assert.That(a.Equals(b), Is.True);
    }

    // The matrix's own == compares with a TOLERANCE. Used here it would report a small move as no move at all: the entry
    // would not be published, the applier would go on composing from the older transform, and successive sub-tolerance
    // moves would drift with nothing ever announcing them.
    [Test]
    public void AMoveSmallerThanTheMatrixToleranceStillCounts()
    {
        // A tenth of the matrix's own tolerance (1e-6), and exactly representable - so the two really do differ.
        var still = Matrix4x4F.Translation(0, 20, 0);
        var nudged = Matrix4x4F.Translation(1e-7f, 20, 0);

        Assert.That(still.M41, Is.Not.EqualTo(nudged.M41), "the two matrices must genuinely differ, or this proves nothing");
        Assert.That(still == nudged, Is.True, "the matrix itself calls these equal - which is why this must not be used");
        Assert.That(Snapshot(still, new Size(100, 50)).Equals(Snapshot(nudged, new Size(100, 50))), Is.False,
            "a move must be published however small - the applier composes from what the delta carries, and what it never hears about, it never applies");
    }

    [Test]
    public void EveryFieldIsPartOfTheAnswer()
    {
        var baseline = Snapshot(Matrix4x4F.Identity, new Size(100, 50));

        Assert.That(baseline.Equals(Snapshot(Matrix4x4F.Translation(1, 0, 0), new Size(100, 50))), Is.False, "transform");
        Assert.That(baseline.Equals(Snapshot(Matrix4x4F.Identity, new Size(101, 50))), Is.False, "width");
        Assert.That(baseline.Equals(Snapshot(Matrix4x4F.Identity, new Size(100, 51))), Is.False, "height");
        Assert.That(baseline.Equals(Snapshot(Matrix4x4F.Identity, new Size(100, 50), 0.5)), Is.False, "opacity");
        Assert.That(baseline.Equals(new LayoutSnapshot(Matrix4x4F.Identity, new Size(100, 50), true, false, Parent)), Is.False, "clip");
        Assert.That(baseline.Equals(new LayoutSnapshot(Matrix4x4F.Identity, new Size(100, 50), false, true, Parent)), Is.False, "motion node");
        Assert.That(baseline.Equals(new LayoutSnapshot(Matrix4x4F.Identity, new Size(100, 50), false, false, new Border())), Is.False, "parent");
        Assert.That(baseline.Equals(new LayoutSnapshot(Matrix4x4F.Identity, new Size(100, 50), false, false, Parent, 1f, 0.5f)), Is.False, "self opacity");
    }
}
