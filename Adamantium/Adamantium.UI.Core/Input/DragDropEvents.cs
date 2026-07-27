using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Input;

/// <summary>
/// The routed drag-drop events - the CONTROL-side half of the drop API (the view-model side is <c>DragDrop.DropCommand</c>
/// and friends). A control that wants to react to a drag flying over it handles these instead of needing a view-model.
/// <para>
/// They are raised on the nearest element with <c>DragDrop.AllowDrop="True"</c> under the pointer and BUBBLE from there,
/// with <c>OriginalSource</c> naming the deepest element actually hit. <c>Effects</c> is decided by
/// <see cref="DragOverEvent"/> - the one that fires on every move; Enter/Leave are notifications of the target changing.
/// Setting <c>Handled</c> stops the route AND suppresses the matching command.
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
}
