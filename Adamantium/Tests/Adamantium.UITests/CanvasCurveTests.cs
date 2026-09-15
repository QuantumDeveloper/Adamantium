using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UITests.Rendering;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>Curves: one item for a Bezier, a B-spline and a NURBS, reshaped by its own points rather than by a box.
/// </summary>
public class CanvasCurveTests
{
    private static InfiniteCanvas Sized(double width = 400, double height = 300)
    {
        var canvas = new InfiniteCanvas();
        canvas.Measure(new Size(width, height), force: true);
        canvas.Arrange(new Rect(0, 0, width, height));

        return canvas;
    }

    private static CurveItem Curve(CanvasCurve kind = CanvasCurve.Bezier) =>
        new(kind, [new Vector2(0, 0), new Vector2(20, -40), new Vector2(60, 40), new Vector2(100, 0)],
            Brushes.White, 2);

    [Test]
    public void ACurveDrawsOneStrokedFigure()
    {
        var session = new RecordingDrawingSession();

        Curve().Render(session, Sized());

        Assert.Multiple(() =>
        {
            Assert.That(session.Geometries, Has.Count.EqualTo(1));
            Assert.That(session.Geometries[0].Brush, Is.Null, "stroked, not filled");
            Assert.That(session.Geometries[0].Pen, Is.Not.Null);
        });
    }

    // Fewer than two points is not a line: drawing one would be a draw call that puts nothing on the screen.
    [Test]
    public void OnePointDrawsNothing()
    {
        var session = new RecordingDrawingSession();

        new CurveItem(CanvasCurve.Bezier, [new Vector2(0, 0)], Brushes.White, 2).Render(session, Sized());

        Assert.That(session.Geometries, Is.Empty);
    }

    // The point the tool is still placing is HANDED to the item, not added to it: drawing runs on its own thread, and a
    // tool that added a point, drew and took it off again raced every click.
    [Test]
    public void AnExtraPointIsDrawnWithoutBeingKept()
    {
        var session = new RecordingDrawingSession();
        var curve = new CurveItem(CanvasCurve.Bezier, [new Vector2(0, 0)], Brushes.White, 2);

        curve.Render(session, Sized(), new Vector2(50, 50));

        Assert.Multiple(() =>
        {
            Assert.That(session.Geometries, Has.Count.EqualTo(1), "two points' worth is a line");
            Assert.That(curve.Count, Is.EqualTo(1), "and the item still holds one");
        });
    }

    // Hit BY THE LINE and not by the box round it: the box of a curve is mostly empty, and one picked up by its
    // emptiness would swallow everything under it.
    [Test]
    public void ACurveIsHitOnTheLine()
    {
        var curve = new CurveItem(CanvasCurve.Bezier,
            [new Vector2(0, 0), new Vector2(40, 0), new Vector2(60, 0), new Vector2(100, 0)], Brushes.White, 2);

        Assert.Multiple(() =>
        {
            Assert.That(curve.HitTest(new Vector2(50, 0), 1), Is.True);
            Assert.That(curve.HitTest(new Vector2(50, 60), 1), Is.False);
        });
    }

    // It offers the BODY grip only - a box with eight grips round a curve offers a second way to reshape it that fights
    // the points.
    [Test]
    public void ACurveOffersOnlyTheBody()
    {
        Assert.That(Curve().Handles, Is.EqualTo(CanvasHandles.Body));
    }

    // ...and the canvas honours that: no grip of the frame answers a press.
    [Test]
    public void NoFrameGripAnswersOnACurve()
    {
        var canvas = Sized();
        var scene = new CanvasScene();
        var curve = Curve();

        scene.Add(curve);
        canvas.Scene = scene;
        canvas.Select(curve, false);

        var corner = canvas.WorldToScreen(new Vector2(curve.Bounds.X, curve.Bounds.Y));

        Assert.Multiple(() =>
        {
            Assert.That(canvas.OfferedHandles, Is.EqualTo(CanvasHandles.Body));
            Assert.That(canvas.HandleAt(corner), Is.EqualTo(CanvasHandle.None));
        });
    }

    // The POINTS answer instead, and the canvas finds them in screen pixels like every other grip.
    [Test]
    public void APointHandleAnswersWhereAPointIs()
    {
        var canvas = Sized();
        var scene = new CanvasScene();
        var curve = Curve();

        scene.Add(curve);
        canvas.Scene = scene;
        canvas.Select(curve, false);

        var second = canvas.WorldToScreen(curve.Points[1]);

        Assert.Multiple(() =>
        {
            Assert.That(canvas.PointHandleAt(second), Is.EqualTo(1));
            Assert.That(canvas.PointHandleAt(new Vector2(second.X + 60, second.Y)), Is.EqualTo(-1));
        });
    }

    // Moving one point moves that point and nothing else.
    [Test]
    public void MovingAPointMovesOnlyIt()
    {
        var curve = Curve();
        var others = new[] { curve.Points[0], curve.Points[2], curve.Points[3] };

        curve.MovePoint(1, new Vector2(-500, -500));

        Assert.Multiple(() =>
        {
            Assert.That(curve.Points[1], Is.EqualTo(new Vector2(-500, -500)));
            Assert.That(curve.Points[0], Is.EqualTo(others[0]));
            Assert.That(curve.Points[2], Is.EqualTo(others[1]));
            Assert.That(curve.Points[3], Is.EqualTo(others[2]));
        });
    }

    // Every kind draws: the three differ by one call to the maths and by nothing else around them.
    [TestCase(CanvasCurve.Bezier)]
    [TestCase(CanvasCurve.BSpline)]
    [TestCase(CanvasCurve.Nurbs)]
    public void EveryKindDraws(CanvasCurve kind)
    {
        var session = new RecordingDrawingSession();

        Curve(kind).Render(session, Sized());

        Assert.That(session.Geometries, Has.Count.EqualTo(1));
    }
}
