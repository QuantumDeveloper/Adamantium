using System;
using Adamantium.Core.Collections;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UITests.Graph;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>EMPTYING THE PLANE. The one action most worth being able to take back, and the one that touches every
/// bookkeeping there is at once: the scene, the graph, the objects, the wires and the history.</summary>
public class CanvasClearTests
{
    private static (InfiniteCanvas Canvas, CanvasScene Scene, TrackingCollection<ICanvasNode> Nodes) Stage()
    {
        var canvas = new InfiniteCanvas();
        canvas.Measure(new Size(800, 600), force: true);
        canvas.Arrange(new Rect(0, 0, 800, 600));

        var scene = new CanvasScene();
        var nodes = new TrackingCollection<ICanvasNode>();

        canvas.Scene = scene;
        canvas.Nodes = nodes;

        return (canvas, scene, nodes);
    }

    private static GraphNode Node(TrackingCollection<ICanvasNode> nodes, double left)
    {
        var node = new GraphNode { Left = left, Width = 160 };

        node.InputCount = 1;
        node.OutputCount = 1;

        nodes.Add(node);

        return node;
    }

    /// <summary>A GRAPH WITH WIRES ON IT, emptied in one go. Every wire is cut by the same sweep that takes the nodes
    /// away, so this is where the scene answering back about its own bookkeeping would go round for ever.</summary>
    [Test]
    [Timeout(15000)]
    public void ClearingAGraphEndsInsteadOfHanging()
    {
        var (canvas, scene, nodes) = Stage();

        var first = Node(nodes, 0);
        var second = Node(nodes, 300);

        _ = new CanvasConnection(first.Outputs[0], second.Inputs[0]);

        Assert.That(scene.Items.Count, Is.GreaterThan(0), "nothing was put on the plane at all");

        scene.Clear();

        Assert.That(scene.Items.Count, Is.EqualTo(0));
    }

    /// <summary>...and through the HISTORY, which is how the page actually does it: a snapshot before, the change, a
    /// snapshot after.</summary>
    [Test]
    [Timeout(15000)]
    public void ClearingThroughTheHistoryEndsInsteadOfHanging()
    {
        var (canvas, scene, nodes) = Stage();

        var first = Node(nodes, 0);
        var second = Node(nodes, 300);

        _ = new CanvasConnection(first.Outputs[0], second.Inputs[0]);

        canvas.History.Record(scene, "Clear", scene.Clear);

        Assert.That(scene.Items.Count, Is.EqualTo(0));
    }

    /// <summary>WITH THE DRAWING SIDE ON THE PLANE TOO - objects the application holds as data, which is the state the
    /// page is actually in. Their containers go with the same sweep while the application still holds the objects.
    /// </summary>
    [Test]
    [Timeout(15000)]
    public void ClearingAPlaneHoldingObjectsEndsInsteadOfHanging()
    {
        var (canvas, scene, nodes) = Stage();

        var objects = new TrackingCollection<ICanvasObject>();
        canvas.Objects = objects;

        objects.Add(new PlacedObject
        {
            Left = 10, Top = 20, Width = 100, Height = 60,
            Content = new PlacedShape(CanvasShape.Rectangle)
        });

        objects.Add(new PlacedObject { Left = 200, Top = 20, Width = 100, Height = 60, Content = "plain" });

        Node(nodes, 0);

        canvas.History.Record(scene, "Clear", scene.Clear);

        Assert.That(scene.Items.Count, Is.EqualTo(0));
    }

