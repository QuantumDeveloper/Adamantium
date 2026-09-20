using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>HOW THE CANVAS WAS LEFT, written down and put back: where the panels stand, how wide they were pulled,
/// what was folded away, where the camera was looking and what was in hand. A person who arranges their panels once
/// and finds them scattered on the next run arranges them once more, and then stops arranging them.</summary>
[TestFixture]
public class CanvasLayoutTests
{
    private FakeApp _app;

    [OneTimeSetUp]
    public void EnsureAppContext()
    {
        _app = new FakeApp(new AdamantiumDependencyContainer()) { ResourceManager = new ResourceManager() };
        UIAppContext.Initialize(_app, null);
    }

    private InfiniteCanvas Built()
    {
        _app.ResourceManager = new ResourceManager();
        typeof(UIAppContext).GetProperty(nameof(UIAppContext.Current)).SetValue(null, _app);

        var themes = new ThemeManager(new AdamantiumDependencyContainer());
        var theme = new Adamantium.UI.Themes.FluentTheme.Fluent();

        _app.ThemeManager = themes;
        ((FakeContext)_app.UIContext).ThemeEngine = themes;
        themes.AddTheme(theme.Name, theme);
        themes.SetTheme(theme);

        var canvas = new InfiniteCanvas { Width = 900, Height = 600, Scene = new CanvasScene() };

        canvas.ApplyCurrentTheme();
        Settle(canvas);

        return canvas;
    }

    private static void Settle(InfiniteCanvas canvas)
    {
        for (var pass = 0; pass < 2; pass++)
        {
            Adamantium.UI.Extensions.WindowExtension.UpdateTree(canvas);
            Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
            canvas.Measure(new Size(900, 600), force: true);
            canvas.Arrange(new Rect(0, 0, 900, 600));
        }
    }

    private static CanvasPane Named(InfiniteCanvas canvas, string name)
    {
        foreach (var pane in canvas.Panes)
        {
            if (pane.Name == name) return pane;
        }

        return null;
    }

    [Test]
    public void EveryPaneIsWrittenDownByItsName()
    {
        var canvas = Built();
        var said = CanvasLayoutSerializer.Save(canvas);

        Assert.That(said, Does.Contain("PART_Inspector").And.Contain("PART_ToolRail").And.Contain("PART_MiniMap"),
            "a panel with no name in the text cannot be found again");
    }

    // THE WHOLE POINT: a panel put somewhere and pulled to a width comes back where it was put, at that width.
    [Test]
    public void APanelComesBackWhereItWasLeft()
    {
        var canvas = Built();
        var inspector = Named(canvas, "PART_Inspector");

        inspector.Placement = CanvasPanePlacement.Free;
        inspector.Anchor = new Vector2(0.25, 0.6);
        inspector.Width = 360;
        inspector.IsOpen = false;
        Settle(canvas);

        var said = CanvasLayoutSerializer.Save(canvas);

        var other = Built();
        var back = Named(other, "PART_Inspector");

        Assert.That(CanvasLayoutSerializer.Load(other, said), Is.True, "the text was refused");
        Settle(other);

        Assert.Multiple(() =>
        {
            Assert.That(back.Placement, Is.EqualTo(CanvasPanePlacement.Free));
            Assert.That(back.Anchor.X, Is.EqualTo(0.25).Within(0.001));
            Assert.That(back.Anchor.Y, Is.EqualTo(0.6).Within(0.001));
            Assert.That(back.Width, Is.EqualTo(360).Within(0.5), "the width it was pulled to was lost");
            Assert.That(back.IsOpen, Is.False, "a panel folded away came back open");
        });
    }

    // A WIDTH IS IN PIXELS, and a window that cannot show it holds it down - a panel wider than the canvas covers the
    // drawing it is about.
    [Test]
    public void AWidthIsNeverNarrowerThanAPanelCanBeDragged()
    {
        var canvas = Built();
        var inspector = Named(canvas, "PART_Inspector");

        Assert.That(CanvasLayoutSerializer.Load(canvas, """
            {"version":1,"panes":[{"name":"PART_Inspector","width":2}]}
            """), Is.True);

        Assert.That(inspector.Width, Is.EqualTo(inspector.MinResizeWidth).Within(0.5));
    }

    [Test]
    public void WhereTheCameraWasLookingComesBack()
    {
        var canvas = Built();

        canvas.Scale = 4;
        canvas.CenterOn(new Vector2(1200, -800));
        Settle(canvas);

        var said = CanvasLayoutSerializer.Save(canvas);
        var other = Built();

        CanvasLayoutSerializer.Load(other, said);
        Settle(other);

        Assert.Multiple(() =>
        {
            Assert.That(other.Scale, Is.EqualTo(4).Within(0.001));
            Assert.That(other.Looking.X, Is.EqualTo(1200).Within(1), "the camera came back looking somewhere else");
            Assert.That(other.Looking.Y, Is.EqualTo(-800).Within(1), "the camera came back looking somewhere else");
        });
    }

    // ...AND IN A WINDOW OF ANOTHER SIZE. The camera is written down as the world point in the MIDDLE of the view, so
    // the same drawing is in front of you whatever the window is; written as the origin in pixels it would be half a
    // window off.
    [Test]
    public void TheCameraComesBackEvenIntoAWindowOfAnotherSize()
    {
        var canvas = Built();

        canvas.Scale = 2;
        canvas.CenterOn(new Vector2(500, 500));
        Settle(canvas);

        var said = CanvasLayoutSerializer.Save(canvas);
        var other = Built();

        other.Width = 1400;
        other.Height = 900;
        other.Measure(new Size(1400, 900), force: true);
        other.Arrange(new Rect(0, 0, 1400, 900));

        CanvasLayoutSerializer.Load(other, said);
        other.Measure(new Size(1400, 900), force: true);
        other.Arrange(new Rect(0, 0, 1400, 900));

        Assert.Multiple(() =>
        {
            Assert.That(other.Looking.X, Is.EqualTo(500).Within(1));
            Assert.That(other.Looking.Y, Is.EqualTo(500).Within(1));
        });
    }

