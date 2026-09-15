using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Extensions;
using NUnit.Framework;

namespace Adamantium.UITests;

// The button that opens a FAMILY of tools opened one and nothing shut it: pressing it again opened a second list over
// the first, and a press anywhere else did nothing at all. The only way out was to pick a tool - which is exactly what
// somebody who opened the list by mistake does not want to do.
[TestFixture]
public class CanvasToolRailTests
{
    private class Nothing : ICanvasTool
    {
        public bool IsBusy => false;

        public string Name { get; set; } = "Tool";

        public string Group { get; set; } = string.Empty;

        public void OnPressed(InfiniteCanvas canvas, CanvasPointerEventArgs e)
        {
        }

        public void OnMoved(InfiniteCanvas canvas, CanvasPointerEventArgs e)
        {
        }

        public void OnReleased(InfiniteCanvas canvas, CanvasPointerEventArgs e)
        {
        }

        public void Render(IDrawingSession session, InfiniteCanvas canvas)
        {
        }

        public void Cancel(InfiniteCanvas canvas)
        {
        }
    }

    private static (CanvasToolRail rail, Window window, Border elsewhere) Built()
    {
        var canvas = new InfiniteCanvas();
        canvas.Tools.Add(new Nothing { Name = "Loner" });
        canvas.Tools.Add(new Nothing { Name = "First", Group = "Family" });
        canvas.Tools.Add(new Nothing { Name = "Second", Group = "Family" });

        var rail = new CanvasToolRail { Canvas = canvas };
        var elsewhere = new Border { Width = 40, Height = 40 };

        var host = new StackPanel();
        host.Children.Add(rail);
        host.Children.Add(elsewhere);

        var window = new Window { Width = 400, Height = 300, Content = host };
        Settle(window);

        return (rail, window, elsewhere);
    }

    private static void Settle(Window window)
    {
        for (var i = 0; i < 5; i++) WindowExtension.UpdateTree(window);
    }

    // The family takes the LAST button - the loner was stated first, and the rail keeps the order it was given.
    private static ToggleButton FamilyButton(CanvasToolRail rail) =>
        rail.Children[rail.Children.Count - 1] as ToggleButton;

    private static void Press(IUIComponent target, Window window)
    {
        ((IObservableComponent)target).RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, MouseButtons.Left,
            MouseButtonState.Pressed, InputModifiers.LeftMouseButton, 0)
        { RoutedEvent = Mouse.PreviewMouseDownEvent });

        Settle(window);
    }

    private static void Click(ButtonBase button, Window window)
    {
        ((IObservableComponent)button).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
        Settle(window);
    }

    [Test]
    public void PressingTheFamilyButtonOpensTheList()
    {
        var (rail, window, _) = Built();

        Click(FamilyButton(rail), window);

        Assert.That(window.PopupLayer.HasPopups, Is.True);
    }

    // The defect: a second press opened ANOTHER list instead of taking the first away.
    [Test]
    public void PressingItAgainClosesTheList()
    {
        var (rail, window, _) = Built();
        var button = FamilyButton(rail);

        Click(button, window);
        Click(button, window);

        Assert.That(window.PopupLayer.HasPopups, Is.False);
    }

    // ...and it has to be able to open again afterwards, which is what goes wrong when the rail closes the list but
    // keeps thinking it is open.
    [Test]
    public void AndOpensAgainAfterThat()
    {
        var (rail, window, _) = Built();
        var button = FamilyButton(rail);

        Click(button, window);
        Click(button, window);
        Click(button, window);

        Assert.That(window.PopupLayer.HasPopups, Is.True);
    }

    // The other half of it: a press that has nothing to do with the rail puts the list away, the way every other flyout
    // in the application behaves.
    [Test]
    public void APressElsewhereClosesTheList()
    {
        var (rail, window, elsewhere) = Built();

        Click(FamilyButton(rail), window);
        Press(elsewhere, window);

        Assert.That(window.PopupLayer.HasPopups, Is.False);
    }

    // And the rail has to HEAR that press: a list dismissed behind its back leaves it thinking one is still open, and
    // the next press on the button closes nothing and opens nothing.
    [Test]
    public void AfterAPressElsewhereTheButtonStillOpensIt()
    {
        var (rail, window, elsewhere) = Built();

        Click(FamilyButton(rail), window);
        Press(elsewhere, window);
        Click(FamilyButton(rail), window);

        Assert.That(window.PopupLayer.HasPopups, Is.True);
    }
}
