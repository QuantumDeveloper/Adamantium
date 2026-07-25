using System.Collections.Generic;
using System.Linq;
using Adamantium.Core.Commands;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Adorners;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Imaging;
using Adamantium.UI.Core.Rendering;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Input;

/// <summary>
/// The in-window drag-drop engine (level 1) + its attached-property facade. Retrofit onto ANY element without touching it:
/// set <c>DragDrop.AllowDrag="True"</c> + <c>DragDrop.DragData</c> on a source, <c>DragDrop.AllowDrop="True"</c> +
/// <c>DragDrop.DropCommand</c> on a target. On press+threshold the source is baked to a ghost that follows the cursor (a
/// real layered OS window, <see cref="IDragGhost"/>); on release the target under the cursor gets the payload through its
/// <c>DropCommand</c> - MVVM-first, no UI types in the VM (docs/DRAG_DROP_PLAN.md). App-global, one drag at a time.
/// </summary>
public static class DragDrop
{
    // Pixels the pointer must travel before a press becomes a drag - a click is not a drag.
    private const double DragThreshold = 4.0;
    // Ghost sits down-right of the cursor so it doesn't hide it.
    private const int GhostCursorOffset = 12;

    // ------------------------------------------------------------------ attached properties
    public static readonly AdamantiumProperty AllowDragProperty = AdamantiumProperty.RegisterAttached(
        "AllowDrag", typeof(bool), typeof(AdamantiumComponent), new PropertyMetadata(false, OnAllowDragChanged));

    public static readonly AdamantiumProperty DragDataProperty = AdamantiumProperty.RegisterAttached(
        "DragData", typeof(object), typeof(AdamantiumComponent), new PropertyMetadata(null));

    public static readonly AdamantiumProperty AllowDropProperty = AdamantiumProperty.RegisterAttached(
        "AllowDrop", typeof(bool), typeof(AdamantiumComponent), new PropertyMetadata(false));

    public static readonly AdamantiumProperty DropCommandProperty = AdamantiumProperty.RegisterAttached(
        "DropCommand", typeof(ICommand), typeof(AdamantiumComponent), new PropertyMetadata(null));

    // Live drag feedback (set by the engine as the cursor moves over a drag): IsDragOver is true on the AllowDrop target
    // currently under the cursor - drive a highlight off it with a trigger. DragOverCommand fires on that target every move
    // so a view-model can decide "can THIS payload land here right now" (and later adjust the cursor).
    public static readonly AdamantiumProperty IsDragOverProperty = AdamantiumProperty.RegisterAttached(
        "IsDragOver", typeof(bool), typeof(AdamantiumComponent), new PropertyMetadata(false));

    public static readonly AdamantiumProperty DragOverCommandProperty = AdamantiumProperty.RegisterAttached(
        "DragOverCommand", typeof(ICommand), typeof(AdamantiumComponent), new PropertyMetadata(null));

    // Source-side lifecycle. The target ADDS the item to its collection; the SOURCE removes it - on a Move only - from the
    // collection it came from. That split is the only thing that works cross-window / cross-view-model (the target can't
    // touch a collection it doesn't own). DragStarted lets the source record WHERE the item came from before any target
    // mutates its lists; DragCompleted fires at the end with the final Effects (None if dropped nowhere).
    public static readonly AdamantiumProperty DragStartedCommandProperty = AdamantiumProperty.RegisterAttached(
        "DragStartedCommand", typeof(ICommand), typeof(AdamantiumComponent), new PropertyMetadata(null));

    public static readonly AdamantiumProperty DragCompletedCommandProperty = AdamantiumProperty.RegisterAttached(
        "DragCompletedCommand", typeof(ICommand), typeof(AdamantiumComponent), new PropertyMetadata(null));

    public static bool GetAllowDrag(AdamantiumComponent e) => e.GetValue<bool>(AllowDragProperty);
    public static void SetAllowDrag(AdamantiumComponent e, bool value) => e.SetValue(AllowDragProperty, value);

    public static object GetDragData(AdamantiumComponent e) => e.GetValue(DragDataProperty);
    public static void SetDragData(AdamantiumComponent e, object value) => e.SetValue(DragDataProperty, value);

    public static bool GetAllowDrop(AdamantiumComponent e) => e.GetValue<bool>(AllowDropProperty);
    public static void SetAllowDrop(AdamantiumComponent e, bool value) => e.SetValue(AllowDropProperty, value);

