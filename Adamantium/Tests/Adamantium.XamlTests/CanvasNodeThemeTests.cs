using System.Collections.Generic;
using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>The NODE of a graph under each theme: it gets a template, it is as big as what is in it, and its title strip
/// wears the theme's accent rather than a colour of the editor's own.</summary>
[TestFixture]
public class CanvasNodeThemeTests
{
    private FakeApp _app;
    private ThemeManager _themes;

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
        _themes = new ThemeManager(new AdamantiumDependencyContainer());
        _app.ThemeManager = _themes;
        ((FakeContext)_app.UIContext).ThemeEngine = _themes;

        _themes.AddTheme(theme.Name, theme);
        _themes.SetTheme(theme);
    }

    private static CanvasNode Built(CanvasNode node)
    {
        node.ApplyCurrentTheme();
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(node);
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        node.Measure(new Size(600, 600));
        node.Arrange(new Rect(0, 0, 600, 600));
        return node;
    }

    private static void Settle(Window window)
    {
        for (var i = 0; i < 6; i++)
        {
            Adamantium.UI.Extensions.WindowExtension.UpdateTree(window);
            Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        }
    }

    private static Theme MacOs() => new Adamantium.UI.Themes.MacOsTheme.MacOs();

    private static Theme Fluent() => new Adamantium.UI.Themes.FluentTheme.Fluent();

    private static Theme EditorPro() => new Adamantium.UI.Themes.EditorProTheme.EditorPro();

    private static Theme ThemeNamed(string name) => name switch
    {
        "MacOs" => MacOs(),
        "EditorPro" => EditorPro(),
        _ => Fluent()
    };

    // A node with no template draws NOTHING, and nothing is exactly what a blank plane looks like - so this is the
    // first thing to ask of it under every theme.
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void ANodeIsTemplatedAndHasASize(string theme)
    {
        Use(ThemeNamed(theme));

        var node = Built(new CanvasNode { Title = "Multiply", Inputs = 2, Outputs = 1 });

        Assert.Multiple(() =>
        {
            Assert.That(node.Template, Is.Not.Null, "no template means it draws nothing at all");
            Assert.That(node.DesiredSize.Width, Is.GreaterThan(0));
            Assert.That(node.DesiredSize.Height, Is.GreaterThan(0));
        });
    }

    // The strip takes the THEME's accent. An editor that painted it a colour of its own would look like a different
    // application inside the application.
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void TheTitleStripWearsTheThemesAccent(string theme)
    {
        Use(ThemeNamed(theme));

        var node = Built(new CanvasNode());

        Assert.That(node.Accent, Is.Not.Null, "the strip is the one thing that says what kind of node it is");
    }

    // More pins is a taller node: the sockets are real rows and not a picture of some.
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void MorePinsMakeATallerNode(string theme)
    {
        Use(ThemeNamed(theme));

        var few = Built(new CanvasNode { Inputs = 1, Outputs = 1 }).DesiredSize.Height;
        var many = Built(new CanvasNode { Inputs = 5, Outputs = 1 }).DesiredSize.Height;

        Assert.That(many, Is.GreaterThan(few));
    }

    // The sockets came out with no colour at all: what was drawn was one hairline of the same stroke the body wears, so
    // the body's own edge ran straight through them and the row read as a line with faint bumps in it. A socket has to
    // come out of the theme with a colour whether or not the application says anything.
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void EverySocketHasAColour(string theme)
    {
        Use(ThemeNamed(theme));

        var node = Built(new CanvasNode { Inputs = 3, Outputs = 2 });

        Assert.Multiple(() =>
        {
            Assert.That(node.PinColor, Is.Not.Null, "with no colour the sockets are drawn in nothing");
            foreach (var pin in node.InputPins) Assert.That(pin.Color, Is.Not.Null, pin.Name);
            foreach (var pin in node.OutputPins) Assert.That(pin.Color, Is.Not.Null, pin.Name);
        });
    }

    // Colouring a socket is how a graph editor says what may be joined to what, so a colour put on one pin outranks the
    // theme's default - including a pin that was there before the theme arrived.
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void APinsOwnColourSurvivesTheTheme(string theme)
    {
        Use(ThemeNamed(theme));

        var node = new CanvasNode { Inputs = 2, Outputs = 1 };
        var mine = Brushes.Red;
        node.InputPins[0].Color = mine;

        Built(node);

        Assert.Multiple(() =>
        {
            Assert.That(node.InputPins[0].Color, Is.SameAs(mine), "the theme took a colour the application had chosen");
            Assert.That(node.InputPins[1].Color, Is.SameAs(node.PinColor));
        });
    }

    // HOLLOW while nothing is docked and SOLID once something is - the reading a blueprint editor gives, and the one
    // that answers "which ends are still loose" without reading a single label. The template paints the middle with
    // Fill and nothing else decides it, so an empty socket has to have no Fill at all.
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void AnEmptySocketIsHollowAndATakenOneIsSolid(string theme)
    {
        Use(ThemeNamed(theme));

        var node = Built(new CanvasNode { Inputs = 2, Outputs = 1 });
        var loose = node.InputPins[0];
        var taken = node.InputPins[1];

        taken.IsConnected = true;

        Assert.Multiple(() =>
        {
            Assert.That(loose.Fill, Is.Null, "an empty socket was filled in, so nothing says it is free");
            Assert.That(taken.Fill, Is.SameAs(taken.Color), "a taken socket stayed hollow");
            Assert.That(loose.Color, Is.Not.Null, "the ring is drawn either way");
        });
    }

    // SOLID, both of them. The inspector shows a colour through a swatch, and a swatch can only show a solid brush -
    // handed anything else it quietly keeps whatever it was showing before, which reads as a row that lies rather than
    // as a row that cannot answer.
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void TheNodesColoursAreSolidSoASwatchCanShowThem(string theme)
    {
        Use(ThemeNamed(theme));

        var node = Built(new CanvasNode());

        Assert.Multiple(() =>
        {
            Assert.That(node.Accent, Is.InstanceOf<SolidColorBrush>());
            Assert.That(node.PinColor, Is.InstanceOf<SolidColorBrush>());
        });
    }

    // ...and they are the THEME's brushes, which everything else asking for that colour is holding too. Marked as such,
    // so an editor handed one leaves a new brush on the node instead of repainting it: recolouring one node's title
    // strip turned every accent in the application that colour, and the node's two colour lines - both starting at the
    // accent - were one object being shown twice.
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void ThoseColoursAreTheThemesAndSayThatTheyAre(string theme)
    {
        Use(ThemeNamed(theme));

        var node = Built(new CanvasNode());

        Assert.Multiple(() =>
        {
            Assert.That(node.Accent.IsShared, Is.True, "the accent is editable in place");
            Assert.That(node.PinColor.IsShared, Is.True, "the socket colour is editable in place");
        });
    }

    // ...and the inspector has to be able to READ them through the item that carries the node, which is what it is
    // actually pointed at. A colour that only the template can see is a row that shows nothing.
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void TheInspectorReadsThoseColoursThroughTheItem(string theme)
    {
        Use(ThemeNamed(theme));

        var node = Built(new CanvasNode());
        var item = new ElementItem(node, new Rect(0, 0, 190, 110));
        var grid = new PropertyGrid();
        var accent = new SolidColorBrushProperty { Binding = new Binding("Element.Accent") };
        var sockets = new SolidColorBrushProperty { Binding = new Binding("Element.PinColor") };

        Assert.Multiple(() =>
        {
            Assert.That(grid.ValueOf(item, accent), Is.SameAs(node.Accent));
            Assert.That(grid.ValueOf(item, sockets), Is.SameAs(node.PinColor));
        });
    }

    // On the PLANE, which is where it matters: asking for more sockets has to make the node taller, and the box it sits
    // in has to follow. It did not - the box was measured at its own height, a measure cannot answer with more than it
    // was offered, so a node given a fourth input drew four sockets in the room for two.
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void AskingForMoreSocketsMakesTheBoxOnThePlaneTaller(string theme)
    {
        Use(ThemeNamed(theme));

        var node = new CanvasNode { Inputs = 2, Outputs = 1 };
        var item = new ElementItem(node, new Rect(0, 0, 190, 110));
        var layer = new CanvasElementLayer();
        layer.Sync(new List<ElementItem> { item });

        // In a REAL window: a detached component's invalidation goes nowhere, so a socket added to a node hanging in
        // nothing never reaches the list that shows it, and the whole question would be unaskable.
        var window = new Window { Width = 600, Height = 600, Content = layer };
        Settle(window);
        var few = item.World.Height;

        node.Inputs = 5;
        Settle(window);

        Assert.Multiple(() =>
        {
            Assert.That(few, Is.GreaterThan(0), "the box never took the node's height at all");
            Assert.That(item.World.Height, Is.GreaterThan(few), "the sockets were added into the same room");
        });
    }

    // Dropping ONE socket, wherever it sits. A count can only take things off the end, so asked to drop the second of
    // three it drops the third and leaves the second where it was - and everything said about the sockets that stay has
    // to stay with them.
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void OneSocketCanBeTakenOutOfTheMiddle(string theme)
    {
        Use(ThemeNamed(theme));

        var node = Built(new CanvasNode { Inputs = 3, Outputs = 1 });
        node.InputPins[0].Name = "Base";
        node.InputPins[2].Name = "Bias";

        node.Remove(node.InputPins[1]);

        Assert.Multiple(() =>
        {
            Assert.That(node.Inputs, Is.EqualTo(2), "the count has to follow, or the next growth undoes this");
            Assert.That(node.InputPins.Count, Is.EqualTo(2));
            Assert.That(node.InputPins[0].Name, Is.EqualTo("Base"));
            Assert.That(node.InputPins[1].Name, Is.EqualTo("Bias"), "it dropped the last one instead");
        });
    }

    // ...and a socket asked for afterwards is not given a name one of the others already has, which counting would do.
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void ASocketAddedAfterOneWasRemovedGetsAFreeName(string theme)
    {
        Use(ThemeNamed(theme));

        var node = Built(new CanvasNode { Inputs = 3, Outputs = 1 });

        node.Remove(node.InputPins[1]);
        node.Inputs = 3;

        Assert.That(node.InputPins[2].Name, Is.EqualTo("In 2"), "it took a name already on the list");
    }

    // The template puts a SOCKET per pin, and it is a socket rather than an anonymous border - which is what makes it
    // findable at all. Asked first, because everything below depends on it and answers "null" if it is not so.
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void EveryPinGetsASocketInTheTree(string theme)
    {
        Use(ThemeNamed(theme));

        var node = new CanvasNode { Inputs = 2, Outputs = 1 };
        // WITH AN OWNER: the layer places its children from the camera, and one without a camera arranges nothing at
        // all - so everything inside a node stays at no size, which is not a state it is ever in on screen.
        var layer = new CanvasElementLayer { Owner = new InfiniteCanvas() };
        layer.Sync(new List<ElementItem> { new(node, new Rect(0, 0, 190, 110)) });

        var window = new Window { Width = 600, Height = 600, Content = layer };
        Settle(window);

        var sockets = Sockets(node);

        Assert.That(sockets, Has.Count.EqualTo(3));

        // ...and each one KNOWS which pin it stands for. Without that the discs are three anonymous circles and
        // nothing can say which of them a connection is held by.
        Assert.Multiple(() =>
        {
            foreach (var socket in sockets)
            {
                Assert.That(socket.Pin, Is.Not.Null, "a socket that does not know its own pin");
                Assert.That(socket.RenderSize.Width, Is.GreaterThan(0), "a socket that was never given a size");
                Assert.That(node.Where(socket.Pin), Is.Not.Null, "a socket the node cannot place");
            }
        });
    }

    private static List<CanvasNodeSocket> Sockets(IUIComponent within)
    {
        var found = new List<CanvasNodeSocket>();
        Gather(within, found);
        return found;
    }

    // Only what is SHOWN, as the node itself counts them: a folded node's stubs live in the tree alongside the rows.
    private static void Gather(IUIComponent within, List<CanvasNodeSocket> found)
    {
        if (within.Visibility != Visibility.Visible) return;

        if (within is CanvasNodeSocket socket) found.Add(socket);

        foreach (var child in within.VisualChildren)
        {
            if (child is IUIComponent visual) Gather(visual, found);
        }
    }

    // WHERE a socket is, which a connection cannot work out for itself: which side it is on, how far down and how far
    // it hangs over the edge are facts about the TEMPLATE, and each theme answers them differently.
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void ANodeSaysWhereItsSocketsAre(string theme)
    {
        Use(ThemeNamed(theme));

        var node = new CanvasNode { Inputs = 2, Outputs = 1 };
        // WITH AN OWNER: the layer places its children from the camera, and one without a camera arranges nothing at
        // all - so everything inside a node stays at no size, which is not a state it is ever in on screen.
        var layer = new CanvasElementLayer { Owner = new InfiniteCanvas() };
        layer.Sync(new List<ElementItem> { new(node, new Rect(0, 0, 190, 110)) });

        var window = new Window { Width = 600, Height = 600, Content = layer };
        Settle(window);

        var first = node.Where(node.InputPins[0]);
        var second = node.Where(node.InputPins[1]);
        var output = node.Where(node.OutputPins[0]);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.Not.Null, "the node cannot find its own socket");
            Assert.That(second, Is.Not.Null);
            Assert.That(output, Is.Not.Null);

            Assert.That(second.Value.Y, Is.GreaterThan(first.Value.Y), "the second input is below the first");
            Assert.That(output.Value.X, Is.GreaterThan(first.Value.X), "an output is on the other side");
            Assert.That(first.Value.X, Is.LessThan(20), "an input sits on the node's left edge");
        });
    }

    // ...and WHICH socket is under a point, which is how a connection is started by aiming at one.
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void ANodeSaysWhichSocketIsUnderAPoint(string theme)
    {
        Use(ThemeNamed(theme));

        var node = new CanvasNode { Inputs = 2, Outputs = 1 };
        // WITH AN OWNER: the layer places its children from the camera, and one without a camera arranges nothing at
        // all - so everything inside a node stays at no size, which is not a state it is ever in on screen.
        var layer = new CanvasElementLayer { Owner = new InfiniteCanvas() };
        layer.Sync(new List<ElementItem> { new(node, new Rect(0, 0, 190, 110)) });

        var window = new Window { Width = 600, Height = 600, Content = layer };
        Settle(window);

        var at = node.Where(node.InputPins[1]).Value;

        Assert.Multiple(() =>
        {
            Assert.That(node.PinAt(at), Is.SameAs(node.InputPins[1]));
            Assert.That(node.PinAt(new Vector2(at.X + 60, at.Y)), Is.Null, "the middle of the node is not a socket");
        });
    }

    // The middle follows the colour too: recolouring a docked pin has to repaint what is in it, not just its ring.
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void RecolouringADockedPinRepaintsItsMiddle(string theme)
    {
        Use(ThemeNamed(theme));

        var node = Built(new CanvasNode { Inputs = 1, Outputs = 1 });
        var pin = node.InputPins[0];
        pin.IsConnected = true;

        pin.Color = Brushes.Red;

        Assert.That(pin.Fill, Is.SameAs(Brushes.Red));
    }

    // WHAT IS IN A NODE. A node is a control because what it is FOR is the editable things inside it - a field, a
    // switch, a picture - and the strip and the sockets are the frame around them.
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void ANodeShowsWhatIsPutInIt(string theme)
    {
        Use(ThemeNamed(theme));

        var body = new Border { Width = 90, Height = 40, Background = Brushes.Red };
        var node = Built(new CanvasNode { Inputs = 1, Outputs = 1, Content = body });

        Assert.Multiple(() =>
        {
            Assert.That(body.VisualParent, Is.Not.Null, "what was put in the node never reached the tree");
            Assert.That(body.RenderSize.Width, Is.EqualTo(90).Within(0.5));
            Assert.That(body.RenderSize.Height, Is.EqualTo(40).Within(0.5));
        });
    }

    // ...and it goes BETWEEN the socket columns, which is the only place a body can be without standing between a
    // socket and the wire reaching for it.
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void TheBodySitsBetweenTheSockets(string theme)
    {
        Use(ThemeNamed(theme));

        var body = new Border { Width = 60, Height = 30, Background = Brushes.Red };
        var node = Built(new CanvasNode { Inputs = 1, Outputs = 1, Content = body });

        var layer = new CanvasElementLayer();
        var window = new Window { Width = 600, Height = 600, Content = layer };
        Settle(window);

        var input = node.Where(node.InputPins[0]);
        var output = node.Where(node.OutputPins[0]);
        var middle = body.TranslatePoint(new Vector2(body.RenderSize.Width / 2, 0), node);

        Assert.Multiple(() =>
        {
            Assert.That(middle.X, Is.GreaterThan(input.Value.X), "the body is over the input column");
            Assert.That(middle.X, Is.LessThan(output.Value.X), "the body is over the output column");
        });
    }

    // EVERY SOCKET ROW CARRIES A GRIP, because moving a socket has to have a target of its own: the disc is where a
    // wire is pulled from, and one press cannot mean both. One per row and not one per node - the row is what moves.
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void EverySocketRowCarriesAGrip(string theme)
    {
        Use(ThemeNamed(theme));

        var node = Built(new CanvasNode { Inputs = 2, Outputs = 1 });
        var grips = Grips(node);

        Assert.That(grips, Has.Count.EqualTo(3), "a socket with no grip cannot be moved");

        Assert.Multiple(() =>
        {
            foreach (var grip in grips)
            {
                Assert.That(grip.RenderSize.Width, Is.GreaterThan(0), "a grip that was never given a size");
                Assert.That(grip.RenderSize.Height, Is.LessThanOrEqualTo(20),
                    "a grip taller than a row pushes the sockets apart");
            }
        });
    }

    // ...and a press on one BELONGS TO THE NODE. The canvas drags a node by a press on it, so a press the grip keeps
    // has to be a press the canvas never sees - which is the difference between moving a socket and moving the node.
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void APressOnTheGripIsTakenByTheNode(string theme)
    {
        Use(ThemeNamed(theme));

        // ON THE PLANE, in a window: the gesture asks where the pointer is, and a node standing on nothing has no
        // coordinates to answer in.
        var node = new CanvasNode { Inputs = 2, Outputs = 1 };
        var layer = new CanvasElementLayer { Owner = new InfiniteCanvas() };
        layer.Sync(new List<ElementItem> { new(node, new Rect(0, 0, 190, 110)) });

        var window = new Window { Width = 600, Height = 600, Content = layer };
        Settle(window);

        var grip = Grips(node)[0];

        var press = new Adamantium.UI.Core.Input.MouseButtonEventArgs(
            Adamantium.UI.Core.Input.Mouse.PrimaryDevice, Adamantium.UI.Core.Input.MouseButtons.Left,
            Adamantium.UI.Core.Input.MouseButtonState.Pressed,
            Adamantium.UI.Core.Input.InputModifiers.LeftMouseButton, 0)
        {
            RoutedEvent = Adamantium.UI.Core.Input.Mouse.MouseDownEvent
        };

        ((IObservableComponent)grip).RaiseEvent(press);

        Assert.That(press.Handled, Is.True, "a press on the grip that the canvas also sees drags the whole node");
    }

    // THE WHOLE GESTURE: a grip taken, drawn down its own side and let go. While it lasts the rows are only DRAWN
    // elsewhere - nothing about the node changes - and the move is said ONCE, when the rows have settled. Said in the
    // middle of it, the socket would be taken out of a list everything else reads by position.
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void DraggingAGripSaysTheSocketMovedOnceAtTheEnd(string theme)
    {
        Use(ThemeNamed(theme));

        var node = new CanvasNode { Inputs = 3, Outputs = 1 };
        var layer = new CanvasElementLayer { Owner = new InfiniteCanvas() };
        layer.Sync(new List<ElementItem> { new(node, new Rect(0, 0, 190, 140)) });

        var window = new Window { Width = 600, Height = 600, Content = layer };
        Settle(window);

        CanvasPinOrderEventArgs said = null;
        node.PinsReordered += (_, e) => said = e;

        var grip = Grips(node)[0];

        Point(window, 0);
        ((IObservableComponent)grip).RaiseEvent(new Adamantium.UI.Core.Input.MouseButtonEventArgs(
            Adamantium.UI.Core.Input.Mouse.PrimaryDevice, Adamantium.UI.Core.Input.MouseButtons.Left,
            Adamantium.UI.Core.Input.MouseButtonState.Pressed,
            Adamantium.UI.Core.Input.InputModifiers.LeftMouseButton, 0)
        {
            RoutedEvent = Adamantium.UI.Core.Input.Mouse.MouseDownEvent
        });

        // Well past the last row: a socket cannot be drawn off its own side, so this is the bottom of the list.
        Point(window, 1000);
        ((IObservableComponent)node).RaiseEvent(new Adamantium.UI.Core.Input.MouseEventArgs(
            Adamantium.UI.Core.Input.Mouse.PrimaryDevice, Adamantium.UI.Core.Input.InputModifiers.LeftMouseButton, 0)
        {
            RoutedEvent = Adamantium.UI.Core.Input.Mouse.MouseMoveEvent
        });

        // The rows that step aside SLIDE there, so they are read once the slide is over.
        Adamantium.UI.Core.Media.Animation.AnimationManager.Tick(0.5);

        Assert.Multiple(() =>
        {
            Assert.That(said, Is.Null, "the move was said while the hand was still holding it");

            // ...and the hand is SHOWN what it is doing: the held row went down with it, and the rows it passed
            // stepped up by one to open the gap it will drop into.
            Assert.That(Shifted(Grips(node)[0]), Is.GreaterThan(0), "the held row stayed where it was");
            Assert.That(Shifted(Grips(node)[1]), Is.LessThan(0), "the rows it passed did not step aside");
        });

        ((IObservableComponent)node).RaiseEvent(new Adamantium.UI.Core.Input.MouseButtonEventArgs(
            Adamantium.UI.Core.Input.Mouse.PrimaryDevice, Adamantium.UI.Core.Input.MouseButtons.Left,
            Adamantium.UI.Core.Input.MouseButtonState.Released,
            Adamantium.UI.Core.Input.InputModifiers.None, 0)
        {
            RoutedEvent = Adamantium.UI.Core.Input.Mouse.MouseUpEvent
        });

        // The rows settle, and that is what says the move happened - the picture is finished before the graph changes.
        Adamantium.UI.Core.Media.Animation.AnimationManager.Tick(0.5);

        Assert.Multiple(() =>
        {
            Assert.That(said, Is.Not.Null, "letting go said nothing at all");
            Assert.That(said.IsInput, Is.True, "a socket crossed to the other side");
            Assert.That(said.From, Is.EqualTo(0));
            Assert.That(said.To, Is.EqualTo(2), "the socket did not go to the end of its side");
        });
    }

    // How far a row is DRAWN from where it was placed, read off the chain above a grip - which is where the gesture
    // puts the offset, because a row is whatever the template made of a pin.
    private static double Shifted(IUIComponent from)
    {
        for (var at = from; at != null; at = at.VisualParent)
        {
            if (at.RenderTransform is { } transform && transform.TranslateY != 0) return transform.TranslateY;
        }

        return 0;
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

    // A node has to be BOTH - dragged about the plane and operated - because what is in one is a field, a switch, a
    // list, and a node whose contents cannot be clicked is a picture of a node. The strip is the handle; everything the
    // application put inside is live.
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void TheStripIsTheHandleAndTheBodyIsNot(string theme)
    {
        Use(ThemeNamed(theme));

        var inside = new Border { Width = 60, Height = 30, Background = Brushes.Red };
        var node = Built(new CanvasNode { Inputs = 1, Outputs = 1, Content = inside });

        var header = node.GetTemplateChild("PART_Header") as IUIComponent;

        Assert.Multiple(() =>
        {
            Assert.That(header, Is.Not.Null, "the node drives this part by name");
            Assert.That(node.IsHandle(header), Is.True, "the strip must pick the node up");
            Assert.That(node.IsHandle(node), Is.True, "so must the node's own chrome");
            Assert.That(node.IsHandle(inside), Is.False,
                "a press on what the application put in the node would drag the node instead of reaching it");
        });
    }

    // WHAT A NODE CARRIES IS DATA, and how that data is drawn is a template chosen for its type - which is the only way
    // an application can put a field in a node without its view-model building controls. The node's template forwards
    // Content and ContentTemplate; forgetting the SELECTOR alongside them leaves it set on the node and never asked,
    // and the node grows to hold a body that draws nothing at all.
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void TheNodeHandsItsTemplateSelectorToWhatDrawsTheContent(string theme)
    {
        Use(ThemeNamed(theme));

        var chooser = new FixedTemplate();
        var node = Built(new CanvasNode { Inputs = 1, Outputs = 1, Content = "carried", ContentTemplateSelector = chooser });

        var presenter = node.GetTemplateChild("PART_ContentPresenter") as ContentPresenter;

        Assert.That(presenter, Is.Not.Null, "the node drives this part by name");
        Assert.That(presenter.ContentTemplateSelector, Is.SameAs(chooser),
            "the selector stops at the node, so nothing ever asks it what to draw");
    }

    private sealed class FixedTemplate : Adamantium.UI.Core.Templates.DataTemplateSelector
    {
    }


    // THE FOLD GOES BOTH WAYS. A switch that only ever folds is a node nobody can open again - and the strip is all
    // that is left of one, so there is nowhere else to reach for.
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void TheFoldOpensTheNodeAgain(string theme)
    {
        Use(ThemeNamed(theme));

        var node = Built(new CanvasNode { Inputs = 2, Outputs = 1 });
        var fold = node.GetTemplateChild("PART_Fold") as Adamantium.UI.Controls.Primitives.ToggleButton;

        Assert.That(fold, Is.Not.Null, "the node drives this part by name");

        fold.IsChecked = true;
        Assert.That(node.IsCollapsed, Is.True, "the switch does not fold the node");

        fold.IsChecked = false;
        Assert.That(node.IsCollapsed, Is.False, "the switch folds the node and cannot open it again");
    }

    // Clicked, not written: a click goes through SetCurrentValue and has to survive the round trip through the binding.
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void TheFoldKeepsAnsweringWhenItIsClicked(string theme)
    {
        Use(ThemeNamed(theme));

        var node = Built(new CanvasNode { Inputs = 2, Outputs = 1 });
        var fold = node.GetTemplateChild("PART_Fold") as Adamantium.UI.Controls.Primitives.ToggleButton;

        Assert.That(fold, Is.Not.Null, "the node drives this part by name");

        fold.PerformClick();
        Assert.That(node.IsCollapsed, Is.True, "a click on the switch does not fold the node");

        fold.PerformClick();
        Assert.That(node.IsCollapsed, Is.False, "the node folds on a click but a second click will not open it again");

        fold.PerformClick();
        Assert.That(node.IsCollapsed, Is.True, "the switch answered twice and then stopped");
    }

    // Somebody else writing the same property - the inspector row bound to it - must not mask the switch for good.
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void TheFoldStillAnswersAfterSomethingElseWroteTheProperty(string theme)
    {
        Use(ThemeNamed(theme));

        var node = Built(new CanvasNode { Inputs = 2, Outputs = 1 });
        var fold = node.GetTemplateChild("PART_Fold") as Adamantium.UI.Controls.Primitives.ToggleButton;

        Assert.That(fold, Is.Not.Null, "the node drives this part by name");

        fold.PerformClick();
        Assert.That(node.IsCollapsed, Is.True, "a click on the switch does not fold the node");

        // The inspector row echoing back the value it was just given.
        node.IsCollapsed = true;

        fold.PerformClick();
        Assert.That(node.IsCollapsed, Is.False,
            "a plain write to the property left the switch clicking away with nothing reaching the node");
    }

    // And using the switch must not promote the property to a stronger slot, or it kills whatever else drives it.
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void TheFoldLeavesThePropertyWhereItWasDrivenFrom(string theme)
    {
        Use(ThemeNamed(theme));

        var node = Built(new CanvasNode { Inputs = 2, Outputs = 1 });
        var fold = node.GetTemplateChild("PART_Fold") as Adamantium.UI.Controls.Primitives.ToggleButton;

        Assert.That(fold, Is.Not.Null, "the node drives this part by name");

        fold.PerformClick();
        Assert.That(node.IsCollapsed, Is.True, "a click on the switch does not fold the node");

        // What an application's own binding writes.
        node.SetValue(CanvasNode.IsCollapsedProperty, false, ValuePriority.Binding);

        Assert.That(node.IsCollapsed, Is.False,
            "using the switch promoted the property out of reach, so the application's binding on it went dead");
    }

    // THE SWITCH DOES NOT MOVE WHEN IT IS USED. It is the only way back from a folded node, and a person folds and
    // opens one by clicking the same place twice - if it jumps aside as the node folds, the second click lands on
    // nothing and the node looks stuck shut. How far it would jump depends on how many sockets the node has, which is
    // why it would look like it worked on one node and not on another.
    [TestCase(1, 1)]
    [TestCase(3, 2)]
    [TestCase(6, 4)]
    public void TheFoldStaysWhereItIsWhenTheNodeFolds(int inputs, int outputs)
    {
        Use(ThemeNamed("Fluent"));

        var node = Built(new CanvasNode { Inputs = inputs, Outputs = outputs });
        var layer = new CanvasElementLayer();
        var window = new Window { Width = 600, Height = 600, Content = layer };
        Settle(window);

        var fold = node.GetTemplateChild("PART_Fold") as IUIComponent;
        var was = fold.TranslatePoint(new Vector2(0, 0), node);

        node.IsCollapsed = true;
        Built(node);
        Settle(window);

        var now = fold.TranslatePoint(new Vector2(0, 0), node);

        Assert.That(now.X, Is.EqualTo(was.X).Within(1.5),
            "the switch moved sideways as the node folded, so the click that opens it again lands on nothing");
    }

    // ...and the switch FOLLOWS the node, so folding it from the inspector shows on the strip too.
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void TheFoldShowsWhatTheNodeIs(string theme)
    {
        Use(ThemeNamed(theme));

        var node = Built(new CanvasNode { Inputs = 2, Outputs = 1 });
        var fold = node.GetTemplateChild("PART_Fold") as Adamantium.UI.Controls.Primitives.ToggleButton;

        node.IsCollapsed = true;
        Assert.That(fold.IsChecked, Is.True, "the node is folded and its switch says it is not");

        node.IsCollapsed = false;
        Assert.That(fold.IsChecked, Is.False);
    }

    // FOLDING a node down to its strip is how a big graph is read: what is finished gets out of the way.
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void AFoldedNodeIsOnlyItsStrip(string theme)
    {
        Use(ThemeNamed(theme));

        var open = Built(new CanvasNode { Inputs = 3, Outputs = 2 });
        var folded = Built(new CanvasNode { Inputs = 3, Outputs = 2, IsCollapsed = true });

        Assert.That(folded.DesiredSize.Height, Is.LessThan(open.DesiredSize.Height / 2),
            "the rows are hidden rather than collapsed, so a folded node is as tall as an open one");
    }

    // AND ITS WIRES STILL LAND SOMEWHERE - all of one side's on one stub. A socket that answered nothing would drop the
    // wire, and a disc per socket turns a folded node back into a caterpillar as wide as its socket count.
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void AFoldedNodeBringsEachSidesWiresToOneStub(string theme)
    {
        Use(ThemeNamed(theme));

        var node = Built(new CanvasNode { Inputs = 2, Outputs = 1, IsCollapsed = true });

        var layer = new CanvasElementLayer();
        var window = new Window { Width = 600, Height = 600, Content = layer };
        Settle(window);

        var first = node.Where(node.InputPins[0]);
        var second = node.Where(node.InputPins[1]);
        var out1 = node.Where(node.OutputPins[0]);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.Not.Null, "a folded node lost its sockets, so its wires have nowhere to land");
            Assert.That(second, Is.Not.Null);
            Assert.That(out1, Is.Not.Null);

            Assert.That((first.Value - second.Value).Length(), Is.LessThan(0.01),
                "the inputs of a folded node did not come to one stub");
            Assert.That(out1.Value.X, Is.GreaterThan(first.Value.X), "the outputs are on the other edge");
        });
    }

    // AND THE NODE STAYS A STRIP however many sockets it has - which is what folding is for.
    [TestCase(2)]
    [TestCase(28)]
    public void AFoldedNodeIsTheSameWhateverItsSocketCount(int sockets)
    {
        Use(ThemeNamed("Fluent"));

        var few = Built(new CanvasNode { Inputs = 1, Outputs = 1, IsCollapsed = true });
        var many = Built(new CanvasNode { Inputs = sockets, Outputs = sockets, IsCollapsed = true });

        Assert.Multiple(() =>
        {
            Assert.That(many.DesiredSize.Height, Is.EqualTo(few.DesiredSize.Height).Within(0.01),
                "the sockets made the folded node taller");
            Assert.That(many.DesiredSize.Width, Is.EqualTo(few.DesiredSize.Width).Within(0.01),
                "the sockets made the folded node wider, so folding a busy node saves nothing");
        });
    }

    // A side with NO sockets shows no stub: an anchor for wires that cannot exist is a lie about the node.
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void AFoldedNodeShowsNoStubOnASideWithNoSockets(string theme)
    {
        Use(ThemeNamed(theme));

        var node = Built(new CanvasNode { Inputs = 0, Outputs = 2, IsCollapsed = true });

        var stubIn = node.GetTemplateChild("PART_FoldedIn") as IUIComponent;
        var stubOut = node.GetTemplateChild("PART_FoldedOut") as IUIComponent;

        Assert.Multiple(() =>
        {
            Assert.That(stubIn.Visibility, Is.Not.EqualTo(Visibility.Visible), "a node with no inputs offers one");
            Assert.That(stubOut.Visibility, Is.EqualTo(Visibility.Visible), "the outputs have nowhere to land");
        });
    }

    // Folded, the node no longer says WHICH socket a point is - so it must not guess, or a wire goes quietly to the
    // wrong one.
    [Test]
    public void AFoldedNodeIsNotAimedAt()
    {
        Use(ThemeNamed("Fluent"));

        var node = Built(new CanvasNode { Inputs = 2, Outputs = 1, IsCollapsed = true });
        var layer = new CanvasElementLayer();
        var window = new Window { Width = 600, Height = 600, Content = layer };
        Settle(window);

        var stub = node.Where(node.InputPins[0]);

        Assert.That(node.PinAt(stub.Value, 4), Is.Null,
            "a press on a folded node's stub picked one of the sockets bunched under it");
    }

    // Folding MOVES every socket - to the strip - and where they are is REMEMBERED, so the memory has to be given up
    // with the fold. Kept, a wire would still be drawn to where the open node had its socket.
    [Test]
    public void FoldingMovesTheSockets()
    {
        Use(ThemeNamed("Fluent"));

        var node = Built(new CanvasNode { Inputs = 1, Outputs = 1 });
        var layer = new CanvasElementLayer();
        var window = new Window { Width = 600, Height = 600, Content = layer };
        Settle(window);

        var was = node.Where(node.InputPins[0]).Value;

        node.IsCollapsed = true;
        Built(node);
        Settle(window);

        Assert.That(node.Where(node.InputPins[0]).Value.Y, Is.Not.EqualTo(was.Y).Within(0.01),
            "the socket is still remembered where the open node had it");
    }

    // Before the node has been arranged there is no answer to give, and the asking is not what makes one: the record is
    // dropped by every arrangement, so an early ask cannot leave the node saying "nowhere" for good.
    [Test]
    public void ASocketAskedForTooEarlyIsAskedAgainLater()
    {
        Use(ThemeNamed("Fluent"));

        var node = new CanvasNode { Inputs = 1, Outputs = 1 };
        var layer = new CanvasElementLayer { Owner = new InfiniteCanvas() };
        layer.Sync(new List<ElementItem> { new(node, new Rect(0, 0, 190, 110)) });

        var window = new Window { Width = 600, Height = 600, Content = layer };

        Assert.That(node.Where(node.InputPins[0]), Is.Null, "it placed a socket that has not been arranged yet");

        Settle(window);

        Assert.That(node.Where(node.InputPins[0]), Is.Not.Null,
            "the node kept the answer it could not give, so nothing ever asked it again");
    }

    // A node with nothing in it costs nothing: a presenter with no content measures to nothing, and the node stays
    // exactly as wide as its sockets.
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void AnEmptyNodeIsNoWiderThanItWas(string theme)
    {
        Use(ThemeNamed(theme));

        var bare = Built(new CanvasNode { Inputs = 2, Outputs = 1 });
        var filled = Built(new CanvasNode { Inputs = 2, Outputs = 1, Content = null });

        Assert.That(filled.DesiredSize.Width, Is.EqualTo(bare.DesiredSize.Width).Within(0.01));
    }

    // FOLDED, the button that folded it must not paint. It is checked while the node is folded, and a checked toggle
    // wears the THEME's accent - which is not the node's: a green node grew a blue pill in its own strip. The checked
    // look is a brush the toggle carries now, so a toggle standing on somebody else's colour can say "not painted".
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void AFoldedNodesButtonDoesNotWearTheThemesAccent(string theme)
    {
        Use(ThemeNamed(theme));

        var node = Built(new CanvasNode { Title = "Number", Inputs = 1, Outputs = 1, IsCollapsed = true });
        var fold = Folder(node);

        Assert.That(fold, Is.Not.Null, "no fold button at all");
        Assert.That(fold.IsChecked, Is.True, "a folded node's button is checked - which is what paints it");

        Assert.Multiple(() =>
        {
            Assert.That(Opaque(fold.BackgroundChecked), Is.False, "it filled itself over the node's own strip");
            Assert.That(Opaque(fold.BorderBrushChecked), Is.False, "it drew an edge on the node's own strip");
            Assert.That(Opaque(fold.BackgroundCheckedPointerOver), Is.False, "it fills as soon as it is hovered");
            Assert.That(Opaque(fold.BackgroundCheckedPressed), Is.False, "it fills as soon as it is held");
        });

        // AND WHAT WAS ACTUALLY PAINTED, not just what it was told: the trigger is what puts the colour on, so a theme
        // that kept writing the accent into the trigger itself would pass every line above and still show the pill.
        var inner = Named(fold, "InnerBorder");

        Assert.That(inner, Is.Not.Null, "the toggle's own chrome is not where it was");
        Assert.That(Opaque(inner.Background), Is.False, "the checked trigger painted over the node's strip");
    }

    // A NODE IS THE SAME NODE AT ANY ZOOM. It was not: measured at the size it takes ON SCREEN, a node made far out
    // was handed a box smaller than its own title, its floors won, and it came out with text the wrong size for itself.
    // The measure is in WORLD units now and the camera is a transform on the drawing, so this is a fact about the
    // layer rather than a number to be re-tuned - which is exactly why it is worth a test.
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void ANodeIsMeasuredTheSameAtEveryZoom(string theme)
    {
        Use(ThemeNamed(theme));

        Assert.That(Sized(1), Is.EqualTo(Sized(12)), "a node made at one zoom is not the node made at another");
        Assert.That(Sized(1), Is.EqualTo(Sized(0.1)), "and the same going the other way");
    }

    private static Size Sized(double scale)
    {
        var node = new CanvasNode { Title = "Multiply", Inputs = 2, Outputs = 1 };
        var layer = new CanvasElementLayer { Owner = new InfiniteCanvas { Scale = scale } };

        layer.Sync(new List<ElementItem> { new(node, new Rect(0, 0, 190, 110)) });

        var window = new Window { Width = 600, Height = 600, Content = layer };

        Settle(window);

        return node.DesiredSize;
    }

    private static Border Named(IUIComponent within, string name)
    {
        if (within is Border found && found.Name == name) return found;

        foreach (var child in within.VisualChildren)
        {
            if (child is not IUIComponent visual) continue;

            if (Named(visual, name) is { } border) return border;
        }

        return null;
    }

    // Anything that would actually cover what is behind it. Null is "no change in that state", and a fully transparent
    // brush covers nothing - everything else paints.
    private static bool Opaque(Brush brush) =>
        brush switch
        {
            null => false,
            SolidColorBrush solid => solid.Color.A > 0,
            _ => true
        };

    private static Adamantium.UI.Controls.Primitives.ToggleButton Folder(IUIComponent within)
    {
        if (within is Adamantium.UI.Controls.Primitives.ToggleButton found && found.Name == "PART_Fold") return found;

        foreach (var child in within.VisualChildren)
        {
            if (child is not IUIComponent visual) continue;

            if (Folder(visual) is { } fold) return fold;
        }

        return null;
    }
}
