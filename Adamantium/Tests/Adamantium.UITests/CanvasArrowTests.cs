using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UITests.Rendering;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>The arrow: a line with a head on one end, the other, or both. What is asserted here is what is DRAWN,
/// because a head is geometry and nothing else about it can be read back.</summary>
public class CanvasArrowTests
{
    private static InfiniteCanvas Sized(double width = 400, double height = 300)
    {
        var canvas = new InfiniteCanvas();
        canvas.Measure(new Size(width, height), force: true);
        canvas.Arrange(new Rect(0, 0, width, height));

        return canvas;
    }

    // Stated rather than assumed: an application may hand a run back to the geometry path on a device that refuses the
    // pass, and these are about what the pass does.
    [SetUp]
    public void UseTheArrowPass() => ShapeItem.PaintsRuns = true;

    private static ShapeItem Arrow(CanvasArrowHead start, CanvasArrowHead end) =>
        new(CanvasShape.Arrow, new Rect(0, 0, 100, 0), Brushes.White, 4)
        {
            StartHead = start,
            EndHead = end
        };

    // What the arrow is PAINTED with - the parameter block that reaches the shader. Null when it was drawn some other
    // way, which is what every test below is really asking about.
    private static CanvasArrowBrush Painted(RecordingDrawingSession session)
    {
        foreach (var rectangle in session.Rectangles)
        {
            if (rectangle.Brush is CanvasArrowBrush arrow) return arrow;
        }

        return null;
    }

    // NO GEOMETRY AT ALL. The whole arrow - shaft and both heads - is one quad whose fragments decide the shape, so
    // there is nothing built, nothing kept and nothing handed to the renderer that can be rewritten under it.
    [Test]
    public void AnArrowIsPaintedAndNotBuilt()
    {
        var canvas = Sized();
        var session = new RecordingDrawingSession();
        var arrow = Arrow(CanvasArrowHead.Barbs, CanvasArrowHead.Triangle);

        arrow.Render(session, canvas);

        Assert.Multiple(() =>
        {
            Assert.That(session.Geometries, Is.Empty, "a head was built as a mesh");
            Assert.That(session.Lines, Is.Empty, "the shaft was stroked as a line");
            Assert.That(Painted(session), Is.Not.Null, "nothing reached the arrow pass");
        });
    }

    // Both ends carry what they were given, and a plain LINE is the same thing with nothing on its ends - one shape,
    // one path, and no second place for the same shaft to be drawn slightly differently.
    [Test]
    public void EachEndCarriesTheHeadItWasGiven()
    {
        var canvas = Sized();
        var both = new RecordingDrawingSession();
        var plain = new RecordingDrawingSession();

        Arrow(CanvasArrowHead.Barbs, CanvasArrowHead.Triangle).Render(both, canvas);
        new ShapeItem(CanvasShape.Line, new Rect(0, 0, 100, 0), Brushes.White, 4).Render(plain, canvas);

        Assert.Multiple(() =>
        {
            Assert.That(Painted(both).StartHead, Is.EqualTo((int)CanvasArrowHead.Barbs));
            Assert.That(Painted(both).EndHead, Is.EqualTo((int)CanvasArrowHead.Triangle));
            Assert.That(Painted(plain), Is.Not.Null, "a line goes through the same pass");
            Assert.That(Painted(plain).StartHead, Is.EqualTo(0));
            Assert.That(Painted(plain).EndHead, Is.EqualTo(0));
        });
    }

    // The colour is the LINE's - a head belongs to the line it sits on and not to whatever the shape is filled with.
    [Test]
    public void TheArrowIsPaintedInTheLineColour()
    {
        var canvas = Sized();
        var session = new RecordingDrawingSession();
        var arrow = Arrow(CanvasArrowHead.None, CanvasArrowHead.Triangle);

        arrow.Render(session, canvas);

        Assert.That(Painted(session).Color, Is.EqualTo(((SolidColorBrush)arrow.Stroke).Color));
    }

    // The numbers are re-read on every record, and the brush is the same object each time - so it has to SAY that they
    // changed, or the paint has nothing to notice and the arrow stays where it was first baked.
    [Test]
    public void MovingTheArrowSaysSoOnTheBrush()
    {
        var canvas = Sized();
        var session = new RecordingDrawingSession();
        var arrow = Arrow(CanvasArrowHead.None, CanvasArrowHead.Triangle);

        arrow.Render(session, canvas);
        var was = Painted(session).Revision;

        arrow.Move(new Vector2(60, 0));
        arrow.Render(session, canvas);

        Assert.That(Painted(session).Revision, Is.GreaterThan(was));
    }

