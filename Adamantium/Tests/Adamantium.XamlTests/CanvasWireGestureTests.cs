using System.Collections.Generic;
using Adamantium.Core.Collections;
using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Resources;
using Adamantium.UITests.Graph;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>DRAWING A WIRE from one socket to another, on a canvas wearing its own chrome - which is the state a person
/// is actually in: the panels are up, something is selected, and the graph is being wired.</summary>
[TestFixture]
public class CanvasWireGestureTests
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

    private static readonly IReadOnlyList<ICanvasNodeKind> Catalogue = [new Sort("source"), new Sort("sink")];

    private static readonly IReadOnlyList<ICanvasSocketKind> Flows = [new Carries(string.Empty), new Carries("Number")];

    private static void Settle(Window window)
    {
        for (var i = 0; i < 6; i++)
        {
            Adamantium.UI.Extensions.WindowExtension.UpdateTree(window);
            Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        }
    }

    private static T Found<T>(IUIComponent within) where T : class
    {
        if (within is T wanted) return wanted;

        foreach (var child in within.VisualChildren)
        {
            if (child is IUIComponent visual && Found<T>(visual) is { } deep) return deep;
        }

        return null;
    }

    private static Vector2 At(ElementItem item, CanvasNodePin pin)
    {
        var local = ((CanvasNode)item.Element).Where(pin) ?? Vector2.Zero;

        return new Vector2(item.World.X + local.X, item.World.Y + local.Y);
    }

    private static ElementItem ItemOf(InfiniteCanvas canvas, ICanvasNode node)
    {
        foreach (var item in new List<ICanvasItem>(canvas.ItemsHere()))
        {
            if (item is ElementItem { Model: { } model } element && ReferenceEquals(model, node)) return element;
        }

        return null;
    }

    // A WIRE PULLED ONTO A SOCKET, with the chrome standing and the node it leaves selected. The application froze
    // solid doing exactly this, so the claim is first of all that it ENDS.
    [Test]
    [Timeout(30000)]
    public void PullingAWireOntoASocketJoinsTheTwoAndEnds()
    {
        Use(new Adamantium.UI.Themes.FluentTheme.Fluent());

        var canvas = new InfiniteCanvas
        {
            Mode = CanvasMode.Nodes,
            Scene = new CanvasScene(),
            NodeKinds = Catalogue,
            SocketKinds = Flows
        };

        var nodes = new TrackingCollection<ICanvasNode>();
        canvas.Nodes = nodes;

        var window = new Window { Width = 1200, Height = 800, Content = canvas };
        Settle(window);

        var from = new GraphNode { Kind = "source", Title = "Source", Left = 40, Top = 60, Width = 160 };
        from.Outputs.Add(new GraphSocket(GraphNode.Branches) { Name = "Out", Kind = "Number" });

        var to = new GraphNode { Kind = "sink", Title = "Sink", Left = 420, Top = 200, Width = 160 };
        to.Inputs.Add(new GraphSocket { Name = "In", Kind = "Number" });

        nodes.Add(from);
        nodes.Add(to);

        Settle(window);

        var fromItem = ItemOf(canvas, from);
        var toItem = ItemOf(canvas, to);

        Assert.That(fromItem, Is.Not.Null, "the first node never reached the plane");
        Assert.That(toItem, Is.Not.Null, "the second node never reached the plane");

        // SELECTED, because that is the state a person is in while wiring: the panel is showing the node's rows and
        // following everything that happens to it.
        canvas.Select(fromItem, false);
        Settle(window);

        var tool = new SelectTool();
        canvas.Tool = tool;

        var start = At(fromItem, ((CanvasNode)fromItem.Element).OutputPins[0]);
        var end = At(toItem, ((CanvasNode)toItem.Element).InputPins[0]);

        Assert.That(tool.Wires.Press(canvas, start), Is.True, "the press missed the socket, so this proves nothing");

        // PULLED, not teleported: the freeze was reported while dragging TOWARD the socket, and every step of the drag
        // asks the plane what is under the pointer.
        for (var step = 1; step <= 8; step++)
        {
            var at = new Vector2(start.X + (end.X - start.X) * step / 8.0, start.Y + (end.Y - start.Y) * step / 8.0);

            tool.Wires.Move(canvas, at);
            Settle(window);
        }

        tool.Wires.Release(canvas, end);
        Settle(window);

        Assert.That(from.Outputs[0].Connections, Has.Count.EqualTo(1), "the wire was not joined to the socket");
    }

    private sealed class Carries : ICanvasSocketKind
    {
        public Carries(string kind) => Kind = kind;

        public string Kind { get; }

        public Color? Color => null;
    }

    private sealed class Sort : ICanvasNodeKind
    {
        public Sort(string kind) => Kind = kind;

        public string Kind { get; }

        public string Title => Kind;

        public Color? Accent => null;

        public ICanvasNodeSpecialization Create() => new Body();

        public ICanvasNode Make()
        {
            var made = new GraphNode { Kind = Kind, Title = Title };

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
}
