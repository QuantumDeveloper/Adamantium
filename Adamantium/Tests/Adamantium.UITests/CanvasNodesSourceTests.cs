using System.Linq;
using Adamantium.Core.Collections;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Core;
using Adamantium.UITests.Graph;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>The graph is the application's own collection of nodes, and the canvas keeps no second account of it:
/// what appears in the collection appears on the plane, what moves on the plane is written back.</summary>
public class CanvasNodesSourceTests
{
    private static (InfiniteCanvas canvas, CanvasScene scene, TrackingCollection<ICanvasNode> nodes) Stage()
    {
        var canvas = new InfiniteCanvas { Mode = CanvasMode.Nodes };
        canvas.Measure(new Size(800, 600), force: true);
        canvas.Arrange(new Rect(0, 0, 800, 600));

        var scene = new CanvasScene();
        canvas.Scene = scene;

        var nodes = new TrackingCollection<ICanvasNode>();
        canvas.Nodes = nodes;

        return (canvas, scene, nodes);
    }

    private static GraphNode Made(string kind, double x, double y, int inputs = 1, int outputs = 1)
    {
        var node = new GraphNode { Kind = kind, Title = kind, Left = x, Top = y };

        for (var i = 0; i < inputs; i++) node.Inputs.Add(new GraphSocket { Name = $"In {i + 1}" });

        // An output BRANCHES - one value can feed several inputs - which is what says so.
        for (var i = 0; i < outputs; i++)
        {
            node.Outputs.Add(new GraphSocket(GraphNode.Branches) { Name = $"Out {i + 1}" });
        }

        return node;
    }

    // What an application's catalogue of kinds is: a word, and something that makes the inside of a node of that sort.
    private sealed class Sort : ICanvasNodeKind
    {
        private readonly int _inputs;
        private readonly int _outputs;

        public Sort(string kind, int inputs = 1, int outputs = 1)
        {
            Kind = kind;
            _inputs = inputs;
            _outputs = outputs;
        }

        public string Kind { get; }

        public string Title => Kind;

        public Color? Accent { get; init; }

        public ICanvasNodeSpecialization Create() => new Body(Kind, _inputs, _outputs);

        // A WHOLE NODE of this kind: the canvas has no node type of its own, so whoever offers kinds offers the nodes.
        public ICanvasNode Make()
        {
            var made = new GraphNode { Kind = Kind, Title = Title, Accent = Accent };

            made.Specialization = Create();

            return made;
        }
    }

    private sealed class Body : ICanvasNodeSpecialization
    {
        private readonly int _inputs;
        private readonly int _outputs;

        public Body(string of, int inputs, int outputs)
        {
            Of = of;
            _inputs = inputs;
            _outputs = outputs;
        }

        public string Of { get; }

        public void Shape(ICanvasNode node)
        {
            GraphNode.Fit(node.Inputs, _inputs, "In");
            GraphNode.Fit(node.Outputs, _outputs, "Out");
        }
    }

    private static CanvasNode Shown(InfiniteCanvas canvas, ICanvasNode model) =>
        canvas.ItemsHere().OfType<ElementItem>()
            .Select(item => item.Element as CanvasNode)
            .FirstOrDefault(node => node != null && node.Title == (model.Title ?? string.Empty));

    // A node put in the collection appears on the plane, wearing what the model says about it.
    [Test]
    public void ANodeAddedToTheCollectionAppearsOnThePlane()
    {
        var (canvas, _, nodes) = Stage();

        nodes.Add(Made("Lerp", 120, 40, inputs: 3, outputs: 1));

        var shown = Shown(canvas, nodes[0]);

        Assert.Multiple(() =>
        {
            Assert.That(shown, Is.Not.Null, "the collection has a node and the plane has none");
            Assert.That(shown.Kind, Is.EqualTo("Lerp"));
            Assert.That(shown.InputPins, Has.Count.EqualTo(3), "the sockets the model describes");
            Assert.That(shown.InputPins[0].Name, Is.EqualTo("In 1"));
        });
    }

    // ...and one taken out of it leaves.
    [Test]
    public void ANodeRemovedFromTheCollectionLeavesThePlane()
    {
        var (canvas, _, nodes) = Stage();
        var model = Made("Lerp", 0, 0);

        nodes.Add(model);
        Assert.That(Shown(canvas, model), Is.Not.Null, "it never arrived, so this proves nothing");

        nodes.Remove(model);

        Assert.That(canvas.ItemsHere().OfType<ElementItem>(), Is.Empty, "the node is gone from the data and still shown");
    }

    // The MODEL is where the place is kept: moved on the plane, it is written back - otherwise the collection is a
    // display and not the account of the graph.
    [Test]
    public void DraggingANodeWritesItsPlaceBackIntoTheModel()
    {
        var (canvas, scene, nodes) = Stage();
        var model = Made("Lerp", 10, 20);
        nodes.Add(model);

        var item = canvas.ItemsHere().OfType<ElementItem>().Single();
        item.Move(new Vector2(40, 15));
        scene.Touch();

        Assert.Multiple(() =>
        {
            Assert.That(model.Left, Is.EqualTo(50).Within(0.01),
                "the node was dragged and the graph still says it is where it was");
            Assert.That(model.Top, Is.EqualTo(35).Within(0.01));
        });
    }

