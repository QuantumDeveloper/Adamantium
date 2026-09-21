using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Base;
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
        // The Track is template-driven now: parts come from the consuming template, so provide a thumb the way a
        // template would (a bare one - DesiredSize 0,0 - so slider-mode sizing clamps to MinThumbLength, as before).
        var track = new Track
        {
            Orientation = o, Minimum = min, Maximum = max, ViewportSize = viewport, Value = value, Thumb = new Thumb()
        };
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

    // ---- Track.ValueFromPoint: click position -> value (move-to-point slider) ----

    [Test]
    public void Track_ValueFromPoint_MapsClickToValue()
    {
        // Horizontal, 200px track, 12px thumb -> 188px travel; the thumb is CENTRED on the click. Click at x=100 (track
        // middle) -> along = 100 - 6 = 94 -> 94/188 * 100 = 50. Ends clamp to Minimum/Maximum.
        var track = ArrangedTrack(Orientation.Horizontal, 0, 100, 0, 0, 200, 12);
        Assert.Multiple(() =>
        {
            Assert.That(track.ValueFromPoint(new Vector2(100, 6)), Is.EqualTo(50).Within(0.5));
            Assert.That(track.ValueFromPoint(new Vector2(6, 6)), Is.EqualTo(0).Within(0.5), "click at the start -> Minimum");
            Assert.That(track.ValueFromPoint(new Vector2(194, 6)), Is.EqualTo(100).Within(0.5), "click at the end -> Maximum");
            Assert.That(track.ValueFromPoint(new Vector2(-50, 6)), Is.EqualTo(0).Within(0.5), "clamped below the track");
        });
    }

    /// <summary>Paging must stop with the thumb's EDGE at the cursor, not its centre - otherwise it swallows the click
    /// point and overshoots by half a thumb, which on a long thumb is the difference between "went where I pointed" and
    /// "went past it".</summary>
    [Test]
    public void Track_PageLimitFromPoint_StopsWithTheThumbEdgeAtTheCursor()
    {
        // Horizontal, 200px track, 12px thumb -> 188px travel, so a value unit is 188/100 px and half a thumb is
        // 6px = 3.19 value units. Centred on x=100 is 50 (above), so the edges sit 3.19 either side of it.
        var track = ArrangedTrack(Orientation.Horizontal, 0, 100, 0, 0, 200, 12);
        Assert.Multiple(() =>
        {
            Assert.That(track.PageLimitFromPoint(new Vector2(100, 6), increasing: true), Is.EqualTo(46.81).Within(0.05),
                "paging right stops when the thumb's RIGHT edge reaches the cursor");
            Assert.That(track.PageLimitFromPoint(new Vector2(100, 6), increasing: false), Is.EqualTo(53.19).Within(0.05),
                "paging left stops when its LEFT edge does");
        });
    }

    /// <summary>The ENDS are the case a middle-of-the-track check cannot see. Deriving the limit by shifting the
    /// centred mapping by half a thumb looked right at x=100 and was wrong at both stops: that mapping clamps its own
    /// travel first, so the shift came off an already-clamped number and the thumb halted half a thumb short.</summary>
    [Test]
    public void Track_PageLimitFromPoint_ReachesBothStops()
    {
        var track = ArrangedTrack(Orientation.Horizontal, 0, 100, 0, 0, 200, 12);
        Assert.Multiple(() =>
        {
            Assert.That(track.PageLimitFromPoint(new Vector2(200, 6), increasing: true), Is.EqualTo(100).Within(0.05),
                "a click at the far end still pages all the way to Maximum");
            Assert.That(track.PageLimitFromPoint(new Vector2(0, 6), increasing: false), Is.EqualTo(0).Within(0.05),
                "and at the near end, all the way to Minimum");
            Assert.That(track.PageLimitFromPoint(new Vector2(400, 6), increasing: true), Is.EqualTo(100).Within(0.05),
                "past the end clamps rather than overruns");
        });
    }

    [Test]
    public void Track_ValueFromPoint_ReversedVertical_TopIsMaximum()
    {
        // A vertical slider sets IsDirectionReversed so the TOP is the maximum: a click near the top yields a high value.
        var track = new Track
        {
            Orientation = Orientation.Vertical, Minimum = 0, Maximum = 100, ViewportSize = 0,
            IsDirectionReversed = true, Thumb = new Thumb()
        };
        track.Measure(new Size(12, 200));
        track.Arrange(new Rect(0, 0, 12, 200));
        Assert.Multiple(() =>
        {
            Assert.That(track.ValueFromPoint(new Vector2(6, 6)), Is.EqualTo(100).Within(0.5), "top -> Maximum");
            Assert.That(track.ValueFromPoint(new Vector2(6, 194)), Is.EqualTo(0).Within(0.5), "bottom -> Minimum");
            Assert.That(track.ValueFromPoint(new Vector2(6, 100)), Is.EqualTo(50).Within(0.5), "middle -> midpoint");
        });
    }

    [Test]
    public void Track_NothingToScroll_ThumbFillsAndIsInert()
    {
        // range == 0 (Maximum == Minimum): nothing to scroll -> the thumb fills the whole track and a drag does nothing.
        var track = ArrangedTrack(Orientation.Vertical, 0, 0, 0, 0, 12, 200);
        Assert.Multiple(() =>
        {
            Assert.That(track.Thumb.Bounds.Height, Is.EqualTo(200).Within(0.5), "full-length thumb");
            Assert.That(track.Thumb.Bounds.Y, Is.EqualTo(0).Within(0.5));
            Assert.That(track.ValueFromDistance(0, 100), Is.EqualTo(0), "inert: a drag maps to no value change");
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

    [Test]
    public void ScrollBar_Default_HasNothingToScroll()
    {
        // A bare scrollbar isn't bound to content yet, so by default there is no scrollable range: the thumb fills
        // the trough and is inert until a Maximum/ViewportSize (or a ScrollViewer) gives it something to scroll.
        var bar = new ScrollBar();
        Assert.That(bar.Maximum, Is.EqualTo(bar.Minimum));
    }

    // ---- ScrollContentPresenter: the physical IScrollableContent (extent capture, translate, clamp) ----

    // Content with a fixed natural size, so the presenter measures a definite extent regardless of layout details.
    private sealed class FixedContent : MeasurableUIComponent
    {
        private readonly Size _size;
        public FixedContent(Size size) => _size = size;
        protected override Size MeasureOverride(Size availableSize) => _size;
    }

    private static ScrollContentPresenter ArrangedPresenter(Size content, Size viewport)
    {
        var p = new ScrollContentPresenter { Content = new FixedContent(content) };
        p.Measure(viewport);
        p.Arrange(new Rect(viewport));
        return p;
    }

    [Test]
    public void ScrollContentPresenter_ReportsContentExtentAndViewport()
    {
        var p = ArrangedPresenter(new Size(800, 1200), new Size(300, 400));
        Assert.Multiple(() =>
        {
            Assert.That(p.Extent, Is.EqualTo(new Size(800, 1200)), "extent = content's natural size (measured unbounded)");
            Assert.That(p.Viewport, Is.EqualTo(new Size(300, 400)), "viewport = the presenter's arranged size");
        });
    }

    [Test]
    public void ScrollContentPresenter_SetOffset_TranslatesChild()
    {
        var p = ArrangedPresenter(new Size(800, 1200), new Size(300, 400));
        p.SetOffset(new Vector2(100, 250));
        p.Arrange(new Rect(new Size(300, 400)));   // SetOffset invalidated the arrange; re-run it

        var child = p.VisualChildren.First();
        Assert.Multiple(() =>
        {
            Assert.That(p.Offset.X, Is.EqualTo(100));
            Assert.That(p.Offset.Y, Is.EqualTo(250));
            Assert.That(child.Bounds.X, Is.EqualTo(-100).Within(0.5), "child shifts left by the horizontal offset");
            Assert.That(child.Bounds.Y, Is.EqualTo(-250).Within(0.5), "child shifts up by the vertical offset");
        });
    }

    [Test]
    public void ScrollContentPresenter_ClampsOffsetToExtentMinusViewport()
    {
        var p = ArrangedPresenter(new Size(800, 1200), new Size(300, 400));
        p.SetOffset(new Vector2(9999, 9999));   // way past the end
        Assert.Multiple(() =>
        {
            Assert.That(p.Offset.X, Is.EqualTo(500), "max X offset = extent.W - viewport.W = 800-300");
            Assert.That(p.Offset.Y, Is.EqualTo(800), "max Y offset = extent.H - viewport.H = 1200-400");
        });
    }

    [Test]
    public void ScrollContentPresenter_ClipsToBounds()
    {
        // The presenter must opt into clipping so the overflowing content is scissored to the viewport by the renderer.
        Assert.That(new ScrollContentPresenter().ClipToBounds, Is.True);
    }
}
