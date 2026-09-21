using Adamantium.Mathematics;
using Adamantium.UI.Controls.Shapes;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Collections;
using Adamantium.UI.Core.Media;
using NUnit.Framework;

namespace Adamantium.UITests;

// Measure-level regression tests for stroked Shape bounds (pure CPU, no GPU). These guard the exact bugs the rest of
// the suite silently missed: a stroke poking past the control, a geometry shape losing its authored position, and a
// Path with a boolean/group geometry collapsing to zero size. They assert DesiredSize (layout) and RenderBounds
// (the painted-stroke rect) for known inputs - the same numbers a headless render produced.
[TestFixture]
public class ShapeMeasureTests
{
    // A Line keeps its authored X1/Y1..X2/Y2 (WPF Stretch=None): it must NOT snap to the control origin, and the
    // element must grow to include the centred stroke + round caps so the stroke can't poke past the control.
    // (Regression: an earlier geometry-normalisation shifted the line to the top-left corner.)
    [Test]
    public void Line_KeepsAuthoredPosition_AndSizeIncludesStroke()
    {
        var line = new Line
        {
            X1 = 100, Y1 = 200, X2 = 500, Y2 = 620,
            StrokeThickness = 12,
            Stroke = Brushes.Red,
            StartLineCap = PenLineCap.ConvexRound,
            EndLineCap = PenLineCap.ConvexRound
        };

        line.Measure(new Size(1280, 720));

        // Painted rect = endpoints +/- half (6) for the round caps: (94,194)..(506,626).
        Assert.Multiple(() =>
        {
            Assert.That(line.RenderBounds.X, Is.EqualTo(94).Within(1), "stroke left edge must stay at X1-half, not snap to 0");
            Assert.That(line.RenderBounds.Y, Is.EqualTo(194).Within(1), "stroke top edge must stay at Y1-half, not snap to 0");
            Assert.That(line.RenderBounds.Width, Is.EqualTo(412).Within(1));
            Assert.That(line.RenderBounds.Height, Is.EqualTo(432).Within(1));
            // Layout size spans origin -> stroke bottom-right, so the stroke never pokes past the control.
            Assert.That(line.DesiredSize.Width, Is.EqualTo(506).Within(1));
            Assert.That(line.DesiredSize.Height, Is.EqualTo(626).Within(1));
        });
    }

    // A Polygon with a sharp MITER corner: RenderBounds must reach the clamped miter spike (half / sin(angle/2),
    // capped at 4*half), well past a flat geometry-bbox + half inflate. (Guards the original stroke-poke fix.)
    [Test]
    public void Polygon_MiterCorner_BoundsReachTheSpike()
    {
        var poly = new Polygon
        {
            Points = new PointsCollection([new Vector2(10, 10), new Vector2(110, 10), new Vector2(60, 110)]),
            StrokeThickness = 10,
            Stroke = Brushes.Red,
            StrokeLineJoin = PenLineJoin.Miter
        };

        poly.Measure(new Size(1280, 720));

        // bbox bottom = 110; a flat +half would give 115. The sharp tip's miter spike pushes to ~121.
        var bottom = poly.RenderBounds.Y + poly.RenderBounds.Height;
        Assert.That(bottom, Is.GreaterThan(117), "miter spike at the sharp tip must extend the bounds past bbox+half");
    }

    // A Path whose Data is a boolean geometry must size from its geometry bounds, not collapse to 0. (Regression:
    // forcing ProcessGeometry at measure time produced an empty mesh for CombinedGeometry and zeroed the size.)
    [Test]
    public void Path_CombinedGeometry_DoesNotCollapse()
    {
        var path = new Path
        {
            Data = new CombinedGeometry
            {
                GeometryCombineMode = GeometryCombineMode.Xor,
                Geometry1 = new EllipseGeometry(new Vector2(75, 75), 40, 40),
                Geometry2 = new EllipseGeometry(new Vector2(125, 75), 40, 40)
            },
            StrokeThickness = 5,
            Stroke = Brushes.Red
        };

        path.Measure(new Size(1280, 720));

        Assert.Multiple(() =>
        {
            Assert.That(path.DesiredSize.Width, Is.GreaterThan(100), "combined-geometry Path must not collapse to 0 width");
            Assert.That(path.DesiredSize.Height, Is.GreaterThan(50), "combined-geometry Path must not collapse to 0 height");
        });
    }
}