    // ...and the other way round: moved in the model, it moves on the plane.
    [Test]
    public void MovingANodeInTheModelMovesItOnThePlane()
    {
        var (canvas, _, nodes) = Stage();
        var model = Made("Lerp", 10, 20);
        nodes.Add(model);

        model.Left = 300;
        model.Top = 200;

        var item = canvas.ItemsHere().OfType<ElementItem>().Single();

        Assert.Multiple(() =>
        {
            Assert.That(item.World.X, Is.EqualTo(300).Within(0.01));
            Assert.That(item.World.Y, Is.EqualTo(200).Within(0.01));
        });
    }

    // A node made ON THE PLANE joins the graph by joining the COLLECTION - that is the only way anything gets onto it.
    [Test]
    public void ANodeMadeOnThePlaneJoinsTheCollection()
    {
        var (canvas, _, nodes) = Stage();

        // A CANVAS TOLD NOTHING ABOUT KINDS MAKES NOTHING: a node is the application's object, and the control has none
        // of its own to reach for - the same way an items control has no item type.
        Assert.That(canvas.AddNode("Lerp", new Vector2(120, 80)), Is.Null,
            "the canvas invented a node of its own");
        Assert.That(nodes, Is.Empty);

        canvas.NodeKinds = new TrackingCollection<ICanvasNodeKind> { new Sort("Lerp") };

        var made = canvas.AddNode("Lerp", new Vector2(120, 80));

        Assert.Multiple(() =>
        {
            Assert.That(nodes, Has.Count.EqualTo(1), "the node was put on the plane and the graph does not have it");
            Assert.That(nodes[0], Is.SameAs(made));
            Assert.That(made.Kind, Is.EqualTo("Lerp"));
            Assert.That(made.Left, Is.EqualTo(120).Within(0.01));
            Assert.That(Shown(canvas, made), Is.Not.Null, "it is in the graph and not on the plane");
        });
    }

    // ...and what it is MADE of comes from the catalogue the application handed over - the shell is the engine's, the
    // inside is the kind's. No factory is given to the control: the kinds are data.
    [Test]
    public void TheCatalogueSaysWhatANodeIsMadeOf()
    {
        var (canvas, _, nodes) = Stage();
        canvas.NodeKinds = new TrackingCollection<ICanvasNodeKind> { new Sort("Lerp", 3, 1) };

        canvas.AddNode("Lerp", Vector2.Zero);

        Assert.Multiple(() =>
        {
            Assert.That(nodes[0].Specialization, Is.TypeOf<Body>(), "the node has no inside");
            Assert.That(nodes[0].Inputs, Has.Count.EqualTo(3), "the specialization never shaped it");
        });
    }

    // A wire made between two sockets is a wire on the plane. Nothing is added to a collection: joining the sockets IS
    // making it.
    [Test]
    public void AWireBetweenTwoSocketsIsDrawnBetweenThem()
    {
        var (canvas, _, nodes) = Stage();

        var left = Made("Add", 0, 0);
        var right = Made("Lerp", 300, 0, inputs: 2);
        nodes.Add(left);
        nodes.Add(right);

        _ = new CanvasConnection(left.Outputs[0], right.Inputs[1]);

        var drawn = canvas.ItemsHere().OfType<ConnectionItem>().SingleOrDefault();

        Assert.Multiple(() =>
        {
            Assert.That(drawn, Is.Not.Null, "the graph has a wire and the plane has none");
            Assert.That(drawn.ToPin.Name, Is.EqualTo("In 2"), "it landed on the wrong socket");
        });
    }

    // A SOCKET ADDED AFTER ONE WAS TAKEN OUT OF THE MIDDLE GETS A NAME NOBODY HAS. Counting what is there and adding
    // one repeats a name the moment a middle socket goes - two sockets called "In 5", which no graph can be read from
    // and no file can say which of them a wire sat on.
    [Test]
    public void ASocketAddedAfterOneWasTakenOutGetsAFreeName()
    {
        var node = Made("Mix", 0, 0, inputs: 5);

        node.Inputs.RemoveAt(2);
        GraphNode.Fit(node.Inputs, 5, "In");

        var names = new System.Collections.Generic.List<string>();
        foreach (var socket in node.Inputs) names.Add(socket.Name);

        Assert.That(names, Is.Unique, "two sockets on one side answer to the same name");
    }

