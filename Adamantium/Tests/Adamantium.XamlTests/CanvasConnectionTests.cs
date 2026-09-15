using System;
using System.Collections.Generic;
using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Resources;
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
}