    // The head is measured in LINE THICKNESSES, so a thicker line carries a proportionally bigger head - that is what
    // keeps the head part of its own line instead of a mark that drifts away from it.
    [Test]
    public void AThickerLineCarriesABiggerHead()
    {
        var thin = new ShapeItem(CanvasShape.Arrow, new Rect(0, 0, 100, 0), Brushes.White, 2);
        var thick = new ShapeItem(CanvasShape.Arrow, new Rect(0, 0, 100, 0), Brushes.White, 8);

        ArrowHead.Points(Vector2.Zero, new Vector2(100, 0), 2, thin.HeadLength, thin.HeadWidth,
            out var thinLeft, out _);
        ArrowHead.Points(Vector2.Zero, new Vector2(100, 0), 8, thick.HeadLength, thick.HeadWidth,
            out var thickLeft, out _);

        Assert.That(100 - thickLeft.X, Is.GreaterThan(100 - thinLeft.X));
    }

    // ...but never longer than the line it sits on. On a very short arrow a head of the stated length would start
    // behind the tail and point backwards.
    [Test]
    public void AHeadIsNeverLongerThanItsLine()
    {
        ArrowHead.Points(Vector2.Zero, new Vector2(3, 0), 10, 4, 3, out var left, out var right);

        Assert.Multiple(() =>
        {
            Assert.That(left.X, Is.GreaterThanOrEqualTo(0));
            Assert.That(right.X, Is.GreaterThanOrEqualTo(0));
        });
    }

    // Two points on top of each other have no direction to point in, and a head built from them would be a random one.
    [Test]
    public void AHeadWithNoDirectionIsRefused()
    {
        Assert.That(ArrowHead.Points(Vector2.Zero, Vector2.Zero, 4, 3, 2, out _, out _), Is.False);
    }

    // WHERE the arrow is, which none of the original tests ever asked: that it is one figure drawn with the right brush
    // says nothing about it being in the right place, and an arrow sitting off the end of its own line is exactly what
    // a person notices first. The pass places the shape from these two points and nothing else.
    [Test]
    public void TheArrowIsPaintedBetweenItsOwnEnds()
    {
        var canvas = Sized();
        var session = new RecordingDrawingSession();
        var arrow = Arrow(CanvasArrowHead.Triangle, CanvasArrowHead.Triangle);

        arrow.Render(session, canvas);

        var from = canvas.WorldToScreen(Vector2.Zero);
        var to = canvas.WorldToScreen(new Vector2(100, 0));
        var painted = Painted(session);

        Assert.Multiple(() =>
        {
            Assert.That(painted.From.X, Is.EqualTo(from.X).Within(0.5));
            Assert.That(painted.From.Y, Is.EqualTo(from.Y).Within(0.5));
            Assert.That(painted.To.X, Is.EqualTo(to.X).Within(0.5));
            Assert.That(painted.To.Y, Is.EqualTo(to.Y).Within(0.5));
        });
    }

    // The head is measured in LINE THICKNESSES and handed over already in the units the shader works in, so a thicker
    // line carries a proportionally bigger head - that is what keeps the head part of its own line instead of a mark
    // that drifts away from it.
    [Test]
    public void TheHeadIsHandedOverInTheLinesOwnThicknesses()
    {
        var canvas = Sized();
        var session = new RecordingDrawingSession();
        var arrow = Arrow(CanvasArrowHead.None, CanvasArrowHead.Triangle);

        arrow.Render(session, canvas);
        var painted = Painted(session);

        Assert.Multiple(() =>
        {
            Assert.That(painted.Thickness, Is.EqualTo(4).Within(0.001));
            Assert.That(painted.HeadLength, Is.EqualTo(4 * arrow.HeadLength).Within(0.001));
            Assert.That(painted.HeadWidth, Is.EqualTo(4 * arrow.HeadWidth / 2).Within(0.001),
                "HALF the width, which is what the shape is measured out from the axis by");
        });
    }

    // The FRAME has to hold what is drawn. An arrow's head reaches well past the line's own box - a head is measured in
    // line thicknesses, and at thickness 18 it is twenty-odd units wide - so a frame taken from the box alone is drawn
    // inside the shape it is supposed to be around.
    [Test]
    public void TheFrameHoldsTheHeads()
    {
        var arrow = new ShapeItem(CanvasShape.Arrow, new Rect(0, 0, 100, 0), Brushes.White, 18)
        {
            StartHead = CanvasArrowHead.Barbs,
            EndHead = CanvasArrowHead.Barbs
        };

        var reach = 18 * arrow.HeadWidth / 2;

        Assert.Multiple(() =>
        {
            Assert.That(arrow.Bounds.Y, Is.LessThanOrEqualTo(-reach), "the head is drawn above the frame");
            Assert.That(arrow.Bounds.Bottom, Is.GreaterThanOrEqualTo(reach), "and below it");
            Assert.That(arrow.Bounds.X, Is.LessThanOrEqualTo(-9), "the stroke itself is outside the frame");
            Assert.That(arrow.Bounds.Right, Is.GreaterThanOrEqualTo(109));
        });
    }

