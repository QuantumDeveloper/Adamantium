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

    // AS INK, and never as a geometry. A geometry is cached by its CONTENT, a curve is built in screen coordinates and
    // so has different content after every pan and every turn of the wheel, and that cache is never emptied and is
    // walked once per frame - one curve on the plane was enough to bring the application to a crawl. The ink pass
    // takes the points as data: one instance, nothing built, nothing left behind.
    [Test]
    public void ACurveIsDrawnAsInkAndNotAsAGeometry()
    {
        var session = new RecordingDrawingSession();

        Curve().Render(session, Sized());

        Assert.Multiple(() =>
        {
            Assert.That(session.Geometries, Is.Empty, "a geometry per frame seeds a cache that is never emptied");
            Assert.That(session.Rectangles, Has.Count.EqualTo(1));
            Assert.That(session.Rectangles[0].Brush, Is.InstanceOf<InkBrush>());
        });
    }

    // The polyline handed to the pass IS the curve, so it has to carry more than the points it was drawn through.
    [Test]
    public void TheInkCarriesTheWalkedCurveAndNotTheControlPoints()
    {
        var session = new RecordingDrawingSession();
        var curve = Curve();

        curve.Render(session, Sized());

        var ink = session.Rectangles[0].Brush as InkBrush;

        Assert.That(ink.Count, Is.GreaterThan(curve.Count), "the pass was handed the corners, not the curve");
    }

    // Fewer than two points is not a line: drawing one would be a draw call that puts nothing on the screen.
    [Test]
    public void OnePointDrawsNothing()
    {
        var session = new RecordingDrawingSession();

        new CurveItem(CanvasCurve.Bezier, [new Vector2(0, 0)], Brushes.White, 2).Render(session, Sized());

        Assert.That(session.Rectangles, Is.Empty);
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
            Assert.That(session.Rectangles, Has.Count.EqualTo(1), "two points' worth is a line");
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

        Assert.That(session.Rectangles, Has.Count.EqualTo(1));
    }

    // WHAT MAKES IT A BEZIER: only the first point and the last are ON the line. Every other one pulls it without
    // being touched by it.
    //
    // A chain of spans of a chosen degree stood here, and it broke exactly this: the place where one span ends and the
    // next begins lies on the curve, so a curve of several spans had interior points sitting on the line with a corner
    // at each of them. A line anchored at points along its length is a spline, not a Bezier.
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(6)]
    [TestCase(7)]
    [TestCase(9)]
    public void OnlyTheFirstAndLastPointsLieOnABezier(int count)
    {
        var session = new RecordingDrawingSession();
        var points = new System.Collections.Generic.List<Vector2>();

        // A zig-zag: no interior point is anywhere near where the curve between its neighbours would pass, so one found
        // on the line is one the walk put there.
        for (var i = 0; i < count; i++) points.Add(new Vector2(i * 30, i % 2 == 0 ? 0 : -50));

        var curve = new CurveItem(CanvasCurve.Bezier, points, Brushes.White, 2);
        var canvas = Sized(800, 600);

        curve.Render(session, canvas);
        var ink = session.Rectangles[0].Brush as InkBrush;

        Assert.Multiple(() =>
        {
            Assert.That(Touches(ink, canvas.WorldToScreen(points[0])), Is.True, "the curve does not start at its first point");
            Assert.That(Touches(ink, canvas.WorldToScreen(points[^1])), Is.True, "and does not end at its last");

            for (var i = 1; i < count - 1; i++)
            {
                Assert.That(Touches(ink, canvas.WorldToScreen(points[i])), Is.False,
                    $"point {i} is sitting on the line - a Bezier has no anchors along its length");
            }
        });
    }

    // ...so its degree is COUNTED and not chosen: a Bezier through N+1 points is of degree N and can be nothing else.
    [TestCase(3, 2)]
    [TestCase(4, 3)]
    [TestCase(6, 5)]
    public void ABeziersDegreeFollowsItsPoints(int count, int degree)
    {
        var points = new System.Collections.Generic.List<Vector2>();
        for (var i = 0; i < count; i++) points.Add(new Vector2(i * 30, i % 2 == 0 ? 0 : -50));

        var curve = new CurveItem(CanvasCurve.Bezier, points, Brushes.White, 2);

        Assert.That(curve.Degree, Is.EqualTo(degree));

        curve.Degree = 2;
        Assert.That(curve.Degree, Is.EqualTo(degree), "a Bezier's degree is not something to be told");

        // A NURBS is ASKED for one, and that is the difference between the two.
        curve.Kind = CanvasCurve.Nurbs;
        Assert.That(curve.Degree, Is.EqualTo(2));
    }

    // Whether the drawn line passes through a point, in the same screen units the pass was handed.
    private static bool Touches(InkBrush ink, Vector2 at)
    {
        for (var i = 0; i < ink.Count; i++)
        {
            var dx = ink.Points[i].X - at.X;
            var dy = ink.Points[i].Y - at.Y;

            if (dx * dx + dy * dy <= 4) return true;
        }

        return false;
    }

    // Whether the polyline handed to the pass actually BENDS, rather than running straight from one control point to
    // the next: three points in a row that are collinear everywhere is a polyline however many samples it holds.
    private static bool Bends(InkBrush ink)
    {
        for (var i = 2; i < ink.Count; i++)
        {
            var ax = ink.Points[i - 1].X - ink.Points[i - 2].X;
            var ay = ink.Points[i - 1].Y - ink.Points[i - 2].Y;
            var bx = ink.Points[i].X - ink.Points[i - 1].X;
            var by = ink.Points[i].Y - ink.Points[i - 1].Y;

            if (System.Math.Abs(ax * by - ay * bx) > 0.01) return true;
        }

        return false;
    }

    // ...and a Bezier has no "follow the points" degree, so zero is not one of its answers: the inspector offers a list
    // of orders, and a curve reporting a degree that is not in it has nothing selected.
    // Two points is a straight line and still a Bezier - of degree one.
    [Test]
    public void TwoPointsAreABezierOfDegreeOne()
    {
        var curve = new CurveItem(CanvasCurve.Bezier, [new Vector2(0, 0), new Vector2(50, 50)], Brushes.White, 2);

        Assert.That(curve.Degree, Is.EqualTo(1));

        curve.Kind = CanvasCurve.Nurbs;
        curve.Degree = 0;
        Assert.That(curve.Degree, Is.EqualTo(0), "a NURBS still follows its points when asked to");
    }
}
