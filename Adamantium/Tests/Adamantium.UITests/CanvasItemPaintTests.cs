using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>WHAT AN ITEM IS PAINTED WITH IS ITS OWN. The canvas's <see cref="InfiniteCanvas.Ink"/> is the SETTING a
/// tool draws with - the colour in hand - and a thing left on the plane keeps the colour it was made with, whatever
/// happens to the setting or to its neighbours afterwards.</summary>
public class CanvasItemPaintTests
{
    private static (InfiniteCanvas Canvas, CanvasScene Scene) Stage(Brush ink)
    {
        var canvas = new InfiniteCanvas { Ink = ink, InkThickness = 2 };
        canvas.Measure(new Size(800, 600), force: true);
        canvas.Arrange(new Rect(0, 0, 800, 600));

        var scene = new CanvasScene();
        canvas.Scene = scene;

        return (canvas, scene);
    }

    private static CanvasPointerEventArgs At(double x, double y) =>
        new()
        {
            World = new Vector2(x, y),
            Pointer = new Vector2(x, y),
            Screen = new Vector2(x, y),
            Button = MouseButtons.Left,
            ClickCount = 1
        };

    private static void Draw(InfiniteCanvas canvas, ICanvasTool tool, Vector2 from, Vector2 to)
    {
        tool.OnPressed(canvas, At(from.X, from.Y));
        tool.OnMoved(canvas, At(to.X, to.Y));
        tool.OnReleased(canvas, At(to.X, to.Y));
    }

    // TWO THINGS DRAWN WITH ONE COLOUR IN HAND are two things, each with its own paint: recolouring one must not
    // recolour the other. The inspector writes a colour INTO the brush an item holds - that is what makes a shared
    // brush repaint everything painting with it - so a stroke and a shape handed the same brush object are one colour
    // for ever after.
    [Test]
    public void EachThingDrawnKeepsAPaintOfItsOwn()
    {
        var ink = new SolidColorBrush(Colors.Red);
        var (canvas, scene) = Stage(ink);

        Draw(canvas, new PenTool(), new Vector2(0, 0), new Vector2(40, 40));
        Draw(canvas, new ShapeTool(CanvasShape.Rectangle), new Vector2(100, 100), new Vector2(200, 160));

        var stroke = (StrokeItem)scene.Items[0];
        var shape = (ShapeItem)scene.Items[1];

        Assert.Multiple(() =>
        {
            Assert.That(stroke.Brush, Is.Not.SameAs(shape.Stroke), "the stroke and the shape share one brush");
            Assert.That(stroke.Brush, Is.Not.SameAs(ink), "the stroke was given the canvas's own setting to hold");
            Assert.That(shape.Stroke, Is.Not.SameAs(ink), "the shape was given the canvas's own setting to hold");
        });

        // ...AND THE PROOF IN THE ONLY TERMS THAT MATTER: a colour written into one, as the inspector writes it.
        ((SolidColorBrush)shape.Stroke).Color = Colors.Blue;

        Assert.Multiple(() =>
        {
            Assert.That(((SolidColorBrush)stroke.Brush).Color, Is.EqualTo(Colors.Red),
                "recolouring the shape recoloured the stroke");
            Assert.That(((SolidColorBrush)canvas.Ink).Color, Is.EqualTo(Colors.Red),
                "recolouring the shape recoloured the colour in hand");
        });
    }

    // A COPY IS A SECOND THING, paint included. Duplicating handed the copy the original's brushes, so the first
    // colour written into either of them repainted both - and pasting is exactly when a person goes on to recolour.
    [Test]
    public void ACopyDoesNotShareItsPaintWithWhatItWasCopiedFrom()
    {
        var (canvas, scene) = Stage(new SolidColorBrush(Colors.Red));

        Draw(canvas, new ShapeTool(CanvasShape.Rectangle), new Vector2(0, 0), new Vector2(80, 50));

        var first = (ShapeItem)scene.Items[0];

        canvas.SelectMany(new ICanvasItem[] { first }, false);
        canvas.Duplicate();

        var copy = (ShapeItem)scene.Items[1];

        Assert.That(copy.Stroke, Is.Not.SameAs(first.Stroke), "the copy shares the original's outline brush");

        ((SolidColorBrush)copy.Stroke).Color = Colors.Blue;

        Assert.That(((SolidColorBrush)first.Stroke).Color, Is.EqualTo(Colors.Red),
            "recolouring the copy recoloured what it was copied from");
    }

    // ...and the FILL of a shape is its own too, for the same reason.
    [Test]
    public void AShapeSFillIsItsOwnAsWell()
    {
        var ink = new SolidColorBrush(Colors.Red);
        var fill = new SolidColorBrush(Colors.Green);
        var (canvas, scene) = Stage(ink);

        canvas.ShapeFill = fill;

        Draw(canvas, new ShapeTool(CanvasShape.Rectangle), new Vector2(0, 0), new Vector2(80, 50));
        Draw(canvas, new ShapeTool(CanvasShape.Rectangle), new Vector2(200, 0), new Vector2(280, 50));

        var first = (ShapeItem)scene.Items[0];
        var second = (ShapeItem)scene.Items[1];

        Assert.That(first.Fill, Is.Not.SameAs(second.Fill), "two shapes share one fill");

        ((SolidColorBrush)first.Fill).Color = Colors.Yellow;

        Assert.Multiple(() =>
        {
            Assert.That(((SolidColorBrush)second.Fill).Color, Is.EqualTo(Colors.Green),
                "filling one shape filled the other");
            Assert.That(((SolidColorBrush)canvas.ShapeFill).Color, Is.EqualTo(Colors.Green),
                "filling one shape changed the fill in hand");
        });
    }
}
