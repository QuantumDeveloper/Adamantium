using System.Collections.Generic;
using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>The edge a chrome panel is widened by. A panel holds rows with names in them, and how much room a name needs
/// is the user's to decide - so the width is not the theme's and not the markup's last word.</summary>
[TestFixture]
public class CanvasPaneSizerTests
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

    private static CanvasPane Built(CanvasPane pane)
    {
        pane.ApplyCurrentTheme();
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(pane);
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        pane.Measure(new Size(600, 600));
        pane.Arrange(new Rect(0, 0, 600, 600));
        return pane;
    }

    private static IUIComponent Sizer(CanvasPane pane, Dock side = Dock.Right) =>
        pane.GetTemplateChild(side == Dock.Left ? "PART_SizerLeft" : "PART_SizerRight") as IUIComponent;

    // The one a hand can actually take hold of, whichever side the pane wears it on.
    private static IUIComponent Shown(CanvasPane pane) =>
        Sizer(pane, Dock.Left) is { Visibility: Visibility.Visible } left ? left : Sizer(pane, Dock.Right);

    [Test]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void EveryThemeCarriesBothEdges(string theme)
    {
        Use(ThemeNamed(theme));
        var pane = Built(new CanvasPane { Kind = CanvasPaneKind.Sheet });

        Assert.Multiple(() =>
        {
            Assert.That(Sizer(pane, Dock.Left), Is.Not.Null, "the pane drives this part by name, so every theme owes it");
            Assert.That(Sizer(pane, Dock.Right), Is.Not.Null, "the pane drives this part by name, so every theme owes it");
        });
    }

    // COLLAPSED and not hidden: a hidden strip still takes its width, and a rail is exactly as wide as its buttons.
    [Test]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void APaneThatDoesNotResizeHasNoEdgeToCatch(string theme)
    {
        Use(ThemeNamed(theme));
        var pane = Built(new CanvasPane { Kind = CanvasPaneKind.Sheet });

        Assert.That(pane.CanResize, Is.False, "off unless asked for");
        Assert.Multiple(() =>
        {
            Assert.That(Sizer(pane, Dock.Left).Visibility, Is.EqualTo(Visibility.Collapsed));
            Assert.That(Sizer(pane, Dock.Right).Visibility, Is.EqualTo(Visibility.Collapsed));
        });
    }

    // A FLOATING pane wears BOTH, each pulling its own way: one edge that widens when pulled one way and narrows the
    // other is a control whose direction has to be learned - and learned again for every side it is docked to.
    [Test]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void AFloatingPaneOffersAnEdgeOnEitherSide(string theme)
    {
        Use(ThemeNamed(theme));
        var pane = Built(new CanvasPane
        {
            Kind = CanvasPaneKind.Sheet,
            CanResize = true,
            Placement = CanvasPanePlacement.Free
        });

        Assert.Multiple(() =>
        {
            Assert.That(Sizer(pane, Dock.Left).Visibility, Is.EqualTo(Visibility.Visible));
            Assert.That(Sizer(pane, Dock.Right).Visibility, Is.EqualTo(Visibility.Visible));
        });
    }

    // Held AGAINST an edge of the canvas, the pane is pinned there: its outer edge has nowhere to travel, so the
    // handle on that side would be one that does nothing however hard it is pulled.
    [Test]
    [TestCase(CanvasPanePlacement.TopRight, Dock.Left)]
    [TestCase(CanvasPanePlacement.Right, Dock.Left)]
    [TestCase(CanvasPanePlacement.BottomRight, Dock.Left)]
    [TestCase(CanvasPanePlacement.TopLeft, Dock.Right)]
    [TestCase(CanvasPanePlacement.Left, Dock.Right)]
    [TestCase(CanvasPanePlacement.BottomLeft, Dock.Right)]
    public void APinnedPaneOffersOnlyTheEdgeFacingIntoTheCanvas(CanvasPanePlacement placement, Dock side)
    {
        Use(ThemeNamed("Fluent"));
        var pane = Built(new CanvasPane { Kind = CanvasPaneKind.Sheet, CanResize = true, Placement = placement });

        Assert.Multiple(() =>
        {
            Assert.That(Sizer(pane, side).Visibility, Is.EqualTo(Visibility.Visible));
            Assert.That(Sizer(pane, side == Dock.Left ? Dock.Right : Dock.Left).Visibility,
                Is.EqualTo(Visibility.Collapsed), "an edge that cannot move anything was offered");
        });
    }

    // Moved AFTER it was templated, so the parts have to be told again - the pane is dragged to another edge far more
    // often than it is declared at one.
    [Test]
    public void MovingThePaneMovesItsEdge()
    {
        Use(ThemeNamed("Fluent"));
        var pane = Built(new CanvasPane
        {
            Kind = CanvasPaneKind.Sheet,
            CanResize = true,
            Placement = CanvasPanePlacement.TopRight
        });

        Assert.That(Sizer(pane, Dock.Left).Visibility, Is.EqualTo(Visibility.Visible));

        pane.Placement = CanvasPanePlacement.TopLeft;

        Assert.Multiple(() =>
        {
            Assert.That(Sizer(pane, Dock.Right).Visibility, Is.EqualTo(Visibility.Visible));
            Assert.That(Sizer(pane, Dock.Left).Visibility, Is.EqualTo(Visibility.Collapsed));
        });
    }

    // Docking takes from what is LEFT, so an edge docked after the handles cuts its strip out of the body alone and
    // stands the handles seven pixels to the side of the plate they belong to.
    [Test]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void TheEdgeIsTakenFromTheWholePaneAndNotOnlyFromItsBody(string theme)
    {
        Use(ThemeNamed(theme));
        var pane = Built(new CanvasPane
        {
            Kind = CanvasPaneKind.Sheet,
            CanResize = true,
            Placement = CanvasPanePlacement.TopRight,
            Content = new Border { Width = 120, Height = 60 }
        });

        var handles = pane.GetTemplateChild("PART_Handles") as IUIComponent;
        var body = pane.GetTemplateChild("PART_Body") as IUIComponent;

        Assert.That(handles.Bounds.X, Is.EqualTo(body.Bounds.X).Within(0.01),
            "the handles and the plate they belong to start at the same edge");
    }

    // ...AND IS SEEN. A transparent strip resizes perfectly and reads as nothing at all: the panel looked like it had
    // no edge to take hold of, which is the same as not having one.
    [Test]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void TheEdgeIsVisibleAndNotJustReachable(string theme)
    {
        Use(ThemeNamed(theme));
        var pane = Built(new CanvasPane { Kind = CanvasPaneKind.Sheet, CanResize = true });

        var painted = 0;

        void Walk(IUIComponent at)
        {
            if (at is Border { Background: SolidColorBrush brush } && brush.Color.A > 0) painted++;

            foreach (var child in at.VisualChildren) Walk(child);
        }

        foreach (var child in Shown(pane).VisualChildren) Walk(child);

        Assert.That(painted, Is.GreaterThan(0), $"{theme}: the edge paints nothing, so nothing says the panel is draggable");
    }

    [Test]
    public void TheEdgeSaysWhatItIs()
    {
        Use(ThemeNamed("Fluent"));
        var pane = Built(new CanvasPane { Kind = CanvasPaneKind.Sheet, CanResize = true });

        Assert.That((Shown(pane) as Border)?.Cursor?.Type, Is.EqualTo(CursorType.SizeEWE),
            "a strip of nothing has to declare itself, or it is a strip of nothing");
    }

    // THE PANEL A PERSON ACTUALLY DRAGS - the inspector inside a canvas, not a pane built by hand. Every part of the
    // ...AND A PRESS ON THE EDGE REACHES IT. The drag cannot be staged (it reads the pointer off the device), but
    // whether the edge is REACHABLE is a hit-test and nothing more - and an edge nothing can land on is an edge that
    // does not resize anything, however visible it is.
    [Test]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void APressOnTheInspectorsEdgeLandsOnIt(string theme)
    {
        Use(ThemeNamed(theme));

        var canvas = new InfiniteCanvas { Scene = new CanvasScene() };

        canvas.ApplyCurrentTheme();

        var window = new Window { Width = 1000, Height = 700, Content = canvas };

        Settle(window);

        // The chrome is laid out inside the canvas's own arrange, so the canvas has to be given a size and settled -
        // a window's layout pass alone leaves every pane standing at nothing.
        for (var pass = 0; pass < 2; pass++)
        {
            canvas.Measure(new Size(1000, 700), force: true);
            canvas.Arrange(new Rect(0, 0, 1000, 700));
        }

        Settle(window);

        var pane = canvas.GetTemplateChild("PART_Inspector") as CanvasPane;
        var sizer = Shown(pane);
        var box = sizer.ClipRectangle;

        Assert.Multiple(() =>
        {
            Assert.That(box.Width, Is.GreaterThan(0), $"{theme}: the edge stands at no width");
            Assert.That(box.Height, Is.GreaterThan(0), $"{theme}: the edge stands at no height");
        });

        // In the WINDOW's coordinates: a hit-test descends by subtracting each element's own origin, so the point has
        // to be the sum of them.
        var at = new Vector2(box.Width / 2, box.Height / 2);

        for (var up = (IUIComponent)sizer; up != null && up != window; up = up.VisualParent)
        {
            at = new Vector2(at.X + up.Bounds.X, at.Y + up.Bounds.Y);
        }

        var hit = ((IUIComponent)window).HitTest(at);

        var under = new List<string>();

        foreach (var visual in ((IUIComponent)window).GetVisualsAt(at)) under.Add(visual.GetType().Name);

        Assert.That(Reaches(hit, sizer), Is.True,
            $"{theme}: a press at {at} (edge box {box}, pane at {pane.Bounds}) landed on "
            + $"{hit?.GetType().Name ?? "nothing"}; under it: {string.Join(", ", under)}");
    }

    private static bool Reaches(IInputComponent hit, IUIComponent wanted)
    {
        for (var at = hit as IUIComponent; at != null; at = at.VisualParent)
        {
            if (ReferenceEquals(at, wanted)) return true;
        }

        return false;
    }

    // edge was tested on its own; what a part-by-part test cannot see is whether the width the drag writes survives
    // the chrome's own arranging of its panes.
    //
    // The DRAG itself cannot be staged here: a move reads the pointer off the device, so faking one would mean taking
    // the mouse. What is staged is what the drag does - it writes a width - and what has to follow from it.
    [Test]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void TheInspectorInACanvasKeepsAWidthItWasGiven(string theme)
    {
        Use(ThemeNamed(theme));

        var canvas = new InfiniteCanvas { Scene = new CanvasScene() };

        canvas.ApplyCurrentTheme();

        var window = new Window { Width = 1000, Height = 700, Content = canvas };

        Settle(window);

        var pane = canvas.GetTemplateChild("PART_Inspector") as CanvasPane;

        Assert.That(pane, Is.Not.Null, "the chrome carries no inspector pane");

        Assert.Multiple(() =>
        {
            Assert.That(pane.CanResize, Is.True, "the panel a person reads rows in cannot be widened");
            Assert.That(Shown(pane)?.Visibility, Is.EqualTo(Visibility.Visible), "it has no edge to catch");
        });

        // ...and the layer has to have TAKEN it: a pane with no layer to measure the drag against reads every press on
        // its edge and does nothing with it, which is exactly what "the resize disappeared" looks like.
        Assert.That(pane.Layer, Is.Not.Null, "the chrome never took the pane, so its edge has nothing to measure by");

        // What a drag of 80 pixels inwards writes.
        var wider = pane.ActualWidth + 80;

        pane.Width = wider;

        Settle(window);

        Assert.Multiple(() =>
        {
            Assert.That(pane.Width, Is.EqualTo(wider).Within(0.5), "the width was written over");
            Assert.That(pane.RenderSize.Width, Is.EqualTo(wider).Within(0.5), "the pane was arranged at another width");
        });
    }


    private static void Settle(Window window)
    {
        for (var i = 0; i < 4; i++)
        {
            Adamantium.UI.Extensions.WindowExtension.UpdateTree(window);
            Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        }
    }
}
