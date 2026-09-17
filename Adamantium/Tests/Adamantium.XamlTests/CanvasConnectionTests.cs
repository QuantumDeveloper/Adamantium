using System;
using System.Collections.Generic;
using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Resources;
using Adamantium.UITests.Graph;
using Adamantium.UITests.Rendering;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>The WIRE between two nodes. Under a theme, because both its ends are sockets and where a socket sits is a
/// fact about the node's template - so a wire cannot be asked anything at all without one.</summary>
[TestFixture]
public class CanvasConnectionTests
{
    private FakeApp _app;

    [OneTimeSetUp]
    public void EnsureAppContext()
    {
        _app = new FakeApp(new AdamantiumDependencyContainer()) { ResourceManager = new ResourceManager() };
        UIAppContext.Initialize(_app, null);
    }

    private void Use(Theme theme)
    {
        _app.ResourceManager = new ResourceManager();
        typeof(UIAppContext).GetProperty(nameof(UIAppContext.Current)).SetValue(null, _app);

        var themes = new ThemeManager(new AdamantiumDependencyContainer());
        _app.ThemeManager = themes;
        ((FakeContext)_app.UIContext).ThemeEngine = themes;

        themes.AddTheme(theme.Name, theme);
        themes.SetTheme(theme);
    }

    private static Theme ThemeNamed(string name) => name switch
    {
        "MacOs" => new Adamantium.UI.Themes.MacOsTheme.MacOs(),
        "EditorPro" => new Adamantium.UI.Themes.EditorProTheme.EditorPro(),
        _ => new Adamantium.UI.Themes.FluentTheme.Fluent()
    };

    // Two nodes side by side, laid out the way they are on the plane - with a camera, because a layer without one
    // arranges nothing and everything inside a node then has no size.
    private (ElementItem Left, ElementItem Right, CanvasElementLayer Layer, Window Window) Graph()
    {
        var left = new ElementItem(new CanvasNode { Title = "A", Inputs = 1, Outputs = 2 }, new Rect(0, 0, 190, 110));
        var right = new ElementItem(new CanvasNode { Title = "B", Inputs = 2, Outputs = 1 }, new Rect(300, 40, 190, 110));

        var layer = new CanvasElementLayer { Owner = new InfiniteCanvas() };
        layer.Sync(new List<ElementItem> { left, right });

        var window = new Window { Width = 800, Height = 600, Content = layer };
        for (var i = 0; i < 6; i++)
        {
            Adamantium.UI.Extensions.WindowExtension.UpdateTree(window);
            Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        }

        return (left, right, layer, window);
    }

    private static CanvasNode Node(ElementItem item) => (CanvasNode)item.Element;

    private static void Settle(Window window)
    {
        for (var i = 0; i < 6; i++)
        {
            Adamantium.UI.Extensions.WindowExtension.UpdateTree(window);
            Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        }
    }

    // A wire is held by what it JOINS. Its ends are where those two sockets are, in world units - not a pair of points
    // it was given and remembers.
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void AWireEndsOnTheTwoSocketsItJoins(string theme)
    {
        Use(ThemeNamed(theme));
        var (left, right, _, _) = Graph();

        var wire = new ConnectionItem(left, Node(left).OutputPins[0], right, Node(right).InputPins[1]);

        Assert.That(wire.Ends(out var from, out var to), Is.True, "the wire cannot find its own ends");

        var output = Node(left).Where(Node(left).OutputPins[0]).Value;
        var input = Node(right).Where(Node(right).InputPins[1]).Value;

        Assert.Multiple(() =>
        {
            Assert.That(from.X, Is.EqualTo(left.World.X + output.X).Within(0.5));
            Assert.That(from.Y, Is.EqualTo(left.World.Y + output.Y).Within(0.5));
            Assert.That(to.X, Is.EqualTo(right.World.X + input.X).Within(0.5));
            Assert.That(to.Y, Is.EqualTo(right.World.Y + input.Y).Within(0.5));
        });
    }

    // ...so moving a node moves the wire, and nobody has to tell it. This is the whole reason it holds sockets rather
    // than points: a wire that remembered where it was would have to be found and corrected on every drag.
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void MovingANodeMovesTheWireWithoutTellingIt(string theme)
    {
        Use(ThemeNamed(theme));
        var (left, right, _, _) = Graph();

        var wire = new ConnectionItem(left, Node(left).OutputPins[0], right, Node(right).InputPins[0]);
        wire.Ends(out var was, out _);

        right.Move(new Vector2(0, 90));
        left.Move(new Vector2(40, 0));

        wire.Ends(out var now, out var arrives);

        Assert.Multiple(() =>
        {
            Assert.That(now.X, Is.EqualTo(was.X + 40).Within(0.5), "the end on the node that moved sideways");
            Assert.That(arrives.Y, Is.EqualTo(right.World.Y + Node(right).Where(Node(right).InputPins[0]).Value.Y)
                .Within(0.5));
        });
    }

    // A wire has NO frame. Both its ends are held by what they join, so a box round it would offer a drag that means
    // nothing and fight the one that means something - moving a node.
    [Test]
    public void AWireOffersNoGrips()
    {
        Use(ThemeNamed("Fluent"));
        var (left, right, _, _) = Graph();

        var wire = new ConnectionItem(left, Node(left).OutputPins[0], right, Node(right).InputPins[0]);

        Assert.That(wire.Handles, Is.EqualTo(CanvasHandles.None));
    }

    // The box has to hold the BEND. A wire leaves its socket sideways, so its belly is outside the straight line
    // between the ends - and a box taken from the ends alone culls the wire while the part you can see is on screen.
    [Test]
    public void TheBoxHoldsTheBendAndNotJustTheEnds()
    {
        Use(ThemeNamed("Fluent"));
        var (left, right, _, _) = Graph();

        // Right to LEFT: the wire has to loop back on itself, which is where the bend leaves the ends furthest behind.
        var wire = new ConnectionItem(right, Node(right).OutputPins[0], left, Node(left).InputPins[0]);

        wire.Ends(out var from, out var to);

        Assert.Multiple(() =>
        {
            Assert.That(wire.Bounds.Right, Is.GreaterThan(Math.Max(from.X, to.X)), "the loop out of the output");
            Assert.That(wire.Bounds.X, Is.LessThan(Math.Min(from.X, to.X)), "and the loop into the input");
        });
    }

