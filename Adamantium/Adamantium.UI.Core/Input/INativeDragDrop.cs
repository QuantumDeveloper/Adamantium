using System;

namespace Adamantium.UI.Core.Input;

/// <summary>
/// The OS drag-drop bridge (level 3 of docs/DRAG_DROP_PLAN.md) - what makes our windows exchange payloads with OTHER
/// applications. Two halves, both platform-specific behind this one contract: DROP-IN (a native drop target on every
/// window, always on, feeding an <see cref="INativeDropSink"/>) and DRAG-OUT (<see cref="BeginDrag"/>, an opt-in
/// per-source escalation that hands the whole gesture to the OS drag loop).
/// <para>
/// Windows = OLE (<c>RegisterDragDrop</c> / <c>IDropTarget</c> / <c>DoDragDrop</c>); macOS = AppKit
/// (<c>NSDraggingDestination</c> / <c>beginDraggingSessionWithItems</c>); Linux = XDND / the Wayland data-device. The
/// engine's public API never changes with the platform - that is what the <see cref="IDataPackage"/> abstraction is for.
/// </para>
/// </summary>
public interface INativeDragDrop
{
    /// <summary>False when the OS bridge could not be brought up (on Windows: the UI thread is not an STA OLE thread).
    /// The in-app drag-drop keeps working; only the crossing to other applications is off.</summary>
    bool IsAvailable { get; }

    /// <summary>Make <paramref name="window"/> a native drop target, delivering to <paramref name="sink"/>. Registered
    /// ONCE at window creation and left on for the window's life - never per drag. Call on the thread that owns the
    /// native window.</summary>
    void RegisterDropTarget(IWindow window, INativeDropSink sink);

    /// <summary>Drop the native drop target registered for <paramref name="window"/> (called as the window closes,
    /// before the native handle is destroyed).</summary>
    void UnregisterDropTarget(IWindow window);

    /// <summary>Hand a drag that STARTED in our app to the OS, so it can be dropped into another application. Returns
    /// false if the platform could not start it (the caller then keeps the in-app drag loop).
    /// <para>
    /// The OS loop is modal on the platform's window thread; this call does NOT block the caller. The gesture's outcome
    /// arrives later through <paramref name="completed"/> (on the UI loop thread) - <see cref="DragDropEffects.None"/>
    /// when it was cancelled or dropped nowhere. Drops back onto OUR windows come through the always-registered drop
    /// target, exactly like a drag from another app.
    /// </para>
    /// <para><paramref name="ghost"/> is the baked drag image the OS carries for the whole gesture (empty = the platform's
    /// bare drag cursor). Our own floating ghost window is NOT used here: once the OS owns the gesture, the OS owns the
    /// picture too - that is what keeps it correct over other applications.</para></summary>
    bool BeginDrag(IWindow source, IDataPackage data, DragDropEffects allowed, DragGhostImage ghost, Action<DragDropEffects> completed);
}
