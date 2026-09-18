using System.Collections.Generic;
using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>THE CANVAS'S OWN CHROME comes with the control, not with the page: every theme's template carries the tool
/// rail, the inspector, the map, the node palette, the selection bar and the view bar, and a canvas taken out of the
/// box arrives wearing them.</summary>
[TestFixture]
public class CanvasChromeThemeTests
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

    private static Theme ThemeNamed(string name) => name switch
    {
        "MacOs" => new Adamantium.UI.Themes.MacOsTheme.MacOs(),
        "EditorPro" => new Adamantium.UI.Themes.EditorProTheme.EditorPro(),
        _ => new Adamantium.UI.Themes.FluentTheme.Fluent()
    };

    private InfiniteCanvas Built(string theme)
    {
        Use(ThemeNamed(theme));

        var canvas = new InfiniteCanvas { Width = 900, Height = 600 };

        canvas.ApplyCurrentTheme();
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(canvas);
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        canvas.Measure(new Size(900, 600));
        canvas.Arrange(new Rect(0, 0, 900, 600));

        return canvas;
    }

    private static List<CanvasPane> Panes(InfiniteCanvas canvas)
    {
        var found = new List<CanvasPane>();
        Walk(canvas, found);
        return found;
    }

    private static void Walk(IUIComponent at, List<CanvasPane> into)
    {
        if (at is CanvasPane pane) into.Add(pane);

        foreach (var child in at.VisualChildren) Walk(child, into);
    }

    [Test]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void ACanvasArrivesWearingItsOwnPanels(string theme)
    {
        var canvas = Built(theme);
        var panes = Panes(canvas);
        var what = new List<string>();

        foreach (var pane in panes) what.Add($"{pane.Placement}/{pane.Kind}/{pane.Content?.GetType().Name ?? "-"}");

        Assert.That(panes, Has.Count.EqualTo(6),
            $"{theme} did not bring the canvas's own panes - it brought: {string.Join(", ", what)}");
    }

    [Test]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void EachPanelHoldsTheControlItIsFor(string theme)
    {
        var canvas = Built(theme);
        var inside = new List<object>();

        foreach (var pane in Panes(canvas))
        {
            if (pane.Content != null) inside.Add(pane.Content);
        }

        Assert.Multiple(() =>
        {
            Assert.That(inside, Has.Some.InstanceOf<CanvasViewBar>(), "no view bar");
            Assert.That(inside, Has.Some.InstanceOf<CanvasSelectionBar>(), "no selection bar");
            Assert.That(inside, Has.Some.InstanceOf<CanvasMiniMap>(), "no map");
            Assert.That(inside, Has.Some.InstanceOf<CanvasNodePalette>(), "no node palette");
            Assert.That(inside, Has.Some.InstanceOf<CanvasInspector>(), "no inspector");
        });
    }

    // Each piece is pointed at the canvas by the pane it rides in, so nothing in a page has to wire them up.
    [Test]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void EveryPieceKnowsWhichCanvasItIsFor(string theme)
    {
        var canvas = Built(theme);

        foreach (var pane in Panes(canvas))
        {
            Assert.That(pane.Canvas, Is.SameAs(canvas), "a pane was not told whose it is");

            switch (pane.Content)
            {
                case CanvasViewBar bar: Assert.That(bar.Canvas, Is.SameAs(canvas)); break;
                case CanvasSelectionBar bar: Assert.That(bar.Canvas, Is.SameAs(canvas)); break;
                case CanvasMiniMap map: Assert.That(map.Canvas, Is.SameAs(canvas)); break;
                case CanvasNodePalette palette: Assert.That(palette.Canvas, Is.SameAs(canvas)); break;
                case CanvasInspector inspector: Assert.That(inspector.Canvas, Is.SameAs(canvas)); break;
            }
        }
    }

    // ...and they are the LAYER's children, which is what makes them placed at all: a pane standing in the tree that
    // the layer never took up is a pane nothing ever arranges, and an editor comes up wearing its tool rail and
    // nothing else.
    [Test]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void EveryPanelIsGivenRoomOnTheGlass(string theme)
    {
        var canvas = Built(theme);

        Assert.Multiple(() =>
        {
            foreach (var pane in Panes(canvas))
            {
                var what = pane.Content?.GetType().Name ?? pane.Kind.ToString();

                if (pane.Visibility != Visibility.Visible) continue;

                Assert.That(pane.Bounds.Width, Is.GreaterThan(0), $"{what} was given no width");
                Assert.That(pane.Bounds.Height, Is.GreaterThan(0), $"{what} was given no height");

                // ...and a piece with a template of its own actually GOT one. A {TemplateBinding} pointed at a plain
                // CLR property resolves to nothing and applying the template throws, which a theme swallows: the
                // control then comes up with no template at all, which is a panel 340 wide and 0 tall.
                if (pane.Content is Adamantium.UI.Controls.Base.Control piece)
                {
                    Assert.That(piece.VisualChildren, Is.Not.Empty, $"{what} was given no template");
                }
            }
        });
    }

    // WHAT IS DONE TO THE WORK lives under the rows of the PROPERTY PANEL and nowhere else: lining nodes up, spreading
    // them out, framing them, bringing them into view and emptying the plane. Every one of them is a command of the
    // canvas's own, so a page binds nothing - and a button bound to a command that is not there does nothing at all.
    [Test]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void TheInspectorCarriesTheSameActions(string theme)
    {
        var canvas = Built(theme);
        var inspector = Piece<CanvasInspector>(canvas);

        Assert.That(inspector, Is.Not.Null, "no inspector at all");

        var window = new Window { Width = 900, Height = 600, Content = canvas };

        for (var i = 0; i < 4; i++)
        {
            Adamantium.UI.Extensions.WindowExtension.UpdateTree(window);
            Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        }

        var commands = new List<object>();
        Buttons(inspector, commands);

        Assert.Multiple(() =>
        {
            Assert.That(commands, Has.Some.SameAs(canvas.AlignCommand), "the panel cannot line the work up");
            Assert.That(commands, Has.Some.SameAs(canvas.SpreadCommand), "the panel cannot spread it out");
            Assert.That(commands, Has.Some.SameAs(canvas.FrameCommand), "the panel cannot frame a part of it");
            Assert.That(commands, Has.Some.SameAs(canvas.FitViewCommand), "the panel cannot bring it into view");
            Assert.That(commands, Has.Some.SameAs(canvas.ClearCommand), "the panel cannot empty the plane");
        });
    }

    // ...and all of them but the bin belong to a GRAPH: ink is put where the hand put it, so lining it up says nothing
    // about a drawing. The bin is about the canvas and stays in both.
    [Test]
    public void TheGraphSButtonsAreNotOfferedToADrawing()
    {
        var canvas = Built("Fluent");
        var panel = Piece<CanvasInspector>(canvas);

        Assert.That(panel.GraphActions, Is.EqualTo(Visibility.Collapsed),
            "a drawing was offered the graph's buttons");

        canvas.Mode = CanvasMode.Nodes;
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();

        Assert.That(panel.GraphActions, Is.EqualTo(Visibility.Visible), "a graph was not offered them");
    }

    // ...AND THE BUTTONS THEMSELVES COME ALIVE. Every step has to hold for a person to see anything: the command says
    // it can be pressed, the canvas tells it to say so again, and the button asks and enables itself. A test on the
    // command alone would pass over a button that never hears.
    [Test]
    public void TheButtonsLightUpWhenThereIsSomethingToActOn()
    {
        var canvas = Built("Fluent");
        var scene = new CanvasScene();
        canvas.Scene = scene;
        canvas.Mode = CanvasMode.Nodes;

        var window = new Window { Width = 1000, Height = 700, Content = canvas };

        for (var i = 0; i < 4; i++)
        {
            Adamantium.UI.Extensions.WindowExtension.UpdateTree(window);
            Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        }

        var inspector = Piece<CanvasInspector>(canvas);
        var buttons = new List<Adamantium.UI.Controls.Primitives.ButtonBase>();
        Pressables(inspector, buttons);

        Adamantium.UI.Controls.Primitives.ButtonBase For(object command)
        {
            foreach (var button in buttons)
            {
                if (ReferenceEquals(button.Command, command)) return button;
            }

            return null;
        }

        var align = For(canvas.AlignCommand);
        var frame = For(canvas.FrameCommand);
        var bin = For(canvas.ClearCommand);

        Assert.That(align, Is.Not.Null, "no button in the panel lines the work up");

        Assert.Multiple(() =>
        {
            Assert.That(align.IsEnabled, Is.False, "lining up was offered over an empty plane");
            Assert.That(frame?.IsEnabled, Is.False, "framing was offered with nothing selected");
            Assert.That(bin?.IsEnabled, Is.False, "the bin was offered over an empty plane");
        });

        var one = new ShapeItem(CanvasShape.Rectangle, new Rect(0, 0, 60, 40),
            Adamantium.UI.Core.Media.Brushes.White, 2);
        var two = new ShapeItem(CanvasShape.Rectangle, new Rect(200, 0, 60, 40),
            Adamantium.UI.Core.Media.Brushes.White, 2);

        scene.Add(one);
        scene.Add(two);
        canvas.SelectMany(new ICanvasItem[] { one, two }, false);

        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();

        Assert.Multiple(() =>
        {
            Assert.That(align.IsEnabled, Is.True, "two things are selected and the button is still grey");
            Assert.That(frame?.IsEnabled, Is.True, "something is selected and framing is still grey");
            Assert.That(bin?.IsEnabled, Is.True, "there is something on the plane and the bin is still grey");
        });
    }

    // THE NUMBER ROWS CARRY THEIR STEPPERS, and whether they do is the CANVAS's switch - read by the rows through a
    // binding rather than decided in a theme. On by default: a number a hand nudges is what the panel is mostly for,
    // and a panel that offers the buttons on a node's row and not on a shape's is two panels in one place.
    [Test]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void TheNumberRowsFollowTheCanvassSwitch(string theme)
    {
        var canvas = Built(theme);
        var scene = new CanvasScene();
        canvas.Scene = scene;

        var window = new Window { Width = 1000, Height = 700, Content = canvas };

        // SOMETHING SELECTED, so the panel's second face is built and its rows are in the tree - the state a person is
        // in whenever these rows are on screen at all.
        var shape = new ShapeItem(CanvasShape.Rectangle, new Rect(0, 0, 60, 40),
            Adamantium.UI.Core.Media.Brushes.White, 2);

        scene.Add(shape);
        canvas.Select(shape, false);

        for (var i = 0; i < 4; i++)
        {
            Adamantium.UI.Extensions.WindowExtension.UpdateTree(window);
            Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        }

        var numbers = new List<NumericProperty>();
        Numbers(Piece<CanvasInspector>(canvas), numbers);

        Assert.That(numbers, Is.Not.Empty, "the panel has no number rows at all");

        Assert.Multiple(() =>
        {
            foreach (var row in numbers)
            {
                Assert.That(row.ShowButtons, Is.True, $"the {row.Header} row came up without its buttons");
            }
        });

        // ...AND THEY FOLLOW IT. A binding read once is the same as a value written in the theme.
        canvas.ShowsNumberButtons = false;
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();

        Assert.Multiple(() =>
        {
            foreach (var row in numbers)
            {
                Assert.That(row.ShowButtons, Is.False, $"the {row.Header} row kept its buttons after the switch");
            }
        });
    }

    // WHICH SIDE those buttons are on is the canvas's too. LEFT to begin with: the right end of a line is where the
    // LINE's own buttons are - the reset, which comes and goes with the value - and a stepper sharing that end moves
    // out from under the hand between one press and the next.
    [Test]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void TheNumberRowsFollowTheCanvassSideForTheirButtons(string theme)
    {
        var canvas = Built(theme);
        var scene = new CanvasScene();
        canvas.Scene = scene;

        var window = new Window { Width = 1000, Height = 700, Content = canvas };
        var shape = new ShapeItem(CanvasShape.Rectangle, new Rect(0, 0, 60, 40),
            Adamantium.UI.Core.Media.Brushes.White, 2);

        scene.Add(shape);
        canvas.Select(shape, false);

        for (var i = 0; i < 4; i++)
        {
            Adamantium.UI.Extensions.WindowExtension.UpdateTree(window);
            Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        }

        var numbers = new List<NumericProperty>();
        Numbers(Piece<CanvasInspector>(canvas), numbers);

        Assert.That(numbers, Is.Not.Empty, "the panel has no number rows at all");

        Assert.Multiple(() =>
        {
            foreach (var row in numbers)
            {
                Assert.That(row.ButtonsPlacement, Is.EqualTo(NumericButtonsPlacement.Left),
                    $"the {row.Header} row put its buttons where the line's own buttons are");
            }
        });

        // ...and it is a matter of TASTE, so it is the application's to change.
        canvas.NumberButtonsPlacement = NumericButtonsPlacement.Split;
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();

        Assert.Multiple(() =>
        {
            foreach (var row in numbers)
            {
                Assert.That(row.ButtonsPlacement, Is.EqualTo(NumericButtonsPlacement.Split),
                    $"the {row.Header} row did not follow the canvas");
            }
        });
    }

    // ...AND SO IS THE RESET BUTTON'S MANNER. The panel is the canvas's own, so what it does with its buttons is asked
    // of the canvas rather than settled in a theme.
    [Test]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void TheInspectorsGridsFollowTheCanvassResetButton(string theme)
    {
        var canvas = Built(theme);

        canvas.Scene = new CanvasScene();

        var window = new Window { Width = 1000, Height = 700, Content = canvas };

        for (var i = 0; i < 4; i++)
        {
            Adamantium.UI.Extensions.WindowExtension.UpdateTree(window);
            Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        }

        var grids = new List<PropertyGrid>();
        Grids(Piece<CanvasInspector>(canvas), grids);

        Assert.That(grids, Is.Not.Empty, "the panel has no grids at all");

        Assert.Multiple(() =>
        {
            foreach (var grid in grids)
            {
                Assert.That(grid.ResetButton, Is.EqualTo(ResetButtonState.Always), "the steady manner is not default");
            }
        });

        canvas.ResetButton = ResetButtonState.WhenModified;
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();

        Assert.Multiple(() =>
        {
            foreach (var grid in grids)
            {
                Assert.That(grid.ResetButton, Is.EqualTo(ResetButtonState.WhenModified),
                    "a grid of the panel did not follow the canvas");
            }
        });
    }

    private static void Grids(IUIComponent within, List<PropertyGrid> into)
    {
        if (within is PropertyGrid grid) into.Add(grid);

        foreach (var child in within.VisualChildren)
        {
            if (child is IUIComponent visual) Grids(visual, into);
        }
    }

    private static void Numbers(IUIComponent within, List<NumericProperty> into)
    {
        if (within is PropertyGrid grid)
        {
            foreach (var section in grid.Sections)
            {
                foreach (var definition in section.Properties) Numbers(definition, into);
            }
        }

        foreach (var child in within.VisualChildren)
        {
            if (child is IUIComponent visual) Numbers(visual, into);
        }
    }

    private static void Numbers(PropertyDefinition definition, List<NumericProperty> into)
    {
        if (definition is NumericProperty number) into.Add(number);

        foreach (var child in definition.Children) Numbers(child, into);
    }

    private static void Pressables(IUIComponent within, List<Adamantium.UI.Controls.Primitives.ButtonBase> into)
    {
        if (within is Adamantium.UI.Controls.Primitives.ButtonBase button) into.Add(button);

        foreach (var child in within.VisualChildren)
        {
            if (child is IUIComponent visual) Pressables(visual, into);
        }
    }

    private static T Piece<T>(InfiniteCanvas canvas) where T : class
    {
        foreach (var pane in Panes(canvas))
        {
            if (pane.Content is T wanted) return wanted;
        }

        return null;
    }

    // Rooted in a WINDOW, because {Ancestor} is answered by the tree a control stands in: a canvas measured on its own
    // has no root to walk up to, and every button on the bar then reads as one bound to nothing.
    private static void Buttons(IUIComponent within, List<object> commands)
    {
        if (within is Adamantium.UI.Controls.Primitives.ButtonBase button && button.Command != null)
            commands.Add(button.Command);

        foreach (var child in within.VisualChildren)
        {
            if (child is IUIComponent visual) Buttons(visual, commands);
        }
    }

    // The switch is the whole of what an application says about a panel it does not want.
    [Test]
    public void APanelSwitchedOffIsNotShown()
    {
        var canvas = Built("Fluent");

        canvas.ShowsViewBar = false;
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();

        foreach (var pane in Panes(canvas))
        {
            if (pane.Content is CanvasViewBar)
            {
                Assert.That(pane.Visibility, Is.EqualTo(Visibility.Collapsed), "the switch did not take the bar away");
                return;
            }
        }

        Assert.Fail("no view bar to switch off");
    }
}