    // A wire is drawn as INK - the points handed over as data - and NOT as a geometry. Drawn as a geometry it cost the
    // frame and cost more the longer a drag went on: a geometry is cached by its content, a wire being pulled about has
    // different content every frame, and that cache is never emptied and is walked once per frame. So every frame of a
    // drag left an entry behind and every frame after walked all of them.
    [Test]
    public void AWireIsPaintedAsInkAndBuildsNoGeometry()
    {
        Use(ThemeNamed("Fluent"));
        var (left, right, _, _) = Graph();

        var canvas = new InfiniteCanvas();
        canvas.Measure(new Size(800, 600), force: true);
        canvas.Arrange(new Rect(0, 0, 800, 600));

        var session = new RecordingDrawingSession();
        var wire = new ConnectionItem(left, Node(left).OutputPins[0], right, Node(right).InputPins[0]);

        wire.Render(session, canvas);

        Assert.Multiple(() =>
        {
            Assert.That(session.Geometries, Is.Empty, "the wire was built as a mesh");
            Assert.That(session.Rectangles, Has.Count.EqualTo(1), "nothing reached the ink pass");
            Assert.That(session.Rectangles[0].Brush, Is.InstanceOf<InkBrush>());
        });
    }

    // ...and it SAYS when it has moved. The points are handed over by reference and rewritten in place, so without a
    // word the paint has nothing to notice and the wire stays baked where it first was.
    [Test]
    public void MovingAWireSaysSoOnItsBrush()
    {
        Use(ThemeNamed("Fluent"));
        var (left, right, _, _) = Graph();

        var canvas = new InfiniteCanvas();
        canvas.Measure(new Size(800, 600), force: true);
        canvas.Arrange(new Rect(0, 0, 800, 600));

        var session = new RecordingDrawingSession();
        var wire = new ConnectionItem(left, Node(left).OutputPins[0], right, Node(right).InputPins[0]);

        wire.Render(session, canvas);
        var was = ((InkBrush)session.Rectangles[0].Brush).Revision;

        right.Move(new Vector2(60, 0));
        wire.Render(session, canvas);

        Assert.That(((InkBrush)session.Rectangles[1].Brush).Revision, Is.GreaterThan(was));
    }

    // A wire is pulled with WHATEVER IS IN HAND. There is no tool to switch to - the socket itself is the offer, which
    // is what every node editor does, and a mode entered twenty times a minute is a mode left on by mistake.
    [Test]
    public void AWireIsPulledWithTheSelectToolInHand()
    {
        Use(ThemeNamed("Fluent"));
        var (left, right, _, _) = Graph();

        var canvas = Canvas(left, right);
        var select = (SelectTool)canvas.Tool;

        var from = At(left, Node(left).OutputPins[0]);
        var to = At(right, Node(right).InputPins[0]);

        Assert.That(select.Wires.Press(canvas, from), Is.True, "a press on a socket was not taken");
        Assert.That(select.Wires.IsBusy, Is.True);

        select.Wires.Release(canvas, to);

        Assert.Multiple(() =>
        {
            Assert.That(canvas.Scene.ItemsIn(new Rect(-4000, -4000, 8000, 8000)),
                Has.Some.InstanceOf<ConnectionItem>(), "no wire was made");
            Assert.That(Node(left).OutputPins[0].IsConnected, Is.True, "the socket it left is still shown empty");
            Assert.That(Node(right).InputPins[0].IsConnected, Is.True);
            Assert.That(select.Wires.IsBusy, Is.False);
        });
    }

    // A press that is NOT on a socket is not taken: the tool has to go on picking things up, or a node could never be
    // dragged with the same button that wires it.
    [Test]
    public void APressThatMissesASocketIsLeftToTheTool()
    {
        Use(ThemeNamed("Fluent"));
        var (left, right, _, _) = Graph();

        var canvas = Canvas(left, right);
        var select = (SelectTool)canvas.Tool;

        var middle = new Vector2(left.World.X + left.World.Width / 2, left.World.Y + left.World.Height / 2);

        Assert.Multiple(() =>
        {
            Assert.That(select.Wires.Press(canvas, middle), Is.False, "the middle of a node counted as a socket");
            Assert.That(select.Wires.IsBusy, Is.False);
        });
    }

    // A FOLDED node opens under a wire. Folded it has one stub a side and cannot say which socket is meant, so a wire
    // let go over it read as a drop on nothing and offered to make a NEW node next to the one being pointed at.
    [Test]
    public void AFoldedNodeOpensUnderAWire()
    {
        Use(ThemeNamed("Fluent"));
        var (left, right, _, window) = Graph();

        var canvas = Canvas(left, right);
        var select = (SelectTool)canvas.Tool;

        Node(right).IsCollapsed = true;
        Settle(window);

        select.Wires.Press(canvas, At(left, Node(left).OutputPins[0]));
        select.Wires.Move(canvas, Middle(right));

        Assert.That(Node(right).IsCollapsed, Is.False,
            "the wire was carried over a folded node and it stayed shut, so there was nothing to aim at");
    }

    // ...and shuts again when the wire leaves without being dropped on it: a node is not unfolded by being passed over.
    [Test]
    public void AFoldedNodeShutsWhenTheWireLeavesIt()
    {
        Use(ThemeNamed("Fluent"));
        var (left, right, _, window) = Graph();

        var canvas = Canvas(left, right);
        var select = (SelectTool)canvas.Tool;

        Node(right).IsCollapsed = true;
        Settle(window);

        select.Wires.Press(canvas, At(left, Node(left).OutputPins[0]));
        select.Wires.Move(canvas, Middle(right));
        select.Wires.Move(canvas, new Vector2(right.World.X + right.World.Width + 400, right.World.Y));

        Assert.That(Node(right).IsCollapsed, Is.True, "a node the wire only passed over was left open");
    }

