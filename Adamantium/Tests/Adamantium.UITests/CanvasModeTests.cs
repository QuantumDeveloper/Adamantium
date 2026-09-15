using System.Collections.Generic;
using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>A canvas is used EITHER as a drawing or as a graph, and the two do not mix. What is not of the mode in hand
/// is not drawn, not picked and not offered a tool - and it is not thrown away either: switching back brings it out
/// again untouched.</summary>
public class CanvasModeTests
{
    private static InfiniteCanvas Sized(double width = 800, double height = 600)
    {
        var canvas = new InfiniteCanvas();
        canvas.Measure(new Size(width, height), force: true);
        canvas.Arrange(new Rect(0, 0, width, height));

        return canvas;
    }

    private static (InfiniteCanvas canvas, CanvasScene scene) Stage()
    {
        var canvas = Sized();
        var scene = new CanvasScene();
        canvas.Scene = scene;

        return (canvas, scene);
    }

    private static ElementItem Node(string title, Rect world) =>
        new(new CanvasNode { Title = title }, world);

    private static bool Works(ICanvasTool tool, CanvasMode mode) => tool.WorksIn(mode);

    [Test]
    public void ACanvasIsADrawingUntilItIsToldOtherwise()
    {
        Assert.That(new InfiniteCanvas().Mode, Is.EqualTo(CanvasMode.Drawing));
    }

    // A node belongs to a graph and every other control on the plane belongs to the drawing it was put on: which of the
    // two a control is, is a fact about the control rather than a flag someone has to remember to set.
    [Test]
    public void WhatAControlIsDecidesWhichModeItBelongsTo()
    {
        // Asked through the INTERFACE, which is how the canvas asks.
        ICanvasItem node = Node("Multiply", new Rect(0, 0, 160, 90));
        ICanvasItem button = new ElementItem(new Button(), new Rect(0, 0, 100, 30));
        ICanvasItem stroke = new StrokeItem(Vector2.Zero, Brushes.White, 2);

        Assert.Multiple(() =>
        {
            Assert.That(node.Mode, Is.EqualTo(CanvasMode.Nodes));
            Assert.That(button.Mode, Is.EqualTo(CanvasMode.Drawing));
            Assert.That(stroke.Mode, Is.EqualTo(CanvasMode.Drawing));
        });
    }

    // THE one place the mode is applied. Every walk over the scene goes through here, so the eraser, the select tool and
    // the drawing all agree about what is there.
    [Test]
    public void OnlyWhatBelongsToTheModeIsOffered()
    {
        var (canvas, scene) = Stage();

        var stroke = new ShapeItem(CanvasShape.Rectangle, new Rect(0, 0, 50, 50), Brushes.White, 2);
        var node = Node("Multiply", new Rect(100, 0, 160, 90));
        scene.Add(stroke);
        scene.Add(node);

        Assert.That(canvas.ItemsHere().ToList(), Is.EqualTo(new List<ICanvasItem> { stroke }));

        canvas.Mode = CanvasMode.Nodes;

        Assert.That(canvas.ItemsHere().ToList(), Is.EqualTo(new List<ICanvasItem> { node }));
    }

    // NOT DELETED. A scene may hold both, and a mode is a way of looking at it rather than a way of emptying it.
    [Test]
    public void SwitchingModeKeepsWhatItStopsShowing()
    {
        var (canvas, scene) = Stage();

        scene.Add(new ShapeItem(CanvasShape.Rectangle, new Rect(0, 0, 50, 50), Brushes.White, 2));
        scene.Add(Node("Multiply", new Rect(100, 0, 160, 90)));

        canvas.Mode = CanvasMode.Nodes;
        canvas.Mode = CanvasMode.Drawing;

        Assert.That(scene.Items, Has.Count.EqualTo(2), "changing how you look at a scene emptied it");
        Assert.That(canvas.ItemsHere().Count(), Is.EqualTo(1));
    }

    // A frame round something the canvas no longer draws is a frame round nothing that still answers Delete.
    [Test]
    public void SwitchingModeLetsGoOfWhatWasSelected()
    {
        var (canvas, scene) = Stage();

        var stroke = new ShapeItem(CanvasShape.Rectangle, new Rect(0, 0, 50, 50), Brushes.White, 2);
        scene.Add(stroke);
        canvas.Select(stroke, false);

        Assert.That(canvas.Selection, Is.Not.Empty);

        canvas.Mode = CanvasMode.Nodes;

        Assert.That(canvas.Selection, Is.Empty, "a selection survived into a mode that cannot show it");
    }

    // A pen in a graph would draw ink into a scene the graph never shows again.
    [Test]
    public void ATooolThatDoesNotBelongIsPutDownWhenTheModeChanges()
    {
        var (canvas, _) = Stage();

        var select = new SelectTool();
        var pen = new PenTool();
        canvas.Tools.Add(select);
        canvas.Tools.Add(pen);
        canvas.DefaultTool = select;
        canvas.Tool = pen;

        canvas.Mode = CanvasMode.Nodes;

        Assert.That(canvas.Tool, Is.SameAs(select), "a pen was left in hand in a graph");
    }

    // ...and one that belongs everywhere stays. Select, move and delete mean the same thing whatever is on the plane.
    [Test]
    public void ATooolThatBelongsEverywhereStaysInHand()
    {
        var (canvas, _) = Stage();

        var select = new SelectTool();
        canvas.Tools.Add(select);
        canvas.Tool = select;

        canvas.Mode = CanvasMode.Nodes;

        Assert.That(canvas.Tool, Is.SameAs(select));
    }

    [Test]
    public void EachToolSaysWhereItBelongs()
    {
        // Asked through the INTERFACE, which is how the rail asks: the answer is a default member, and calling it on
        // the concrete type proves the concrete type and nothing about what the canvas sees.
        Assert.Multiple(() =>
        {
            Assert.That(Works(new SelectTool(), CanvasMode.Nodes), Is.True, "selecting works on anything");
            Assert.That(Works(new SelectTool(), CanvasMode.Drawing), Is.True);

            Assert.That(Works(new PenTool(), CanvasMode.Nodes), Is.False);
            Assert.That(Works(new EraseTool(), CanvasMode.Nodes), Is.False);
            Assert.That(Works(new TextTool(), CanvasMode.Nodes), Is.False);
            Assert.That(Works(new ShapeTool(CanvasShape.Rectangle), CanvasMode.Nodes), Is.False);
            Assert.That(Works(new CurveTool(CanvasCurve.Bezier), CanvasMode.Nodes), Is.False);
        });
    }

    // The tool that puts a control on the plane is the one tool used by BOTH - it makes a button in a drawing and a node
    // in a graph - so it is told which, rather than the engine building a control to find out.
    [Test]
    public void TheControlToolIsToldWhichModeItIsFor()
    {
        var buttons = new ElementTool(() => new Button());
        var nodes = new ElementTool(() => new CanvasNode()) { Mode = CanvasMode.Nodes };

        Assert.Multiple(() =>
        {
            Assert.That(Works(buttons, CanvasMode.Drawing), Is.True);
            Assert.That(Works(buttons, CanvasMode.Nodes), Is.False);
            Assert.That(Works(nodes, CanvasMode.Nodes), Is.True);
            Assert.That(Works(nodes, CanvasMode.Drawing), Is.False);
        });
    }
}