    // A NODE ARRIVING LEAVES THE OTHERS ALONE. The layer that holds the controls used to be emptied and filled again
    // whenever the set changed, so every node that was staying was taken off the plane and put back - detached,
    // re-attached, recorded from nothing. What is drawn does not survive that round trip: one node added blanked every
    // node already there until something walked the whole scene again.
    [Test]
    public void AddingANodeDoesNotTakeTheOthersOffThePlane()
    {
        var layer = new CanvasElementLayer { Owner = new InfiniteCanvas() };

        var first = new ElementItem(new CanvasNode(), new Rect(0, 0, 190, 110));
        var second = new ElementItem(new CanvasNode(), new Rect(300, 0, 190, 110));

        layer.Sync([first, second]);

        var left = Watch(layer, out var count);

        layer.Sync([first, second, new ElementItem(new CanvasNode(), new Rect(600, 0, 190, 110))]);

        Assert.Multiple(() =>
        {
            Assert.That(left.Count, Is.Zero, "the nodes already on the plane were taken off it and put back");
            Assert.That(count(), Is.EqualTo(3), "the new node never arrived");
        });
    }

    // ...and one LEAVING takes only itself.
    [Test]
    public void RemovingANodeLeavesTheOthersWhereTheyAre()
    {
        var layer = new CanvasElementLayer { Owner = new InfiniteCanvas() };

        var going = new ElementItem(new CanvasNode(), new Rect(0, 0, 190, 110));
        var staying = new ElementItem(new CanvasNode(), new Rect(300, 0, 190, 110));

        layer.Sync([going, staying]);

        var left = Watch(layer, out var count);

        layer.Sync([staying]);

        Assert.Multiple(() =>
        {
            Assert.That(left, Has.Count.EqualTo(1), "what left the plane was not exactly the node that went");
            Assert.That(left, Does.Contain(going.Element), "the node that was staying was taken off the plane");
            Assert.That(count(), Is.EqualTo(1));
        });
    }

    // ...and one that only changes PLACE in the drawn order moves, while the rest stay as they are.
    [Test]
    public void ReorderingTouchesOnlyWhatMoved()
    {
        var layer = new CanvasElementLayer { Owner = new InfiniteCanvas() };

        var first = new ElementItem(new CanvasNode(), new Rect(0, 0, 190, 110));
        var second = new ElementItem(new CanvasNode(), new Rect(300, 0, 190, 110));
        var third = new ElementItem(new CanvasNode(), new Rect(600, 0, 190, 110));

        layer.Sync([first, second, third]);

        var left = Watch(layer, out var count);

        // The last one brought to the front, which is what raising a node does.
        layer.Sync([third, first, second]);

        Assert.Multiple(() =>
        {
            Assert.That(left, Is.EqualTo(new[] { third.Element }), "more than the node that moved was taken off");
            Assert.That(count(), Is.EqualTo(3));
            Assert.That(layer.Children[0], Is.SameAs(third.Element), "the node did not end up where it was put");
        });
    }

    // What LEAVES the layer, in the order it leaves - and how many are left at the end.
    private static System.Collections.Generic.List<object> Watch(CanvasElementLayer layer, out System.Func<int> count)
    {
        var left = new System.Collections.Generic.List<object>();

        layer.Children.CollectionChanged += (_, e) =>
        {
            if (e.OldItems == null) return;

            foreach (var gone in e.OldItems) left.Add(gone);
        };

        var children = layer.Children;
        count = () => children.Count;

        return left;
    }

    // A SOCKET KEEPS ITS OWN NAME WHEN IT MOVES. The pins are fitted to the sockets by position and each is BOUND to
    // the one it stands for - both ways, so a name typed into a pin reaches the socket. Which makes the default name a
    // pin is born with dangerous: written where a binding writes, the pin hands it back to whatever socket it is next
    // fitted to, and a node whose sockets are called A, B, Amount comes back from a move called In 1, In 2, In 3.
    [Test]
    public void MovingASocketDoesNotRenameAnything()
    {
        var (canvas, _, nodes) = Stage();

        var model = Made("Mix", 0, 0, inputs: 4);
        model.Inputs[0].Name = "A";
        model.Inputs[1].Name = "B";
        model.Inputs[2].Name = "Amount";
        nodes.Add(model);

        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();

        var node = (CanvasNode)canvas.ItemsHere().OfType<ElementItem>().Single().Element;

        var before = new System.Collections.Generic.List<string>();
        foreach (var socket in model.Inputs) before.Add(socket.Name);
        foreach (var pin in node.InputPins) before.Add("/" + pin.Name);

        model.Inputs.Move(3, 0);

        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();

        var sockets = new System.Collections.Generic.List<string>();
        foreach (var socket in model.Inputs) sockets.Add(socket.Name);

        var pins = new System.Collections.Generic.List<string>();
        foreach (var pin in node.InputPins) pins.Add(pin.Name);

        Assert.Multiple(() =>
        {
            Assert.That(before, Is.EqualTo(new[] { "A", "B", "Amount", "In 4", "/A", "/B", "/Amount", "/In 4" }),
                "the sockets and their pins do not even agree before the move");
            Assert.That(sockets, Is.EqualTo(new[] { "In 4", "A", "B", "Amount" }), "the sockets were renamed by a move");
            Assert.That(pins, Is.EqualTo(sockets), "the pins say something other than the sockets they stand for");
        });
    }

