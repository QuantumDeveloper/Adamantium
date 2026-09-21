using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Input;

/// <summary>
/// The routed drag-drop events - the CONTROL-side half of the drop API (the view-model side is <c>DragDrop.DropCommand</c>
/// and friends). A control that wants to react to a drag flying over it handles these instead of needing a view-model.
/// <para>
/// They are raised on the nearest element with <c>DragDrop.AllowDrop="True"</c> under the pointer: first the Preview
/// pair TUNNELS from the window down to it, then the plain one BUBBLES back out - one argument object shared by both, so
/// a parent that sets <c>Handled</c> in the preview vetoes the bubbling handlers AND the matching command.
/// <c>OriginalSource</c> names the deepest element actually hit. <c>Effects</c> is decided by
/// <see cref="DragOverEvent"/> - the one that fires on every move; Enter/Leave are notifications of the target changing.
/// </para>
/// <para>
/// Declared here rather than on <c>DragDrop</c> because that class lives above the controls that expose the CLR event
/// wrappers - the same reason <see cref="Mouse"/> owns the pointer events.
/// </para>
/// </summary>
public static class DragDropEvents
{
    /// <summary>A drag entered this drop target.</summary>
    public static readonly RoutedEvent DragEnterEvent = EventManager.RegisterRoutedEvent("DragEnter",
        RoutingStrategy.Bubble, typeof(DragDropEventHandler), typeof(DragDropEvents));

    /// <summary>A drag is moving over this drop target - set <c>Effects</c> here to say what would happen (and
    /// <c>DragDropEffects.None</c> to refuse the payload).</summary>
    public static readonly RoutedEvent DragOverEvent = EventManager.RegisterRoutedEvent("DragOver",
        RoutingStrategy.Bubble, typeof(DragDropEventHandler), typeof(DragDropEvents));

    /// <summary>The drag left this drop target (moved to another one, off the window, or ended).</summary>
    public static readonly RoutedEvent DragLeaveEvent = EventManager.RegisterRoutedEvent("DragLeave",
        RoutingStrategy.Bubble, typeof(DragDropEventHandler), typeof(DragDropEvents));

    /// <summary>The payload was dropped on this target. Fires only when the drop is going ahead (<c>Effects</c> is not
    /// None), after the source has removed on a Move - see the ordering note in <c>DragDrop.CompleteDrop</c>.</summary>
    public static readonly RoutedEvent DropEvent = EventManager.RegisterRoutedEvent("Drop",
        RoutingStrategy.Bubble, typeof(DragDropEventHandler), typeof(DragDropEvents));

    // The tunnelling half: a CONTAINER sees the drag before the element it contains does, which is the only way to veto
    // from above (a locked panel refusing every drop inside it, a parent that answers for its children).
    /// <summary>Tunnels ahead of <see cref="DragEnterEvent"/>.</summary>
    public static readonly RoutedEvent PreviewDragEnterEvent = EventManager.RegisterRoutedEvent("PreviewDragEnter",
        RoutingStrategy.Tunnel, typeof(DragDropEventHandler), typeof(DragDropEvents));

    /// <summary>Tunnels ahead of <see cref="DragOverEvent"/> - set <c>Handled</c> here to answer for the whole subtree.</summary>
    public static readonly RoutedEvent PreviewDragOverEvent = EventManager.RegisterRoutedEvent("PreviewDragOver",
        RoutingStrategy.Tunnel, typeof(DragDropEventHandler), typeof(DragDropEvents));

    /// <summary>Tunnels ahead of <see cref="DragLeaveEvent"/>.</summary>
    public static readonly RoutedEvent PreviewDragLeaveEvent = EventManager.RegisterRoutedEvent("PreviewDragLeave",
        RoutingStrategy.Tunnel, typeof(DragDropEventHandler), typeof(DragDropEvents));

    /// <summary>Tunnels ahead of <see cref="DropEvent"/>.</summary>
    public static readonly RoutedEvent PreviewDropEvent = EventManager.RegisterRoutedEvent("PreviewDrop",
        RoutingStrategy.Tunnel, typeof(DragDropEventHandler), typeof(DragDropEvents));
}