    // ...and a wire DROPPED on it is an ordinary wire, on the node that was pointed at.
    [Test]
    public void AWireDroppedOnAFoldedNodeWiresThatNode()
    {
        Use(ThemeNamed("Fluent"));
        var (left, right, _, window) = Graph();

        var canvas = Canvas(left, right);
        var select = (SelectTool)canvas.Tool;

        var offered = 0;
        canvas.WireDropped += (_, _) => offered++;

        Node(right).IsCollapsed = true;
        Settle(window);

        select.Wires.Press(canvas, At(left, Node(left).OutputPins[0]));
        select.Wires.Move(canvas, Middle(right));
        Settle(window);

        select.Wires.Release(canvas, At(right, Node(right).InputPins[0]));

        Assert.Multiple(() =>
        {
            Assert.That(offered, Is.Zero, "it offered a new node instead of wiring the one under the pointer");
            Assert.That(Node(right).InputPins[0].IsConnected, Is.True, "no wire reached the folded node");
            Assert.That(Node(right).IsCollapsed, Is.False, "it shut again over the socket the wire was just put on");
        });
    }

    private static Vector2 Middle(ElementItem item) =>
        new(item.World.X + item.World.Width / 2, item.World.Y + item.World.Height / 2);

    // THE WHOLE CHAIN, from the graph's own objects and back to them: a node in the collection is on the plane, a wire
    // pulled off it and let go over nothing offers the kinds the canvas was given, the pick puts that node into the
    // collection, and the wire that asked for it is written down as a connection.
    [Test]
    public void AWireLetGoOverNothingOffersTheKindsAndJoinsWhatIsPicked()
    {
        Use(ThemeNamed("Fluent"));

        var canvas = new InfiniteCanvas { Mode = CanvasMode.Nodes };
        var scene = new CanvasScene();
        canvas.Scene = scene;
        canvas.Tool = new SelectTool();

        var nodes = new Adamantium.Core.Collections.TrackingCollection<ICanvasNode>();
        canvas.Nodes = nodes;

        // HOW MANY SOCKETS a kind has is the application's knowledge and nobody else's - and it says so in the
        // catalogue, which is also what makes the inside of the node that gets picked.
        var lerp = new Sort("Lerp");
        canvas.NodeKinds = new Adamantium.Core.Collections.TrackingCollection<ICanvasNodeKind>
        {
            new Sort("Add"),
            lerp
        };

        var source = new GraphNode { Kind = "Add", Title = "Add" };
        source.Inputs.Add(new GraphSocket { Name = "In 1" });
        source.Outputs.Add(new GraphSocket(GraphNode.Branches) { Name = "Out 1" });
        nodes.Add(source);

        // The node has to be LAID OUT before a socket has a place: the sockets are parts of its template.
        var layer = new CanvasElementLayer { Owner = canvas };
        var item = canvas.ItemOf(source);
        layer.Sync(new List<ElementItem> { item });

        var window = new Window { Width = 800, Height = 600, Content = layer };
        Settle(window);

        canvas.Measure(new Size(800, 600), force: true);
        canvas.Arrange(new Rect(0, 0, 800, 600));

        var pin = ((CanvasNode)item.Element).OutputPins[0];
        var from = ((CanvasNode)item.Element).Where(pin) ?? Vector2.Zero;

        var select = (SelectTool)canvas.Tool;

        Assert.That(select.Wires.Press(canvas, new Vector2(item.World.X + from.X, item.World.Y + from.Y)), Is.True,
            "the press missed the socket, so this proves nothing");

        select.Wires.Release(canvas, new Vector2(500, 300));

        Assert.That(canvas.IsPaletteOpen, Is.True, "the wire was let go over nothing and nothing was offered");

        canvas.PickedKind = lerp;

        Assert.Multiple(() =>
        {
            Assert.That(nodes, Has.Count.EqualTo(2), "the picked node never joined the graph");
            Assert.That(nodes[1].Kind, Is.EqualTo("Lerp"));
            Assert.That(source.Outputs[0].Connections, Has.Count.EqualTo(1),
                "the wire that asked for the node was not joined to it");
            Assert.That(source.Outputs[0].Connections[0].ToNode, Is.SameAs(nodes[1]));
        });
    }

    // A SOCKET DRAGGED TO ANOTHER PLACE ON ITS SIDE COMES OUT OF IT STILL SHOWN AS TAKEN - and so does the one it
    // changed places with. Whether a socket is filled in says whether anything is docked there; a wire drawn to a
    // socket the node says is empty is the graph contradicting itself.
    [Test]
    public void DraggingASocketLeavesBothItAndTheOneItPassedFilledIn()
    {
        Use(ThemeNamed("Fluent"));

        var canvas = new InfiniteCanvas { Mode = CanvasMode.Nodes, Scene = new CanvasScene() };
        canvas.ApplyCurrentTheme();
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(canvas);

        var nodes = new Adamantium.Core.Collections.TrackingCollection<ICanvasNode>();
        canvas.Nodes = nodes;

        // WITH KINDS, as an application has them: what a socket carries decides its colour, and the colour is what a
        // filled-in socket is filled with.
        canvas.SocketKinds = new Adamantium.Core.Collections.TrackingCollection<ICanvasSocketKind>
        {
            new Carries(string.Empty, null),
            new Carries("Color", Colors.Yellow),
            new Carries("Number", Colors.DeepSkyBlue)
        };

        var source = Node("Add", 0, 0);
        source.Outputs[0].Kind = "Color";

        var target = Node("Lerp", 500, 0);
        target.Inputs[0].Kind = "Color";
        target.Inputs.Add(new GraphSocket { Name = "In 2", Kind = "Number" });
        target.Inputs.Add(new GraphSocket { Name = "In 3", Kind = "Color" });

        nodes.Add(source);
        nodes.Add(target);

        var window = new Window { Width = 1600, Height = 1200, Content = canvas };
        Settle(window);

        // The one that moves and the one it changes places with, both wired: an output branches, so one feeds both.
        _ = new CanvasConnection(source.Outputs[0], target.Inputs[0]);
        _ = new CanvasConnection(source.Outputs[0], target.Inputs[2]);

        Settle(window);

        var node = (CanvasNode)canvas.ItemOf(target).Element;
        var grips = Grips(node);

        Assert.That(grips, Has.Count.GreaterThanOrEqualTo(4), "the node's rows have no grips to drag");
        Assert.That(node.InputPins[2].IsConnected, Is.True, "the wire never reached the socket it was drawn to");

        // The third input taken by its grip and drawn to the top of its side.
        Point(window, 0);
        ((IObservableComponent)grips[2]).RaiseEvent(new Adamantium.UI.Core.Input.MouseButtonEventArgs(
            Adamantium.UI.Core.Input.Mouse.PrimaryDevice, Adamantium.UI.Core.Input.MouseButtons.Left,
            Adamantium.UI.Core.Input.MouseButtonState.Pressed,
            Adamantium.UI.Core.Input.InputModifiers.LeftMouseButton, 0)
        {
            RoutedEvent = Adamantium.UI.Core.Input.Mouse.MouseDownEvent
        });

        Point(window, -1000);
        ((IObservableComponent)node).RaiseEvent(new Adamantium.UI.Core.Input.MouseEventArgs(
            Adamantium.UI.Core.Input.Mouse.PrimaryDevice, Adamantium.UI.Core.Input.InputModifiers.LeftMouseButton, 0)
        {
            RoutedEvent = Adamantium.UI.Core.Input.Mouse.MouseMoveEvent
        });

        ((IObservableComponent)node).RaiseEvent(new Adamantium.UI.Core.Input.MouseButtonEventArgs(
            Adamantium.UI.Core.Input.Mouse.PrimaryDevice, Adamantium.UI.Core.Input.MouseButtons.Left,
            Adamantium.UI.Core.Input.MouseButtonState.Released,
            Adamantium.UI.Core.Input.InputModifiers.None, 0)
        {
            RoutedEvent = Adamantium.UI.Core.Input.Mouse.MouseUpEvent
        });

        // The rows settle, and only then is the move a fact.
        Adamantium.UI.Core.Media.Animation.AnimationManager.Tick(0.5);
        Settle(window);

        Assert.Multiple(() =>
        {
            Assert.That(target.Inputs[0].Name, Is.EqualTo("In 3"), "the socket did not move");
            Assert.That(node.InputPins[0].IsConnected, Is.True, "the socket that moved came out drawn as empty");
            Assert.That(node.InputPins[1].IsConnected, Is.True,
                "the socket it changed places with came out drawn as empty");
            Assert.That(node.InputPins[2].IsConnected, Is.False, "a socket with no wire was filled in");

            // ...and what a taken socket is filled WITH is its own colour, which is what is actually SEEN.
            Assert.That(node.InputPins[0].Fill, Is.Not.Null, "the socket that moved lost its middle");
            Assert.That(node.InputPins[1].Fill, Is.Not.Null, "the one it changed places with lost its middle");
            Assert.That(node.InputPins[2].Fill, Is.Null, "an empty socket was given a middle");
        });
    }

