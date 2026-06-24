using Adamantium.Mathematics;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

// Pure-CPU (no GPU) regression tests for the scrolling primitives added this session: RangeBase value coercion,
// the Track's thumb sizing/positioning + drag mapping, and ScrollBar's orientation thickness.
[TestFixture]
public class ScrollControlsTests
{
    private sealed class TestRange : RangeBase { }

    // ---- RangeBase: Value is always clamped into [Minimum, Maximum] ----

    [Test]
    public void Value_ClampsToMaximum()
    {
        var r = new TestRange { Minimum = 0, Maximum = 10, Value = 25 };
        Assert.That(r.Value, Is.EqualTo(10));
    }

    [Test]
    public void Value_ClampsToMinimum()
    {
        var r = new TestRange { Minimum = 0, Maximum = 10, Value = -5 };
        Assert.That(r.Value, Is.EqualTo(0));
    }

    [Test]
    public void LoweringMaximum_RecoercesValue()
    {
        var r = new TestRange { Minimum = 0, Maximum = 10, Value = 8 };
        r.Maximum = 5;
        Assert.That(r.Value, Is.EqualTo(5), "Value must drop to the new Maximum");
    }

    [Test]
    public void RaisingMinimum_RecoercesValue()
    {
        var r = new TestRange { Minimum = 0, Maximum = 10, Value = 3 };
        r.Minimum = 6;
        Assert.That(r.Value, Is.EqualTo(6), "Value must rise to the new Minimum");
    }

    [Test]
    public void ValueChanged_FiresWithOldAndNew()
    {
        var r = new TestRange { Minimum = 0, Maximum = 10, Value = 0 };
        double oldV = -1, newV = -1;
        var count = 0;
        r.ValueChanged += (_, e) => { oldV = e.OldValue; newV = e.NewValue; count++; };

        r.Value = 7;

        Assert.Multiple(() =>
        {
            Assert.That(count, Is.EqualTo(1));
            Assert.That(oldV, Is.EqualTo(0));
            Assert.That(newV, Is.EqualTo(7));
        });
    }

    // ---- Track: thumb sizing/positioning + drag-to-value mapping ----

    private static Track ArrangedTrack(Orientation o, double min, double max, double value, double viewport,
        double width, double height)
    {
        var track = new Track { Orientation = o, Minimum = min, Maximum = max, ViewportSize = viewport, Value = value };
        track.Measure(new Size(width, height));
        track.Arrange(new Rect(0, 0, width, height));
        return track;
    }

    [Test]
    public void Track_Vertical_ThumbOffsetTracksValue()
    {
        // No viewport -> minimum thumb (12) on a 200px track; travel = 188.
        var top = ArrangedTrack(Orientation.Vertical, 0, 100, 0, 0, 12, 200);
        var mid = ArrangedTrack(Orientation.Vertical, 0, 100, 50, 0, 12, 200);
        var bottom = ArrangedTrack(Orientation.Vertical, 0, 100, 100, 0, 12, 200);

        Assert.Multiple(() =>
        {
            Assert.That(top.Thumb.Bounds.Y, Is.EqualTo(0).Within(0.5));
            Assert.That(mid.Thumb.Bounds.Y, Is.EqualTo(94).Within(0.5));
            Assert.That(bottom.Thumb.Bounds.Y, Is.EqualTo(188).Within(0.5));
            Assert.That(top.Thumb.Bounds.Height, Is.EqualTo(12).Within(0.5), "no viewport -> minimum thumb length");
        });
    }

    [Test]
    public void Track_Viewport_SizesThumbProportionally()
    {
        // Viewport == range -> the thumb fills half the track (viewport / (range + viewport) = 100/200).
        var track = ArrangedTrack(Orientation.Vertical, 0, 100, 0, 100, 12, 200);
        Assert.That(track.Thumb.Bounds.Height, Is.EqualTo(100).Within(0.5));
    }

    [Test]
    public void Track_ValueFromDistance_MapsPixelsToValue()
    {
        var track = ArrangedTrack(Orientation.Vertical, 0, 100, 0, 0, 12, 200);
        Assert.Multiple(() =>
        {
            // 188px of thumb travel spans the whole 0..100 range.
            Assert.That(track.ValueFromDistance(0, 188), Is.EqualTo(100).Within(0.5));
            Assert.That(track.ValueFromDistance(0, 94), Is.EqualTo(50).Within(0.5));
            Assert.That(track.ValueFromDistance(99, 0), Is.EqualTo(0), "vertical track ignores horizontal delta");
        });
    }

    [Test]
    public void Track_Horizontal_UsesXAxis()
    {
        var track = ArrangedTrack(Orientation.Horizontal, 0, 100, 50, 0, 200, 12);
        Assert.Multiple(() =>
        {
            Assert.That(track.Thumb.Bounds.X, Is.EqualTo(94).Within(0.5));
            Assert.That(track.Thumb.Bounds.Width, Is.EqualTo(12).Within(0.5));
            Assert.That(track.ValueFromDistance(94, 0), Is.EqualTo(50).Within(0.5));
        });
    }

    // ---- ScrollBar ----

    [Test]
    public void ScrollBar_Orientation_SetsThickness()
    {
        var vertical = new ScrollBar { Orientation = Orientation.Vertical };
        var horizontal = new ScrollBar { Orientation = Orientation.Horizontal };

        Assert.Multiple(() =>
        {
            Assert.That(vertical.Width, Is.EqualTo(12), "vertical scrollbar fixes its width");
            Assert.That(double.IsNaN(vertical.Height), Is.True, "vertical scrollbar stretches in height");
            Assert.That(horizontal.Height, Is.EqualTo(12), "horizontal scrollbar fixes its height");
            Assert.That(double.IsNaN(horizontal.Width), Is.True, "horizontal scrollbar stretches in width");
        });
    }

    [Test]
    public void ScrollBar_IsRangeBase_CoercesValue()
    {
        var bar = new ScrollBar { Minimum = 0, Maximum = 50, Value = 999 };
        Assert.That(bar.Value, Is.EqualTo(50));
    }
}
