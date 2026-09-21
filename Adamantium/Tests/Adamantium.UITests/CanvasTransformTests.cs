using System;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>Turning and leaning: ONE transform and not two tools, about the middle of the item's own box.</summary>
public class CanvasTransformTests
{
    private static InfiniteCanvas Sized(double width = 400, double height = 300)
    {
        var canvas = new InfiniteCanvas();
        canvas.Measure(new Size(width, height), force: true);
        canvas.Arrange(new Rect(0, 0, width, height));

        return canvas;
    }

    private static ShapeItem Box() =>
        new(CanvasShape.Rectangle, new Rect(-50, -25, 100, 50), Brushes.White, 1,
            new SolidColorBrush(Colors.White));

    // Nothing said is nothing done - and everything that costs something is skipped on that answer.
    [Test]
    public void ATransformThatSaysNothingChangesNothing()
    {
        var none = CanvasTransform.None;
        var point = new Vector2(7, -3);

        Assert.Multiple(() =>
        {
            Assert.That(none.IsSomething, Is.False);
            Assert.That(none.Apply(point, Vector2.Zero), Is.EqualTo(point));
        });
    }

    // A quarter turn clockwise: the plane's Y runs down the screen, so (1,0) goes to (0,1).
    [Test]
    public void AQuarterTurnGoesClockwise()
    {
        var turned = new CanvasTransform(90).Apply(new Vector2(1, 0), Vector2.Zero);

        Assert.Multiple(() =>
        {
            Assert.That(turned.X, Is.EqualTo(0).Within(1e-9));
            Assert.That(turned.Y, Is.EqualTo(1).Within(1e-9));
        });
    }

    // About the MIDDLE it is given: a point at the centre of a turn does not move.
    [Test]
    public void TheMiddleOfATurnStaysStill()
    {
        var about = new Vector2(120, -80);

        Assert.That(new CanvasTransform(37, 12, -4).Apply(about, about), Is.EqualTo(about));
    }

    // Undo is the exact inverse, skew included and in the right order - the whole reason hit-testing can ask a turned
    // shape a question by un-turning the point rather than by learning about angles.
    [TestCase(30d, 0d, 0d)]
    [TestCase(0d, 20d, 0d)]
    [TestCase(0d, 0d, -15d)]
    [TestCase(-72d, 11d, 23d)]
    public void UndoTakesAPointBackExactly(double angle, double skewX, double skewY)
    {
        var transform = new CanvasTransform(angle, skewX, skewY);
        var about = new Vector2(5, -9);
        var point = new Vector2(41, 17);

        var there = transform.Apply(point, about);
        var back = transform.Undo(there, about);

        Assert.Multiple(() =>
        {
            Assert.That(back.X, Is.EqualTo(point.X).Within(1e-9));
            Assert.That(back.Y, Is.EqualTo(point.Y).Within(1e-9));
        });
    }

    // BOUNDS stay the box of the shape UNTURNED. A box that grew as a shape turned would make resizing it a different
    // size every time it was let go.
    [Test]
    public void TurningDoesNotChangeTheBounds()
    {
        var shape = Box();
        var before = shape.Bounds;

        shape.Angle = 45;

        Assert.That(shape.Bounds, Is.EqualTo(before));
    }

    // ...and the hit test follows the shape, not the box: a point that was inside before the turn is outside after it
    // if the shape has turned away from it.
    [Test]
    public void ATurnedShapeIsHitWhereItNowIs()
    {
        var shape = new ShapeItem(CanvasShape.Rectangle, new Rect(-50, -10, 100, 20), Brushes.White, 1,
            new SolidColorBrush(Colors.White));

        Assert.That(shape.HitTest(new Vector2(40, 0), 0.5), Is.True, "along the long side, before the turn");

        shape.Angle = 90;

        Assert.Multiple(() =>
        {
            Assert.That(shape.HitTest(new Vector2(40, 0), 0.5), Is.False, "the shape has turned away from it");
            Assert.That(shape.HitTest(new Vector2(0, 40), 0.5), Is.True, "and towards this");
        });
    }

    // One number at a time, because a transform is a struct and a struct cannot be written half at a time - the same
    // reason the corner radii are four properties.
    [Test]
    public void EachNumberCanBeWrittenOnItsOwn()
    {
        var shape = Box();

        shape.Angle = 10;
        shape.SkewX = 20;
        shape.SkewY = 30;

        Assert.That(shape.Transform, Is.EqualTo(new CanvasTransform(10, 20, 30)));
    }

    // A frame round SEVERAL things is never turned: their turns are not one turn, and a box that pretended otherwise
    // would lie about every one of them.
    [Test]
    public void AGripOfATurnedShapeIsFoundWhereItIsDrawn()
    {
        var canvas = Sized();
        var scene = new CanvasScene();
        var shape = Box();

        scene.Add(shape);
        canvas.Scene = scene;
        canvas.Select(shape, false);
        shape.Angle = 90;

        // The top-left corner of the unturned box, turned a quarter clockwise about the middle, lands where the
        // top-right was.
        var corner = canvas.WorldToScreen(new Vector2(25, -50));

        Assert.That(canvas.HandleAt(corner), Is.EqualTo(CanvasHandle.TopLeft));
    }