    // What a socket may carry, as an application says it: a word and what it looks like.
    private sealed class Carries : ICanvasSocketKind
    {
        public Carries(string kind, Color? colour)
        {
            Kind = kind;
            Color = colour;
        }

        public string Kind { get; }

        public Color? Color { get; }
    }

    private static void Point(Window window, double y) =>
        Adamantium.UI.Core.Input.Mouse.PrimaryDevice.SetExternalPosition(window, new PixelPoint(20, y));

    private static List<IUIComponent> Grips(IUIComponent within)
    {
        var found = new List<IUIComponent>();

        GatherGrips(within, found);

        return found;
    }

    private static void GatherGrips(IUIComponent within, List<IUIComponent> found)
    {
        if (within.Visibility != Visibility.Visible) return;

        if (within is AdamantiumComponent component && CanvasNode.GetIsSocketGrip(component)) found.Add(within);

        foreach (var child in within.VisualChildren)
        {
            if (child is IUIComponent visual) GatherGrips(visual, found);
        }
    }

    // A CANVAS GIVEN MORE ROOM PUTS WHAT CAME INTO VIEW ON THE PLANE. Which controls are hosted is answered from the
    // viewport - and the pass that asks is the pass that SETS the viewport, so asking it its size there answers with
    // the size it used to be. Everything that had just come into view was left off, and nothing asked again until
    // something unrelated re-synced: a window resized, and half the graph gone until the first zoom.
    [Test]
    public void GrowingTheViewportPutsWhatCameIntoViewOnThePlane()
    {
        Use(ThemeNamed("Fluent"));

        var canvas = new InfiniteCanvas { Mode = CanvasMode.Nodes, Scene = new CanvasScene() };
        canvas.ApplyCurrentTheme();
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(canvas);

        var nodes = new Adamantium.Core.Collections.TrackingCollection<ICanvasNode>();
        canvas.Nodes = nodes;

        nodes.Add(Node("Add", 0, 0));

        // Well outside a 400x300 viewport - the origin sits in the middle of one - and well inside a 1600x1200 one.
        nodes.Add(Node("Lerp", 500, 300));

        canvas.Measure(new Size(400, 300), force: true);
        canvas.Arrange(new Rect(0, 0, 400, 300));

        Assert.That(Hosted(canvas), Has.Count.EqualTo(1), "the far node was hosted while it could not be seen");

        canvas.Measure(new Size(1600, 1200), force: true);
        canvas.Arrange(new Rect(0, 0, 1600, 1200));

        Assert.That(Hosted(canvas), Has.Count.EqualTo(2),
            "the node that came into view was left off the plane until something else asked again");
    }

    private static GraphNode Node(string kind, double x, double y)
    {
        var node = new GraphNode { Kind = kind, Title = kind, Left = x, Top = y };

        node.Inputs.Add(new GraphSocket { Name = "In 1" });
        node.Outputs.Add(new GraphSocket(GraphNode.Branches) { Name = "Out 1" });

        return node;
    }

    // The controls the canvas is actually holding - the children of the layer in its own template.
    private static List<IMeasurableComponent> Hosted(IUIComponent within)
    {
        var found = new List<IMeasurableComponent>();

        Gather(within, found);

        return found;
    }

    private static void Gather(IUIComponent within, List<IMeasurableComponent> found)
    {
        if (within is CanvasElementLayer layer)
        {
            found.AddRange(layer.Children);
            return;
        }

        foreach (var child in within.VisualChildren)
        {
            if (child is IUIComponent visual) Gather(visual, found);
        }
    }

    // The application's catalogue of kinds, as a page would hand it over: a word and what a node of that sort is made
    // of.
    private sealed class Sort : ICanvasNodeKind
    {
        public Sort(string kind) => Kind = kind;

        public string Kind { get; }

        public string Title => Kind;

        public Adamantium.Mathematics.Color? Accent => null;

