using System.Collections.Generic;
using System.Linq;
using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>Copying, pasting and duplicating - and what those mean for a GRAPH, where a piece of one is a set of nodes
/// plus the wires that run between them. Under a theme, because where a socket sits is a fact about the node's
/// template and a wire cannot be asked anything without one.</summary>
[TestFixture]
public class CanvasCopyTests
{
    private FakeApp _app;

    [OneTimeSetUp]
    public void EnsureAppContext()
    {
        _app = new FakeApp(new AdamantiumDependencyContainer()) { ResourceManager = new ResourceManager() };
        UIAppContext.Initialize(_app, null);
    }

    [SetUp]
    public void UseFluent()
    {
        _app.ResourceManager = new ResourceManager();
        typeof(UIAppContext).GetProperty(nameof(UIAppContext.Current)).SetValue(null, _app);

        var themes = new ThemeManager(new AdamantiumDependencyContainer());
        _app.ThemeManager = themes;
        ((FakeContext)_app.UIContext).ThemeEngine = themes;

        var theme = new Adamantium.UI.Themes.FluentTheme.Fluent();
        themes.AddTheme(theme.Name, theme);
        themes.SetTheme(theme);
    }

    private static (InfiniteCanvas Canvas, CanvasScene Scene) Stage()
    {
        var canvas = new InfiniteCanvas { Mode = CanvasMode.Nodes };
        canvas.Measure(new Size(800, 600), force: true);
        canvas.Arrange(new Rect(0, 0, 800, 600));

        var scene = new CanvasScene();
        canvas.Scene = scene;

        return (canvas, scene);
    }

    private static ElementItem Node(string title, Rect world, int inputs = 1, int outputs = 1) =>
        new(new CanvasNode { Title = title, Inputs = inputs, Outputs = outputs }, world);

    private static CanvasNode Of(ElementItem item) => (CanvasNode)item.Element;

    private static int Count<T>(CanvasScene scene) => scene.Items.OfType<T>().Count();

    // A COPY and not the thing itself: what is pasted must not change when the original is edited afterwards.
    [Test]
    public void APastedNodeIsItsOwnThing()
    {
        var (canvas, scene) = Stage();
        var node = Node("Multiply", new Rect(0, 0, 190, 110));
        scene.Add(node);

        canvas.Select(node, false);
        canvas.Copy();

        Of(node).Title = "Changed after copying";
        canvas.Paste();

        var pasted = scene.Items.OfType<ElementItem>().Last();

        Assert.Multiple(() =>
        {
            Assert.That(Count<ElementItem>(scene), Is.EqualTo(2));
            Assert.That(Of(pasted).Title, Is.EqualTo("Multiply"), "the paste followed the original");
            Assert.That(pasted, Is.Not.SameAs(node));
        });
    }

    // ...put a little to one side, so it is visible that there are two of them.
    [Test]
    public void APastedCopyIsNotOnTopOfTheOriginal()
    {
        var (canvas, scene) = Stage();
        var node = Node("Multiply", new Rect(10, 20, 190, 110));
        scene.Add(node);

        canvas.Select(node, false);
        canvas.Copy();
        canvas.Paste();

        var pasted = scene.Items.OfType<ElementItem>().Last();

        Assert.That(pasted.World.X, Is.GreaterThan(node.World.X));
        Assert.That(pasted.World.Y, Is.GreaterThan(node.World.Y));
    }

    // ...and it is what is SELECTED afterwards: what you are looking at after a paste is the thing you just made.
    [Test]
    public void WhatWasPastedIsWhatIsSelected()
    {
        var (canvas, scene) = Stage();
        var node = Node("Multiply", new Rect(0, 0, 190, 110));
        scene.Add(node);

        canvas.Select(node, false);
        canvas.Copy();
        canvas.Paste();

        var pasted = scene.Items.OfType<ElementItem>().Last();

        Assert.That(canvas.Selection, Is.EqualTo(new List<ICanvasItem> { pasted }));
    }