    // AN ARROW IS PLACED BY ITS ENDS: one pinned to what it comes from, the other pulled to what it points at. A box
    // round it can only scale both at once, which is no use for putting one exactly where it has to go.
    [Test]
    public void MovingOneEndLeavesTheOtherWhereItIs()
    {
        var arrow = Arrow(CanvasArrowHead.None, CanvasArrowHead.Triangle);
        var pinned = arrow.Points[0];

        arrow.MovePoint(1, new Vector2(40, 70));

        Assert.Multiple(() =>
        {
            Assert.That(arrow.Points[0].X, Is.EqualTo(pinned.X).Within(0.001), "the other end moved too");
            Assert.That(arrow.Points[0].Y, Is.EqualTo(pinned.Y).Within(0.001));
            Assert.That(arrow.Points[1].X, Is.EqualTo(40).Within(0.001));
            Assert.That(arrow.Points[1].Y, Is.EqualTo(70).Within(0.001));
        });
    }

    // ...and the ends STAY the ends. A box and one bit cannot say which of the two corners a line starts at, so pulling
    // one end past the other turned the arrow round - the head jumped to the end that had not been touched.
    [TestCase(-60, 0, TestName = "past it to the left")]
    [TestCase(-60, 40, TestName = "past it and below")]
    [TestCase(0, -40, TestName = "straight up past it")]
    [TestCase(0, 40, TestName = "straight down past it")]
    public void AnEndDraggedPastTheOtherDoesNotTurnTheArrowRound(double x, double y)
    {
        var arrow = Arrow(CanvasArrowHead.None, CanvasArrowHead.Triangle);

        arrow.MovePoint(1, new Vector2(x, y));

        Assert.Multiple(() =>
        {
            Assert.That(arrow.Points[0].X, Is.EqualTo(0).Within(0.001), "the pinned end is no longer the first");
            Assert.That(arrow.Points[0].Y, Is.EqualTo(0).Within(0.001));
            Assert.That(arrow.Points[1].X, Is.EqualTo(x).Within(0.001));
            Assert.That(arrow.Points[1].Y, Is.EqualTo(y).Within(0.001));
        });
    }

    // The head follows the ends, which is the whole reason the ends have to stay put: it is drawn on whichever end it
    // was put on, wherever that end has been dragged to.
    [Test]
    public void TheHeadStaysOnTheEndItWasPutOnAfterADrag()
    {
        var canvas = Sized();
        var session = new RecordingDrawingSession();
        var arrow = Arrow(CanvasArrowHead.None, CanvasArrowHead.Triangle);

        arrow.MovePoint(1, new Vector2(-60, 0));
        arrow.Render(session, canvas);

        var tip = canvas.WorldToScreen(new Vector2(-60, 0));
        var painted = Painted(session);

        Assert.Multiple(() =>
        {
            Assert.That(painted.To.X, Is.EqualTo(tip.X).Within(0.5), "the head is on the end that was not dragged");
            Assert.That(painted.EndHead, Is.EqualTo((int)CanvasArrowHead.Triangle));
            Assert.That(painted.StartHead, Is.EqualTo(0));
        });
    }

    // A box round a line offers grips on corners that are not on the shape at all, and dragging one moves both ends -
    // the gesture that fights the one that means something.
    //
    // Asked of the CANVAS with the line SELECTED, which is the only form of the question worth asking. Asked of the
    // item - line.Handles - it passed while the frame was still drawn round every line on screen: the canvas reads that
    // property through ICanvasItem, which carries a default of its own, and a test that calls it on the concrete type
    // proves the concrete type and nothing about what the canvas sees.
    [TestCase(CanvasShape.Line)]
    [TestCase(CanvasShape.Arrow)]
    public void ARunSelectedOnTheCanvasOffersItsEndsAndNotABox(CanvasShape shape)
    {
        var canvas = Sized();
        var run = new ShapeItem(shape, new Rect(0, 0, 100, 40), Brushes.White, 2);

        var scene = new CanvasScene();
        scene.Add(run);
        canvas.Scene = scene;
        canvas.Select(run, false);

        Assert.Multiple(() =>
        {
            Assert.That(canvas.OfferedHandles, Is.EqualTo(CanvasHandles.Body), "the canvas still draws a box round it");
            Assert.That(canvas.PointHandleAt(canvas.WorldToScreen(run.Points[0])), Is.EqualTo(0),
                "and offers no end to grab");
        });
    }

    // ...and a shape that IS a box still is one.
    [Test]
    public void ABoxSelectedOnTheCanvasStillOffersItsBox()
    {
        var canvas = Sized();
        var box = new ShapeItem(CanvasShape.Rectangle, new Rect(0, 0, 100, 40), Brushes.White, 2);

        var scene = new CanvasScene();
        scene.Add(box);
        canvas.Scene = scene;
        canvas.Select(box, false);

        Assert.Multiple(() =>
        {
            Assert.That(canvas.OfferedHandles, Is.EqualTo(CanvasHandles.All));
            Assert.That(box.Points, Is.Empty);
        });
    }

    // ...and a plain line, with no head on it, is not given the room one would have taken.
    [Test]
    public void ALineIsNotGivenRoomForAHeadItDoesNotHave()
    {
        var line = new ShapeItem(CanvasShape.Line, new Rect(0, 0, 100, 0), Brushes.White, 18);

        Assert.That(line.Bounds.Height, Is.EqualTo(18).Within(0.5), "half a thickness each side and nothing more");
    }
}