        public ICanvasNodeSpecialization Create() => new Body();

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
        public void Shape(ICanvasNode node)
        {
            GraphNode.Fit(node.Inputs, 1, "In");
            GraphNode.Fit(node.Outputs, 1, "Out");
        }
    }

    // A BAND round two nodes selects them - and having selected them, the canvas has a frame to draw round them. The
    // bin that follows the selection appeared without one, which reads as a button belonging to nothing.
    [Test]
    public void ABandRoundNodesSelectsThemAndHasABoxToDraw()
    {
        Use(ThemeNamed("Fluent"));
        var (left, right, _, _) = Graph();

        var canvas = Canvas(left, right);
        var select = (SelectTool)canvas.Tool;

        var from = new Vector2(left.World.X - 40, left.World.Y - 40);
        var to = new Vector2(right.World.X + right.World.Width + 40, right.World.Y + right.World.Height + 40);

        select.OnPressed(canvas, Pointer(from));
        select.OnMoved(canvas, Pointer(to));
        select.OnReleased(canvas, Pointer(to));

        Assert.Multiple(() =>
        {
            Assert.That(canvas.Selection, Has.Count.EqualTo(2), "the band did not take the nodes it was drawn round");
            Assert.That(canvas.SelectionBounds, Is.Not.Null, "there is a selection and no box round it to draw");

            // ...and SELECTING them is all that happened. The first band drawn round nodes folded them, which is a
            // selection doing something a selection does not do.
            Assert.That(Node(left).IsCollapsed, Is.False, "the band folded the node it selected");
            Assert.That(Node(right).IsCollapsed, Is.False, "the band folded the node it selected");
        });
    }

    // ...and the WIRE the band caught along with them must not take the frame away. A wire offers no handles at all,
    // and counted into the intersection it left the nodes with a bar over no frame and no way to drag them together.
    [Test]
    public void AWireInTheSelectionDoesNotTakeTheFrameFromTheNodes()
    {
        Use(ThemeNamed("Fluent"));
        var (canvas, _, left, right, _) = Wired();

        var from = new Vector2(left.World.X - 40, left.World.Y - 40);
        var to = new Vector2(right.World.X + right.World.Width + 40, right.World.Y + right.World.Height + 40);

        var select = (SelectTool)canvas.Tool;
        select.OnPressed(canvas, Pointer(from));
        select.OnMoved(canvas, Pointer(to));
        select.OnReleased(canvas, Pointer(to));

        Assert.Multiple(() =>
        {
            Assert.That(canvas.Selection, Has.Some.InstanceOf<ConnectionItem>(),
                "the band did not catch the wire, so this is not the case being tested");
            Assert.That(canvas.OfferedHandles.HasFlag(CanvasHandles.Corners), Is.True,
                "no grip is offered, so no frame is drawn round the nodes the band took");
            Assert.That(canvas.OfferedHandles.HasFlag(CanvasHandles.Body), Is.True,
                "the nodes the band took cannot be dragged together");
        });
    }

    // A BAND DRAWN ROUND NODES SELECTS THEM AND LEAVES THEM AS THEY WERE. Folding a node is a switch on its strip and
    // nothing else does it - a node that shuts because it was selected loses everything a person was looking at, and
    // the first band of a session is exactly when they are looking.
    [Test]
    public void ABandRoundNodesDoesNotFoldThem()
    {
        Use(ThemeNamed("Fluent"));
        var (canvas, _, left, right, select) = Wired();

        var from = new Vector2(left.World.X - 60, left.World.Y - 60);
        var to = new Vector2(right.World.X + right.World.Width + 60, right.World.Y + right.World.Height + 60);

        select.OnPressed(canvas, Pointer(from));
        select.OnMoved(canvas, Pointer(to));
        select.OnReleased(canvas, Pointer(to));

        Assert.Multiple(() =>
        {
            Assert.That(canvas.Selection, Has.Count.GreaterThanOrEqualTo(2), "the band caught nothing");
            Assert.That(Node(left).IsCollapsed, Is.False, "the node folded itself when it was selected");
            Assert.That(Node(right).IsCollapsed, Is.False, "the node folded itself when it was selected");
        });

        // ...and a SECOND band, over one of them, leaves it open too.
        select.OnPressed(canvas, Pointer(from));
        select.OnMoved(canvas, Pointer(new Vector2(left.World.X + left.World.Width + 10, left.World.Y + left.World.Height + 10)));
        select.OnReleased(canvas, Pointer(new Vector2(left.World.X + left.World.Width + 10, left.World.Y + left.World.Height + 10)));

        Assert.That(Node(left).IsCollapsed, Is.False, "the node folded on the second band");
    }

    private static CanvasPointerEventArgs Pointer(Vector2 world) =>
        new()
        {
            World = world, Pointer = world, Screen = world,
            Button = Adamantium.UI.Core.Input.MouseButtons.Left, ClickCount = 1
        };

    // Two sockets facing the same way have nothing to say to each other, and a socket cannot be joined to its own node.
    [Test]
    public void OnlyAnOutputAndAnInputOfTwoDifferentNodesAreJoined()
    {
        Use(ThemeNamed("Fluent"));
        var (left, right, _, _) = Graph();

        Assert.Multiple(() =>
        {
            Assert.That(ConnectGesture.Joinable(left, Node(left).OutputPins[0], right, Node(right).InputPins[0]),
                Is.True);
            Assert.That(ConnectGesture.Joinable(left, Node(left).OutputPins[0], right, Node(right).OutputPins[0]),
                Is.False, "two outputs");
            Assert.That(ConnectGesture.Joinable(left, Node(left).OutputPins[0], left, Node(left).InputPins[0]),
                Is.False, "a node to itself");
        });
    }

    private static InfiniteCanvas Canvas(ElementItem left, ElementItem right)
    {
        // A GRAPH, which is the only place a wire exists at all: a canvas being used as a drawing does not offer nodes,
        // and a gesture that joins two of them has nothing to take hold of.
        var canvas = new InfiniteCanvas { Mode = CanvasMode.Nodes };
        canvas.Measure(new Size(800, 600), force: true);
        canvas.Arrange(new Rect(0, 0, 800, 600));

        var scene = new CanvasScene();
        scene.Add(left);
        scene.Add(right);
        canvas.Scene = scene;
        canvas.Tool = new SelectTool();

        return canvas;
    }

