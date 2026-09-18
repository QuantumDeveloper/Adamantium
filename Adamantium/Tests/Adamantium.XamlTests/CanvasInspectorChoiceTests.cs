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

/// <summary>THE DROP-DOWN LINES OF THE INSPECTOR. A node's kind and a socket's kind are chosen from the catalogues the
/// canvas was handed, and a line offering an empty list can neither say what a node is nor change it.
/// <para>The catalogues arrive through BINDINGS on the page, which is to say AFTER the canvas has been templated and
/// its panels have taken it up - so a panel that read them once, when it was handed the canvas, reads null for ever.
/// </para></summary>
[TestFixture]
public class CanvasInspectorChoiceTests
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

    private static readonly IReadOnlyList<ICanvasNodeKind> Catalogue = [new Sort("mix"), new Sort("add")];

    private static readonly IReadOnlyList<ICanvasSocketKind> Flows =
        [new Carries(string.Empty), new Carries("Number"), new Carries("Color")];

    private static void Settle(InfiniteCanvas canvas)
    {
        for (var i = 0; i < 4; i++)
        {
            Adamantium.UI.Extensions.WindowExtension.UpdateTree(canvas);
            Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
            canvas.Measure(new Size(900, 700), force: true);
            canvas.Arrange(new Rect(0, 0, 900, 700));
        }
    }

    // A CANVAS WEARING ITS OWN CHROME, with one node on it and that node selected - which is when the inspector shows
    // a node's lines at all. The catalogues come LAST, the way a page's bindings hand them over.
    private static InfiniteCanvas WithNodeSelected()
    {
        var canvas = new InfiniteCanvas { Mode = CanvasMode.Nodes, Scene = new CanvasScene() };

        var nodes = new TrackingCollection<ICanvasNode>();
        canvas.Nodes = nodes;

        var node = new GraphNode { Kind = "mix", Title = "Mix" };
        node.Inputs.Add(new GraphSocket { Name = "A", Kind = "Number" });
        node.Outputs.Add(new GraphSocket(GraphNode.Branches) { Name = "Out", Kind = "Color" });
        nodes.Add(node);

        canvas.ApplyCurrentTheme();
        Settle(canvas);

        foreach (var item in new List<ICanvasItem>(canvas.ItemsHere()))
        {
            if (item is ElementItem { Model: ICanvasNode } element) canvas.Select(element, false);
        }

        Settle(canvas);

        canvas.NodeKinds = Catalogue;
        canvas.SocketKinds = Flows;

        Settle(canvas);

        return canvas;
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

    private static List<PropertyRow> Rows(IUIComponent within)
    {
        var found = new List<PropertyRow>();
        Gather(within, found);
        return found;
    }

    private static void Gather(IUIComponent within, List<PropertyRow> into)
    {
        if (within is PropertyRow row) into.Add(row);

        foreach (var child in within.VisualChildren)
        {
            if (child is IUIComponent visual) Gather(visual, into);
        }
    }

    private static List<PropertyRow> ChoiceRows(InfiniteCanvas canvas)
    {
        var found = new List<PropertyRow>();

        foreach (var row in Rows(Found<CanvasInspector>(canvas)))
        {
            if (row.Definition is ChoiceProperty && Equals(row.Definition.Header, "Kind")) found.Add(row);
        }

        return found;
    }

    // THE CATALOGUES REACH THE PANEL, WHENEVER THEY ARRIVE. The panel is not the canvas: it is handed one by the pane
    // it rides in, and a page states its catalogues with bindings that are pushed after all of that.
    [Test]
    public void TheInspectorFollowsTheCanvassCatalogues()
    {
        Use(new Adamantium.UI.Themes.FluentTheme.Fluent());

        var canvas = WithNodeSelected();
        var inspector = Found<CanvasInspector>(canvas);

        Assert.That(inspector, Is.Not.Null, "no inspector in the canvas's own chrome");

        Assert.Multiple(() =>
        {
            Assert.That(inspector.NodeKinds, Is.SameAs(Catalogue), "the node kinds never reached the panel");
            Assert.That(inspector.SocketKinds, Is.SameAs(Flows), "the socket kinds never reached the panel");
        });
    }

    // ...AND A LINE THAT CHOOSES ONE OFFERS IT. A drop-down with nothing in it cannot say what a node is, and cannot be
    // used to change it either - which is a line standing blank beside a node that plainly has a kind.
    [Test]
    public void EveryKindLineOffersTheCatalogueAndShowsWhatIsSet()
    {
        Use(new Adamantium.UI.Themes.FluentTheme.Fluent());

        var canvas = WithNodeSelected();
        var rows = ChoiceRows(canvas);

        Assert.That(rows, Is.Not.Empty, "the node's lines were not built at all");

        Assert.Multiple(() =>
        {
            foreach (var row in rows)
            {
                var choice = (ChoiceProperty)row.Definition;

                Assert.That(choice.Choices(), Is.Not.Null, $"the line holding [{row.Value}] offers nothing");

                // ...and the drop-down built over it SHOWS what the object holds: a list nothing is picked out of
                // leaves the line standing blank beside a node that plainly has a kind.
                if (row.Editor is not DropDown drop) continue;

                Assert.That(drop.ItemsSource, Is.Not.Null, "the drop-down was left with no list");
                Assert.That(drop.SelectedItem, Is.Not.Null, $"nothing is shown for the value [{row.Value}]");
            }
        });
    }

    // PICKING THE NEXT THING follows. The canvas edits its selection IN PLACE, so a panel that hands the same list over
    // again says nothing has changed - and the rows go on showing the node that was selected first, however many more
    // are clicked after it.
    [Test]
    public void PickingAnotherNodeMovesTheLinesOntoIt()
    {
        Use(new Adamantium.UI.Themes.FluentTheme.Fluent());

        var canvas = WithNodeSelected();

        var second = new GraphNode { Kind = "add", Title = "Add" };
        second.Inputs.Add(new GraphSocket { Name = "A", Kind = "Number" });
        ((TrackingCollection<ICanvasNode>)canvas.Nodes).Add(second);

        Settle(canvas);

        foreach (var item in new List<ICanvasItem>(canvas.ItemsHere()))
        {
            if (item is ElementItem { Model: ICanvasNode node } element && ReferenceEquals(node, second))
                canvas.Select(element, false);
        }

        Settle(canvas);

        var titles = new List<PropertyRow>();

        foreach (var row in Rows(Found<CanvasInspector>(canvas)))
        {
            if (Equals(row.Definition?.Header, "Title")) titles.Add(row);
        }

        Assert.That(titles, Is.Not.Empty, "the node's title line was not built at all");

        Assert.Multiple(() =>
        {
            foreach (var row in titles)
            {
                Assert.That(row.Targets, Is.Not.Empty, "the line was never pointed at what is selected");
                Assert.That(row.Value, Is.EqualTo("Add"), "the lines stayed on the node that was selected first");
            }
        });
    }

    // What a socket may carry, as an application says it.
    private sealed class Carries : ICanvasSocketKind
    {
        public Carries(string kind) => Kind = kind;

        public string Kind { get; }

        public Color? Color => null;
    }

    // The application's catalogue of node kinds.
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
