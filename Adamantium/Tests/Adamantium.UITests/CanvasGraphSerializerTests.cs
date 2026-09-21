using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>Writing a graph out and reading it back. What the engine can say about a graph is its SHAPE - which nodes
/// there are, where they sit, what their sockets are called and which of them are joined - and what a node MEANS stays
/// the application's, carried through the file untouched.</summary>
public class CanvasGraphSerializerTests
{
    private static (InfiniteCanvas canvas, CanvasScene scene) Stage()
    {
        var canvas = new InfiniteCanvas { Mode = CanvasMode.Nodes };
        canvas.Measure(new Size(800, 600), force: true);
        canvas.Arrange(new Rect(0, 0, 800, 600));

        var scene = new CanvasScene();
        canvas.Scene = scene;

        return (canvas, scene);
    }

    // Through the node's OWN sockets, not by adding to the collection behind it: a node already has one of each, and
    // pushing more in beside them is a graph nobody drew.
    private static CanvasNode Made(string title, params string[] inputs)
    {
        var node = new CanvasNode { Title = title, Inputs = inputs.Length, Outputs = 1 };

        for (var i = 0; i < inputs.Length; i++) node.InputPins[i].Name = inputs[i];
        node.OutputPins[0].Name = "Out 1";

        return node;
    }

    // WHAT A NODE IS survives the round trip on its own, with no callback at all: the kind rides on the node, so a
    // graph comes back knowing what its nodes were even where the application that opens it builds nothing itself.
    // Without it only the SHAPE returns and every node comes back a stranger - the look restored and the behaviour not.
    [Test]
    public void ANodesKindComesBackWithIt()
    {
        var (canvas, scene) = Stage();

        var node = Made("Times two", "In 1");
        node.Kind = "math.multiply";
        scene.Add(new ElementItem(node, new Rect(0, 0, 160, 90)));

        var text = CanvasGraphSerializer.Save(canvas);

        var (other, _) = Stage();
        Assert.That(CanvasGraphSerializer.Load(other, text), Is.True);

        var back = (CanvasNode)other.ItemsHere().OfType<ElementItem>().First().Element;

        Assert.Multiple(() =>
        {
            Assert.That(back.Kind, Is.EqualTo("math.multiply"), "the node came back not knowing what it is");
            Assert.That(back.Title, Is.EqualTo("Times two"),
                "the title is a label of its own and must not be replaced by the kind");
        });
    }

    // ...and a COPY is a node of the same sort. Copied without it, a pasted node looks right and does nothing.
    [Test]
    public void ACopiedNodeIsOfTheSameKind()
    {
        var node = Made("Times two", "In 1");
        node.Kind = "math.multiply";

        var copy = new ElementItem(node, new Rect(0, 0, 160, 90)).Copy() as ElementItem;

        Assert.That(((CanvasNode)copy.Element).Kind, Is.EqualTo("math.multiply"));
    }

    // Two nodes and the wire between them, out and back: the same graph, with the ends on the same sockets.
    [Test]
    public void AGraphSurvivesBeingWrittenOutAndReadBack()
    {
        var (canvas, scene) = Stage();

        var left = new ElementItem(Made("Multiply", "In 1", "In 2"), new Rect(10, 20, 160, 90));
        var right = new ElementItem(Made("Clamp", "In 1"), new Rect(300, 140, 160, 90));
        scene.Add(left);
        scene.Add(right);

        var from = ((CanvasNode)left.Element).OutputPins[0];
        var to = ((CanvasNode)right.Element).InputPins[0];
        scene.Add(new ConnectionItem(left, from, right, to));

        var text = CanvasGraphSerializer.Save(canvas);

        var (other, _) = Stage();
        Assert.That(CanvasGraphSerializer.Load(other, text), Is.True);

        var nodes = other.ItemsHere().OfType<ElementItem>().ToList();
        var wires = other.ItemsHere().OfType<ConnectionItem>().ToList();

        Assert.Multiple(() =>
        {
            Assert.That(nodes, Has.Count.EqualTo(2));
            Assert.That(((CanvasNode)nodes[0].Element).Title, Is.EqualTo("Multiply"));
            Assert.That(nodes[0].World, Is.EqualTo(new Rect(10, 20, 160, 90)));
            Assert.That(((CanvasNode)nodes[0].Element).InputPins.Select(p => p.Name),
                Is.EqualTo(new[] { "In 1", "In 2" }));

            Assert.That(wires, Has.Count.EqualTo(1));
            Assert.That(wires[0].FromPin.Name, Is.EqualTo("Out 1"));
            Assert.That(wires[0].ToPin.Name, Is.EqualTo("In 1"));
            Assert.That(wires[0].FromItem, Is.SameAs(nodes[0]), "the wire came back on the wrong node");
            Assert.That(wires[0].ToItem, Is.SameAs(nodes[1]));
        });
    }