    // A SOCKET WITH A WIRE ON IT IS FILLED IN, and one with none is hollow - which is the only way to tell, on a node
    // with eight sockets, which of them are in use. Not a flag anybody sets: it is read off the socket's own wires, so
    // cutting one empties it again.
    [Test]
    public void ASocketWithAWireOnItIsFilledIn()
    {
        var (canvas, _, nodes) = Stage();

        var left = Made("Add", 0, 0);
        var right = Made("Lerp", 300, 0, inputs: 2);
        nodes.Add(left);
        nodes.Add(right);

        var wire = new CanvasConnection(left.Outputs[0], right.Inputs[1]);

        var from = (CanvasNode)canvas.ItemsHere().OfType<ElementItem>().First().Element;
        var to = (CanvasNode)canvas.ItemsHere().OfType<ElementItem>().Last().Element;

        Assert.Multiple(() =>
        {
            Assert.That(from.OutputPins[0].IsConnected, Is.True, "the end the wire leaves was left hollow");
            Assert.That(to.InputPins[1].IsConnected, Is.True, "the end it arrives at was left hollow");
            Assert.That(to.InputPins[0].IsConnected, Is.False, "an empty socket was filled in");
        });

        // ...and what a taken socket is filled WITH is its own colour, which is the pin's own doing.
        to.InputPins[1].Color = Adamantium.UI.Core.Media.Brushes.Red;
        Assert.That(to.InputPins[1].Fill, Is.SameAs(to.InputPins[1].Color), "a taken socket with an unpainted middle");

        wire.Disconnect();

        Assert.Multiple(() =>
        {
            Assert.That(from.OutputPins[0].IsConnected, Is.False, "the socket stayed filled after the wire was cut");
            Assert.That(to.InputPins[1].Fill, Is.Null, "the middle stayed painted after the wire was cut");
        });
    }

    // A SOCKET MOVED ALONG ITS OWN SIDE TAKES ITS WIRES WITH IT. One change to the list and not a socket taken out and
    // put back: taken out, it is for that moment not on the node at all - the pins are fitted to what is there, so one
    // is destroyed, and the wires are rebuilt against a node one socket short.
    [Test]
    public void MovingASocketKeepsTheWiresOnIt()
    {
        var (canvas, _, nodes) = Stage();

        var left = Made("Add", 0, 0);
        var right = Made("Lerp", 300, 0, inputs: 3);
        nodes.Add(left);
        nodes.Add(right);

        var third = right.Inputs[2];
        var first = right.Inputs[0];

        // BOTH ENDS OF THE SWAP are wired: the socket that moves and the one it changes places with. An output branches,
        // so one of them can feed both.
        _ = new CanvasConnection(left.Outputs[0], third);
        _ = new CanvasConnection(left.Outputs[0], first);

        right.Inputs.Move(2, 0);

        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();

        var node = (CanvasNode)canvas.ItemsHere().OfType<ElementItem>().Last().Element;
        var drawn = canvas.ItemsHere().OfType<ConnectionItem>().ToList();

        Assert.Multiple(() =>
        {
            Assert.That(right.Inputs[0], Is.SameAs(third), "the socket did not move");
            Assert.That(right.Inputs[1], Is.SameAs(first), "the socket it changed places with did not move");
            Assert.That(third.Connections, Has.Count.EqualTo(1), "the socket lost its wire on the way");
            Assert.That(first.Connections, Has.Count.EqualTo(1), "the one it swapped with lost its wire");
            Assert.That(left.Outputs[0].Connections, Has.Count.EqualTo(2), "the other end lost them");
            Assert.That(drawn, Has.Count.EqualTo(2), "a wire left the plane");

            // ...and BOTH sockets are still SHOWN as taken. Whether one is filled in is read off its own wires, and a
            // move does not touch those - so a socket that comes out of one hollow is drawn as free with a wire on it.
            Assert.That(node.InputPins[0].IsConnected, Is.True, "the socket that moved came out drawn as empty");
            Assert.That(node.InputPins[1].IsConnected, Is.True,
                "the socket it changed places with came out drawn as empty");
            Assert.That(node.InputPins[2].IsConnected, Is.False, "a socket with no wire was filled in");
        });
    }

    // ...and cut, it leaves the plane - and both sockets, which is the wire's own doing.
    [Test]
    public void ACutWireLeavesThePlaneAndBothSockets()
    {
        var (canvas, _, nodes) = Stage();

        var left = Made("Add", 0, 0);
        var right = Made("Lerp", 300, 0);
        nodes.Add(left);
        nodes.Add(right);

        var wire = new CanvasConnection(left.Outputs[0], right.Inputs[0]);
        wire.Disconnect();

        Assert.Multiple(() =>
        {
            Assert.That(canvas.ItemsHere().OfType<ConnectionItem>(), Is.Empty);
            Assert.That(left.Outputs[0].Connections, Is.Empty, "it is still sitting on the end it left");
            Assert.That(right.Inputs[0].Connections, Is.Empty);
        });
    }