    // A socket's place on the PLANE: the node is arranged into the item's box, so its own coordinates start at the
    // box's corner.
    private static Vector2 At(ElementItem item, CanvasNodePin pin)
    {
        var local = ((CanvasNode)item.Element).Where(pin).Value;

        return new Vector2(item.World.X + local.X, item.World.Y + local.Y);
    }

    // A wire is the colour of the socket it LEAVES, read when it is drawn and not remembered from when it was made -
    // so recolouring the socket recolours the wire, with nothing to keep in step.
    [Test]
    public void AWireTakesTheColourOfTheSocketItLeaves()
    {
        Use(ThemeNamed("Fluent"));
        var (left, right, _, _) = Graph();

        var canvas = new InfiniteCanvas();
        canvas.Measure(new Size(800, 600), force: true);
        canvas.Arrange(new Rect(0, 0, 800, 600));

        var output = Node(left).OutputPins[0];
        var wire = new ConnectionItem(left, output, right, Node(right).InputPins[0]);

        output.Color = Brushes.Red;

        var session = new RecordingDrawingSession();
        wire.Render(session, canvas);

        Assert.That(((InkBrush)session.Rectangles[0].Brush).Color, Is.EqualTo(Colors.Red));

        output.Color = Brushes.Lime;
        wire.Render(session, canvas);

        Assert.That(((InkBrush)session.Rectangles[1].Brush).Color, Is.EqualTo(Colors.Lime),
            "the wire kept the colour it was made with");
    }

    // Pointing AT the wire finds it, and pointing near it does not. It is a curve, so what answers is the curve and not
    // the box round it - most of that box is nowhere near the wire.
    [Test]
    public void AWireIsHitOnTheCurveAndNotInItsBox()
    {
        Use(ThemeNamed("Fluent"));
        var (left, right, _, _) = Graph();

        var wire = new ConnectionItem(left, Node(left).OutputPins[0], right, Node(right).InputPins[0]);
        wire.Ends(out var from, out var to);

        var corner = new Vector2(Math.Min(from.X, to.X) + 4, Math.Min(from.Y, to.Y) - 40);

        Assert.Multiple(() =>
        {
            Assert.That(wire.HitTest(from, 3), Is.True, "its own end is not on it");
            Assert.That(wire.HitTest(corner, 3), Is.False, "a corner of the box counted as a hit");
        });
    }

    // The gesture, set up so a wire is already there: A's first output into B's first input.
    private (InfiniteCanvas Canvas, CanvasScene Scene, ElementItem Left, ElementItem Right, SelectTool Tool) Wired()
    {
        var (left, right, _, _) = Graph();
        var canvas = Canvas(left, right);
        var tool = (SelectTool)canvas.Tool;

        tool.Wires.Press(canvas, At(left, Node(left).OutputPins[0]));
        tool.Wires.Release(canvas, At(right, Node(right).InputPins[0]));

        return (canvas, (CanvasScene)canvas.Scene, left, right, tool);
    }

    private static int Wires(CanvasScene scene)
    {
        var count = 0;
        foreach (var item in scene.Items)
        {
            if (item is ConnectionItem) count++;
        }

        return count;
    }

