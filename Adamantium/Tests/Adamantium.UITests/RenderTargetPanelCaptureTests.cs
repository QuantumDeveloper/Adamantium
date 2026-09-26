using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Extensions;
using NUnit.Framework;

namespace Adamantium.UITests;

[TestFixture]
public class RenderTargetPanelCaptureTests
{
    [TearDown]
    public void ReleaseThePointer()
    {
        Mouse.Capture(null);
    }

    [Test]
    public void APress_KeepsThePointerUntilItsRelease()
    {
        var panel = Built();

        Raise(panel, MouseButtons.Left, Mouse.MouseDownEvent);

        Assert.That(Mouse.Captured, Is.SameAs(panel));

        Raise(panel, MouseButtons.Left, Mouse.MouseUpEvent);

        Assert.That(Mouse.Captured, Is.Null);
    }

    [Test]
    public void ReleasingOneOfTwoButtons_KeepsThePointer()
    {
        var panel = Built();
        Raise(panel, MouseButtons.Left, Mouse.MouseDownEvent);
        Raise(panel, MouseButtons.Middle, Mouse.MouseDownEvent);

        Raise(panel, MouseButtons.Left, Mouse.MouseUpEvent);

        Assert.That(Mouse.Captured, Is.SameAs(panel));

        Raise(panel, MouseButtons.Middle, Mouse.MouseUpEvent);

        Assert.That(Mouse.Captured, Is.Null);
    }

    [Test]
    public void AButtonHeldWhenThePointerIsTaken_IsForgotten()
    {
        var panel = Built();
        Raise(panel, MouseButtons.Left, Mouse.MouseDownEvent);

        Mouse.Capture(null);
        Raise(panel, MouseButtons.Right, Mouse.MouseDownEvent);
        Raise(panel, MouseButtons.Right, Mouse.MouseUpEvent);

        Assert.That(Mouse.Captured, Is.Null);
    }

    private static RenderTargetPanel Built()
    {
        var panel = new RenderTargetPanel { Width = 200, Height = 100 };
        var window = new Window { Width = 400, Height = 300, Content = panel };
        for (var i = 0; i < 5; i++)
        {
            WindowExtension.UpdateTree(window);
        }

        return panel;
    }

    private static void Raise(RenderTargetPanel panel, MouseButtons button, RoutedEvent routedEvent)
    {
        var state = routedEvent == Mouse.MouseDownEvent ? MouseButtonState.Pressed : MouseButtonState.Released;
        ((IObservableComponent)panel).RaiseEvent(
            new MouseButtonEventArgs(Mouse.PrimaryDevice, button, state, InputModifiers.None, 0) { RoutedEvent = routedEvent });
    }
}
