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

    // A Border is itself an input control now, so the mouse HitTest lands ON it (no fall-through to the panel behind).
    // GetVisualsAt likewise returns it: the designer selects the authored Border under the pointer, not the background
    // panel. (HitTest returns the topmost IInputComponent; GetVisualsAt returns any visual on its geometry - here the same
    // Border, since it is both.)
    [Test]
    public void GetVisualsAt_AndHitTest_SelectTheAuthoredBorder()
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
            Assert.That(inputHit, Is.SameAs(border), "the Border is an input control, so the mouse HitTest lands on it");
            Assert.That(visualHit, Is.SameAs(border), "the designer's visual hit-test also selects the Border");
        });
    }

    private static void PointAt(IObservableComponent component) =>
        component.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, InputModifiers.None, 0)
        { RoutedEvent = Mouse.MouseEnterEvent });

    // A cursor that changes while the pointer STANDS STILL has to reach the screen. Anything that decides its cursor
    // from where inside itself the pointer is - a column separator, a splitter, a resize grip - changes it without any
    // enter to carry it, and the pointer then lies about what a press will do: the grid's header showed the resize
    // cursor several pixels past the separator, and the press there reordered the column instead.
    [Test]
    public void ACursorChangedUnderAStillPointer_ReachesTheScreen()
    {
        var border = new Border { Width = 100, Height = 50 };
        PointAt(border);

        border.Cursor = Cursors.SizeEWE;
        Assert.That(Mouse.Cursor, Is.EqualTo(Cursors.SizeEWE), "the element under the pointer says what the pointer is");

        border.Cursor = Cursors.Arrow;
        Assert.That(Mouse.Cursor, Is.EqualTo(Cursors.Arrow), "and it goes back without waiting for the next enter");
    }

    // ...but only the element the pointer is ON may speak. Otherwise any control anywhere - a row realized off screen,
    // a template part being built - would grab the cursor while the pointer is nowhere near it.
    [Test]
    public void ACursorChangedAwayFromThePointer_ChangesNothing()
    {
        var hovered = new Border();
        PointAt(hovered);
        hovered.Cursor = Cursors.Hand;

        var elsewhere = new Border();
        elsewhere.Cursor = Cursors.SizeNS;

        Assert.That(Mouse.Cursor, Is.EqualTo(Cursors.Hand), "the pointer is still over the first one");
    }
}