    // The frame stands OFF what it is round by half a grip, so the grips sit against the selection instead of half
    // inside it - over a node's own filled edge that half was simply invisible.
    [Test]
    public void TheFrameStandsOffTheSelectionByHalfAGrip()
    {
        var canvas = Sized();
        var scene = new CanvasScene();
        var shape = Box();

        scene.Add(shape);
        canvas.Scene = scene;
        canvas.Select(shape, false);

        var box = canvas.ToScreen(shape.Bounds);
        var frame = canvas.FrameOf(shape.Bounds);

        Assert.Multiple(() =>
        {
            Assert.That(frame.X, Is.EqualTo(box.X - canvas.HandleSize / 2).Within(0.01));
            Assert.That(frame.Y, Is.EqualTo(box.Y - canvas.HandleSize / 2).Within(0.01));
            Assert.That(frame.Width, Is.EqualTo(box.Width + canvas.HandleSize).Within(0.01));
            Assert.That(frame.Height, Is.EqualTo(box.Height + canvas.HandleSize).Within(0.01));
        });
    }

    // ...and a grip is aimed at where it is DRAWN. The two come from one place, so moving the frame cannot leave the
    // grips being hunted for at the old box.
    [Test]
    public void AGripIsFoundAtTheFrameAndNotAtTheBox()
    {
        var canvas = Sized();
        var scene = new CanvasScene();
        var shape = Box();

        scene.Add(shape);
        canvas.Scene = scene;
        canvas.Select(shape, false);

        var frame = canvas.FrameOf(shape.Bounds);

        Assert.Multiple(() =>
        {
            Assert.That(canvas.HandleAt(new Vector2(frame.X, frame.Y)), Is.EqualTo(CanvasHandle.TopLeft));
            Assert.That(canvas.HandleAt(new Vector2(frame.X + frame.Width, frame.Y + frame.Height)),
                Is.EqualTo(CanvasHandle.BottomRight));
        });
    }

    private static Vector2 Through(Matrix4x4F matrix, Vector2 point)
    {
        var moved = Vector3F.TransformCoordinate(new Vector3F((float)point.X, (float)point.Y, 0), matrix);

        return new Vector2(moved.X, moved.Y);
    }

    // THE MATRIX THE PIXELS GO THROUGH IS THIS SAME TRANSFORM. A shape is drawn by a matrix and reasoned about point by
    // point - the frame round it, the grips, the hit test - so the two have to be one arithmetic. They were not: the
    // matrix sheared x and y from the ORIGINAL numbers and the point-by-point walk sheared y by the NEW x, which agrees
    // while only one lean is set and walks apart the moment both are.
    [TestCase(-117d, 19.1665d, 40.8334d)]
    [TestCase(0d, 25d, 0d)]
    [TestCase(0d, 0d, 25d)]
    [TestCase(30d, 0d, 0d)]
    [TestCase(-45d, 10d, 7d)]
    public void TheMatrixAgreesWithThePointByPoint(double angle, double skewX, double skewY)
    {
        var about = new Vector2(100, 60);
        var turn = new CanvasTransform(angle, skewX, skewY);
        var matrix = turn.Matrix(about);

        foreach (var corner in new[]
                 {
                     new Vector2(40, 20), new Vector2(160, 20), new Vector2(160, 100), new Vector2(40, 100)
                 })
        {
            var expected = turn.Apply(corner, about);
            var actual = Through(matrix, corner);

            Assert.Multiple(() =>
            {
                Assert.That(actual.X, Is.EqualTo(expected.X).Within(0.001),
                    $"the drawn corner {corner} is not where the frame puts it");
                Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(0.001),
                    $"the drawn corner {corner} is not where the frame puts it");
            });
        }
    }

    // ...AND THE ENGINE'S OWN TRANSFORM LEANS THE SAME WAY. A hosted control is turned by that one and the frame round
    // it by this one, so two conventions for a lean would be a control and its frame walking apart.
    [Test]
    public void TheEngineSTransformLeansTheSameWay()
    {
        var turn = new CanvasTransform(0, 19.1665, 40.8334);
        var ours = turn.Matrix(Vector2.Zero);

        var theirs = new Transform { SkewX = turn.SkewX, SkewY = turn.SkewY };

        var point = new Vector2(150, 30);
        var expected = Through((Matrix4x4F)theirs.Matrix, point);
        var actual = Through(ours, point);

        Assert.Multiple(() =>
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(0.001), "the two leans do not agree");
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(0.001), "the two leans do not agree");
        });
    }
}