    // A plane has no edges, and a big graph opened at the origin with the work three screens away reads as an empty
    // document.
    [Test]
    public void TheCameraIsSavedWithTheGraph()
    {
        var (canvas, scene) = Stage();
        scene.Add(new ElementItem(Made("Multiply"), new Rect(0, 0, 160, 90)));

        canvas.Scale = 2.5;
        canvas.Offset = new Vector2(-640, 480);

        var text = CanvasGraphSerializer.Save(canvas);

        var (other, _) = Stage();
        CanvasGraphSerializer.Load(other, text);

        Assert.Multiple(() =>
        {
            Assert.That(other.Scale, Is.EqualTo(2.5).Within(0.001));
            Assert.That(other.Offset.X, Is.EqualTo(-640).Within(0.001));
            Assert.That(other.Offset.Y, Is.EqualTo(480).Within(0.001));
        });
    }

    // What a node MEANS is the application's, and the engine carries it without once looking inside.
    [Test]
    public void TheApplicationsOwnDescriptionOfANodeIsCarriedThrough()
    {
        var (canvas, scene) = Stage();
        scene.Add(new ElementItem(Made("Multiply"), new Rect(0, 0, 160, 90)));

        var text = CanvasGraphSerializer.Save(canvas, _ => ("math.multiply", "{\"operands\":2}"));

        CanvasNodeSeed asked = null;
        var (other, _) = Stage();
        CanvasGraphSerializer.Load(other, text, seed =>
        {
            asked = seed;
            return null;
        });

        Assert.Multiple(() =>
        {
            Assert.That(asked, Is.Not.Null, "the application was never asked to make its own node");
            Assert.That(asked.Kind, Is.EqualTo("math.multiply"));
            Assert.That(asked.Payload, Is.EqualTo("{\"operands\":2}"));
        });
    }

    // ...and an application that does not answer still gets the graph, drawn as the plain shape the file describes.
    // A graph saved by a program you do not have must still open and still be legible.
    [Test]
    public void AGraphWhoseNodesNobodyClaimsStillOpens()
    {
        var (canvas, scene) = Stage();
        scene.Add(new ElementItem(Made("Multiply", "In 1"), new Rect(0, 0, 160, 90)));

        var text = CanvasGraphSerializer.Save(canvas, _ => ("some.kind.you.do.not.have", "..."));

        var (other, _) = Stage();
        Assert.That(CanvasGraphSerializer.Load(other, text, _ => null), Is.True);

        var node = other.ItemsHere().OfType<ElementItem>().Single().Element as CanvasNode;

        Assert.That(node.Title, Is.EqualTo("Multiply"));
        Assert.That(node.InputPins.Select(p => p.Name), Is.EqualTo(new[] { "In 1" }));
    }

    // A socket the file names and the node does not have is a wire that is not made - never one pointed at whatever
    // socket happens to be in that position.
    [Test]
    public void AWireToASocketThatIsNotThereIsDroppedAndNotGuessed()
    {
        var (canvas, scene) = Stage();

        var left = new ElementItem(Made("Multiply"), new Rect(0, 0, 160, 90));
        var right = new ElementItem(Made("Clamp", "In 1"), new Rect(300, 0, 160, 90));
        scene.Add(left);
        scene.Add(right);
        scene.Add(new ConnectionItem(left, ((CanvasNode)left.Element).OutputPins[0],
            right, ((CanvasNode)right.Element).InputPins[0]));

        var text = CanvasGraphSerializer.Save(canvas).Replace("\"toSocket\": \"In 1\"", "\"toSocket\": \"In 9\"");

        var (other, _) = Stage();
        CanvasGraphSerializer.Load(other, text);

        Assert.That(other.ItemsHere().OfType<ConnectionItem>(), Is.Empty);
        Assert.That(other.ItemsHere().OfType<ElementItem>().Count(), Is.EqualTo(2), "the nodes went with it");
    }

    // A drawing in the same scene is not the graph's to touch: a graph is loaded INTO a canvas, not over it.
    [Test]
    public void LoadingAGraphLeavesTheDrawingAlone()
    {
        var (canvas, scene) = Stage();
        scene.Add(new ElementItem(Made("Multiply"), new Rect(0, 0, 160, 90)));
        var text = CanvasGraphSerializer.Save(canvas);

        var (other, target) = Stage();
        var ink = new ShapeItem(CanvasShape.Rectangle, new Rect(0, 0, 50, 50), Brushes.White, 2);
        target.Add(ink);

        CanvasGraphSerializer.Load(other, text);

        Assert.That(target.Items, Does.Contain(ink), "loading a graph took the drawing with it");
    }

    // A file this version cannot read changes nothing, and says so rather than half-loading.
    [TestCase("")]
    [TestCase("not json at all")]
    [TestCase("{\"version\": 9999}")]
    public void TextThisVersionCannotReadIsRefused(string text)
    {
        var (canvas, scene) = Stage();
        var kept = new ElementItem(Made("Multiply"), new Rect(0, 0, 160, 90));
        scene.Add(kept);

        Assert.That(CanvasGraphSerializer.Load(canvas, text), Is.False);
        Assert.That(scene.Items, Does.Contain(kept), "a refused file emptied the canvas anyway");
    }
}
