using System;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;

namespace Adamantium.UI.Input;

/// <summary>
/// The OS half of the drag-drop engine (docs/DRAG_DROP_PLAN.md phases 5-6) - payloads crossing between our app and
/// OTHER applications. Two directions, one engine:
/// <list type="bullet">
/// <item>DROP-IN: every window carries a native drop target from the moment it is created. Files from Explorer, text
/// from an editor - the payload is read into an <see cref="IDataPackage"/> and then travels the SAME path as an in-app
/// drag: <c>AllowDrop</c> targeting, <c>IsDragOver</c> highlight, the insertion caret, spring-loading, auto-scroll and
/// finally <c>DropCommand</c>. A view-model reads <c>data.Get(DataFormats.Files)</c> and never learns which app it
/// came from.</item>
/// <item>DRAG-OUT: a source with <c>DragDrop.AllowExternalDrag="True"</c> hands the whole gesture to the platform drag
/// loop, so it can be dropped into other applications - and dropped back onto our own windows through the same
/// always-registered drop target, where the LIVE CLR payload is unwrapped again (no serialization round trip).</item>
/// </list>
/// <para>
/// THREADING: the platform calls the sink on its window/message thread, inside the drag source's modal loop. Nothing
/// here touches the visual tree there: each callback records the drag point and POSTS the work onto the UI loop thread,
/// answering the OS with the effect the last update settled on. That is one frame of lag on the cursor feedback and a
/// deadlock-free bridge - the alternative, blocking the OS drag loop on our render loop, would hang both apps.
/// </para>
/// </summary>
public static partial class DragDrop
{
    private static readonly NativeDropSink Sink = new();

    private static INativeDragDrop _native;
    private static bool _nativeResolved;

    // Source side: this gesture was handed to the OS (AllowExternalDrag), and the platform loop is running.
    private static bool _externalCapable;
    private static bool _nativeDragActive;
    private static bool _sourceCompleted;   // the source's DragCompleted already ran (the drop landed on one of OUR windows)

    // Target side: an OS-driven drag is currently over one of our windows. Written by the platform thread, read by the loop.
    private static bool _externalActive;
    private static IWindow _externalWindow;
    private static IDataPackage _externalData;
    // The drag point PACKED into one 64-bit slot (screen X in the high half, Y in the low): written by the platform
    // thread, read by the loop thread, and a Vector2 written field by field could be read half-updated.
    private static long _externalPointPacked;
    private static InputModifiers _externalModifiers;
    private static DragDropEffects _externalAllowed;
    private static DragDropEffects _externalEffect;
    private static bool _externalUpdatePending;

    /// <summary>The OS drag-drop bridge, or null on a platform/configuration without one (in-app drag-drop is then
    /// entirely unaffected). Resolved once, lazily - the container is only complete after the app has started.</summary>
    private static INativeDragDrop Native
    {
        get
        {
            if (_nativeResolved) return _native;
            var container = UIApplication.Current?.Container;
            if (container == null) return null;   // too early - try again later
            _nativeResolved = true;
            if (container.IsRegistered<INativeDragDrop>()) _native = container.Resolve<INativeDragDrop>();
            return _native;
        }
    }

    /// <summary>The platform service, for the queries only the OS can answer during a drag (which window is on top at a
    /// given point). Null before the app is up, or where the platform has no answer - callers fall back.</summary>
    private static Platforms.IApplicationPlatform Platform
    {
        get
        {
            if (_platformResolved) return _platform;
            var container = UIApplication.Current?.Container;
            if (container == null) return null;
            _platformResolved = true;
            if (container.IsRegistered<Platforms.IApplicationPlatform>()) _platform = container.Resolve<Platforms.IApplicationPlatform>();
            return _platform;
        }
    }

    private static Platforms.IApplicationPlatform _platform;
    private static bool _platformResolved;

    /// <summary>True while ANY drag is in flight - ours or the OS's. Guards a second gesture and keeps the drag-only
    /// timers (spring-load, auto-scroll) alive for an external drag too.</summary>
    private static bool IsDragActive => _dragging || _externalActive;

    /// <summary>The drag point in PHYSICAL screen coordinates. During an OS-driven drag it is the point the platform
    /// reported (authoritative, and the one the device position was published from), otherwise the live cursor.</summary>
    private static Vector2 DragScreenPoint => _externalActive ? ExternalPoint : Mouse.ScreenCoordinates;

    private static Vector2 ExternalPoint
    {
        get
        {
            var packed = System.Threading.Volatile.Read(ref _externalPointPacked);
            return new Vector2((int)(packed >> 32), (int)packed);
        }
        set => System.Threading.Volatile.Write(ref _externalPointPacked, ((long)(int)value.X << 32) | (uint)(int)value.Y);
    }

    /// <summary>The window the drag is over: named by the platform for an OS drag (exact even with overlapping
    /// windows), found by screen coordinates for ours.</summary>
    private static IWindow DragWindow(Vector2 screen) => _externalActive ? _externalWindow : WindowUnderCursor(screen);

