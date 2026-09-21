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

/// <summary>THE NODE PALETTE. It is opened by a gesture and ANSWERED BY PICKING: choosing a kind is the whole of the
/// answer, so the node has to be on the plane the moment one is chosen.</summary>
[TestFixture]
public class CanvasPaletteTests
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

    private static readonly IReadOnlyList<ICanvasNodeKind> Catalogue = [new Sort("mix", "Blend"), new Sort("add", "Blend")];

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

    // PICKING A KIND PUTS THE NODE DOWN. The list's selection is the answer the gesture was waiting for, and the whole
    // path - tree selection, the canvas's PickedKind, the catalogue's Make - has to carry it.
    [Test]
    public void PickingAKindPutsThatNodeOnThePlane()
    {
        Use(new Adamantium.UI.Themes.FluentTheme.Fluent());

        var canvas = new InfiniteCanvas
        {
            Mode = CanvasMode.Nodes,
            Scene = new CanvasScene(),
            NodeKinds = Catalogue
        };

        var nodes = new TrackingCollection<ICanvasNode>();
        canvas.Nodes = nodes;

        var window = new Window { Width = 900, Height = 600, Content = canvas };
        Settle(window);

        Assert.That(canvas.AskForNode(new Vector2(120, 80)), Is.True, "the palette would not open at all");

        Settle(window);

        var palette = Found<CanvasNodePalette>(canvas);

        Assert.That(palette, Is.Not.Null, "no palette in the canvas's own chrome");

        var tree = Found<TreeView>(palette);

        Assert.That(tree, Is.Not.Null, "the palette has no list of kinds");

        // WHAT A CLICK ON A ROW LEAVES BEHIND, and nothing else: the row is selected, and everything after that is the
        // control's own business.
        tree.SelectedItem = Catalogue[0];

        Settle(window);

        Assert.Multiple(() =>
        {
            Assert.That(nodes, Has.Count.EqualTo(1), "picking a kind put no node on the plane");
            Assert.That(canvas.IsPaletteOpen, Is.False, "the list stayed open after it had been answered");
        });
    }

    // THE HALF BELOW THE LIST, on its own: writing the answer straight into the canvas has to put the node down. This
    // is what tells a broken binding from a broken placement.
    [Test]
    public void AnsweringTheCanvasDirectlyPutsTheNodeDown()
    {
        Use(new Adamantium.UI.Themes.FluentTheme.Fluent());

        var canvas = new InfiniteCanvas
        {
            Mode = CanvasMode.Nodes,
            Scene = new CanvasScene(),
            NodeKinds = Catalogue
        };

        var nodes = new TrackingCollection<ICanvasNode>();
        canvas.Nodes = nodes;

        var window = new Window { Width = 900, Height = 600, Content = canvas };
        Settle(window);

        canvas.AskForNode(new Vector2(120, 80));
        Settle(window);

        canvas.PickedKind = Catalogue[0];
        Settle(window);

        Assert.That(nodes, Has.Count.EqualTo(1), "the canvas itself did not put the node down");
    }

    private sealed class Sort : ICanvasNodeKind
    {
        public Sort(string kind, string group)
        {
            Kind = kind;
            Group = group;
        }

        public string Kind { get; }

        public string Title => Kind;

        public string Group { get; }

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
