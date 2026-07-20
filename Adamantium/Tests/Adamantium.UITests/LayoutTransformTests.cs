using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// Pins LayoutTransform's WPF semantics: the element is measured/arranged in its OWN (untransformed) space, the parent
/// sees the transform's BOUNDING BOX (footprint), RenderSize stays the inner content size, and a value change re-runs
/// layout. The null path (no LayoutTransform) must be unchanged.
/// </summary>
[TestFixture]
public class LayoutTransformTests
{
    // Left/Top so the element shrinks to its (transformed) DesiredSize in arrange instead of stretching to fill the slot,
    // which is what lets these tests read the footprint directly.
    private sealed class FixedContent : MeasurableUIComponent
    {
        private readonly Size _content;
        public FixedContent(double w = 100, double h = 50)
        {
            _content = new Size(w, h);
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Top;
        }
        protected override Size MeasureOverride(Size availableSize) => _content;
    }

    [Test]
    public void NoTransform_SizesUnchanged()
    {
        var c = new FixedContent();
        c.Measure(new Size(1280, 720));
        c.Arrange(new Rect(0, 0, 1280, 720));
        Assert.Multiple(() =>
        {
            Assert.That(c.DesiredSize, Is.EqualTo(new Size(100, 50)));
            Assert.That(c.RenderSize, Is.EqualTo(new Size(100, 50)));
            Assert.That(c.Bounds.Size, Is.EqualTo(new Size(100, 50)));
        });
    }

    [Test]
    public void Scale_Measure_ReportsOuterFootprint()
    {
        var c = new FixedContent { LayoutTransform = new Transform { ScaleX = 2, ScaleY = 2 } };
        c.Measure(new Size(1280, 720));
        Assert.That(c.DesiredSize, Is.EqualTo(new Size(200, 100)));
    }

    [Test]
    public void Scale_Arrange_RenderSizeInner_BoundsOuter()
    {
        var c = new FixedContent { LayoutTransform = new Transform { ScaleX = 2, ScaleY = 2 } };
        c.Measure(new Size(1280, 720));
        c.Arrange(new Rect(0, 0, 1280, 720));
        Assert.Multiple(() =>
        {
            Assert.That(c.RenderSize, Is.EqualTo(new Size(100, 50)), "content renders at its own (inner) size");
            Assert.That(c.Bounds.Size, Is.EqualTo(new Size(200, 100)), "footprint = content x scale");
            // ActualWidth must be the INNER size (= RenderSize): controls draw geometry at ActualWidth, and LocalTransform
            // scales it - if ActualWidth were the outer footprint the transform would scale it AGAIN (double-scale).
            Assert.That(c.ActualWidth, Is.EqualTo(100), "ActualWidth is the inner size (geometry is drawn here, then scaled)");
            Assert.That(c.ActualHeight, Is.EqualTo(50));
        });
    }

    [Test]
    public void NonUniformScale_Measure()
    {
        var c = new FixedContent { LayoutTransform = new Transform { ScaleX = 2, ScaleY = 3 } };
        c.Measure(new Size(1280, 720));
        Assert.That(c.DesiredSize, Is.EqualTo(new Size(200, 150)));
    }

    [Test]
    public void ExplicitSize_Scale_Measure()
    {
        var c = new FixedContent { Width = 100, Height = 50, LayoutTransform = new Transform { ScaleX = 1.5, ScaleY = 1.5 } };
        c.Measure(new Size(1280, 720));
        Assert.That(c.DesiredSize, Is.EqualTo(new Size(150, 75)));
    }

    [Test]
    public void ExplicitSize_Scale_Arrange_FootprintScales()
    {
        var c = new FixedContent { Width = 100, Height = 50, LayoutTransform = new Transform { ScaleX = 1.5, ScaleY = 1.5 } };
        c.Measure(new Size(1280, 720));
        c.Arrange(new Rect(0, 0, 1280, 720));
        Assert.Multiple(() =>
        {
            Assert.That(c.Bounds.Size, Is.EqualTo(new Size(150, 75)), "explicit-size element footprint must scale too");
            Assert.That(c.RenderSize, Is.EqualTo(new Size(100, 50)), "content stays its own explicit size");
        });
    }

    [Test]
    public void MeasuredWithInfiniteAxis_NoNaN()
    {
        // A StackPanel/WrapPanel measures with an infinite main axis - the transform math must not produce NaN.
        var c = new FixedContent { LayoutTransform = new Transform { ScaleX = 2, ScaleY = 2 } };
        c.Measure(new Size(double.PositiveInfinity, 720));
        Assert.That(c.DesiredSize, Is.EqualTo(new Size(200, 100)));
    }

    [Test]
    public void ValueChange_ReMeasuresToNewFootprint()
    {
        var t = new Transform { ScaleX = 2, ScaleY = 2 };
        var c = new FixedContent { LayoutTransform = t };
        c.Measure(new Size(1280, 720));
        Assert.That(c.DesiredSize, Is.EqualTo(new Size(200, 100)));

        t.ScaleX = 3;   // changing a LayoutTransform value must invalidate the owner's measure
        Assert.That(c.IsMeasureValid, Is.False, "value change must invalidate measure");

        c.Measure(new Size(1280, 720));
        Assert.That(c.DesiredSize, Is.EqualTo(new Size(300, 100)));
    }

    [Test]
    public void HorizontalStackPanel_PositionsSiblingByScaledFootprint()
    {
        // The demo scenario: A[100] | B[100 x2 = 200 footprint] | C. C must start at 300, not overlap B.
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        var a = new FixedContent();
        var b = new FixedContent { LayoutTransform = new Transform { ScaleX = 2, ScaleY = 2 } };
        var c = new FixedContent();
        panel.Children.Add(a);
        panel.Children.Add(b);
        panel.Children.Add(c);

        panel.Measure(new Size(1280, 720));
        panel.Arrange(new Rect(0, 0, 1280, 720));

        Assert.Multiple(() =>
        {
            Assert.That(a.Bounds.X, Is.EqualTo(0));
            Assert.That(b.Bounds.X, Is.EqualTo(100), "B starts after A");
            Assert.That(b.Bounds.Width, Is.EqualTo(200), "B's footprint is scaled x2");
            Assert.That(c.Bounds.X, Is.EqualTo(300), "C starts after B's SCALED footprint (100 + 200), no overlap");
        });
    }
}
