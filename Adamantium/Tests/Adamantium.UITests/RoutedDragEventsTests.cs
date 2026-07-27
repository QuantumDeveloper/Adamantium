using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// The routed drag-drop events (docs/DRAG_DROP_PLAN.md): the control-side half of the drop API, so a control can react
/// to a drag flying over it without a view-model. The gesture itself needs real windows and a mouse capture, so what is
/// covered here is the contract the engine relies on: the route, what stops it, and that a handler's Effects is what
/// the engine reads back.
/// </summary>
[TestFixture]
public class RoutedDragEventsTests
{
    private static (Border inner, Grid outer) BuildTree()
    {
        var inner = new Border { Width = 100, Height = 50 };
        var outer = new Grid();
        outer.Children.Add(inner);
        outer.Measure(new Size(100, 50));
        outer.Arrange(new Rect(0, 0, 100, 50));
        return (inner, outer);
    }

    private static DragDropEventArgs DragOverArgs(object originalSource) =>
        new(new DataPackage("payload"), null, default)
        {
            RoutedEvent = DragDropEvents.DragOverEvent,
            OriginalSource = originalSource,
        };

    // The engine raises on the AllowDrop target; from there the event must reach that target's ancestors, and every
    // handler must still see the element actually under the pointer as OriginalSource.
    [Test]
    public void DragOver_BubblesFromTarget_KeepingOriginalSource()
    {
        var (inner, outer) = BuildTree();
        var route = new List<object>();
        var originals = new List<object>();
        inner.DragOver += (s, e) => { route.Add(s); originals.Add(e.OriginalSource); };
        outer.DragOver += (s, e) => { route.Add(s); originals.Add(e.OriginalSource); };

        ((IObservableComponent)inner).RaiseEvent(DragOverArgs(inner));

        Assert.That(route, Is.EqualTo(new object[] { inner, outer }), "the event must bubble from the target outwards");
        Assert.That(originals, Is.All.SameAs(inner), "OriginalSource must stay the deepest element for the whole route");
    }

    // Handled is what a control uses to say "this drag is mine" - the engine reads it to skip the DragOver/Drop command,
    // and the route must not carry on to ancestors either.
    [Test]
    public void Handled_StopsTheRoute()
    {
        var (inner, outer) = BuildTree();
        var reachedOuter = false;
        inner.DragOver += (_, e) => e.Handled = true;
        outer.DragOver += (_, _) => reachedOuter = true;

        var args = DragOverArgs(inner);
        ((IObservableComponent)inner).RaiseEvent(args);

        Assert.That(args.Handled, Is.True);
        Assert.That(reachedOuter, Is.False, "a handled drag must not reach the ancestors");
    }

    // The Preview pair tunnels INWARDS before the plain event bubbles out, so a container sees the drag first.
    [Test]
    public void Preview_TunnelsToTarget_BeforeTheBubblingEvent()
    {
        var (inner, outer) = BuildTree();
        var order = new List<string>();
        outer.PreviewDragOver += (_, _) => order.Add("preview:outer");
        inner.PreviewDragOver += (_, _) => order.Add("preview:inner");
        inner.DragOver += (_, _) => order.Add("bubble:inner");
        outer.DragOver += (_, _) => order.Add("bubble:outer");

        var args = DragOverArgs(inner);
        var observable = (IObservableComponent)inner;
        args.RoutedEvent = DragDropEvents.PreviewDragOverEvent;
        observable.RaiseEvent(args);
        args.RoutedEvent = DragDropEvents.DragOverEvent;
        observable.RaiseEvent(args);

        Assert.That(order, Is.EqualTo(new[] { "preview:outer", "preview:inner", "bubble:inner", "bubble:outer" }));
    }

    // The point of the tunnel: a PARENT answers for its whole subtree. Its Handled must reach the bubbling handlers,
    // which is only true because both events share ONE args object - the engine relies on it to skip the command too.
    [Test]
    public void ParentHandlingThePreview_VetoesTheBubblingEvent()
    {
        var (inner, outer) = BuildTree();
        var reached = false;
        outer.PreviewDragOver += (_, e) => e.Handled = true;
        inner.DragOver += (_, _) => reached = true;

        var args = DragOverArgs(inner);
        var observable = (IObservableComponent)inner;
        args.RoutedEvent = DragDropEvents.PreviewDragOverEvent;
        observable.RaiseEvent(args);
        args.RoutedEvent = DragDropEvents.DragOverEvent;
        observable.RaiseEvent(args);

        Assert.That(args.Handled, Is.True);
        Assert.That(reached, Is.False, "a preview handled from above must veto the bubbling handlers");
    }

    // Refusing a payload: a handler sets Effects.None and the engine picks that up from the same args object it raised.
    [Test]
    public void EffectsSetByHandler_IsVisibleToTheEngine()
    {
        var (inner, _) = BuildTree();
        inner.DragOver += (_, e) => e.Effects = DragDropEffects.None;

        var args = DragOverArgs(inner);
        args.Effects = DragDropEffects.Move;
        ((IObservableComponent)inner).RaiseEvent(args);

        Assert.That(args.Effects, Is.EqualTo(DragDropEffects.None));
    }
}
