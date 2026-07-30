using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Templates;
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

    // A rotated footprint comes out of a matrix, so it lands a few millionths off a round number. The SCALE tests can
    // compare exactly (scaling is a multiply); rotation cannot, and pretending otherwise only produces failures that say
    // "50.0000043 is not 50".
    private static void AssertSize(Size actual, double width, double height, string because = null)
    {
        Assert.Multiple(() =>
        {
            Assert.That(actual.Width, Is.EqualTo(width).Within(0.001), because);
            Assert.That(actual.Height, Is.EqualTo(height).Within(0.001), because);
        });
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

    /// <summary>
    /// A quarter turn swaps the footprint: 100x50 laid on its side occupies 50x100. This is what makes a sideways label
    /// possible at all - the parent has to be told the TURNED extent, or it reserves room for text lying flat and the
    /// text is then drawn outside it.
    /// <para>Found through docking: a tab folded against a side edge kept the width of its horizontal label (measured
    /// 78px for "Inspector"), so the collapsed strip stayed as wide as the words in it.</para>
    /// </summary>
    [Test]
    public void Rotate90_Measure_SwapsTheFootprint()
    {
        var c = new FixedContent { LayoutTransform = new Transform { RotationAngle = 90 } };
        c.Measure(new Size(1280, 720));
        AssertSize(c.DesiredSize, 50, 100);
    }

    /// <summary>Turning the other way is the same footprint - a bounding box has no sense of direction.</summary>
    [Test]
    public void RotateMinus90_Measure_SwapsTheFootprintToo()
    {
        var c = new FixedContent { LayoutTransform = new Transform { RotationAngle = -90 } };
        c.Measure(new Size(1280, 720));
        AssertSize(c.DesiredSize, 50, 100);
    }

    /// <summary>Half a turn changes nothing about how much room is needed.</summary>
    [Test]
    public void Rotate180_Measure_KeepsTheFootprint()
    {
        var c = new FixedContent { LayoutTransform = new Transform { RotationAngle = 180 } };
        c.Measure(new Size(1280, 720));
        AssertSize(c.DesiredSize, 100, 50);
    }

    /// <summary>An angle that is not a right angle still has a bounding box: 45 degrees over 100x50 needs
    /// (100+50)/sqrt(2) each way. The general case matters because it is the one a formula can get wrong while the right
    /// angles happen to come out.</summary>
    [Test]
    public void Rotate45_Measure_ReportsTheBoundingBox()
    {
        var c = new FixedContent { LayoutTransform = new Transform { RotationAngle = 45 } };
        c.Measure(new Size(1280, 720));

        var expected = (100 + 50) / System.Math.Sqrt(2);
        Assert.Multiple(() =>
        {
            Assert.That(c.DesiredSize.Width, Is.EqualTo(expected).Within(0.5));
            Assert.That(c.DesiredSize.Height, Is.EqualTo(expected).Within(0.5));
        });
    }

    /// <summary>And the arrange half of it: the content still renders at its own size, while the space it takes is the
    /// turned one - the same split scale already keeps.</summary>
    [Test]
    public void Rotate90_Arrange_RenderSizeInner_BoundsTurned()
    {
        var c = new FixedContent { LayoutTransform = new Transform { RotationAngle = 90 } };
        c.Measure(new Size(1280, 720));
        c.Arrange(new Rect(0, 0, 1280, 720));

        Assert.Multiple(() =>
        {
            AssertSize(c.RenderSize, 100, 50, "content renders at its own (inner) size");
            AssertSize(c.Bounds.Size, 50, 100, "the room it takes is the turned footprint");
        });
    }

    /// <summary>
    /// The footprint is only half the promise: what is DRAWN has to land inside it. A layout transform reserves a
    /// bounding box and the content must fill that box - so after the transform, the content's own corners map to
    /// (0,0)..(footprint) and nowhere else.
    /// <para>Scaling happens to satisfy this for free, because scaling about the origin grows right and down into the
    /// box. Rotation does not: turning about the origin sends the content out of the box entirely, and it is then drawn
    /// beside or above the space that was reserved for it - measured in the sandbox, a 90-degree box overlapped its
    /// neighbours while the layout around it was correctly spaced.</para>
    /// </summary>
    [Test]
    public void Rotate90_DrawsInsideTheFootprintItReserved()
    {
        var c = new FixedContent { LayoutTransform = new Transform { RotationAngle = 90 } };
        c.Measure(new Size(1280, 720));
        c.Arrange(new Rect(0, 0, 1280, 720));

        var corners = TransformedCorners(c);

        Assert.Multiple(() =>
        {
            Assert.That(corners.MinX, Is.EqualTo(0).Within(0.001), "nothing is drawn to the LEFT of the reserved box");
            Assert.That(corners.MinY, Is.EqualTo(0).Within(0.001), "nor ABOVE it");
            Assert.That(corners.MaxX, Is.EqualTo(50).Within(0.001), "and it fills the box exactly");
            Assert.That(corners.MaxY, Is.EqualTo(100).Within(0.001));
        });
    }

    /// <summary>The same promise for a scale, which already kept it - so the fix for rotation cannot quietly break it.</summary>
    [Test]
    public void Scale_DrawsInsideTheFootprintItReserved()
    {
        var c = new FixedContent { LayoutTransform = new Transform { ScaleX = 2, ScaleY = 2 } };
        c.Measure(new Size(1280, 720));
        c.Arrange(new Rect(0, 0, 1280, 720));

        var corners = TransformedCorners(c);

        Assert.Multiple(() =>
        {
            Assert.That(corners.MinX, Is.EqualTo(0).Within(0.001));
            Assert.That(corners.MinY, Is.EqualTo(0).Within(0.001));
            Assert.That(corners.MaxX, Is.EqualTo(200).Within(0.001));
            Assert.That(corners.MaxY, Is.EqualTo(100).Within(0.001));
        });
    }

    // Where the element's own four corners actually land once its transform has been applied - which is what the renderer
    // draws, as opposed to Bounds, which is only what layout set aside.
    private static (double MinX, double MinY, double MaxX, double MaxY) TransformedCorners(IUIComponent component)
    {
        var size = component.RenderSize;
        var matrix = component.LocalTransform;

        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var corner in new[]
                 {
                     new Vector2(0, 0),
                     new Vector2(size.Width, 0),
                     new Vector2(0, size.Height),
                     new Vector2(size.Width, size.Height)
                 })
        {
            var x = corner.X * matrix.M11 + corner.Y * matrix.M21 + matrix.M41;
            var y = corner.X * matrix.M12 + corner.Y * matrix.M22 + matrix.M42;

            minX = System.Math.Min(minX, x);
            minY = System.Math.Min(minY, y);
            maxX = System.Math.Max(maxX, x);
            maxY = System.Math.Max(maxY, y);
        }

        return (minX, minY, maxX, maxY);
    }

    /// <summary>What the docking strip actually needs: turned labels stacked down a column, each taking the width of its
    /// HEIGHT. Without this the column is as wide as the longest word.</summary>
    [Test]
    public void VerticalStackPanel_StacksRotatedChildrenByTheirTurnedFootprint()
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical };
        var first = new FixedContent { LayoutTransform = new Transform { RotationAngle = 90 } };
        var second = new FixedContent { LayoutTransform = new Transform { RotationAngle = 90 } };
        panel.Children.Add(first);
        panel.Children.Add(second);

        panel.Measure(new Size(1280, 720));
        panel.Arrange(new Rect(0, 0, 1280, 720));

        Assert.Multiple(() =>
        {
            Assert.That(second.Bounds.Y, Is.EqualTo(100).Within(0.5), "the second starts below the first's TURNED height");
            Assert.That(panel.DesiredSize.Width, Is.EqualTo(50).Within(0.5), "and the column is as wide as a turned label");
        });
    }

    /// <summary>
    /// The transform has to survive being INSIDE a template. A rotated label in a docking strip is not a control someone
    /// rotated directly - it is the root of a DataTemplate, presented by a ContentPresenter, and the presenter is what
    /// the parent measures. If the presenter reports its child's untransformed size, everything above it reserves room
    /// for text lying flat however correctly the child itself was turned.
    /// </summary>
    [Test]
    public void RotatedTemplateRoot_ReportsItsTurnedFootprintThroughThePresenter()
    {
        var presenter = new ContentPresenter
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Content = "anything",
            ContentTemplate = new DataTemplate(() => new TemplateResult
            {
                RootComponent = new Border
                {
                    Width = 100,
                    Height = 50,
                    LayoutTransform = new Transform { RotationAngle = 90 }
                }
            })
        };

        presenter.Measure(new Size(1280, 720));

        AssertSize(presenter.DesiredSize, 50, 100, "the presenter is as big as its turned content, not as its flat content");
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
