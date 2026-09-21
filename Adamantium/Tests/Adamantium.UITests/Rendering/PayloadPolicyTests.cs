using Adamantium.Graphics.Fonts;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.ProceduralGeometry.Shapes;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Rendering.Payloads;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

// Pure, GPU-free tests of each payload's rebuild-vs-cheap-update policy. The rule everywhere:
// a GEOMETRY change forces a buffer rebuild; colour (and, for filled shapes, pen) changes do not. Lines are
// pure stroke, so their endpoints AND pen feed the rebuild decision.
[TestFixture]
public class PayloadPolicyTests
{
    private static readonly Rect BoxA = new Rect(0, 0, 10, 10);
    private static readonly Rect BoxB = new Rect(0, 0, 20, 20);

    [Test]
    public void Rectangle_ColourOrPen_Change_IsCheapUpdate()
    {
        var a = new RectanglePayload(Brushes.Red, BoxA, new CornerRadius(0), null);
        var brushOnly = new RectanglePayload(Brushes.Blue, BoxA, new CornerRadius(0), null);
        var penOnly = new RectanglePayload(Brushes.Red, BoxA, new CornerRadius(0), new Pen(Brushes.Green, 2));

        Assert.That(a.RequiresBufferRebuild(brushOnly), Is.False, "brush change must not rebuild");
        Assert.That(a.RequiresBufferRebuild(penOnly), Is.False, "pen change must not rebuild the fill");
    }

    [Test]
    public void Rectangle_GeometryChange_RequiresRebuild()
    {
        var a = new RectanglePayload(Brushes.Red, BoxA, new CornerRadius(0), null);
        var sizeChanged = new RectanglePayload(Brushes.Red, BoxB, new CornerRadius(0), null);
        var cornerChanged = new RectanglePayload(Brushes.Red, BoxA, new CornerRadius(5), null);

        Assert.That(a.RequiresBufferRebuild(sizeChanged), Is.True);
        Assert.That(a.RequiresBufferRebuild(cornerChanged), Is.True);
    }

    [Test]
    public void Ellipse_ColourOrPen_Change_IsCheapUpdate()
    {
        var a = new EllipsePayload(Brushes.Red, BoxA, 0, 360, EllipseType.Sector, null);
        var brushOnly = new EllipsePayload(Brushes.Blue, BoxA, 0, 360, EllipseType.Sector, null);
        var penOnly = new EllipsePayload(Brushes.Red, BoxA, 0, 360, EllipseType.Sector, new Pen(Brushes.Green, 2));

        Assert.That(a.RequiresBufferRebuild(brushOnly), Is.False);
        Assert.That(a.RequiresBufferRebuild(penOnly), Is.False);
    }

    [Test]
    public void Ellipse_GeometryChange_RequiresRebuild()
    {
        var a = new EllipsePayload(Brushes.Red, BoxA, 0, 360, EllipseType.Sector, null);
        var sweepChanged = new EllipsePayload(Brushes.Red, BoxA, 0, 180, EllipseType.Sector, null);
        var rectChanged = new EllipsePayload(Brushes.Red, BoxB, 0, 360, EllipseType.Sector, null);

        Assert.That(a.RequiresBufferRebuild(sweepChanged), Is.True);
        Assert.That(a.RequiresBufferRebuild(rectChanged), Is.True);
    }

    [Test]
    public void Line_EndpointOrPen_Change_RequiresRebuild()
    {
        // LinePayload's rebuild check is hash-based and Pen hashes by reference, so "unchanged" means the
        // same pen instance (which is what a control passes when its Pen property hasn't changed).
        var pen = new Pen(Brushes.Red, 1);
        var a = new LinePayload(new Vector2(0, 0), new Vector2(10, 10), pen);
        var same = new LinePayload(new Vector2(0, 0), new Vector2(10, 10), pen);
        var moved = new LinePayload(new Vector2(0, 0), new Vector2(20, 20), pen);
        var penChanged = new LinePayload(new Vector2(0, 0), new Vector2(10, 10), new Pen(Brushes.Blue, 1));

        Assert.That(a.RequiresBufferRebuild(same), Is.False);
        Assert.That(a.RequiresBufferRebuild(moved), Is.True);
        Assert.That(a.RequiresBufferRebuild(penChanged), Is.True, "a line is pure stroke: its pen drives rebuild");
    }

    [Test]
    public void Geometry_OnlyGeometryReference_Drives_Rebuild()
    {
        var geom = new RectangleGeometry(BoxA);
        var a = new GeometryPayload(Brushes.Red, geom);
        var brushOnly = new GeometryPayload(Brushes.Blue, geom);
        var differentGeom = new GeometryPayload(Brushes.Red, new RectangleGeometry(BoxA));

        Assert.That(a.RequiresBufferRebuild(brushOnly), Is.False, "same geometry instance -> cheap update");
        Assert.That(a.RequiresBufferRebuild(differentGeom), Is.True, "different geometry instance -> rebuild");
    }

    [Test]
    public void Text_ColourChange_IsCheapUpdate_SizeChange_Rebuilds()
    {
        var prms = new TextRenderingParameters();
        var size = new Size(100, 20);

        var a = new TextPayload(prms, size, null, Brushes.Red, Brushes.Transparent, Brushes.Black);
        var colourOnly = new TextPayload(prms, size, null, Brushes.Blue, Brushes.Yellow, Brushes.White);
        var sizeChanged = new TextPayload(prms, new Size(200, 20), null, Brushes.Red, Brushes.Transparent, Brushes.Black);

        Assert.That(a.RequiresBufferRebuild(colourOnly), Is.False, "colour-only change must use the cheap re-raster path");
        Assert.That(a.RequiresBufferRebuild(sizeChanged), Is.True);
    }
}