    // A wire DRAWN on the plane joins the sockets themselves: the graph is the account of itself, and the gesture that
    // made the wire knows nothing about the application's objects.
    [Test]
    public void AWireDrawnOnThePlaneJoinsTheSockets()
    {
        var (canvas, scene, nodes) = Stage();

        var left = Made("Add", 0, 0);
        var right = Made("Lerp", 300, 0, inputs: 2);
        nodes.Add(left);
        nodes.Add(right);

        var items = canvas.ItemsHere().OfType<ElementItem>().ToList();
        var from = (CanvasNode)items[0].Element;
        var to = (CanvasNode)items[1].Element;

        scene.Add(new ConnectionItem(items[0], from.OutputPins[0], items[1], to.InputPins[1]));
        scene.Touch();

        Assert.Multiple(() =>
        {
            Assert.That(left.Outputs[0].Connections, Has.Count.EqualTo(1),
                "a wire was drawn and the graph does not know about it");
            Assert.That(right.Inputs[1].Connections, Has.Count.EqualTo(1),
                "it was written down against the wrong socket");
            Assert.That(left.Outputs[0].Connections[0].ToNode, Is.SameAs(right));
        });
    }

    // ...and one cut on the plane leaves them.
    [Test]
    public void AWireCutOnThePlaneLeavesTheSockets()
    {
        var (canvas, scene, nodes) = Stage();

        var left = Made("Add", 0, 0);
        var right = Made("Lerp", 300, 0);
        nodes.Add(left);
        nodes.Add(right);

        _ = new CanvasConnection(left.Outputs[0], right.Inputs[0]);

        var drawn = canvas.ItemsHere().OfType<ConnectionItem>().Single();
        scene.Remove(drawn);
        scene.Touch();

        Assert.That(right.Inputs[0].Connections, Is.Empty, "the wire was cut and the graph still holds it");
    }

    // THE POINT OF ALL OF IT: the graph is walkable, and upstream - "what feeds my inputs" - is the walk a node editor
    // actually makes. It costs reading one socket's wires, not a sweep over the graph.
    [Test]
    public void TheGraphWalksUpstreamFromASocket()
    {
        var (_, _, nodes) = Stage();

        var source = Made("Texture", 0, 0);
        var sink = Made("Output", 300, 0);
        nodes.Add(source);
        nodes.Add(sink);

        _ = new CanvasConnection(source.Outputs[0], sink.Inputs[0]);

        var feeding = sink.Inputs[0].Connections[0].FromNode;

        Assert.That(feeding, Is.SameAs(source), "the wire cannot say where it comes from");
    }

    // An input takes ONE wire: a second one displaces the first, which is what dropping a wire on a taken input means.
    [Test]
    public void ASecondWireIntoAnInputDisplacesTheFirst()
    {
        var (_, _, nodes) = Stage();

        var first = Made("Add", 0, 0);
        var second = Made("Noise", 0, 200);
        var sink = Made("Output", 300, 0);
        nodes.Add(first);
        nodes.Add(second);
        nodes.Add(sink);

        _ = new CanvasConnection(first.Outputs[0], sink.Inputs[0]);
        _ = new CanvasConnection(second.Outputs[0], sink.Inputs[0]);

        Assert.Multiple(() =>
        {
            Assert.That(sink.Inputs[0].Connections, Has.Count.EqualTo(1));
            Assert.That(sink.Inputs[0].Connections[0].FromNode, Is.SameAs(second));
            Assert.That(first.Outputs[0].Connections, Is.Empty, "the displaced wire is still on the end it left");
        });
    }

    // ...and an input that says it takes TWO takes two, and loses its oldest on the third. Many-to-one is a number on
    // the socket, so a graph that needs it says so instead of being told by the engine that it cannot.
    [Test]
    public void AnInputTakesAsManyWiresAsItSaysItDoes()
    {
        var (_, _, nodes) = Stage();

        var first = Made("Add", 0, 0);
        var second = Made("Noise", 0, 200);
        var third = Made("Texture", 0, 400);
        var sink = Made("Output", 300, 0);
        nodes.Add(first);
        nodes.Add(second);
        nodes.Add(third);
        nodes.Add(sink);

        sink.Inputs[0].Capacity = 2;

        _ = new CanvasConnection(first.Outputs[0], sink.Inputs[0]);
        _ = new CanvasConnection(second.Outputs[0], sink.Inputs[0]);

        Assert.That(sink.Inputs[0].Connections, Has.Count.EqualTo(2), "it said two and took one");

        _ = new CanvasConnection(third.Outputs[0], sink.Inputs[0]);

        Assert.Multiple(() =>
        {
            Assert.That(sink.Inputs[0].Connections, Has.Count.EqualTo(2), "it said two and took three");
            Assert.That(sink.Inputs[0].Connections[0].FromNode, Is.SameAs(second), "the wrong wire was displaced");
        });
    }