    /// <summary>CLEARED WHILE SOMETHING IS SELECTED - which is the ordinary way it happens, because a person selects,
    /// looks at what they have, and then decides to empty the plane. The frame, its grips and the selection itself all
    /// point at things that are no longer there.</summary>
    [Test]
    [Timeout(15000)]
    public void ClearingWithASelectionEndsInsteadOfHanging()
    {
        var (canvas, scene, nodes) = Stage();

        var objects = new TrackingCollection<ICanvasObject>();
        canvas.Objects = objects;

        objects.Add(new PlacedObject
        {
            Left = 10, Top = 20, Width = 100, Height = 60,
            Content = new PlacedShape(CanvasShape.Rectangle)
        });

        var first = Node(nodes, 0);
        var second = Node(nodes, 300);

        _ = new CanvasConnection(first.Outputs[0], second.Inputs[0]);

        canvas.SelectMany(new System.Collections.Generic.List<ICanvasItem>(scene.Items), false);

        Assert.That(canvas.Selection.Count, Is.GreaterThan(0), "nothing got selected, so this proves nothing");

        canvas.History.Record(scene, "Clear", scene.Clear);

        Assert.Multiple(() =>
        {
            Assert.That(scene.Items.Count, Is.EqualTo(0));
            Assert.That(canvas.Selection.Count, Is.EqualTo(0), "the selection still holds things that are gone");
        });
    }

    /// <summary>A SELECTION SURVIVES AN EDIT. Letting go of what has left the plane must not let go of what is still on
    /// it - an inspector writes a colour, the scene is told something changed, and if the selection evaporated there
    /// the row it was writing through has nothing left to write to.</summary>
    [Test]
    [Timeout(15000)]
    public void AnEditDoesNotDropTheSelection()
    {
        var (canvas, scene, nodes) = Stage();

        var objects = new TrackingCollection<ICanvasObject>();
        canvas.Objects = objects;

        objects.Add(new PlacedObject
        {
            Left = 10, Top = 20, Width = 100, Height = 60,
            Content = new PlacedShape(CanvasShape.Rectangle)
        });

        var node = Node(nodes, 0);

        canvas.SelectMany(new System.Collections.Generic.List<ICanvasItem>(scene.Items), false);

        var held = canvas.Selection.Count;

        Assert.That(held, Is.GreaterThan(0), "nothing got selected, so this proves nothing");

        // WHAT AN INSPECTOR DOES when a value is written: the drawing changed, so the plane is told.
        scene.Touch();

        Assert.That(canvas.Selection.Count, Is.EqualTo(held), "the selection was dropped by an ordinary edit");
    }

    /// <summary>...and a child INSIDE A GROUP is still selected. A group's children leave the scene's own list, so
    /// anything asking the scene "is this still there" is told no about something that plainly is.</summary>
    [Test]
    [Timeout(15000)]
    public void ASelectedChildOfAGroupIsNotForgotten()
    {
        var (canvas, scene, nodes) = Stage();

        var first = new ShapeItem(CanvasShape.Rectangle, new Rect(0, 0, 60, 40), Brushes.White, 2);
        var second = new ShapeItem(CanvasShape.Rectangle, new Rect(100, 0, 60, 40), Brushes.White, 2);

        scene.Add(first);
        scene.Add(second);

        canvas.SelectMany(new System.Collections.Generic.List<ICanvasItem> { first, second }, false);
        canvas.GroupSelection();

        canvas.SelectMany(new System.Collections.Generic.List<ICanvasItem> { first }, false);

        Assert.That(canvas.Selection.Count, Is.EqualTo(1), "a child of a group could not be selected at all");

        scene.Touch();

        Assert.That(canvas.Selection.Count, Is.EqualTo(1), "the child was forgotten because the scene does not hold it");
    }

    /// <summary>And what the graph says afterwards: a plane with nothing on it is a graph with no wires. A node whose
    /// wire was cut by the sweep must not be left saying it still has one.</summary>
    [Test]
    [Timeout(15000)]
    public void ClearingCutsTheWiresOutOfTheGraph()
    {
        var (canvas, scene, nodes) = Stage();

        var first = Node(nodes, 0);
        var second = Node(nodes, 300);

        _ = new CanvasConnection(first.Outputs[0], second.Inputs[0]);

        scene.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(first.Outputs[0].Connections.Count, Is.EqualTo(0));
            Assert.That(second.Inputs[0].Connections.Count, Is.EqualTo(0));
        });
    }
}
