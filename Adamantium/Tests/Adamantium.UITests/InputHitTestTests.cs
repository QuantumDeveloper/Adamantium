using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using NUnit.Framework;

namespace Adamantium.UITests;

[TestFixture]
public class InputHitTestTests
{
    // Hit-testing must descend THROUGH a non-input container (a Border / any Decorator) to reach the interactive
    // content it wraps. The hit-test used to filter to IInputComponent children, so anything inside a Border was dead
    // to the mouse - e.g. a ScrollBar whose template root is a Border had an unclickable thumb.
    [Test]
    public void HitTest_DescendsThroughBorder_ToReachInteractiveContent()
    {
        var inner = new Button { Width = 100, Height = 50 };
        var border = new Border { Child = inner };
        var root = new Grid();
        root.Children.Add(border);

        root.Measure(new Size(100, 50));
        root.Arrange(new Rect(0, 0, 100, 50));

        var hit = ((IInputComponent)root).HitTest(new Vector2(50, 25));

        Assert.That(hit, Is.SameAs(inner), "the button inside the Border must be the hit target, not the Grid");
    }

    // A control painted ON TOP of a full-size background panel (overlapping siblings): clicking it must hit the control,
    // not the panel behind it. (Designer repro: selecting a control kept selecting the background panel.)
    [Test]
    public void HitTest_ReturnsForegroundControl_OverBackgroundPanel()
    {
        var background = new Border { Width = 200, Height = 200 };       // full-size background "panel"
        var foreground = new Button { Width = 100, Height = 50 };        // control on top (added later = painted over)
        var root = new Grid();
        root.Children.Add(background);
        root.Children.Add(foreground);

        root.Measure(new Size(200, 200));
        root.Arrange(new Rect(0, 0, 200, 200));

        var hit = ((IInputComponent)root).HitTest(new Vector2(50, 25));   // inside the foreground button

        Assert.That(hit, Is.SameAs(foreground), "the foreground control must be hit, not the background panel behind it");
    }

    // A control inside a panel that itself fills the window: clicking the control must hit it, not the containing panel.
    [Test]
    public void HitTest_ReturnsChildControl_NotContainingPanel()
    {
        var button = new Button { Width = 100, Height = 50 };
        var panel = new StackPanel();
        panel.Children.Add(button);
        var root = new Grid();
        root.Children.Add(panel);

        root.Measure(new Size(300, 300));
        root.Arrange(new Rect(0, 0, 300, 300));

        var hit = ((IInputComponent)root).HitTest(new Vector2(50, 25));   // inside the button

        Assert.That(hit, Is.SameAs(button), "the child control must be hit, not its containing panel");
    }

    // A CENTERED shape over a full-size background panel: clicking the shape must hit it, not the panel. (Designer repro:
    // a centered shape - a rounded rect "pill" - kept selecting the background panel instead of itself.)
    [Test]
    public void HitTest_ReturnsCenteredShape_OverBackgroundPanel()
    {
        var background = new Border { Width = 200, Height = 200 };
        var shape = new Adamantium.UI.Controls.Shapes.Rectangle
        {
            Width = 80, Height = 40, Fill = Brushes.Red,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var root = new Grid();
        root.Children.Add(background);
        root.Children.Add(shape);

        root.Measure(new Size(200, 200));
        root.Arrange(new Rect(0, 0, 200, 200));

        var hit = ((IInputComponent)root).HitTest(new Vector2(100, 100));   // the centre = centre of the centred shape

        Assert.That(hit, Is.SameAs(shape), "the centred shape must be hit, not the background panel behind it");
    }

    // The designer must be able to select a NON-INPUT authored element (a bare Border, a Shape). Mouse HitTest returns
    // only IInputComponent targets, so a Border falls through to whatever interactive element is behind it (the panel) -
    // the "selecting a control picks the background panel" designer bug. GetVisualsAt returns the Border for selection.
    [Test]
    public void GetVisualsAt_FindsNonInputBorder_WhereHitTestFallsThrough()
    {
        var border = new Border
        {
            Width = 280, Height = 155,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var panel = new RenderTargetPanel();   // the user's root container (a Grid that composites a surface)
        panel.Children.Add(border);

        panel.Measure(new Size(1280, 720));
        panel.Arrange(new Rect(0, 0, 1280, 720));

        var p = new Vector2(640, 360);   // dead centre = inside the centred Border
        var inputHit = ((IInputComponent)panel).HitTest(p);
        var visualHit = ((IUIComponent)panel).GetVisualsAt(p).FirstOrDefault();

        Assert.Multiple(() =>
        {
            Assert.That(inputHit, Is.Not.SameAs(border), "mouse HitTest skips the non-input Border (root of the designer bug)");
            Assert.That(visualHit, Is.SameAs(border), "the designer's visual hit-test selects the Border");
        });
    }
}