    // ------------------------------------------------------------------ drop-in: native drop target per window

    /// <summary>Make <paramref name="window"/> accept drops from other applications. Called once by the platform window
    /// worker as the native window comes up (on the thread that owns it); a no-op where there is no OS bridge.</summary>
    public static void RegisterNativeDropTarget(IWindow window) => Native?.RegisterDropTarget(window, Sink);

    /// <summary>Drop the native drop target as the window closes (before its native handle is destroyed).</summary>
    public static void UnregisterNativeDropTarget(IWindow window) => Native?.UnregisterDropTarget(window);

    /// <summary>Forwards the platform's drop-target callbacks into the engine. A tiny adapter so the engine can stay the
    /// single static <see cref="DragDrop"/> class it is.</summary>
    private sealed class NativeDropSink : INativeDropSink
    {
        public DragDropEffects DragEnter(IWindow window, IDataPackage data, Vector2 screenPoint, InputModifiers modifiers,
            DragDropEffects allowed) => ExternalDragOver(window, data, screenPoint, modifiers, allowed);

        public DragDropEffects DragOver(IWindow window, Vector2 screenPoint, InputModifiers modifiers,
            DragDropEffects allowed) => ExternalDragOver(window, _externalData, screenPoint, modifiers, allowed);

        public void DragLeave(IWindow window) => ExternalDragLeave();

        public DragDropEffects Drop(IWindow window, IDataPackage data, Vector2 screenPoint, InputModifiers modifiers,
            DragDropEffects allowed) => ExternalDrop(window, data, screenPoint, modifiers, allowed);
    }

    // Platform thread. Record the state and schedule ONE update - a move storm collapses into a single pass, since
    // drag-over is idempotent state, not a stream of events. The answer is the last effect the engine settled on.
    private static DragDropEffects ExternalDragOver(IWindow window, IDataPackage data, Vector2 screenPoint,
        InputModifiers modifiers, DragDropEffects allowed)
    {
        _externalWindow = window;
        _externalData = data;
        ExternalPoint = screenPoint;
        _externalModifiers = modifiers;
        _externalAllowed = allowed;
        _externalActive = true;

        if (!_externalUpdatePending)
        {
            _externalUpdatePending = true;
            Post(RunExternalUpdate);
        }
        return _externalEffect;
    }

    private static void ExternalDragLeave()
    {
        _externalActive = false;
        _externalEffect = DragDropEffects.None;
        Post(ClearExternalFeedback);
    }

    // Platform thread. The drop is delivered on the loop thread a frame later; what we answer here is what the SOURCE
    // application is told happened - and it is the last DragOver's effect, which by release time names the real target.
    private static DragDropEffects ExternalDrop(IWindow window, IDataPackage data, Vector2 screenPoint,
        InputModifiers modifiers, DragDropEffects allowed)
    {
        _externalWindow = window;
        _externalData = data;
        ExternalPoint = screenPoint;
        _externalModifiers = modifiers;
        _externalAllowed = allowed;
        var effect = _externalEffect;
        Post(CompleteExternalDrop);
        return effect;
    }

    // Loop thread. Publish the platform's drag point onto the mouse device (no move message reaches us while another
    // app owns the pointer, so every GetPosition in the drop machinery would otherwise read a stale point), then run
    // the ONE shared drag-over pass.
    private static void RunExternalUpdate()
    {
        _externalUpdatePending = false;
        if (!_externalActive || _externalWindow == null) return;
        _data = _externalData;
        Mouse.PrimaryDevice.SetExternalPosition(_externalWindow as IInputComponent, ExternalPoint);
        UpdateDragOver();
    }

    // Loop thread. Re-target at the release point (so the drop is decided on fresh state, not on the last posted move),
    // then deliver: on OUR OWN native drag the source removes FIRST and the target adds after - the same order the
    // in-app path uses, which is what makes a drop back into the origin collection a reorder instead of a delete.
    private static void CompleteExternalDrop()
    {
        _externalActive = true;   // the leave that follows a drop must not pre-empt this pass
        RunExternalUpdate();

        var effects = _dragEffects;
        var target = _currentTarget;
        var args = new DragDropEventArgs(_data, _source, _externalWindow?.PointToClient(ExternalPoint) ?? default)
        {
            Effects = effects,
            InsertIndex = _insertIndex,
            InsertBefore = _insertBefore,
            DropTarget = _dropTarget,
            Placement = _placement
        };

        if (_nativeDragActive && _source != null && effects != DragDropEffects.None)
        {
            RunDragCompleted(args);
            _sourceCompleted = true;
        }

        if (target != null && effects != DragDropEffects.None)
        {
            var drop = GetDropCommand((AdamantiumComponent)target);
            if (drop != null && drop.CanExecute(args)) drop.Execute(args);
        }

        ClearExternalFeedback();
    }