    public static ICommand GetDropCommand(AdamantiumComponent e) => e.GetValue(DropCommandProperty) as ICommand;
    public static void SetDropCommand(AdamantiumComponent e, ICommand value) => e.SetValue(DropCommandProperty, value);

    public static bool GetIsDragOver(AdamantiumComponent e) => e.GetValue<bool>(IsDragOverProperty);
    public static void SetIsDragOver(AdamantiumComponent e, bool value) => e.SetValue(IsDragOverProperty, value);

    public static ICommand GetDragOverCommand(AdamantiumComponent e) => e.GetValue(DragOverCommandProperty) as ICommand;
    public static void SetDragOverCommand(AdamantiumComponent e, ICommand value) => e.SetValue(DragOverCommandProperty, value);

    public static ICommand GetDragStartedCommand(AdamantiumComponent e) => e.GetValue(DragStartedCommandProperty) as ICommand;
    public static void SetDragStartedCommand(AdamantiumComponent e, ICommand value) => e.SetValue(DragStartedCommandProperty, value);

    public static ICommand GetDragCompletedCommand(AdamantiumComponent e) => e.GetValue(DragCompletedCommandProperty) as ICommand;
    public static void SetDragCompletedCommand(AdamantiumComponent e, ICommand value) => e.SetValue(DragCompletedCommandProperty, value);

    // ------------------------------------------------------------------ session state (one drag at a time, app-global)
    private static IInputComponent _source;      // the pressed source; armed until the threshold, then dragging
    private static Vector2 _startScreen;
    private static bool _dragging;
    private static IDataPackage _data;
    private static byte[] _ghostBgra;
    private static int _ghostW, _ghostH;
    private static bool _ghostShown;

    // Multi-item ghost: one snapshot per selected element, composited into a stack once they've all arrived.
    private static (byte[] bgra, int w, int h)[] _ghostParts;
    private static int _ghostPending;

    // The AllowDrop target currently under the cursor (live drag-over feedback).
    private static IUIComponent _currentTarget;

    // The insertion line: which items host, where the line sits, and the resulting insert index handed to the drop.
    private static DropInsertionAdorner _indicator;
    private static AdornerLayer _indicatorLayer;
    private static ListBox _indicatorList;
    private static double _indicatorY = double.NaN;
    private static int _insertIndex = -1;

    private static IVisualRenderer _renderer;
    private static IDragGhost _ghost;
    private static IVisualRenderer Renderer => _renderer ??= UIApplication.Current?.Container.Resolve<IVisualRenderer>();
    private static IDragGhost Ghost => _ghost ??= UIApplication.Current?.Container.Resolve<IDragGhost>();