    // ...and the same when the wires are DRAWN rather than made: what the socket says has to reach the gesture, or the
    // rule lives twice and the one in the canvas wins. It did - "one wire per input" was written into the gesture, so a
    // socket told to take five still took one.
    [Test]
    public void AJoinOnThePlaneObeysWhatTheSocketTakes()
    {
        var (canvas, scene, nodes) = Stage();

        var first = Made("Add", 0, 0);
        var second = Made("Noise", 0, 200);
        var sink = Made("Output", 300, 0);
        nodes.Add(first);
        nodes.Add(second);
        nodes.Add(sink);

        sink.Inputs[0].Capacity = 5;
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();

        var into = canvas.ItemOf(sink);
        var pin = ((CanvasNode)into.Element).InputPins[0];

        canvas.Join(canvas.ItemOf(first), ((CanvasNode)canvas.ItemOf(first).Element).OutputPins[0], into, pin);
        canvas.Join(canvas.ItemOf(second), ((CanvasNode)canvas.ItemOf(second).Element).OutputPins[0], into, pin);

        Assert.Multiple(() =>
        {
            Assert.That(ConnectionItem.CountInto(scene, pin), Is.EqualTo(2), "the second line was cut as it was drawn");
            Assert.That(sink.Inputs[0].Connections, Has.Count.EqualTo(2), "the second wire displaced the first anyway");
        });
    }

    // ...and a socket that takes ONE still loses its wire to the next one drawn, which is what dropping a wire on a
    // taken input means everywhere.
    [Test]
    public void AJoinOnAFullSocketDisplacesTheWireItHeld()
    {
        var (canvas, scene, nodes) = Stage();

        var first = Made("Add", 0, 0);
        var second = Made("Noise", 0, 200);
        var sink = Made("Output", 300, 0);
        nodes.Add(first);
        nodes.Add(second);
        nodes.Add(sink);

        var into = canvas.ItemOf(sink);
        var pin = ((CanvasNode)into.Element).InputPins[0];

        canvas.Join(canvas.ItemOf(first), ((CanvasNode)canvas.ItemOf(first).Element).OutputPins[0], into, pin);
        canvas.Join(canvas.ItemOf(second), ((CanvasNode)canvas.ItemOf(second).Element).OutputPins[0], into, pin);

        Assert.Multiple(() =>
        {
            // ON THE PLANE as well as in the graph: the model displaces on its own, so asking it alone would pass with
            // the rule missing from the canvas and two lines left drawn into one socket.
            Assert.That(ConnectionItem.CountInto(scene, pin), Is.EqualTo(1), "it takes one and two are drawn into it");
            Assert.That(sink.Inputs[0].Connections, Has.Count.EqualTo(1), "it takes one and is holding two");
            Assert.That(sink.Inputs[0].Connections[0].FromNode, Is.SameAs(second));
        });
    }

    // Told it takes fewer than it holds, a socket lets the oldest go - it says what it holds, so it cannot be left
    // holding more than it says.
    [Test]
    public void LoweringWhatASocketTakesDropsTheOldestWires()
    {
        var (_, _, nodes) = Stage();

        var first = Made("Add", 0, 0);
        var second = Made("Noise", 0, 200);
        var sink = Made("Output", 300, 0);
        nodes.Add(first);
        nodes.Add(second);
        nodes.Add(sink);

        sink.Inputs[0].Capacity = 0;

        _ = new CanvasConnection(first.Outputs[0], sink.Inputs[0]);
        _ = new CanvasConnection(second.Outputs[0], sink.Inputs[0]);

        sink.Inputs[0].Capacity = 1;

        Assert.Multiple(() =>
        {
            Assert.That(sink.Inputs[0].Connections, Has.Count.EqualTo(1));
            Assert.That(first.Outputs[0].Connections, Is.Empty, "the dropped wire is still on the end it left");
        });
    }

    // An OUTPUT branches: one value feeding two inputs is the ordinary case, not a displacement.
    [Test]
    public void AnOutputFeedsAsManyInputsAsItIsGiven()
    {
        var (_, _, nodes) = Stage();

        var source = Made("Texture", 0, 0);
        var one = Made("Output", 300, 0);
        var two = Made("Output", 300, 200);
        nodes.Add(source);
        nodes.Add(one);
        nodes.Add(two);

        _ = new CanvasConnection(source.Outputs[0], one.Inputs[0]);
        _ = new CanvasConnection(source.Outputs[0], two.Inputs[0]);

        Assert.That(source.Outputs[0].Connections, Has.Count.EqualTo(2));
    }