    // EACH SWITCH ANSWERS FOR ITSELF. A drawing looked over as a whole is opened at 1:1 in the corner it was left in,
    // which is the camera without the zoom.
    [Test]
    public void ASwitchThatIsOffKeepsThatPartOut()
    {
        var canvas = Built();

        canvas.RemembersZoom = false;
        canvas.Scale = 6;
        canvas.CenterOn(new Vector2(300, 300));
        Settle(canvas);

        var said = CanvasLayoutSerializer.Save(canvas);

        Assert.That(said, Does.Not.Contain("\"scale\": 6"), "the zoom was written down after all");

        var other = Built();

        CanvasLayoutSerializer.Load(other, said);
        Settle(other);

        Assert.Multiple(() =>
        {
            Assert.That(other.RemembersZoom, Is.False, "the switch itself was forgotten");
            Assert.That(other.Scale, Is.EqualTo(1).Within(0.001), "the zoom came back anyway");
            Assert.That(other.Looking.X, Is.EqualTo(300).Within(1), "the camera was lost with the zoom");
        });
    }

    [Test]
    public void TheToolInHandComesBack()
    {
        var canvas = Built();
        var pen = new PenTool();

        canvas.Tools.Add(new SelectTool());
        canvas.Tools.Add(pen);
        canvas.Tool = pen;
        Settle(canvas);

        var said = CanvasLayoutSerializer.Save(canvas);
        var other = Built();

        other.Tools.Add(new SelectTool());
        other.Tools.Add(new PenTool());

        CanvasLayoutSerializer.Load(other, said);

        Assert.That(other.Tool, Is.TypeOf<PenTool>());
    }

    // THE CANVAS WRITES ITSELF DOWN. The application binds one string and keeps it; nothing else is asked of it.
    [Test]
    public void TheCanvasSaysSoItselfWhenSomethingSettles()
    {
        var canvas = Built();
        var inspector = Named(canvas, "PART_Inspector");

        Assert.That(canvas.Layout, Is.Null.Or.Empty, "it wrote itself down before anything happened");

        inspector.Placement = CanvasPanePlacement.Free;
        inspector.Anchor = new Vector2(0.4, 0.4);
        canvas.RememberLayout();

        Assert.That(canvas.Layout, Does.Contain("PART_Inspector"), "the canvas never said how it is set up");
    }

    // ...AND DRESSES ITSELF FROM ONE. A string written into it is the whole feature from the application's side.
    [Test]
    public void TextWrittenIntoTheCanvasDressesIt()
    {
        var canvas = Built();
        var map = Named(canvas, "PART_MiniMap");

        canvas.Layout = """
            {"version":1,"panes":[{"name":"PART_MiniMap","placement":"Free","x":0.5,"y":0.5,"open":false}]}
            """;
        Settle(canvas);

        Assert.Multiple(() =>
        {
            Assert.That(map.Placement, Is.EqualTo(CanvasPanePlacement.Free));
            Assert.That(map.Anchor.X, Is.EqualTo(0.5).Within(0.001));
            Assert.That(map.IsOpen, Is.False);
        });
    }

    // HOME MEANS THE VIEW THAT WAS KEPT. Without this the button is "back to the origin", which on a drawing that does
    // not live near the origin is a button that takes you away from your work.
    [Test]
    public void HomeGoesToTheViewThatWasKept()
    {
        var canvas = Built();

        canvas.Scale = 3;
        canvas.CenterOn(new Vector2(700, 200));
        Settle(canvas);

        canvas.RememberView();

        canvas.Scale = 0.5;
        canvas.CenterOn(new Vector2(-4000, 9000));
        Settle(canvas);

        canvas.ResetCamera();
        Settle(canvas);

        Assert.Multiple(() =>
        {
            Assert.That(canvas.Scale, Is.EqualTo(3).Within(0.001));
            Assert.That(canvas.Looking.X, Is.EqualTo(700).Within(1));
            Assert.That(canvas.Looking.Y, Is.EqualTo(200).Within(1));
        });
    }

    [Test]
    public void AKeptViewIsWrittenDownWithEverythingElse()
    {
        var canvas = Built();

        canvas.Scale = 2.5;
        canvas.CenterOn(new Vector2(-300, 40));
        Settle(canvas);
        canvas.RememberView();

        var other = Built();

        CanvasLayoutSerializer.Load(other, CanvasLayoutSerializer.Save(canvas));
        other.ResetCamera();
        Settle(other);

        Assert.Multiple(() =>
        {
            Assert.That(other.Scale, Is.EqualTo(2.5).Within(0.001));
            Assert.That(other.Looking.X, Is.EqualTo(-300).Within(1));
        });
    }

    [Test]
    public void TextThatCannotBeReadChangesNothing()
    {
        var canvas = Built();
        var inspector = Named(canvas, "PART_Inspector");
        var was = inspector.Placement;

        Assert.Multiple(() =>
        {
            Assert.That(CanvasLayoutSerializer.Load(canvas, "{ this is not json"), Is.False);
            Assert.That(CanvasLayoutSerializer.Load(canvas, """{"version":99}"""), Is.False);
            Assert.That(inspector.Placement, Is.EqualTo(was));
        });
    }
}