    // Loop thread. The OS drag left our windows (or ended): drop every bit of drop feedback, but keep the SOURCE
    // session alive when our own native drag is still running - it ends on its own completion callback.
    private static void ClearExternalFeedback()
    {
        _externalActive = false;
        _externalWindow = null;
        _externalData = null;
        _externalEffect = DragDropEffects.None;
        ResetDropFeedback();
        if (!_nativeDragActive) _data = null;
    }

    /// <summary>Everything the drag-over pass lit up: the hovered target's highlight, the insertion cue, the dwell and
    /// auto-scroll timers. Shared by both resets - an in-app drag ending and an OS drag leaving clear the same things.</summary>
    private static void ResetDropFeedback()
    {
        _dragEffects = DragDropEffects.None;
        if (_currentTarget != null) SetIsDragOver((AdamantiumComponent)_currentTarget, false);
        _currentTarget = null;
        _springTimer?.Stop();
        _springTarget = null;
        _raiseTimer?.Stop();
        _raiseCandidate = null;
        StopAutoScroll();
        ClearIndicator();
    }

    // The default effect for an OS-driven drag: the platform's own modifier convention (Ctrl = copy, Shift = move,
    // both = link), narrowed to what the SOURCE allows. With no modifier held, a payload from ANOTHER app defaults to
    // Copy - answering Move would tell that app to delete the user's original - while our own drag keeps the in-app
    // Move default. A target's DragOverCommand can still downgrade any of this to None.
    private static DragDropEffects ExternalDefaultEffects()
    {
        var control = (_externalModifiers & (InputModifiers.LeftControl | InputModifiers.RightControl)) != 0;
        var shift = (_externalModifiers & (InputModifiers.LeftShift | InputModifiers.RightShift)) != 0;
        var wanted = control && shift ? DragDropEffects.Link
            : control ? DragDropEffects.Copy
            : shift ? DragDropEffects.Move
            : _nativeDragActive ? DragDropEffects.Move : DragDropEffects.Copy;

        if ((wanted & _externalAllowed) != 0) return wanted;
        if ((_externalAllowed & DragDropEffects.Copy) != 0) return DragDropEffects.Copy;
        if ((_externalAllowed & DragDropEffects.Move) != 0) return DragDropEffects.Move;
        if ((_externalAllowed & DragDropEffects.Link) != 0) return DragDropEffects.Link;
        return DragDropEffects.None;
    }

    // ------------------------------------------------------------------ drag-out: our gesture handed to the OS

    /// <summary>Hand the started gesture to the platform drag loop, ghost and all. Called once the snapshot has baked -
    /// the last thing our own loop owed this drag.</summary>
    private static void StartNativeDrag()
    {
        var source = _source;
        if (!_dragging || source == null) return;

        // Unhook BEFORE releasing the capture: the release raises LostMouseCapture, which would cancel the very drag we
        // are handing over. From here the OS owns the pointer.
        Detach(source);
        if (ReferenceEquals(Mouse.Captured, source)) Mouse.Capture(null);

        // ptOffset (0,0): the cursor sits at the image's top-left, so the ghost trails down-right of it - the same
        // relationship our own floating ghost has.
        var ghost = new DragGhostImage(_ghostBgra, _ghostW, _ghostH, 0, 0);
        _nativeDragActive = true;
        var allowed = DragDropEffects.Copy | DragDropEffects.Move;
        if (Native?.BeginDrag(SourceWindow(source as IUIComponent), _data, allowed, ghost, OnNativeDragCompleted) == true) return;

        _nativeDragActive = false;
        CancelDrag();   // the platform refused to start - end the gesture cleanly instead of leaving it half-armed
    }

    // Loop thread (posted by the platform when its modal loop ends). The effect is what the OS reports the target did -
    // for a drop into ANOTHER application that is the only word we get on whether it was a copy or a move.
    private static void OnNativeDragCompleted(DragDropEffects effects)
    {
        _nativeDragActive = false;
        if (!_sourceCompleted && _source != null)
        {
            RunDragCompleted(new DragDropEventArgs(_data, _source, default) { Effects = effects });
        }
        Reset();
    }

    private static void RunDragCompleted(DragDropEventArgs args)
    {
        var completed = GetDragCompletedCommand((AdamantiumComponent)_source);
        if (completed != null && completed.CanExecute(args)) completed.Execute(args);
    }

    private static IWindow SourceWindow(IUIComponent source)
    {
        for (var node = source; node != null; node = node.VisualParent)
        {
            if (node is IWindow window) return window;
        }
        return UIApplication.Current?.ActiveWindow;
    }

    /// <summary>Queue work onto the UI loop thread (runs inline when already there). The platform's drag callbacks use
    /// it to get off their own thread before touching anything the loop owns.</summary>
    private static void Post(Action action) => Threading.Dispatcher.CurrentDispatcher?.Post(action);
}