    // THE WIRES INSIDE THE PIECE come with it. A graph is nodes AND what runs between them, and a pair of nodes pasted
    // without the wire between them is not the piece that was copied.
    [Test]
    public void TheWiresBetweenCopiedNodesComeWithThem()
    {
        var (canvas, scene) = Stage();
        var left = Node("A", new Rect(0, 0, 190, 110));
        var right = Node("B", new Rect(300, 0, 190, 110));
        scene.Add(left);
        scene.Add(right);
        scene.Add(new ConnectionItem(left, Of(left).OutputPins[0], right, Of(right).InputPins[0]));

        canvas.SelectMany(new ICanvasItem[] { left, right }, false);
        canvas.Copy();
        canvas.Paste();

        var wires = scene.Items.OfType<ConnectionItem>().ToList();
        var nodes = scene.Items.OfType<ElementItem>().ToList();

        Assert.Multiple(() =>
        {
            Assert.That(nodes, Has.Count.EqualTo(4));
            Assert.That(wires, Has.Count.EqualTo(2), "the copied pair came back unwired");
            Assert.That(wires[1].FromItem, Is.SameAs(nodes[2]), "the new wire is fastened to the old nodes");
            Assert.That(wires[1].ToItem, Is.SameAs(nodes[3]));
        });
    }

    // ...and a wire whose FAR END is not being copied is not part of the piece: it would have nowhere to arrive.
    [Test]
    public void AWireLeavingThePieceIsNotCopied()
    {
        var (canvas, scene) = Stage();
        var left = Node("A", new Rect(0, 0, 190, 110));
        var right = Node("B", new Rect(300, 0, 190, 110));
        scene.Add(left);
        scene.Add(right);
        scene.Add(new ConnectionItem(left, Of(left).OutputPins[0], right, Of(right).InputPins[0]));

        canvas.Select(left, false);
        canvas.Copy();
        canvas.Paste();

        Assert.That(Count<ConnectionItem>(scene), Is.EqualTo(1), "a wire was pasted with one end in the air");
    }

    // Pasting TWICE gives two, not one thing standing in two places.
    [Test]
    public void PastingTwiceMakesTwo()
    {
        var (canvas, scene) = Stage();
        var node = Node("Multiply", new Rect(0, 0, 190, 110));
        scene.Add(node);

        canvas.Select(node, false);
        canvas.Copy();
        canvas.Paste();
        canvas.Paste();

        var nodes = scene.Items.OfType<ElementItem>().ToList();

        Assert.That(nodes, Has.Count.EqualTo(3));
        Assert.That(nodes[1], Is.Not.SameAs(nodes[2]));
    }

    // DUPLICATING is its own gesture and must not spend what was copied an hour ago.
    [Test]
    public void DuplicatingLeavesTheClipboardAlone()
    {
        var (canvas, scene) = Stage();
        var first = Node("First", new Rect(0, 0, 190, 110));
        var second = Node("Second", new Rect(400, 0, 190, 110));
        scene.Add(first);
        scene.Add(second);

        canvas.Select(first, false);
        canvas.Copy();

        canvas.Select(second, false);
        canvas.Duplicate();

        canvas.Paste();

        var titles = scene.Items.OfType<ElementItem>().Select(e => Of(e).Title?.ToString()).ToList();

        Assert.That(titles, Does.Contain("First").And.Contain("Second"));
        Assert.That(titles.Count(t => t == "Second"), Is.EqualTo(2), "the duplicate did not happen");
        Assert.That(titles.Count(t => t == "First"), Is.EqualTo(2), "duplicating spent the clipboard");
    }

    // WHAT CANNOT BE COPIED IS LEFT BEHIND rather than pasted as a broken half. A control the application put on the
    // plane is one of those: the engine has never seen it and cannot make a second one.
    [Test]
    public void AControlTheEngineDidNotMakeIsNotCopied()
    {
        var (canvas, scene) = Stage();
        canvas.Mode = CanvasMode.Drawing;

        var button = new ElementItem(new Adamantium.UI.Controls.Buttons.Button(), new Rect(0, 0, 120, 40));
        var shape = new ShapeItem(CanvasShape.Rectangle, new Rect(0, 0, 60, 60), Brushes.White, 2);
        scene.Add(button);
        scene.Add(shape);

        canvas.SelectMany(new ICanvasItem[] { button, shape }, false);
        canvas.Copy();
        canvas.Paste();

        Assert.Multiple(() =>
        {
            Assert.That(Count<ElementItem>(scene), Is.EqualTo(1), "a control was copied that the engine cannot make");
            Assert.That(Count<ShapeItem>(scene), Is.EqualTo(2), "the shape beside it was refused too");
        });
    }

    // Copying nothing is not an error, and it does not leave the previous copy behind to be pasted by surprise.
    [Test]
    public void CopyingNothingEmptiesTheClipboard()
    {
        var (canvas, scene) = Stage();
        var node = Node("Multiply", new Rect(0, 0, 190, 110));
        scene.Add(node);

        canvas.Select(node, false);
        canvas.Copy();
        Assert.That(canvas.CanPaste, Is.True);

        canvas.ClearSelection();
        canvas.Copy();

        Assert.That(canvas.CanPaste, Is.False);
    }
}