    // A PIN TAKES ITS COLOUR FROM WHAT FLOWS THROUGH IT, read off the catalogue - so every socket carrying the same
    // thing looks the same, and nothing on a socket says what shade it is.
    private sealed class Carries : ICanvasSocketKind
    {
        public Carries(string kind, Color? color)
        {
            Kind = kind;
            Color = color;
        }

        public string Kind { get; }

        public Color? Color { get; }
    }

    [Test]
    public void APinIsColouredByWhatItCarries()
    {
        var (canvas, _, nodes) = Stage();
        canvas.SocketKinds = new TrackingCollection<ICanvasSocketKind>
        {
            new Carries(string.Empty, null),
            new Carries("Number", Colors.Yellow)
        };

        var model = Made("Add", 0, 0);
        model.Inputs[0].Kind = "Number";
        nodes.Add(model);

        var node = (CanvasNode)canvas.ItemsHere().OfType<ElementItem>().Single().Element;

        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();

        Assert.Multiple(() =>
        {
            Assert.That(node.InputPins[0].Color, Is.TypeOf<Adamantium.UI.Core.Media.SolidColorBrush>(),
                "what the socket carries never reached the pin");
            Assert.That(((Adamantium.UI.Core.Media.SolidColorBrush)node.InputPins[0].Color).Color,
                Is.EqualTo(Colors.Yellow));
            Assert.That(node.OutputPins[0].Color, Is.Null, "a socket that carries anything was painted anyway");
        });
    }

    // A PRESS ON SOMETHING ALREADY THERE is about that thing. The node tool put a new node on top of the one that was
    // clicked, which is the one thing a press on a node cannot mean.
    [Test]
    public void TheNodeToolDoesNotPutANodeOnTopOfOne()
    {
        var (canvas, _, nodes) = Stage();
        canvas.NodeKinds = new TrackingCollection<ICanvasNodeKind> { new Sort("Add") };

        var select = new SelectTool();
        canvas.DefaultTool = select;

        var tool = new NodeTool();
        canvas.Tool = tool;

        var model = Made("Add", 0, 0);
        nodes.Add(model);

        var item = canvas.ItemsHere().OfType<ElementItem>().Single();
        var middle = new Vector2(item.World.X + item.World.Width / 2, item.World.Y + item.World.Height / 2);

        tool.OnPressed(canvas, new CanvasPointerEventArgs
        {
            World = middle,
            Pointer = middle,
            Button = Adamantium.UI.Core.Input.MouseButtons.Left,
            ClickCount = 1
        });

        Assert.Multiple(() =>
        {
            Assert.That(canvas.IsPaletteOpen, Is.False, "it offered to make a node on top of the one pressed");
            Assert.That(nodes, Has.Count.EqualTo(1));
            Assert.That(canvas.Selection, Has.Count.EqualTo(1), "the press did not take hold of what it landed on");
        });
    }

    // A COLOUR is a value on the node and a brush on the strip, and the brush is made per node: written into a shared
    // one, a node's colour took the theme's accent with it and recoloured everything else wearing it.
    [Test]
    public void ANodeSColourIsAValueOnItAndABrushOnTheStrip()
    {
        var (canvas, _, nodes) = Stage();
        var model = Made("Add", 0, 0);
        nodes.Add(model);

        var node = (CanvasNode)canvas.ItemsHere().OfType<ElementItem>().Single().Element;

        Assert.That(node.Accent, Is.Null, "a node nobody coloured was painted anyway");

        model.Accent = Colors.Red;
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();

        Assert.That(node.Accent, Is.TypeOf<Adamantium.UI.Core.Media.SolidColorBrush>(), "the colour never reached the strip");
        Assert.That(((Adamantium.UI.Core.Media.SolidColorBrush)node.Accent).Color, Is.EqualTo(Colors.Red));
    }

    // A node LEAVING the graph takes its wires, both ends of each - nothing is left pointing at a node nobody holds.
    [Test]
    public void ANodeRemovedTakesItsWires()
    {
        var (_, _, nodes) = Stage();

        var source = Made("Texture", 0, 0);
        var sink = Made("Output", 300, 0);
        nodes.Add(source);
        nodes.Add(sink);

        _ = new CanvasConnection(source.Outputs[0], sink.Inputs[0]);

        nodes.Remove(source);

        Assert.That(sink.Inputs[0].Connections, Is.Empty, "the far end still holds a wire from a node that is gone");
    }

    // The list of kinds opens where a wire was let go over nothing, and PICKING is the answer: writing a kind puts that
    // node on the plane and closes the list. What the wire does when it lands is asked where nodes really lay out -
    // sockets are template parts, and a theme is what gives a node one (CanvasConnectionTests).
    [Test]
    public void PickingAKindPutsThatNodeOnThePlane()
    {
        var (canvas, _, nodes) = Stage();
        var lerp = new Sort("Lerp");
        canvas.NodeKinds = new TrackingCollection<ICanvasNodeKind> { new Sort("Add"), lerp };
        canvas.PaletteAt = new Vector2(40, 60);

        canvas.IsPaletteOpen = true;
        canvas.PickedKind = lerp;

        Assert.Multiple(() =>
        {
            Assert.That(canvas.IsPaletteOpen, Is.False, "picking is the answer, so the list closes");
            Assert.That(nodes, Has.Count.EqualTo(1), "the picked node never joined the graph");
            Assert.That(nodes[0].Kind, Is.EqualTo("Lerp"));
            Assert.That(canvas.PickedKind, Is.Null, "the pick is spent - left standing, the next open answers itself");
        });
    }

