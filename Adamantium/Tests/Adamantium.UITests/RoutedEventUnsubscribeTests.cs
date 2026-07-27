using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// The CLR event wrappers must actually UNSUBSCRIBE. MouseMove and MouseWheel had <c>AddHandler</c> in their remove
/// accessor, so every <c>-=</c> quietly added another handler: DragDrop.Detach ran after each gesture, doubling the
/// per-move drag work every time, and OverlayWindow's template teardown did the same. Cheap guard against the typo
/// coming back on any wrapper it is added to.
/// </summary>
[TestFixture]
public class RoutedEventUnsubscribeTests
{
    [Test]
    public void MouseMove_MinusEquals_ActuallyUnsubscribes()
    {
        var element = new Border();
        var calls = 0;
        void Handler(object sender, MouseEventArgs e) => calls++;

        element.MouseMove += Handler;
        Raise(element, Mouse.MouseMoveEvent);
        Assert.That(calls, Is.EqualTo(1), "the handler must run while subscribed");

        element.MouseMove -= Handler;
        Raise(element, Mouse.MouseMoveEvent);
        Assert.That(calls, Is.EqualTo(1), "-= must remove the handler, not add a second one");
    }

    [Test]
    public void MouseWheel_MinusEquals_ActuallyUnsubscribes()
    {
        var element = new Border();
        var calls = 0;
        void Handler(object sender, MouseWheelEventArgs e) => calls++;

        element.MouseWheel += Handler;
        Raise(element, Mouse.MouseWheelEvent);
        Assert.That(calls, Is.EqualTo(1));

        element.MouseWheel -= Handler;
        Raise(element, Mouse.MouseWheelEvent);
        Assert.That(calls, Is.EqualTo(1));
    }

    private static void Raise(IObservableComponent element, Adamantium.UI.Core.RoutedEvents.RoutedEvent routedEvent)
    {
        MouseEventArgs args = routedEvent == Mouse.MouseWheelEvent
            ? new MouseWheelEventArgs(Mouse.PrimaryDevice, InputModifiers.None, 120, 0)
            : new MouseEventArgs(Mouse.PrimaryDevice, InputModifiers.None, 0);
        args.RoutedEvent = routedEvent;
        element.RaiseEvent(args);
    }
}
