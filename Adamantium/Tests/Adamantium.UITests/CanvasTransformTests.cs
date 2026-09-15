using System;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
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
}