    // PULLING A WIRE BACK OFF an input: the same drag that would have made one takes the one that is there and carries
    // on from its far end, which is how every graph editor does it and why deleting a wire needs no gesture of its own.
    [Test]
    public void PressingATakenInputTakesItsWire()
    {
        Use(ThemeNamed("Fluent"));
        var (canvas, scene, left, right, tool) = Wired();

        Assert.That(Wires(scene), Is.EqualTo(1), "nothing was wired to begin with");

        Assert.That(tool.Wires.Press(canvas, At(right, Node(right).InputPins[0])), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(Wires(scene), Is.Zero, "the wire is still in the scene while its end is in the hand");
            Assert.That(Node(right).InputPins[0].IsConnected, Is.False, "the socket is still drawn as taken");
            Assert.That(Node(left).OutputPins[0].IsConnected, Is.False);
            Assert.That(tool.Wires.IsBusy, Is.True, "nothing is being dragged");
        });
    }

    // ...dropped on another socket it is RE-ROUTED.
    [Test]
    public void AWirePulledOffAnInputCanBeDroppedOnAnother()
    {
        Use(ThemeNamed("Fluent"));
        var (canvas, scene, left, right, tool) = Wired();

        tool.Wires.Press(canvas, At(right, Node(right).InputPins[0]));
        tool.Wires.Release(canvas, At(right, Node(right).InputPins[1]));

        Assert.Multiple(() =>
        {
            Assert.That(Wires(scene), Is.EqualTo(1));
            Assert.That(Node(right).InputPins[0].IsConnected, Is.False, "it is still fastened where it was");
            Assert.That(Node(right).InputPins[1].IsConnected, Is.True, "it never arrived");
        });
    }

    // ...and dropped on NOTHING it is gone. That is how a wire is deleted.
    [Test]
    public void AWireDroppedOnNothingIsGone()
    {
        Use(ThemeNamed("Fluent"));
        var (canvas, scene, left, right, tool) = Wired();

        tool.Wires.Press(canvas, At(right, Node(right).InputPins[0]));
        tool.Wires.Release(canvas, new Vector2(-4000, -4000));

        Assert.Multiple(() =>
        {
            Assert.That(Wires(scene), Is.Zero);
            Assert.That(Node(left).OutputPins[0].IsConnected, Is.False, "a socket left drawn as taken");
            Assert.That(Node(right).InputPins[0].IsConnected, Is.False);
        });
    }

    // ONE WIRE PER INPUT. An input is a value arriving, and two values arriving at one place is not something a graph
    // can mean - so a second wire replaces the first.
    [Test]
    public void ASecondWireIntoAnInputReplacesTheFirst()
    {
        Use(ThemeNamed("Fluent"));
        var (canvas, scene, left, right, tool) = Wired();

        // The node's OTHER output into the same input.
        tool.Wires.Press(canvas, At(left, Node(left).OutputPins[1]));
        tool.Wires.Release(canvas, At(right, Node(right).InputPins[0]));

        Assert.Multiple(() =>
        {
            Assert.That(Wires(scene), Is.EqualTo(1), "the input is taking two values at once");
            Assert.That(Node(left).OutputPins[0].IsConnected, Is.False, "the wire that was replaced still says it is on");
            Assert.That(Node(left).OutputPins[1].IsConnected, Is.True);
        });
    }

    // WHAT FLOWS decides what may be joined. A word and not the colour: the colour is how a person tells types apart
    // at a glance, but two shades of one idea and one shade shared by two are both things an application may do.
    [Test]
    public void SocketsThatDisagreeAboutWhatFlowsAreNotJoined()
    {
        Use(ThemeNamed("Fluent"));
        var (left, right, _, _) = Graph();
        var canvas = Canvas(left, right);
        var tool = (SelectTool)canvas.Tool;

        Node(left).OutputPins[0].Kind = "float";
        Node(right).InputPins[0].Kind = "image";
        Node(right).InputPins[1].Kind = "float";

        tool.Wires.Press(canvas, At(left, Node(left).OutputPins[0]));
        tool.Wires.Release(canvas, At(right, Node(right).InputPins[0]));

        Assert.That(Wires((CanvasScene)canvas.Scene), Is.Zero, "an image socket took a float");

        tool.Wires.Press(canvas, At(left, Node(left).OutputPins[0]));
        tool.Wires.Release(canvas, At(right, Node(right).InputPins[1]));

        Assert.That(Wires((CanvasScene)canvas.Scene), Is.EqualTo(1), "two floats refused each other");
    }

    // ...and a socket that says nothing takes anything, so a graph never told about types wires up as it always did.
    [Test]
    public void ASocketThatSaysNothingTakesAnything()
    {
        Use(ThemeNamed("Fluent"));
        var (left, right, _, _) = Graph();

        Node(left).OutputPins[0].Kind = "float";

        Assert.That(ConnectGesture.Joinable(left, Node(left).OutputPins[0], right, Node(right).InputPins[0]), Is.True);
    }

    // A LOOP is refused. A graph that lets a wire run back round into its own source and says nothing is one that
    // breaks the first time somebody reads it, and only the person drawing it could have known not to.
    [Test]
    public void AWireThatWouldCloseALoopIsRefused()
    {
        Use(ThemeNamed("Fluent"));
        var (canvas, scene, left, right, tool) = Wired();

        // A -> B is there. Now B's output back into A's input.
        tool.Wires.Press(canvas, At(right, Node(right).OutputPins[0]));
        tool.Wires.Release(canvas, At(left, Node(left).InputPins[0]));

        Assert.That(Wires(scene), Is.EqualTo(1), "the graph now runs round in a circle");
        Assert.That(Node(left).InputPins[0].IsConnected, Is.False);
    }

    // The refusal is SAID WHILE THE HAND IS STILL MOVING, so the answer arrives before the button does.
    [Test]
    public void AWireOverASocketThatWillNotTakeItSaysSo()
    {
        Use(ThemeNamed("Fluent"));
        var (canvas, _, left, right, tool) = Wired();

        Node(right).InputPins[1].Kind = "image";
        Node(left).OutputPins[1].Kind = "float";

        tool.Wires.Press(canvas, At(left, Node(left).OutputPins[1]));

        Assert.Multiple(() =>
        {
            Assert.That(tool.Wires.OverRefusal(canvas, At(right, Node(right).InputPins[1])), Is.True,
                "a socket that will not take the wire looks willing");
            Assert.That(tool.Wires.OverRefusal(canvas, new Vector2(-4000, -4000)), Is.False,
                "empty plane is not a refusal - there is nothing there to refuse");
        });
    }

    // A WIRE LET GO OVER NOTHING is OFFERED before it is thrown away: this is the gesture a graph is actually built
    // with - drop the wire where there is nothing and pick the node that goes there, already joined.
    [Test]
    public void AWireLetGoOverNothingIsOffered()
    {
        Use(ThemeNamed("Fluent"));
        var (left, right, _, _) = Graph();
        var canvas = Canvas(left, right);
        var tool = (SelectTool)canvas.Tool;

        CanvasWireDroppedEventArgs offered = null;
        canvas.WireDropped += (_, e) => offered = e;

        tool.Wires.Press(canvas, At(left, Node(left).OutputPins[0]));
        tool.Wires.Release(canvas, new Vector2(-500, 260));

        Assert.Multiple(() =>
        {
            Assert.That(offered, Is.Not.Null, "the wire was thrown away without anybody being asked");
            Assert.That(offered.FromItem, Is.SameAs(left));
            Assert.That(offered.FromPin, Is.SameAs(Node(left).OutputPins[0]));
            Assert.That(offered.World.X, Is.EqualTo(-500).Within(0.01), "the list would open somewhere else");
        });
    }

    // ...and whoever takes the offer joins the node they made through the CANVAS, so the rules about what may be wired
    // to what stay in one place.
    [Test]
    public void WhoeverAnswersJoinsThroughTheCanvas()
    {
        Use(ThemeNamed("Fluent"));
        var (left, right, _, _) = Graph();
        var canvas = Canvas(left, right);

        var wire = canvas.Join(left, Node(left).OutputPins[0], right, Node(right).InputPins[0]);

        Assert.Multiple(() =>
        {
            Assert.That(wire, Is.Not.Null);
            Assert.That(Wires((CanvasScene)canvas.Scene), Is.EqualTo(1));
            Assert.That(Node(right).InputPins[0].IsConnected, Is.True);
        });

        // ...and it refuses exactly what the gesture refuses.
        Assert.That(canvas.Join(right, Node(right).OutputPins[0], left, Node(left).InputPins[0]), Is.Null,
            "a loop was allowed through the back door");
    }

    // A WIRE GOES ROUND what is in its way. Straight through a node, a wire says two things at once - "this is a
    // connection" and "this node is somewhere on it" - and the second one is a lie.
    [Test]
    public void AWireGoesRoundANodeInItsWay()
    {
        Use(ThemeNamed("Fluent"));
        var (left, right, _, _) = Graph();

        // Right is far to the right of left; a node parked between them, straight across the wire's path.
        right.World = new Rect(700, 0, 190, 110);

        var between = new ElementItem(new CanvasNode { Title = "In the way" }, new Rect(330, 0, 190, 110));
        var canvas = Canvas(left, right);
        canvas.Scene.Add(between);

        var wire = new ConnectionItem(left, Node(left).OutputPins[0], right, Node(right).InputPins[0]);
        canvas.Scene.Add(wire);

        var session = new RecordingDrawingSession();
        wire.Render(session, canvas);

        Assert.That(wire.Ends(out var from, out var to), Is.True);

        // Every point of the curve has to be outside the node standing in the middle.
        var inside = 0;
        for (var step = 0; step <= 24; step++)
        {
            var at = Crosses(wire, from, to, step / 24.0);
            if (between.World.Contains(at)) inside++;
        }

        Assert.That(inside, Is.Zero, "the wire is drawn straight through a node");
    }

    // ...and TURNED OFF it goes straight through, which is what proves the detour above is the routing and not the
    // bend happening to miss.
    [Test]
    public void AWireToldNotToRouteGoesStraightThrough()
    {
        Use(ThemeNamed("Fluent"));
        var (left, right, _, _) = Graph();
        right.World = new Rect(700, 0, 190, 110);

        var between = new ElementItem(new CanvasNode { Title = "In the way" }, new Rect(330, 0, 190, 110));
        var canvas = Canvas(left, right);
        canvas.Scene.Add(between);

        var wire = new ConnectionItem(left, Node(left).OutputPins[0], right, Node(right).InputPins[0])
        {
            Routes = false
        };
        canvas.Scene.Add(wire);

        var session = new RecordingDrawingSession();
        wire.Render(session, canvas);

        wire.Ends(out var from, out var to);

        var inside = 0;
        for (var step = 0; step <= 24; step++)
        {
            if (between.World.Contains(Crosses(wire, from, to, step / 24.0))) inside++;
        }

        Assert.That(inside, Is.GreaterThan(0), "it went round anyway, so the detour above proves nothing");
    }

    // ...and with NOTHING in the way it is the plain bend: a wire that wandered for no reason would be a wire that says
    // something about a graph that is not true.
    [Test]
    public void AWireWithNothingInTheWayIsNotBent()
    {
        Use(ThemeNamed("Fluent"));
        var (left, right, _, _) = Graph();
        var canvas = Canvas(left, right);

        var wire = new ConnectionItem(left, Node(left).OutputPins[0], right, Node(right).InputPins[0]);
        canvas.Scene.Add(wire);

        var session = new RecordingDrawingSession();
        wire.Render(session, canvas);

        wire.Ends(out var from, out var to);
        var middle = Crosses(wire, from, to, 0.5);

        // The plain bend keeps the belly between the two ends' heights.
        Assert.That(middle.Y, Is.InRange(Math.Min(from.Y, to.Y) - 1, Math.Max(from.Y, to.Y) + 1),
            "the wire wandered off with nothing in its way");
    }

    // A point ON the wire as it was actually drawn, by walking its own hit test outwards - the wire keeps the bend it
    // took, and this is what reads it back without opening it up.
    private static Vector2 Crosses(ConnectionItem wire, Vector2 from, Vector2 to, double t)
    {
        // The box the wire reports already holds the bend it took; walking the curve again needs its control points,
        // and the honest way to get at them from outside is to ask the wire where it is - which is what HitTest does.
        // Sampled here by bisecting the box instead: the wire is a cubic between the two ends with the belly inside
        // its own bounds, so the mid-height of the box is where a detour shows up.
        var box = wire.Bounds;
        var x = from.X + (to.X - from.X) * t;

        // The Y the wire is at for this X, found by asking the wire itself.
        for (var y = box.Y; y <= box.Y + box.Height; y += 1)
        {
            if (wire.HitTest(new Vector2(x, y), 2)) return new Vector2(x, y);
        }

        return new Vector2(x, from.Y);
    }

    // A WIRE IS A STEP. Drawing one is a change to the graph like any other, and one that could not be taken back would
    // be the only change on the plane that could not.
    [Test]
    public void DrawingAWireCanBeUndone()
    {
        Use(ThemeNamed("Fluent"));
        var (left, right, _, _) = Graph();
        var canvas = Canvas(left, right);
        canvas.History = new CanvasHistory();
        var tool = (SelectTool)canvas.Tool;

        canvas.BeginEdit("Gesture");
        tool.Wires.Press(canvas, At(left, Node(left).OutputPins[0]));
        tool.Wires.Release(canvas, At(right, Node(right).InputPins[0]));
        canvas.EndEdit();

        Assert.That(Wires((CanvasScene)canvas.Scene), Is.EqualTo(1), "nothing was wired to begin with");

        Assert.That(canvas.Undo(), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(Wires((CanvasScene)canvas.Scene), Is.Zero, "the wire is still there");
            Assert.That(Node(right).InputPins[0].IsConnected, Is.False,
                "the socket is still drawn as taken with nothing on it");
        });
    }

    // ...and taking one off is a step too, with the socket saying so again when the step is put back.
    [Test]
    public void TakingAWireOffCanBeUndone()
    {
        Use(ThemeNamed("Fluent"));
        var (canvas, scene, left, right, tool) = Wired();
        canvas.History = new CanvasHistory();

        canvas.BeginEdit("Gesture");
        tool.Wires.Press(canvas, At(right, Node(right).InputPins[0]));
        tool.Wires.Release(canvas, new Vector2(-4000, -4000));
        canvas.EndEdit();

        Assert.That(Wires(scene), Is.Zero);

        Assert.That(canvas.Undo(), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(Wires(scene), Is.EqualTo(1), "the wire did not come back");
            Assert.That(Node(right).InputPins[0].IsConnected, Is.True,
                "the wire is back and the socket still says it is loose");
            Assert.That(Node(left).OutputPins[0].IsConnected, Is.True);
        });
    }

    // A NODE TAKES ITS WIRES WITH IT. Left behind, a wire would be drawn from a node that is not there, and no gesture
    // could ever reach it to take it away.
    [Test]
    public void DeletingANodeTakesItsWires()
    {
        Use(ThemeNamed("Fluent"));
        var (canvas, scene, left, right, _) = Wired();

        canvas.Select(right, false);
        canvas.DeleteSelection();

        Assert.Multiple(() =>
        {
            Assert.That(Wires(scene), Is.Zero, "a wire is hanging off a node that is gone");
            Assert.That(Node(left).OutputPins[0].IsConnected, Is.False, "the socket at the far end is still drawn as taken");
        });
    }
}
