using System.Collections.Generic;
using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
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

    // EVERY THEME OFFERS THE LAYER NUMBER, and offers it for anything on the plane. The bar's two buttons say "over the
    // thing in front of you"; this says the place outright, which is what a person who already knows the answer wants -
    // and one theme that carried it while another did not would be a canvas that changed what it could do when dressed.
    [Test]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void EveryThemeOffersTheLayerNumber(string theme)
    {
        var canvas = Built(theme);
        var scene = new CanvasScene();

        canvas.Scene = scene;

        // SOMETHING SELECTED, because that is when the panel has anything to say. The lines come from a set chosen by
        // what the thing IS, and nothing is not a kind.
        var picture = new ElementItem(new Image { Width = 80, Height = 60 }, new Rect(0, 0, 80, 60));

        scene.Add(picture);

        var window = new Window { Width = 1000, Height = 700, Content = canvas };

        Settle(window);
        canvas.Select(picture, false);
        Settle(window);

        var found = false;

        foreach (var grid in Grids(canvas))
        {
            foreach (var section in grid.Displayed)
            {
                if (section.Header != "Layer") continue;

                Assert.That(section.Properties, Is.Not.Empty, "the Layer section is empty");
                found = true;
            }
        }

        Assert.That(found, Is.True, $"{theme} offers no way to say which layer a thing is on");
    }

    // A LINE THAT IS ABOUT PICTURES SHOWS ITSELF ONLY FOR ONE. "Source" on a button is a line about something a button
    // does not have, and a person reading the panel reasonably asks what it is doing there. The line decides it by
    // asking what is selected - see CanvasKindConverter - so a panel never has to learn the kinds.
    [Test]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void ThePictureLineIsShownForAPictureAndNotForAButton(string theme)
    {
        var canvas = Built(theme);
        var scene = new CanvasScene();

        canvas.Scene = scene;

        var picture = new ElementItem(new Image { Width = 80, Height = 60 }, new Rect(0, 0, 80, 60));
        var button = new ElementItem(new Adamantium.UI.Controls.Buttons.Button { Content = "Press" },
            new Rect(100, 0, 80, 30));

        scene.Add(picture);
        scene.Add(button);

        var window = new Window { Width = 1000, Height = 700, Content = canvas };

        Settle(window);

        canvas.Select(button, false);
        Settle(window);

        // BOTH WAYS ROUND, because a line that only ever answers the FIRST thing it was pointed at looks right the once
        // and is wrong ever after - which is exactly what an ancestor binding on a plain, one-hop path turned out to do.
        Assert.That(Source(canvas), Is.False, $"{theme} offers a button a line for a picture it cannot hold");

        canvas.Select(picture, false);
        Settle(window);

        Assert.That(Source(canvas), Is.True, $"{theme} hides the picture's own Source line from a picture");

        canvas.Select(button, false);
        Settle(window);

        Assert.That(Source(canvas), Is.False, $"{theme} left the picture line on a button it had been swapped to");
    }

    // Layout, bindings and whatever those two set off, until nothing moves. An application runs a pass per frame; a
    // test that changes one thing has to say how far the change is allowed to travel before it looks.
    private static void Settle(Window window)
    {
        for (var i = 0; i < 4; i++)
        {
            Adamantium.UI.Extensions.WindowExtension.UpdateTree(window);
            Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        }
    }

    // AN APPLICATION ADDS ITS OWN LINES, and that is the whole point of the panel's contents being a resource rather
    // than part of a theme: a set naming a kind the default already covers replaces it, one naming a new kind is added,
    // and nobody has to copy the panel to say one thing about one object.
    [Test]
    public void AnApplicationsOwnSetsAreLaidOverTheThemes()
    {
        var canvas = Built("Fluent");
        var scene = new CanvasScene();

        canvas.Scene = scene;

        var mine = new CanvasInspectorSections();
        var set = new CanvasSectionSet { For = "Image" };

        set.Sections.Add(new PropertySection { Header = "Mine", IsExpanded = true });
        set.Sections[0].Properties.Add(new StringProperty
        {
            Header = "Note",
            Binding = new Adamantium.UI.Core.Data.Binding("Label")
        });
        mine.Sets.Add(set);

        canvas.InspectorSections = mine;

        var picture = new ElementItem(new Image { Width = 80, Height = 60 }, new Rect(0, 0, 80, 60));

        scene.Add(picture);

        var window = new Window { Width = 1000, Height = 700, Content = canvas };

        Settle(window);
        canvas.Select(picture, false);
        Settle(window);

        var headers = new List<object>();

        foreach (var grid in Grids(canvas))
        {
            foreach (var section in grid.Displayed) headers.Add(section.Header);
        }

        Assert.Multiple(() =>
        {
            // The one it names REPLACES the default for that kind - which is what a set for a kind already covered
            // means - so the picture's own lines are gone and these are in their place.
            Assert.That(headers, Does.Contain("Mine"), "the application's own set never reached the panel");
            Assert.That(headers, Does.Not.Contain("Picture"), "a set naming a kind did not replace the default for it");

            // ...and everything it does NOT name is untouched: a control's place and size still come from the theme's.
            Assert.That(headers, Does.Contain("Control"), "laying one set over the rest took the others away");
            Assert.That(headers, Does.Contain("Layer"), "the lines every kind gets were lost");
        });
    }

    // SEVERAL THINGS AT ONCE still have SOMETHING in common - where they stand, if nothing else. A panel that went
    // blank the moment a band was drawn round a drawing would read as a panel that had lost its contents, and the one
    // thing a person does right after selecting several is move them about.
    [Test]
    public void AMixedSelectionKeepsTheLinesEveryKindHas()
    {
        var canvas = Built("Fluent");
        var scene = new CanvasScene();

        canvas.Scene = scene;

        var picture = new ElementItem(new Image { Width = 80, Height = 60 }, new Rect(0, 0, 80, 60));
        var box = new ShapeItem(CanvasShape.Rectangle, new Rect(100, 0, 60, 40), null, 0, Adamantium.UI.Core.Media.Brushes.DeepPink);
        var round = new ShapeItem(CanvasShape.Ellipse, new Rect(200, 0, 60, 40), null, 0, Adamantium.UI.Core.Media.Brushes.Lime);

        scene.Add(picture);
        scene.Add(box);
        scene.Add(round);

        var window = new Window { Width = 1000, Height = 700, Content = canvas };

        Settle(window);

        // A PICTURE AND A SHAPE agree about nothing but being on the plane.
        canvas.SelectMany(new List<ICanvasItem> { picture, box }, false);
        Settle(window);

        var headers = Headers(canvas);

        Assert.Multiple(() =>
        {
            Assert.That(headers, Does.Contain("Layer"), "a mixed selection was left with no lines at all");
            Assert.That(headers, Does.Not.Contain("Picture"), "a shape was offered a picture's own lines");
            Assert.That(headers, Does.Not.Contain("Shape"), "a picture was offered a shape's own lines");
        });

        // TWO SHAPES OF ONE FAMILY share what every shape has, and nothing of what only one form has.
        canvas.SelectMany(new List<ICanvasItem> { box, round }, false);
        Settle(window);

        headers = Headers(canvas);

        Assert.Multiple(() =>
        {
            Assert.That(headers, Does.Contain("Shape"), "two shapes were not offered what every shape has");
            Assert.That(headers, Does.Not.Contain("Corner"), "an ellipse was offered a rectangle's corners");
        });
    }

    // PICKING UP A TOOL SHOWS WHAT IT IS SET TO, whatever is selected. Settings are chosen BEFORE the stroke, so a
    // panel that answered "let go of what you are holding first" left a person drawing with the last thing's settings.
    // Touching the selection says the opposite - that IS the thing being worked on - and the panel follows back.
    [Test]
    public void ReachingForAToolBringsItsSettingsForward()
    {
        var canvas = Built("Fluent");
        var scene = new CanvasScene();

        canvas.Scene = scene;

        var box = new ShapeItem(CanvasShape.Rectangle, new Rect(0, 0, 60, 40), null, 0,
            Adamantium.UI.Core.Media.Brushes.DeepPink);

        scene.Add(box);

        var window = new Window { Width = 1000, Height = 700, Content = canvas };

        Settle(window);
        canvas.Select(box, false);
        Settle(window);

        var inspector = Piece<CanvasInspector>(canvas);

        Assert.That(inspector.SelectionFace, Is.EqualTo(Visibility.Visible),
            "picking something up did not show what it is");

        // ...and now a pen is reached for, with the shape still held.
        canvas.Tool = new PenTool();
        Settle(window);

        Assert.Multiple(() =>
        {
            Assert.That(inspector.ToolFace, Is.EqualTo(Visibility.Visible),
                "a tool was picked up and its settings stayed out of reach");
            Assert.That(Headers(canvas), Does.Contain("Pen"), "the pen's own lines were not offered");
            Assert.That(canvas.Selection, Has.Count.EqualTo(1), "reaching for a tool dropped what was held");
        });

        // ...and touching the selection again brings the thing back.
        canvas.Select(box, false);
        Settle(window);

        Assert.That(inspector.SelectionFace, Is.EqualTo(Visibility.Visible),
            "the panel would not come back to the thing that was picked up again");
    }

    private static List<object> Headers(InfiniteCanvas canvas)
    {
        var found = new List<object>();

        foreach (var grid in Grids(canvas))
        {
            foreach (var section in grid.Displayed) found.Add(section.Header);
        }

        return found;
    }

    private static List<PropertyGrid> Grids(InfiniteCanvas canvas)
    {
        var grids = new List<PropertyGrid>();

        Grids(Piece<CanvasInspector>(canvas), grids);

        return grids;
    }

    // Whether the Source line would be shown, asked of the definition the theme wrote rather than of a row: a row that
    // is not shown is a row the grid never built, so there would be nothing to find.
    private static bool Source(InfiniteCanvas canvas)
    {
        foreach (var grid in Grids(canvas))
        {
            foreach (var section in grid.Displayed)
            {
                foreach (var definition in section.Properties)
                {
                    if (definition is ImageSourceProperty) return true;
                }
            }
        }

        return false;
    }

    // A DRAWING HAS ITS OWN FILE BUTTONS, and they are not the graph's: a drawing is written as SVG because other
    // people's tools have a claim on it, a graph as JSON because it means something only here. So the panel offers one
    // pair or the other, never both.
    [Test]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void ThePanelOffersTheDrawingsFileButtonsInADrawing(string theme)
    {
        var canvas = Built(theme);

        canvas.Scene = new CanvasScene();
        canvas.Mode = CanvasMode.Drawing;

        var window = new Window { Width = 1000, Height = 700, Content = canvas };

        for (var i = 0; i < 4; i++)
        {
            Adamantium.UI.Extensions.WindowExtension.UpdateTree(window);
            Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        }

        var inspector = Piece<CanvasInspector>(canvas);

        Assert.Multiple(() =>
        {
            Assert.That(inspector.DrawingActions, Is.EqualTo(Visibility.Visible),
                "a drawing was offered no way to write itself out");
            Assert.That(inspector.GraphActions, Is.EqualTo(Visibility.Collapsed),
                "a drawing was offered the graph's buttons");
        });

        // ...AND THE EXPORT LIGHTS UP once there is something to write. A button that is always grey is a button that
        // was never wired to the thing it does - which is exactly how the last row of these was found.
        Assert.That(canvas.ExportSvgCommand.CanExecute(null), Is.False, "an empty plane offered to export itself");

        canvas.Scene.Add(new ShapeItem(CanvasShape.Rectangle, new Rect(0, 0, 40, 30),
            Adamantium.UI.Core.Media.Brushes.White, 2));

        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();

        Assert.That(canvas.ExportSvgCommand.CanExecute(null), Is.True, "a drawing could not be exported");

        canvas.Mode = CanvasMode.Nodes;
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();

        Assert.That(inspector.DrawingActions, Is.EqualTo(Visibility.Collapsed),
            "a graph kept the drawing's buttons");
    }

    // A LAYER PER RUN, cut by the canvas from the scene's own order - so a theme's template says WHERE the stack goes
    // and never how many layers are in it. A theme that still declared them by hand would pin the old two-band world
    // in place and nothing on the plane could be reordered past a control.
    [Test]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void EveryThemeLeavesTheStackToTheCanvas(string theme)
    {
        var canvas = Built(theme);
        var scene = new CanvasScene();

        canvas.Scene = scene;
        canvas.Mode = CanvasMode.Drawing;

        var window = new Window { Width = 1000, Height = 700, Content = canvas };
        var ink = new StrokeItem(new Vector2(0, 0), Adamantium.UI.Core.Media.Brushes.White, 2);

        ink.Add(new Vector2(0, 0));
        ink.Add(new Vector2(40, 40));

        scene.Add(new ElementItem(new Adamantium.UI.Controls.Buttons.Button(), new Rect(0, 0, 60, 30)));
        scene.Add(ink);

        for (var i = 0; i < 4; i++)
        {
            Adamantium.UI.Extensions.WindowExtension.UpdateTree(window);
            Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        }

        var stack = canvas.GetTemplateChild("PART_Layers") as Adamantium.UI.Controls.Panels.Panel;

        Assert.That(stack, Is.Not.Null, $"{theme} has nowhere to put the plane's layers");
        Assert.That(stack.Children.Count, Is.EqualTo(2),
            "a control and a drawn thing after it is two runs, and each wants its own layer");
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
            foreach (var section in grid.Displayed)
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

    // THE PANEL THAT LISTS THE PLANE OPENS A GROUP. It used to show the top level only, so gathering a picture and the
    // stroke over it took both of them out of the list and left "Group (2)" standing where they had been - the panel
    // saying the plane had lost them, and nothing but breaking the group open would bring them back.
    [Test]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void TheStructureOpensAGroupToWhatIsInIt(string theme)
    {
        var canvas = Built(theme);
        var picture = new ElementItem(new Image { Width = 40, Height = 40 }, new Rect(0, 0, 40, 40));
        var ink = new StrokeItem(new Vector2(0, 0), Adamantium.UI.Core.Media.Brushes.White, 2);

        ink.Add(new Vector2(10, 10));

        canvas.Scene = new CanvasScene();
        canvas.Scene.Add(picture);
        canvas.Scene.Add(ink);
        canvas.SelectMany([picture, ink], false);

        var group = canvas.GroupSelection();

        Assert.That(group, Is.Not.Null, "nothing was gathered, so there is nothing to ask about");

        var inspector = Piece<CanvasInspector>(canvas);

        inspector.ShowsStructure = true;
        Settle(canvas);

        var rows = Rows(canvas);

        Assert.That(Headers(rows), Is.EqualTo(new object[] { group }), "the panel is not showing the plane's top level");

        var branch = rows[0];

        Assert.That(branch.HasItems, Is.True, $"{theme} shows a group as a leaf - there is nothing to open");

        branch.IsExpanded = true;
        Settle(canvas);

        Assert.That(Headers(Rows(canvas)), Is.EqualTo(new object[] { group, ink, picture }),
            $"{theme} opened the group onto something other than what is in it, topmost first");
    }

    // A CONTROL MADE AT A ZOOM IS THE SIZE IT CAN BE, and the frame round it says the same. A box dragged out 200
    // screen pixels wide is 25 in the world at 8x - which a themed control refuses: it has floors of its own, laid
    // itself out at its minimum and drew eight times that, while the frame and every hit stayed on a rectangle a
    // fraction of its size. Themed on purpose: the floors come from the theme, so headless there is nothing to refuse.
    [Test]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void AControlTooSmallForItselfWidensTheItemToMatch(string theme)
    {
        var canvas = Built(theme);
        var control = new Adamantium.UI.Controls.Text.TextBox();
        var item = new ElementItem(control, new Rect(0, 0, 25, 17.5));

        canvas.Scene = new CanvasScene();
        canvas.Scene.Add(item);
        canvas.Scale = 8;
        canvas.Offset = new Vector2(450, 300);
        Settle(canvas);

        Assert.Multiple(() =>
        {
            Assert.That(item.World.Width, Is.EqualTo(control.RenderSize.Width).Within(0.5),
                $"{theme}: the frame is not the width of what it is drawn round");
            Assert.That(item.World.Height, Is.EqualTo(control.RenderSize.Height).Within(0.5),
                $"{theme}: the frame is not the height of what it is drawn round");
        });
    }

    // THE BOX A HAND DRAGGED OUT IS THE SIZE IT MEANT. Zoomed out, a box 300 screen pixels wide is 1500 in the world -
    // and a themed field, laid out at its own height and sitting in the middle of the room it is offered, came out as
    // a thin control adrift in a large frame.
    [Test]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void AControlFillsTheBoxItWasDrawnIn(string theme)
    {
        var canvas = Built(theme);
        var control = new Adamantium.UI.Controls.Text.TextBox();
        var item = new ElementItem(control, new Rect(0, 0, 1500, 1000));

        canvas.Scene = new CanvasScene();
        canvas.Scene.Add(item);
        canvas.Scale = 0.2;
        canvas.Offset = new Vector2(450, 300);
        Settle(canvas);

        Assert.Multiple(() =>
        {
            Assert.That(control.RenderSize.Width, Is.EqualTo(1500).Within(0.5), $"{theme}: it did not fill the box across");
            Assert.That(control.RenderSize.Height, Is.EqualTo(1000).Within(0.5), $"{theme}: it did not fill the box down");
        });
    }

    // THE ZOOM IS SETTABLE BY HAND, and so are the two limits it is held inside. A plane worked at 40x needs a way to
    // say 40 - the wheel and the buttons cannot be asked for a number - and how far the zoom may go is a setting like
    // any other.
    [Test]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void TheCanvasPageOffersTheZoomAndItsLimits(string theme)
    {
        var canvas = Built(theme);
        var inspector = Piece<CanvasInspector>(canvas);

        inspector.ShowsProperties = true;
        Settle(canvas);

        var headers = new List<string>();

        foreach (var row in Rows<PropertyRow>(canvas))
        {
            if (row.Definition?.Header is { } header) headers.Add(header.ToString());
        }

        Assert.That(headers, Has.Member("Zoom"),
            $"{theme}: the canvas page says nothing about the zoom - it shows: {string.Join(", ", headers)}");

        PropertyDefinition zoom = null;

        // The COMPOSITE one: "Zoom" is also the name of a line under Remember, which is a plain switch and has no
        // children to look through.
        foreach (var row in Rows<PropertyRow>(canvas))
        {
            if (row.Definition is CompositeProperty && (row.Definition.Header as string) == "Zoom") zoom = row.Definition;
        }

        var under = new List<string>();

        foreach (var child in zoom.Children) under.Add(child.Header?.ToString());

        Assert.Multiple(() =>
        {
            Assert.That(under, Has.Member("Now"), $"{theme}: the zoom cannot be typed in");
            Assert.That(under, Has.Member("Most"), $"{theme}: there is no way to set how far it may zoom in");
            Assert.That(under, Has.Member("Least"), $"{theme}: there is no way to set how far it may zoom out");
        });
    }

    // A COLOUR PICKED IN THE PANEL LANDS ON THE SHAPE. It lands on a control - the same panel, the same kind of row -
    // and on a shape nothing moved at all.
    [Test]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void AColorPickedForAShapeLandsOnIt(string theme)
    {
        var canvas = Built(theme);
        var scene = new CanvasScene();

        canvas.Scene = scene;

        var window = new Window { Width = 1000, Height = 700, Content = canvas };
        var shape = new ShapeItem(CanvasShape.Rectangle, new Rect(0, 0, 60, 40),
            new SolidColorBrush(Colors.White), 2, new SolidColorBrush(Colors.Gray));

        scene.Add(shape);
        canvas.Select(shape, false);
        Settle(window);

        var rows = new Dictionary<string, PropertyRow>();

        foreach (var row in Rows<PropertyRow>(canvas))
        {
            if (row.Definition is SolidColorBrushProperty && row.Definition.Header is string header) rows[header] = row;
        }

        Assert.That(rows.Keys, Does.Contain("Stroke").And.Contain("Fill"),
            $"{theme}: a shape has no colour lines - it has: {string.Join(", ", rows.Keys)}");

        Assert.That(canvas.Inspector, Is.Not.Null,
            $"{theme}: the canvas was never handed the panel's grid, so a line written in it reaches nobody");

        rows["Fill"].Owner.Write(rows["Fill"], Colors.Red);
        rows["Stroke"].Owner.Write(rows["Stroke"], Colors.Blue);
        Settle(window);

        Assert.Multiple(() =>
        {
            Assert.That((shape.Fill as SolidColorBrush)?.Color, Is.EqualTo(Colors.Red), $"{theme}: the fill did not take");
            Assert.That((shape.Stroke as SolidColorBrush)?.Color, Is.EqualTo(Colors.Blue), $"{theme}: the stroke did not take");
        });

    }

    // SEVERAL SHAPES, EACH ITS OWN COLOUR. The line had no swatch at all then - a colour editor is refused an empty
    // state, and objects that disagree leave the row empty - so on a plane where the shapes are different colours no
    // colour could be picked for any of them. Controls hid it: theirs agree, so theirs had a swatch.
    [Test]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void AColorCanBePickedForShapesThatDisagreeAboutIt(string theme)
    {
        var canvas = Built(theme);
        var scene = new CanvasScene();

        canvas.Scene = scene;

        var window = new Window { Width = 1000, Height = 700, Content = canvas };
        var one = new ShapeItem(CanvasShape.Rectangle, new Rect(0, 0, 60, 40),
            new SolidColorBrush(Colors.White), 2, new SolidColorBrush(Colors.Gray));
        var two = new ShapeItem(CanvasShape.Rectangle, new Rect(200, 0, 60, 40),
            new SolidColorBrush(Colors.Black), 2, new SolidColorBrush(Colors.Green));

        scene.Add(one);
        scene.Add(two);
        canvas.SelectMany(new ICanvasItem[] { one, two }, false);
        Settle(window);

        PropertyRow fill = null;

        foreach (var row in Rows<PropertyRow>(canvas))
        {
            if (row.Definition is SolidColorBrushProperty && (row.Definition.Header as string) == "Fill") fill = row;
        }

        Assert.That(fill, Is.Not.Null, $"{theme}: no fill line for a selection of shapes");
        Assert.That(fill.IsMixed, Is.True, $"{theme}: two different colours were read as one");

        var swatch = Rows<ColorPickerButton>(fill).Count > 0 ? Rows<ColorPickerButton>(fill)[0] : null;

        Assert.That(swatch, Is.Not.Null, $"{theme}: the line offers nothing to pick a colour with");
        Assert.That(swatch.IsIndeterminate, Is.True,
            $"{theme}: the swatch shows a colour neither of them holds");

        swatch.SelectedColor = Colors.Red;
        Settle(window);

        Assert.Multiple(() =>
        {
            Assert.That((one.Fill as SolidColorBrush)?.Color, Is.EqualTo(Colors.Red), $"{theme}: the first kept its own");
            Assert.That((two.Fill as SolidColorBrush)?.Color, Is.EqualTo(Colors.Red), $"{theme}: the second kept its own");
        });
    }

    // A DRAWING READ IN FROM AN SVG can be painted too. It had no set of its own at all, so a whole imported icon
    // arrived with nowhere to say what colour it is.
    [Test]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void AnImportedDrawingOffersItsColors(string theme)
    {
        var canvas = Built(theme);
        var scene = new CanvasScene();

        canvas.Scene = scene;

        var window = new Window { Width = 1000, Height = 700, Content = canvas };
        var path = new PathItem("M 0 0 L 20 0 L 20 20 Z", new SolidColorBrush(Colors.Gray),
            new SolidColorBrush(Colors.White)) { World = new Rect(0, 0, 20, 20) };

        scene.Add(path);
        canvas.Select(path, false);
        Settle(window);

        PropertyRow fill = null;
        var seen = new List<string>();

        foreach (var row in Rows<PropertyRow>(canvas))
        {
            seen.Add(row.Definition?.Header?.ToString() ?? "?");
            if (row.Definition is SolidColorBrushProperty && (row.Definition.Header as string) == "Fill") fill = row;
        }

        Assert.That(fill, Is.Not.Null,
            $"{theme}: an imported drawing has no colour line - selection={canvas.Selection.Count}"
            + $" sort={path.Sort} it shows: {string.Join(", ", seen)}");

        fill.Owner.Write(fill, Colors.Red);
        Settle(window);

        Assert.That((path.Fill as SolidColorBrush)?.Color, Is.EqualTo(Colors.Red), $"{theme}: the colour went nowhere");
    }

    // ...AND WHEN THEY AGREE, all of them move. A write INTO a value edits the object holding it, and the row's copy is
    // the FIRST object's - so one shape turned red and the rest stayed as they were.
    [Test]
    public void AColorPickedForSeveralShapesLandsOnEveryOne()
    {
        var canvas = Built("Fluent");
        var scene = new CanvasScene();

        canvas.Scene = scene;

        var window = new Window { Width = 1000, Height = 700, Content = canvas };
        var one = new ShapeItem(CanvasShape.Rectangle, new Rect(0, 0, 60, 40),
            new SolidColorBrush(Colors.White), 2, new SolidColorBrush(Colors.Gray));
        var two = new ShapeItem(CanvasShape.Rectangle, new Rect(200, 0, 60, 40),
            new SolidColorBrush(Colors.White), 2, new SolidColorBrush(Colors.Gray));

        scene.Add(one);
        scene.Add(two);
        canvas.SelectMany(new ICanvasItem[] { one, two }, false);
        Settle(window);

        PropertyRow fill = null;

        foreach (var row in Rows<PropertyRow>(canvas))
        {
            if (row.Definition is SolidColorBrushProperty && (row.Definition.Header as string) == "Fill") fill = row;
        }

        Assert.That(fill?.IsMixed, Is.False, "two shapes of one colour were read as disagreeing");

        fill.Owner.Write(fill, Colors.Red);
        Settle(window);

        Assert.Multiple(() =>
        {
            Assert.That((one.Fill as SolidColorBrush)?.Color, Is.EqualTo(Colors.Red), "the first was left as it was");
            Assert.That((two.Fill as SolidColorBrush)?.Color, Is.EqualTo(Colors.Red), "the second was left as it was");
        });
    }

    // WHERE THE CAMERA IS, and typed as well as read: a plane with no edges cannot be looked at to find out where it
    // is being looked at, and a drawing left somewhere out in the empty part of it is found by putting the camera back.
    [Test]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void TheCanvasPageOffersWhereTheCameraIs(string theme)
    {
        var canvas = Built(theme);
        var inspector = Piece<CanvasInspector>(canvas);

        canvas.Offset = new Vector2(310, 95);
        inspector.ShowsProperties = true;
        Settle(canvas);

        PropertyDefinition origin = null;
        PropertyGrid page = null;

        foreach (var row in Rows<PropertyRow>(canvas))
        {
            if ((row.Definition?.Header as string) != "Origin") continue;

            origin = row.Definition;
            page = row.Owner;
        }

        Assert.That(origin, Is.Not.Null, $"{theme}: the canvas page does not say where the camera is");

        var rows = new Dictionary<string, PropertyDefinition>();

        foreach (var child in origin.Children) rows[child.Header?.ToString()] = child;

        Assert.That(rows.Keys, Is.EquivalentTo(new[] { "X", "Y" }),
            $"{theme}: the camera's position is not two numbers");

        Assert.Multiple(() =>
        {
            Assert.That(page.ValueOf(canvas, rows["X"]), Is.EqualTo(310.0), $"{theme}: the row is not reading the camera");
            Assert.That(page.ValueOf(canvas, rows["Y"]), Is.EqualTo(95.0), $"{theme}: the row is not reading the camera");
        });

        canvas.Offset = new Vector2(-40, 95);
        Settle(canvas);

        Assert.That(page.ValueOf(canvas, rows["X"]), Is.EqualTo(-40.0),
            $"{theme}: the row froze at what it was bound with");

        page.WriteTo(canvas, rows["X"], 128.0);
        Settle(canvas);

        Assert.Multiple(() =>
        {
            Assert.That(canvas.Offset.X, Is.EqualTo(128).Within(0.01), $"{theme}: a number typed in went nowhere");
            Assert.That(canvas.Offset.Y, Is.EqualTo(95).Within(0.01), $"{theme}: the other half of the camera was lost");
        });
    }

    // WHERE HOME IS, and PUTTING IT BACK. A view kept by the bookmark is a written value like any other, so the line
    // that shows it offers the same reset - which is the only way back to the world's origin at one to one short of
    // finding it by hand and keeping that.
    [Test]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void TheHomeItGoesBackToCanBePutBack(string theme)
    {
        var canvas = Built(theme);
        var inspector = Piece<CanvasInspector>(canvas);

        canvas.Scale = 3;
        canvas.CenterOn(new Vector2(640, -220));
        Settle(canvas);
        canvas.RememberView();

        inspector.ShowsProperties = true;
        Settle(canvas);

        var rows = new Dictionary<string, PropertyRow>();
        PropertyDefinition home = null;

        foreach (var row in Rows<PropertyRow>(canvas))
        {
            if (row.Definition is CompositeProperty && (row.Definition.Header as string) == "Home") home = row.Definition;
        }

        Assert.That(home, Is.Not.Null, $"{theme}: the canvas page does not say where home is");

        foreach (var row in Rows<PropertyRow>(canvas))
        {
            if (row.Definition != null && home.Children.Contains(row.Definition))
                rows[row.Definition.Header?.ToString()] = row;
        }

        Assert.That(rows.Keys, Is.EquivalentTo(new[] { "X", "Y", "Zoom" }), $"{theme}: home is not three numbers");

        Assert.Multiple(() =>
        {
            Assert.That(rows["X"].Value, Is.EqualTo(640.0).Within(1), $"{theme}: the line is not reading home");
            Assert.That(rows["X"].IsModified, Is.True, $"{theme}: the reset is not offered on a home that was kept");
        });

        foreach (var row in rows.Values) row.ResetToDefault();

        Settle(canvas);

        Assert.Multiple(() =>
        {
            Assert.That(canvas.HomeAt.X, Is.EqualTo(0).Within(0.01), $"{theme}: home did not go back");
            Assert.That(canvas.HomeAt.Y, Is.EqualTo(0).Within(0.01), $"{theme}: home did not go back");
            Assert.That(canvas.HomeScale, Is.EqualTo(1).Within(0.01), $"{theme}: the zoom of home did not go back");
        });
    }

    // ...AND IN ONE PRESS, beside the button that kept it. Offered only when something was kept: with Home already
    // meaning the origin there is nothing for it to do, and a button that does nothing is worse than no button.
    [Test]
    public void GivingUpTheKeptViewIsOnePress()
    {
        var canvas = Built("Fluent");

        Assert.That(canvas.KeepsHomeFace, Is.EqualTo(Visibility.Collapsed), "it offers to give up what nobody kept");

        canvas.Scale = 2;
        canvas.CenterOn(new Vector2(-900, 120));
        Settle(canvas);
        canvas.RememberView();

        Assert.That(canvas.KeepsHomeFace, Is.EqualTo(Visibility.Visible), "a kept view is not offered back");

        canvas.ForgetViewCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(canvas.HomeAt, Is.EqualTo(Vector2.Zero));
            Assert.That(canvas.HomeScale, Is.EqualTo(1).Within(0.001));
            Assert.That(canvas.KeepsHomeFace, Is.EqualTo(Visibility.Collapsed), "it still offers to give up nothing");
        });
    }

    // A THICKNESS IS IN WORLD UNITS, so a step stated as a number means something different at every zoom: a tenth is
    // a hair at 1:1 and a white blob at seventeen thousand, where the thinnest line the panel offered was already far
    // too fat to see anything through.
    [Test]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void AThicknessIsNudgedByTheSameAmountOnScreenAtAnyZoom(string theme)
    {
        var canvas = Built(theme);
        var scene = new CanvasScene();

        canvas.Scene = scene;

        var window = new Window { Width = 1000, Height = 700, Content = canvas };
        var shape = new ShapeItem(CanvasShape.Rectangle, new Rect(0, 0, 60, 40),
            new SolidColorBrush(Colors.White), 2);

        scene.Add(shape);
        canvas.Select(shape, false);
        Settle(window);

        NumericProperty Thickness()
        {
            foreach (var row in Rows<PropertyRow>(canvas))
            {
                if (row.Definition is NumericProperty number && (number.Header as string) == "Thickness") return number;
            }

            return null;
        }

        Assert.That(Thickness(), Is.Not.Null, $"{theme}: a shape has no thickness line");

        Assert.Multiple(() =>
        {
            Assert.That(Thickness().Step, Is.EqualTo(0.1).Within(0.0001), $"{theme}: at 1:1 the nudge is not a tenth");
            Assert.That(canvas.ThicknessLeast, Is.EqualTo(0.01).Within(0.0001));
        });

        canvas.Scale = 1000;
        Settle(window);

        Assert.Multiple(() =>
        {
            Assert.That(Thickness().Step, Is.EqualTo(0.0001).Within(1e-9),
                $"{theme}: the nudge did not follow the camera");
            Assert.That(canvas.ThicknessLeast, Is.EqualTo(0.00001).Within(1e-9),
                $"{theme}: the thinnest line did not follow the camera");
        });

        // Zoomed OUT it must not get coarser: a jump of a whole unit is not a nudge.
        canvas.Scale = 0.05;
        Settle(window);

        Assert.That(Thickness().Step, Is.EqualTo(0.1).Within(0.0001), $"{theme}: zooming out made the nudge a jump");
    }

    // EVERY SECTION INSIDE THE PANEL IT BELONGS TO. The sets are rebuilt whenever what is selected changes kind, and
    // the common one is in every set - so it is the section that moves between builds, and one left standing at the
    // window's own corner is what "a piece of the panel flew to the top left" looks like.
    [Test]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void NoSectionEscapesThePanelWhenTheSelectionChangesKind(string theme)
    {
        var canvas = Built(theme);
        var scene = new CanvasScene();

        canvas.Scene = scene;

        var window = new Window { Width = 1000, Height = 700, Content = canvas };
        var box = new ShapeItem(CanvasShape.Rectangle, new Rect(0, 0, 60, 40), new SolidColorBrush(Colors.White), 2);
        var arrow = new ShapeItem(CanvasShape.Arrow, new Rect(100, 0, 120, 20), new SolidColorBrush(Colors.White), 2);
        var stroke = new StrokeItem(new Vector2(0, 200), new SolidColorBrush(Colors.White), 2);

        scene.Add(box);
        scene.Add(arrow);
        scene.Add(stroke);

        foreach (var pick in new ICanvasItem[] { box, arrow, stroke, arrow, box })
        {
            canvas.Select(pick, false);
            Settle(window);

            for (var pass = 0; pass < 2; pass++)
            {
                canvas.Measure(new Size(1000, 700), force: true);
                canvas.Arrange(new Rect(0, 0, 1000, 700));
            }

            Settle(window);

            var pane = canvas.GetTemplateChild("PART_Inspector") as CanvasPane;

            foreach (var section in Rows<PropertySection>(canvas))
            {
                if (section.Visibility == Visibility.Collapsed) continue;

                var at = new Vector2(section.Bounds.X, section.Bounds.Y);

                for (var up = ((IUIComponent)section).VisualParent; up != null && up != window; up = up.VisualParent)
                {
                    at = new Vector2(at.X + up.Bounds.X, at.Y + up.Bounds.Y);
                }

                Assert.That(at.X, Is.GreaterThanOrEqualTo(pane.Bounds.X - 1),
                    $"{theme}: section '{section.Header}' stands at {at}, outside the panel at {pane.Bounds}"
                    + $" (picked {pick.Sort})");
            }
        }
    }

    // HOW MUCH IS ON THE PLANE, over the list of it.
    [Test]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void TheStructureSaysHowMuchIsOnThePlane(string theme)
    {
        var canvas = Built(theme);
        var inspector = Piece<CanvasInspector>(canvas);

        canvas.Scene = new CanvasScene();
        inspector.ShowsStructure = true;
        Settle(canvas);

        Assert.That(Said(canvas), Does.Contain("Nothing"), $"{theme}: an empty plane says: {Said(canvas)}");

        canvas.Scene.Add(new ShapeItem(CanvasShape.Rectangle, new Rect(0, 0, 40, 40),
            Adamantium.UI.Core.Media.Brushes.White, 2));
        Settle(canvas);

        Assert.That(Said(canvas), Does.Contain("1 item"), $"{theme}: one thing on the plane says: {Said(canvas)}");

        canvas.Scene.Add(new ShapeItem(CanvasShape.Ellipse, new Rect(60, 0, 40, 40),
            Adamantium.UI.Core.Media.Brushes.White, 2));
        Settle(canvas);

        Assert.That(Said(canvas), Does.Contain("2 items"), $"{theme}: two things on the plane say: {Said(canvas)}");
    }

    private static string Said(InfiniteCanvas canvas)
    {
        var inspector = Piece<CanvasInspector>(canvas);

        foreach (var block in Rows<Adamantium.UI.Controls.Text.TextBlock>(inspector as IUIComponent))
        {
            if (block.Text == inspector.Counted) return block.Text;
        }

        return "nothing at all";
    }

    private static List<T> Rows<T>(IUIComponent within) where T : class
    {
        var found = new List<T>();

        void Walk(IUIComponent at)
        {
            if (at is T wanted) found.Add(wanted);

            foreach (var child in at.VisualChildren) Walk(child);
        }

        Walk(within);
        return found;
    }

    private static void Settle(InfiniteCanvas canvas)
    {
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(canvas);
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        canvas.Measure(new Size(900, 600), force: true);
        canvas.Arrange(new Rect(0, 0, 900, 600));
    }

    private static List<TreeViewItem> Rows(InfiniteCanvas canvas)
    {
        var found = new List<TreeViewItem>();

        Gather(canvas, found);
        return found;
    }

    private static void Gather(IUIComponent at, List<TreeViewItem> into)
    {
        if (at is TreeViewItem row) into.Add(row);

        foreach (var child in at.VisualChildren) Gather(child, into);
    }

    private static List<object> Headers(List<TreeViewItem> rows)
    {
        var what = new List<object>();

        foreach (var row in rows) what.Add(row.Header);

        return what;
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
