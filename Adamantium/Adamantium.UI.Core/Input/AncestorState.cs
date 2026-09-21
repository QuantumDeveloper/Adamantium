using System;
using System.Collections.Generic;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Input;

/// <summary>
/// Drives "self-or-descendant" aggregate input states up the visual tree - states that are true for an element while a
/// focal leaf (the element under the pointer, the captured element, the focused element, ...) is that element or one of
/// its visual descendants. <see cref="IInputComponent.IsMouseOver"/> was the first such state; <c>IsKeyboardFocusWithin</c>
/// runs on the same machinery (see FocusManager.AnnounceFocusMove), and IsMouseCaptureWithin / IsStylusOver would fit it
/// too when they are added.
///
/// The backing enter/leave events are Direct (they must not bubble), so when the leaf moves the dispatcher raises them
/// individually: <paramref name="leaveEvent"/> on every element that left the leaf's ancestor chain and
/// <paramref name="enterEvent"/> on every element that joined it. Common ancestors are left untouched, so their state
/// never spuriously toggles when the leaf moves between their descendants.
/// </summary>
internal static class AncestorState
{
    /// <param name="makeArgs">Creates a fresh event-args for the given event - one per raise, so per-element Handled /
    /// Source state never leaks between targets.</param>
    public static void Transition(IInputComponent oldLeaf, IInputComponent newLeaf,
        RoutedEvent enterEvent, RoutedEvent leaveEvent, Func<RoutedEvent, RoutedEventArgs> makeArgs)
    {
        if (ReferenceEquals(oldLeaf, newLeaf)) return;

        var oldChain = AncestorChain(oldLeaf);
        var newChain = AncestorChain(newLeaf);
        var oldSet = new HashSet<IInputComponent>(oldChain);
        var newSet = new HashSet<IInputComponent>(newChain);

        foreach (var element in oldChain)   // left the chain (deepest first)
        {
            if (!newSet.Contains(element))
                element.RaiseEvent(makeArgs(leaveEvent));
        }

        foreach (var element in newChain)   // joined the chain (deepest first)
        {
            if (!oldSet.Contains(element))
                element.RaiseEvent(makeArgs(enterEvent));
        }
    }

    /// <summary>Visual ancestor chain (deepest first) of the input-aware components from <paramref name="leaf"/> up to
    /// the root; a null leaf yields an empty chain.</summary>
    public static List<IInputComponent> AncestorChain(IInputComponent leaf)
    {
        var chain = new List<IInputComponent>();
        IUIComponent v = leaf;
        while (v != null)
        {
            if (v is IInputComponent input)
                chain.Add(input);
            v = v.VisualParent;
        }
        return chain;
    }
}