    private static void OnAllowDragChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not IInputComponent input) return;
        input.PreviewMouseLeftButtonDown -= OnSourceDown;   // idempotent
        if (e.NewValue is true) input.PreviewMouseLeftButtonDown += OnSourceDown;
    }

    private static void OnSourceDown(object sender, MouseButtonEventArgs e)
    {
        if (_dragging || sender is not IInputComponent input) return;
        _source = input;
        _startScreen = Mouse.ScreenCoordinates;
        // Track move/up only while a press is live - added here, removed on up. Move keeps arriving after capture.
        input.MouseMove += OnMove;
        input.MouseLeftButtonUp += OnUp;
    }

    private static void OnMove(object sender, MouseEventArgs e)
    {
        if (_source == null) return;
        if (!_dragging)
        {
            if ((Mouse.ScreenCoordinates - _startScreen).Length() < DragThreshold) return;
            BeginDrag();
        }
        ShowOrMoveGhost();
        UpdateDragOver();
    }

    // Live feedback, every move: which AllowDrop target is under the cursor (across windows), highlight it via IsDragOver,
    // fire its DragOverCommand, and auto-scroll a scroll area the cursor is near the edge of.
    private static void UpdateDragOver()
    {
        var screen = Mouse.ScreenCoordinates;
        IUIComponent target = null;
        IWindow window = null;
        IUIComponent hit = null;
        if (WindowUnderCursor(screen) is { } w && w is IUIComponent root)
        {
            window = w;
            hit = root.HitTest(window.PointToClient(screen)) as IUIComponent;
            AutoScroll(hit);
            target = FindAllowDropAncestor(hit);
        }

        if (!ReferenceEquals(target, _currentTarget))
        {
            if (_currentTarget != null) SetIsDragOver((AdamantiumComponent)_currentTarget, false);
            _currentTarget = target;
            if (_currentTarget != null) SetIsDragOver((AdamantiumComponent)_currentTarget, true);
        }

        UpdateInsertionIndicator(target != null ? window : null, hit);

        if (target != null && GetDragOverCommand((AdamantiumComponent)target) is { } over)
        {
            var args = new DragDropEventArgs(_data, _source, Mouse.GetPosition((IInputComponent)target));
            if (over.CanExecute(args)) over.Execute(args);
        }
    }

    // The insertion line: over an AllowDrop items host, show a bar at the position an item would land and remember that
    // index for the drop. Recreated (not mutated) when the list or line moves - a fresh adorner renders immediately.
    private static void UpdateInsertionIndicator(IWindow window, IUIComponent hit)
    {
        ListBox list = null;
        for (var c = hit; c != null; c = c.VisualParent)
        {
            if (c is ListBox lb) { list = lb; break; }
        }

        if (list == null || window is not IAdornerHost host)
        {
            ClearIndicator();
            return;
        }

        var index = ComputeInsertIndex(list, hit, out var lineY);
        _insertIndex = index;

        if (ReferenceEquals(list, _indicatorList) && ReferenceEquals(host.AdornerLayer, _indicatorLayer) && lineY == _indicatorY && _indicator != null)
        {
            return;   // nothing moved
        }

        ClearIndicator();
        _indicatorList = list;
        _indicatorLayer = host.AdornerLayer;
        _indicatorY = lineY;
        _indicator = new DropInsertionAdorner(list, lineY) { Stroke = InsertionBrush() };
        _indicatorLayer.Add(_indicator);
    }

    // The insertion caret's colour, from the active theme (the accent) so it matches the app - not a hard-coded hue.
    private static Brush InsertionBrush()
    {
        if (UIApplication.Current?.ThemeManager?.CurrentTheme is { } theme &&
            theme.TryGetResource("AccentFillColorDefault", out var value) && value is Brush brush)
        {
            return brush;
        }
        return new SolidColorBrush(Colors.DodgerBlue);
    }

    // Insert index + the line's Y (in the LIST's local coords) for the cursor: before/after the item under it by its
    // mid-line; an empty area (or below all items) appends at the bottom of the last item.
    private static int ComputeInsertIndex(ListBox list, IUIComponent hit, out double lineY)
    {
        var pList = Mouse.GetPosition((IInputComponent)list);

        ListBoxItem container = null;
        for (var c = hit; c != null && !ReferenceEquals(c, list); c = c.VisualParent)
        {
            if (c is ListBoxItem li) { container = li; break; }
        }

        if (container != null)
        {
            var index = list.ItemContainerGenerator.IndexFromContainer(container);
            var pItem = Mouse.GetPosition((IInputComponent)container);
            var top = pList.Y - pItem.Y;                 // the item's top in list coords (a pure coordinate transform)
            var itemHeight = container.RenderSize.Height;
            var below = pItem.Y > itemHeight / 2.0;
            lineY = top + (below ? itemHeight : 0);
            return index + (below ? 1 : 0);
        }

        // No item under the cursor: append. Put the line at the bottom of the last realised item, or the top if empty.
        var count = list.Items?.Count ?? 0;
        if (count > 0 && list.ItemContainerGenerator.ContainerFromIndex(count - 1) is { } lastC)
        {
            var pLast = Mouse.GetPosition((IInputComponent)lastC);
            lineY = (pList.Y - pLast.Y) + lastC.RenderSize.Height;
        }
        else
        {
            lineY = 0;
        }
        return count;
    }

    private static void ClearIndicator()
    {
        if (_indicator != null) _indicatorLayer?.Remove(_indicator);
        _indicator = null;
        _indicatorLayer = null;
        _indicatorList = null;
        _indicatorY = double.NaN;
        _insertIndex = -1;
    }

    private static IUIComponent FindAllowDropAncestor(IUIComponent hit)
    {
        for (var c = hit; c != null; c = c.VisualParent)
        {
            if (GetAllowDrop((AdamantiumComponent)c)) return c;
        }
        return null;
    }

    // Scroll the nearest ScrollViewer when the cursor is within an edge band of it. Fires per move; holding still at the
    // edge waits for the next move (continuous hold-to-scroll would need a timer - a later refinement).
    private static void AutoScroll(IUIComponent hit)
    {
        for (var c = hit; c != null; c = c.VisualParent)
        {
            if (c is not ScrollViewer sv) continue;
            var local = Mouse.GetPosition((IInputComponent)sv);
            var height = sv.RenderSize.Height;
            const double band = 28, step = 20;
            double dy = local.Y < band ? -step : local.Y > height - band ? step : 0;
            if (dy != 0) sv.SetScrollOffset(sv.ScrollOffset + new Vector2(0, dy));
            return;
        }
    }

    private static void BeginDrag()
    {
        _dragging = true;
        _data = ResolveData((AdamantiumComponent)_source);
        Mouse.Capture(_source);   // so move/up keep coming once the cursor leaves the source
        _source.LostMouseCapture += OnLostCapture;   // an external overlay (a screenshot tool) stealing capture must not hang the drag

        // Tell the source the drag has begun - it records WHERE the payload came from now, before any target touches its
        // collections (so DragCompleted can remove it from the right place).
        var startArgs = new DragDropEventArgs(_data, _source, _startScreen);
        var started = GetDragStartedCommand((AdamantiumComponent)_source);
        if (started != null && started.CanExecute(startArgs)) started.Execute(startArgs);

        // The ghost is a snapshot of every dragged element (the whole multi-selection), composited into a stack. Each bake
        // runs OFF the render thread (queued); the ghost appears once they've all arrived (a frame or two later).
        var elements = GetGhostElements(_source);
        _ghostParts = new (byte[], int, int)[elements.Count];
        _ghostPending = elements.Count;
        for (int i = 0; i < elements.Count; i++)
        {
            int index = i;
            Renderer?.RequestSnapshot(elements[i], img => OnGhostPartReady(index, img));
        }
    }

    private static void OnGhostPartReady(int index, ImageSource img)
    {
        if (!_dragging || _ghostParts == null || index >= _ghostParts.Length) return;
        if (img is BitmapSource bs)
        {
            _ghostParts[index] = (DragGhostPixels.ToPremultipliedBgra(bs), (int)bs.PixelWidth, (int)bs.PixelHeight);
        }
        if (--_ghostPending > 0) return;   // wait for every part

        var parts = _ghostParts.Where(p => p.bgra != null).ToList();
        if (parts.Count == 0) return;
        var (bgra, w, h) = parts.Count == 1 ? parts[0] : DragGhostPixels.StackVertical(parts, 4);
        _ghostBgra = bgra;
        _ghostW = w;
        _ghostH = h;
        ShowOrMoveGhost();
    }

    // The elements the ghost pictures: the whole selection when the source is inside a multi-select ListBox with more than
    // one item selected (the selected containers, in list order), otherwise just the pressed source.
    private static IReadOnlyList<IUIComponent> GetGhostElements(IInputComponent source)
    {
        if (source is IUIComponent visual &&
            visual.GetVisualAncestors().OfType<ListBox>().FirstOrDefault() is { SelectionMode: SelectionMode.Multiple or SelectionMode.Extended } lb &&
            lb.SelectedItems is { Count: > 1 } selected)
        {
            var containers = new List<IUIComponent>();
            for (int i = 0; i < lb.Items.Count; i++)
            {
                if (selected.Contains(lb.Items[i]) && lb.ItemContainerGenerator.ContainerFromIndex(i) is { } c) containers.Add(c);
            }
            if (containers.Count > 1) return containers;
        }
        return [(IUIComponent)source];
    }

    // Capture yanked away mid-drag (a screenshot overlay, Alt-Tab, another control grabbing it) - cancel cleanly so the
    // ghost never hangs and the source isn't left half-dragged. No drop; the source's DragCompleted still runs with None.
    private static void OnLostCapture(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        Ghost?.Hide();
        var completed = GetDragCompletedCommand((AdamantiumComponent)_source);
        var cancelArgs = new DragDropEventArgs(_data, _source, default) { Effects = DragDropEffects.None };
        if (completed != null && completed.CanExecute(cancelArgs)) completed.Execute(cancelArgs);
        Detach(_source);
        Reset();
    }

    private static void ShowOrMoveGhost()
    {
        if (_ghostBgra == null) return;
        var s = Mouse.ScreenCoordinates;
        int x = (int)s.X + GhostCursorOffset, y = (int)s.Y + GhostCursorOffset;
        if (!_ghostShown)
        {
            Ghost?.Show(_ghostBgra, _ghostW, _ghostH, x, y);
            _ghostShown = true;
        }
        else
        {
            Ghost?.Move(x, y);
        }
    }

    private static void OnUp(object sender, MouseButtonEventArgs e)
    {
        var wasDragging = _dragging;
        // Unsubscribe BEFORE Reset releases capture - releasing raises LostMouseCapture, and we must not re-enter OnLostCapture.
        Detach(_source);
        if (wasDragging) CompleteDrop();
        Reset();
    }

    // Drop every per-drag pointer subscription off the source (move / up / lost-capture).
    private static void Detach(IInputComponent source)
    {
        if (source == null) return;
        source.MouseMove -= OnMove;
        source.MouseLeftButtonUp -= OnUp;
        source.LostMouseCapture -= OnLostCapture;
    }

    private static void CompleteDrop()
    {
        Ghost?.Hide();

        // User picks the gesture with a modifier: Ctrl = Copy, otherwise Move (the WPF/Explorer convention). None when the
        // drop lands on no target. The target's DropCommand may adjust Effects (Phase 4: a copy-only target downgrades).
        var ctrl = (Keyboard.Modifiers & (InputModifiers.LeftControl | InputModifiers.RightControl)) != 0;
        var target = HitTestDropTarget(out var positionInTarget);
        var args = new DragDropEventArgs(_data, _source, positionInTarget)
        {
            Effects = target != null ? (ctrl ? DragDropEffects.Copy : DragDropEffects.Move) : DragDropEffects.None,
            InsertIndex = _insertIndex   // where the insertion line was; -1 if not over an items host
        };

        // Order matters: the SOURCE removes (on Move) FIRST, then the TARGET adds. Dropping back into the SAME collection
        // then nets to a re-add (a no-op / reorder), not a delete - which the reverse order would cause (the target's add is
        // a no-op because the item is still there, then the source's remove would wipe it). This split is also what makes
        // cross-window / cross-VM Move work: the target can't touch a collection it doesn't own, so the source removes.
        var completed = GetDragCompletedCommand((AdamantiumComponent)_source);
        if (completed != null && completed.CanExecute(args)) completed.Execute(args);   // source REMOVES on Move

        if (target != null)
        {
            var drop = GetDropCommand((AdamantiumComponent)target);
            if (drop != null && drop.CanExecute(args)) drop.Execute(args);              // target ADDS
        }
    }

    // The nearest AllowDrop ancestor of whatever is under the cursor, in WHICHEVER app window the cursor is over (level 2:
    // cross-window, same app - the ghost is a topmost window so the drop can land in another of our windows or the same
    // one). Screen-coordinate hit-test: find the window under the cursor, then hit-test its tree. HitTest ignores capture,
    // so it finds the real element even though the source holds mouse capture during the drag.
    private static IUIComponent HitTestDropTarget(out Vector2 positionInTarget)
    {
        positionInTarget = default;
        var screen = Mouse.ScreenCoordinates;
        if (WindowUnderCursor(screen) is not { } window || window is not IUIComponent root) return null;

        var pointInRoot = window.PointToClient(screen);   // physical screen -> logical client coords (DPI-scaled)
        var target = FindAllowDropAncestor(root.HitTest(pointInRoot) as IUIComponent);
        positionInTarget = pointInRoot;   // in the target window's client space (element-relative is a later nicety)
        return target;
    }

    // The app window whose client area the physical-screen cursor is over. Iterates UIApplication.Windows; the ghost is a
    // raw click-through Win32 window (not an IWindow), so it is never a candidate. NB: window ORDER, not OS z-order - fine
    // for side-by-side windows; overlapping windows would need WindowFromPoint / a real z-order query (later).
    private static IWindow WindowUnderCursor(Vector2 screen)
    {
        var app = UIApplication.Current;
        if (app == null) return null;
        foreach (var w in app.Windows)
        {
            var client = w.PointToClient(screen);
            if (client.X >= 0 && client.Y >= 0 && client.X <= w.ClientWidth && client.Y <= w.ClientHeight) return w;
        }
        return null;
    }

    private static void Reset()
    {
        if (_dragging && ReferenceEquals(Mouse.Captured, _source)) Mouse.Capture(null);
        if (_currentTarget != null) SetIsDragOver((AdamantiumComponent)_currentTarget, false);
        _currentTarget = null;
        ClearIndicator();
        _source = null;
        _data = null;
        _dragging = false;
        _ghostBgra = null;
        _ghostShown = false;
        _ghostParts = null;
        _ghostPending = 0;
    }

    private static IDataPackage ResolveData(AdamantiumComponent source)
    {
        var d = GetDragData(source);
        return d as IDataPackage ?? new DataPackage(d);
    }
}
