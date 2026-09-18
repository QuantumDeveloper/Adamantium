using System.Collections.Generic;
using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>THE LOOK OF A CONTROL STANDING ON THE PLANE, as the inspector sets it. A control put on a canvas is a
/// control: its background, its outline and the room in its corners are things a person changes, and a row that reads
/// a value but cannot write one is a row that lies.</summary>
[TestFixture]
public class CanvasElementLookTests
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

    private static T Found<T>(IUIComponent within) where T : class
    {
        if (within is T wanted) return wanted;

        foreach (var child in within.VisualChildren)
        {
            if (child is IUIComponent visual && Found<T>(visual) is { } deep) return deep;
        }

        return null;
    }

    private static void Rows(IUIComponent within, List<PropertyRow> into)
    {
        if (within is PropertyRow row) into.Add(row);

        foreach (var child in within.VisualChildren)
        {
            if (child is IUIComponent visual) Rows(visual, into);
        }
    }

    private static PropertyRow Row(InfiniteCanvas canvas, string header)
    {
        var found = new List<PropertyRow>();
        Rows(Found<CanvasInspector>(canvas), found);

        foreach (var row in found)
        {
            if (Equals(row.Definition?.Header, header)) return row;
        }

        return null;
    }

    // A composite is FOLDED until somebody opens it, and a folded line has no row - so the test opens every one of
    // them, exactly as a click on each chevron does. All of them, because the same word names more than one line: a
    // shape has a Corner block and so does a control.
    private static void Open(PropertyGrid grid)
    {
        foreach (var section in grid.Sections)
        {
            foreach (var definition in section.Properties) Unfold(definition);
        }

        grid.Rebuild();
    }

    private static void Unfold(PropertyDefinition definition)
    {
        if (definition is CompositeProperty composite) composite.IsExpanded = true;

        foreach (var child in definition.Children) Unfold(child);
    }

    // The LINE ITSELF, wherever it sits in the panel - a line inside a folded composite has no row built for it, and
    // what is being asked here is whether the line can write at all, not whether it happens to be unfolded.
    private static PropertyDefinition Line(PropertyGrid grid, string header)
    {
        foreach (var section in grid.Sections)
        {
            foreach (var definition in section.Properties)
            {
                if (Line(definition, header) is { } found) return found;
            }
        }

        return null;
    }

    private static PropertyDefinition Line(PropertyDefinition definition, string header)
    {
        if (Equals(definition.Header, header)) return definition;

        foreach (var child in definition.Children)
        {
            if (Line(child, header) is { } found) return found;
        }

        return null;
    }

    // A BUTTON ON THE PLANE, selected, with the panel showing its rows - the state a person is in when they go to
    // recolour it.
    private static (InfiniteCanvas Canvas, Button Button, PropertyGrid Grid) WithButtonSelected(Window window,
        InfiniteCanvas canvas)
    {
        var button = new Button { Content = "Press me" };
        var item = new ElementItem(button, new Rect(20, 20, 120, 40));

        canvas.Scene.Add(item);
        canvas.Select(item, false);

        for (var i = 0; i < 6; i++)
        {
            Adamantium.UI.Extensions.WindowExtension.UpdateTree(window);
            Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        }

        return (canvas, button, Found<CanvasInspector>(canvas).Canvas?.Inspector);
    }

    private static (Window Window, InfiniteCanvas Canvas) Stage()
    {
        var canvas = new InfiniteCanvas { Scene = new CanvasScene() };

        canvas.ApplyCurrentTheme();

        var window = new Window { Width = 1000, Height = 700, Content = canvas };

        for (var i = 0; i < 4; i++)
        {
            Adamantium.UI.Extensions.WindowExtension.UpdateTree(window);
            Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        }

        return (window, canvas);
    }

    // WHAT THE THREE COLOUR ROWS ARE ABOUT, and whether writing one reaches the control at all.
    [Test]
    public void TheColourRowsReachTheControl()
    {
        Use(new Adamantium.UI.Themes.FluentTheme.Fluent());

        var (window, canvas) = Stage();
        var (_, button, grid) = WithButtonSelected(window, canvas);

        Assert.That(grid, Is.Not.Null, "the panel has no grid to write through");

        Open(grid);

        for (var i = 0; i < 4; i++)
        {
            Adamantium.UI.Extensions.WindowExtension.UpdateTree(window);
            Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        }

        var background = Row(canvas, "Background");
        var foreground = Row(canvas, "Foreground");
        var border = Row(canvas, "Border");

        Assert.Multiple(() =>
        {
            Assert.That(background, Is.Not.Null, "no Background row");
            Assert.That(foreground, Is.Not.Null, "no Foreground row");
            Assert.That(border, Is.Not.Null, "no Border row");
        });

        // WHAT THE SWATCH PRODUCES IS A COLOUR - that is the path a person actually takes, and the one that has to
        // land on the control.
        grid.Write(background, Colors.Tomato);
        grid.Write(foreground, Colors.Lime);
        grid.Write(border, Colors.Cyan);

        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();

        Assert.Multiple(() =>
        {
            Assert.That((button.Background as SolidColorBrush)?.Color, Is.EqualTo(Colors.Tomato),
                "the background row did not reach the control");
            Assert.That((button.Foreground as SolidColorBrush)?.Color, Is.EqualTo(Colors.Lime),
                "the foreground row did not reach the control");
            Assert.That((button.BorderBrush as SolidColorBrush)?.Color, Is.EqualTo(Colors.Cyan),
                "the border row did not reach the control");
        });
    }

    // AN APPLICATION'S OWN OBJECT is put on the plane inside a ContentPresenter with its template in it, so the button
    // a person points at is the presenter's CHILD. The panel's lines have to reach THAT: written on the presenter, a
    // background is painted under an opaque button and a corner radius belongs to something with no corners - which is
    // "the colour picker does nothing, and only the text colour ever changes".
    [Test]
    public void TheLookRowsReachTheControlInsideAHostedObject()
    {
        Use(new Adamantium.UI.Themes.FluentTheme.Fluent());

        var (window, canvas) = Stage();

        // The same shape as CanvasDrawingHost builds for an application's object.
        var button = new Button { Content = "Press me" };
        var presenter = new ContentPresenter { Content = button };
        var item = new ElementItem(presenter, new Rect(20, 20, 140, 44));

        canvas.Scene.Add(item);
        canvas.Select(item, false);

        for (var i = 0; i < 6; i++)
        {
            Adamantium.UI.Extensions.WindowExtension.UpdateTree(window);
            Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        }

        Assert.That(item.Painted, Is.SameAs(button), "the panel is pointed at the host, not at the control in it");

        var grid = Found<CanvasInspector>(canvas).Canvas?.Inspector;

        Open(grid);

        for (var i = 0; i < 4; i++)
        {
            Adamantium.UI.Extensions.WindowExtension.UpdateTree(window);
            Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        }

        grid.Write(Row(canvas, "Background"), Colors.Tomato);
        grid.Write(Row(canvas, "Top left"), 10.0);

        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();

        Assert.Multiple(() =>
        {
            Assert.That((button.Background as SolidColorBrush)?.Color, Is.EqualTo(Colors.Tomato),
                "the background was written on the host and the control kept its own");
            Assert.That(button.CornerRadius.TopLeft, Is.EqualTo(10).Within(1e-9),
                "the corner was written on the host, which has no corners");
        });
    }

    // WHAT IT SAYS reaches the control too - the label row wrote onto the host, where it replaced the application's
    // object with a string and the button went on saying what it always said.
    [Test]
    public void TheLabelRowReachesTheControlInsideAHostedObject()
    {
        Use(new Adamantium.UI.Themes.FluentTheme.Fluent());

        var (window, canvas) = Stage();

        var button = new Button { Content = "Press me" };
        var presenter = new ContentPresenter { Content = button };
        var item = new ElementItem(presenter, new Rect(20, 20, 140, 44));

        canvas.Scene.Add(item);
        canvas.Select(item, false);

        for (var i = 0; i < 6; i++)
        {
            Adamantium.UI.Extensions.WindowExtension.UpdateTree(window);
            Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        }

        Assert.That(item.Label, Is.EqualTo("Press me"), "the label was read off the host, not off the control");

        item.Label = "Renamed";

        Assert.That(button.Content, Is.EqualTo("Renamed"), "the label never reached the control");
    }

    // A CONTROL TURNS like a shape does: the three numbers land on it, and a point is asked about in its own frame so
    // a turned control is still picked where it is seen.
    [Test]
    public void AControlTurnsAndIsPickedWhereItIsSeen()
    {
        Use(new Adamantium.UI.Themes.FluentTheme.Fluent());

        var (window, canvas) = Stage();

        var button = new Button { Content = "Press me" };
        var item = new ElementItem(button, new Rect(0, 0, 200, 40));

        canvas.Scene.Add(item);

        for (var i = 0; i < 4; i++)
        {
            Adamantium.UI.Extensions.WindowExtension.UpdateTree(window);
            Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        }

        item.Angle = 90;

        canvas.Scene.Touch();

        for (var i = 0; i < 4; i++)
        {
            Adamantium.UI.Extensions.WindowExtension.UpdateTree(window);
            Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        }

        Assert.Multiple(() =>
        {
            Assert.That(button.RenderTransform?.RotationAngle, Is.EqualTo(90).Within(1e-9),
                "the turn never reached the control");

            // THE ZOOM IS STILL THERE. One control has one render transform, and a turn written over it left the
            // control at its own size inside a frame drawn at the camera's - which is how a frame and a control walk
            // away from each other.
            Assert.That(button.RenderTransform?.ScaleX, Is.EqualTo(canvas.Scale).Within(1e-9),
                "the turn replaced the camera's own scale");
            Assert.That(button.RenderTransform?.RotationCenterX, Is.EqualTo(100).Within(1e-9),
                "it turns about something other than its middle");

            // Turned a quarter, the long box stands up: a point off its LONG side is now outside it, and one off the
            // short side is inside.
            Assert.That(item.HitTest(new Vector2(100, 90), 1), Is.True, "a point on the turned control missed it");
            Assert.That(item.HitTest(new Vector2(190, 20), 1), Is.False, "a point beside the turned control hit it");
        });
    }

    // ...AND THE NUMBERS BESIDE THEM: the room in the corners and the weight of the outline.
    [Test]
    public void TheCornerAndOutlineRowsReachTheControl()
    {
        Use(new Adamantium.UI.Themes.FluentTheme.Fluent());

        var (window, canvas) = Stage();
        var (_, button, grid) = WithButtonSelected(window, canvas);

        Open(grid);

        for (var i = 0; i < 4; i++)
        {
            Adamantium.UI.Extensions.WindowExtension.UpdateTree(window);
            Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        }

        var corner = Row(canvas, "Top left");

        Assert.That(corner, Is.Not.Null, "no corner row");

        grid.Write(corner, 12.0);
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();

        Assert.That(button.CornerRadius.TopLeft, Is.EqualTo(12).Within(1e-9),
            "the corner row did not reach the control");
    }
}