    // What a node IS can change without the node being a different node: the kind is data, so the wires, the selection
    // and the undo step it is part of all survive it.
    [Test]
    public void ChangingTheKindKeepsTheSameNode()
    {
        var (canvas, _, nodes) = Stage();
        var model = Made("Add", 0, 0);
        nodes.Add(model);

        var before = canvas.ItemsHere().OfType<ElementItem>().Single();

        model.Kind = "Lerp";
        model.Title = "Lerp";

        // What the container shows is BOUND to the node, and a source change is applied once per frame - which a layout
        // pass does and a test has to ask for.
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();

        var after = canvas.ItemsHere().OfType<ElementItem>().Single();

        Assert.Multiple(() =>
        {
            Assert.That(after, Is.SameAs(before), "the node was rebuilt for a change of kind");
            Assert.That(((CanvasNode)after.Element).Kind, Is.EqualTo("Lerp"));
        });
    }

    // TOLD IT IS NOW A TEXTURE, a node becomes one: the catalogue says what the word means, the specialization is
    // THE SAME KIND PICKED TWICE PUTS TWO NODES ON THE PLANE. A list is answered by being picked FROM, so the canvas
    // lets go of the choice the moment it has acted on it - and the list has to let go with it, or the second pick of
    // the same row is no change at all and the canvas never hears it.
    [Test]
    public void PickingTheSameKindTwicePutsTwoNodesOnThePlane()
    {
        var (canvas, _, nodes) = Stage();
        canvas.NodeKinds = new TrackingCollection<ICanvasNodeKind> { new Sort("Color") };

        var list = new TreeView { ItemsSource = canvas.NodeKinds };
        list.SetBinding(TreeView.SelectedItemProperty,
            new Adamantium.UI.Core.Data.Binding(nameof(InfiniteCanvas.PickedKind))
            {
                Source = canvas,
                Mode = Adamantium.UI.Core.Data.BindingMode.TwoWay
            });

        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();

        canvas.AskForNode(new Vector2(0, 0));
        Pick(list);

        Assert.Multiple(() =>
        {
            Assert.That(nodes, Has.Count.EqualTo(1), "picking a kind put nothing on the plane");
            Assert.That(canvas.PickedKind, Is.Null, "the canvas is still holding a pick it has acted on");
            Assert.That(list.SelectedItem, Is.Null, "the list kept a pick the canvas has already answered");
        });

        canvas.AskForNode(new Vector2(200, 200));
        Pick(list);

        Assert.That(nodes, Has.Count.EqualTo(2), "the same kind picked a second time put nothing on the plane");
    }

    // Taking the FIRST row of a list, the way a click on it does - through the tree's own selection, which is what the
    // canvas is listening to. Home rather than a step, because a step goes to the NEXT row and a click goes to the one
    // under it however many times in a row it lands there - which is the whole point here.
    private static void Pick(TreeView list)
    {
        list.RaiseEvent(new Adamantium.UI.Core.Input.KeyEventArgs(
            Adamantium.UI.Core.Input.KeyboardDevice.CurrentDevice, Adamantium.UI.Core.Input.Key.Home,
            Adamantium.UI.Core.Input.InputModifiers.None, 0)
        {
            RoutedEvent = Adamantium.UI.Core.Input.Keyboard.KeyDownEvent
        });

        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
    }

    // replaced and reshapes the node, and what the container draws follows.
    [Test]
    public void ChangingTheKindReplacesWhatTheNodeIs()
    {
        var (canvas, _, nodes) = Stage();
        canvas.NodeKinds = new TrackingCollection<ICanvasNodeKind>
        {
            new Sort("Add", 2, 1),
            new Sort("Output", 1, 0)
        };

        var model = Made("Add", 0, 0, inputs: 2);
        nodes.Add(model);

        var node = (CanvasNode)canvas.ItemsHere().OfType<ElementItem>().Single().Element;

        model.Kind = "Output";

        // What the container shows is BOUND to the node, and a source change is applied once per frame - which a layout
        // pass does and a test has to ask for.
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();

        Assert.Multiple(() =>
        {
            Assert.That(((Body)model.Specialization).Of, Is.EqualTo("Output"), "it is still what it was");
            Assert.That(model.Inputs, Has.Count.EqualTo(1), "the new specialization never reshaped it");
            Assert.That(model.Outputs, Is.Empty);
            Assert.That(node.Content, Is.SameAs(model.Specialization), "the container draws what the node no longer is");
        });
    }
}
