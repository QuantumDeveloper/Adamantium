using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
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

    private static IUIComponent Sizer(CanvasPane pane) => pane.GetTemplateChild("PART_Sizer") as IUIComponent;

    [Test]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void EveryThemeCarriesTheEdge(string theme)
    {
        Use(ThemeNamed(theme));
        var pane = Built(new CanvasPane { Kind = CanvasPaneKind.Sheet });

        Assert.That(Sizer(pane), Is.Not.Null, "the pane drives this part by name, so every theme owes it");
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
        Assert.That(Sizer(pane).Visibility, Is.EqualTo(Visibility.Collapsed));
    }

    [Test]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void AskingForItShowsTheEdge(string theme)
    {
        Use(ThemeNamed(theme));
        var pane = Built(new CanvasPane { Kind = CanvasPaneKind.Sheet, CanResize = true });

        Assert.That(Sizer(pane).Visibility, Is.EqualTo(Visibility.Visible));
    }

    // The edge goes on the side facing INTO the canvas - the side there is room to pull towards. On the outer side it
    // would either hang off the viewport or walk the pane across the screen as it grew.
    [Test]
    [TestCase(CanvasPanePlacement.TopRight, Dock.Left)]
    [TestCase(CanvasPanePlacement.Right, Dock.Left)]
    [TestCase(CanvasPanePlacement.BottomRight, Dock.Left)]
    [TestCase(CanvasPanePlacement.TopLeft, Dock.Right)]
    [TestCase(CanvasPanePlacement.Left, Dock.Right)]
    [TestCase(CanvasPanePlacement.Free, Dock.Right)]
    public void TheEdgeFacesIntoTheCanvas(CanvasPanePlacement placement, Dock side)
    {
        Use(ThemeNamed("Fluent"));
        var pane = Built(new CanvasPane { Kind = CanvasPaneKind.Sheet, CanResize = true, Placement = placement });

        Assert.That(DockPanel.GetDock(Sizer(pane)), Is.EqualTo(side));
    }

    // Moved AFTER it was templated, so the part has to be told again - the pane is dragged to another edge far more
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

        Assert.That(DockPanel.GetDock(Sizer(pane)), Is.EqualTo(Dock.Left));

        pane.Placement = CanvasPanePlacement.TopLeft;

        Assert.That(DockPanel.GetDock(Sizer(pane)), Is.EqualTo(Dock.Right));
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

    [Test]
    public void TheEdgeSaysWhatItIs()
    {
        Use(ThemeNamed("Fluent"));
        var pane = Built(new CanvasPane { Kind = CanvasPaneKind.Sheet, CanResize = true });

        Assert.That((Sizer(pane) as Border)?.Cursor?.Type, Is.EqualTo(CursorType.SizeEWE),
            "a strip of nothing has to declare itself, or it is a strip of nothing");
    }
}
