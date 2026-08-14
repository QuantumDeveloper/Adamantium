using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Imaging;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>A VisualBrush is the one brush whose content cannot be replayed - a live subtree has layout and state of
/// its own - so it paints from a PICTURE that is re-taken when the source changes. What these pin is the bookkeeping
/// around that picture, because getting it wrong costs a render target per frame rather than a wrong colour.</summary>
[TestFixture]
public class VisualBrushTests
{
    private static BitmapSource Picture(uint size = 4) =>
        new(size, size, 1, 1, SurfaceFormat.B8G8R8A8.UNorm, new byte[size * size * 4]);

    [Test]
    public void ItHasNothingToPaintWithUntilItsSourceHasBeenDrawn()
    {
        var brush = new VisualBrush(new Border());

        Assert.That(brush.ContentSource, Is.Null, "no picture yet - the fill must draw nothing, not guess");
        Assert.That(brush.ContentSize, Is.EqualTo(default(Size)));
        Assert.That(brush.NeedsBake, Is.True);
    }

    [Test]
    public void TheDeliveredPictureBecomesItsContent()
    {
        var brush = new VisualBrush(new Border());
        var picture = Picture(8);

        brush.Deliver(picture);

        Assert.That(brush.ContentSource, Is.SameAs(picture));
        Assert.That(brush.ContentSize, Is.EqualTo(new Size(8, 8)), "the picture's own pixels are the content size");
    }

    // The render path reads a SNAPSHOT, and a fresh one is published on every property change. If the picture belonged
    // to the snapshot, each change would start from nothing and draw the source off-screen again - a render target per
    // knob, which is what took the app down when DrawingBrush did it.
    [Test]
    public void EveryFrozenCloneSharesTheOnePictureAndItsState()
    {
        var brush = new VisualBrush(new Border());
        brush.Deliver(Picture());
        brush.ForRendering();

        brush.TileMode = TileMode.FlipXY;
        brush.Stretch = Stretch.Uniform;
        var frozen = (VisualBrush)brush.Snapshot;

        Assert.That(frozen, Is.Not.SameAs(brush), "a snapshot is a clone, or this proves nothing");
        Assert.That(frozen.ContentSource, Is.SameAs(brush.ContentSource));
        Assert.That(frozen.Origin, Is.SameAs(brush), "and the state that survives a freeze is the original's");
    }

    // A clone must not be able to answer "already baked" on its own, or the source would be re-drawn per snapshot.
    [Test]
    public void MarkingACloneStaleMarksTheOriginal()
    {
        var brush = new VisualBrush(new Border());
        brush.Deliver(Picture());
        brush.NeedsBake = false;
        brush.ForRendering();

        var frozen = (VisualBrush)brush.Snapshot;
        frozen.Refresh();

        Assert.That(brush.NeedsBake, Is.True);
    }

    // A binding re-pushes the same source on every refresh of the brush's expressions - on attach, and again when the
    // DataContext arrives. Treating that as a change cost an off-screen render, with its own render target, per push.
    [Test]
    public void ReassigningTheSameSourceAsksForNothing()
    {
        var source = new Border();
        var brush = new VisualBrush(source);
        brush.Deliver(Picture());
        brush.NeedsBake = false;

        brush.Visual = source;

        Assert.That(brush.NeedsBake, Is.False, "the same element - there is nothing new to draw");
    }

    // The loop that lost the device: delivering a picture publishes a snapshot, the snapshot is a CLONE, and the clone
    // being handed the source reads as null -> source. Marking the original for that re-baked on every publish, for ever -
    // and a bake replaces the picture, so the texture the in-flight frame was sampling was destroyed under it.
    [Test]
    public void PublishingASnapshotDoesNotAskForANewPicture()
    {
        var brush = new VisualBrush(new Border());
        brush.Deliver(Picture());
        brush.NeedsBake = false;
        brush.ForRendering();

        _ = brush.Snapshot;

        Assert.That(brush.NeedsBake, Is.False, "a clone copying the source is not the source changing");
    }

    [Test]
    public void SwappingTheSourceAsksForANewPicture()
    {
        var brush = new VisualBrush(new Border());
        brush.Deliver(Picture());
        brush.NeedsBake = false;

        brush.Visual = new Border();

        Assert.That(brush.NeedsBake, Is.True);
    }

    [Test]
    public void TheFrozenSnapshotCarriesTheTilingAndTheSource()
    {
        var source = new Border();
        var brush = new VisualBrush(source)
        {
            TileMode = TileMode.FlipX,
            Stretch = Stretch.UniformToFill,
            AlignmentX = AlignmentX.Left,
            Viewport = new Rect(0, 0, 0.5, 0.5),
            RotationAngle = 30,
            Tint = Colors.Lime,
            Opacity = 0.25
        };

        brush.ForRendering();
        var frozen = (VisualBrush)brush.Snapshot;

        Assert.That(frozen.Visual, Is.SameAs(source));
        Assert.That(frozen.TileMode, Is.EqualTo(TileMode.FlipX));
        Assert.That(frozen.Stretch, Is.EqualTo(Stretch.UniformToFill));
        Assert.That(frozen.AlignmentX, Is.EqualTo(AlignmentX.Left));
        Assert.That(frozen.Viewport, Is.EqualTo(new Rect(0, 0, 0.5, 0.5)));
        Assert.That(frozen.RotationAngle, Is.EqualTo(30));
        Assert.That(frozen.Tint, Is.EqualTo(Colors.Lime));
        Assert.That(frozen.Opacity, Is.EqualTo(0.25));
    }
}
