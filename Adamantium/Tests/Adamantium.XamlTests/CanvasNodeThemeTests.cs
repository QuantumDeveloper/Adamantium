using System.Collections.Generic;
using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
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

    private static void Gather(IUIComponent within, List<CanvasNodeSocket> found)
    {
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
}
