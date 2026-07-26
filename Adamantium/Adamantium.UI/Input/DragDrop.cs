using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Core.Commands;
using Adamantium.UI.Core.Dispatcher;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Adorners;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
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
    // How long the cursor must dwell over a spring-loadable before it activates/expands.
    private const double SpringLoadDwellMs = 600;
    // Auto-scroll edge band (px), timer cadence, and default speed (px/sec) if a ScrollViewer doesn't set AutoScrollSpeed.
    private const double AutoScrollBand = 32;
    private const double AutoScrollTickMs = 16;
    private const double AutoScrollDefaultSpeed = 450;
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

    // Auto-scroll speed (px/sec) while a drag dwells in a ScrollViewer's edge band. Set it on a ScrollViewer to tune how
    // fast that list auto-scrolls during a drop; default AutoScrollDefaultSpeed. The engine ramps it by band depth.
    public static readonly AdamantiumProperty AutoScrollSpeedProperty = AdamantiumProperty.RegisterAttached(
        "AutoScrollSpeed", typeof(double), typeof(AdamantiumComponent), new PropertyMetadata(AutoScrollDefaultSpeed));

    public static double GetAutoScrollSpeed(AdamantiumComponent e) => e.GetValue<double>(AutoScrollSpeedProperty);
    public static void SetAutoScrollSpeed(AdamantiumComponent e, double value) => e.SetValue(AutoScrollSpeedProperty, value);

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

    // Spring-loading: dwell over an ISpringLoadable (a tab, a tree node) and it activates/expands so you can drop into
    // content that isn't visible yet. The timer is armed on entry and fires once if the cursor is still over it.
    private static ISpringLoadable _springTarget;
    private static DispatcherTimer _springTimer;

    // Timer-driven auto-scroll: the ScrollViewer being scrolled + how far (px, signed) to move each tick.
    private static ScrollViewer _autoScrollViewer;
    private static double _autoScrollPerTick;
    private static DispatcherTimer _autoScrollTimer;

    // The insertion cue: which items host, the caret's rect + orientation, and the resulting insert index handed to the drop.
    private static DropInsertionIndicator _indicator;
    private static AdornerLayer _indicatorLayer;
    private static ItemsControl _indicatorList;
    private static Rect _indicatorRect = Rect.Empty;
    private static int _insertIndex = -1;
    private static object _insertBefore;   // the item at the insertion point - insert BEFORE it (robust to same-list shifts)

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

        UpdateSpringLoad(hit);

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

    // The insertion line: over an AllowDrop items host (a ListBox or a TreeView), show a bar where an item would land and
    // remember that index for the drop. Recreated (not mutated) when the list or caret moves - a fresh adorner renders at
    // once. The bar runs ACROSS the item flow: a horizontal caret between stacked rows, a vertical one between side-by-side
    // items (a WrapPanel), so it reads right in any layout.
    private static void UpdateInsertionIndicator(IWindow window, IUIComponent hit)
    {
        ItemsControl list = null;
        for (var c = hit; c != null; c = c.VisualParent)
        {
            if (c is ListBox or TreeView) { list = (ItemsControl)c; break; }
        }

        if (list == null || window is not IAdornerHost host)
        {
            ClearIndicator();
            return;
        }

        var flow = ItemFlowOrientation(list);
        var index = ComputeInsertion(list, flow, out var caret);
        _insertIndex = index;
        _insertBefore = index >= 0 && index < (list.Items?.Count ?? 0) ? list.Items[index] : null;   // the item after the caret

        if (ReferenceEquals(list, _indicatorList) && ReferenceEquals(host.AdornerLayer, _indicatorLayer)
            && caret == _indicatorRect && _indicator != null)
        {
            return;   // nothing moved
        }

        ClearIndicator();
        _indicatorList = list;
        _indicatorLayer = host.AdornerLayer;
        _indicatorRect = caret;

        // A real, themed control: its look is a ControlTemplate from the active theme (restyle the drop cue in the theme,
        // not here). The bar orientation is PERPENDICULAR to the item flow; the theme template keys its end caps off it.
        var barOrientation = flow == Orientation.Vertical ? Orientation.Horizontal : Orientation.Vertical;
        var indicator = new DropInsertionIndicator { AdornedElement = list, Orientation = barOrientation };
        if (UIApplication.Current?.ThemeManager is { CurrentTheme: { } theme } manager) manager.ApplyTheme(theme, indicator);
        ((IMeasurableComponent)indicator).Measure(caret.Size, true);
        ((IMeasurableComponent)indicator).Arrange(caret, true);
        _indicator = indicator;
        _indicatorLayer.Add(_indicator);
    }

    // The item-flow direction of a host's realized panel: a WrapPanel/StackPanel expose it; anything else (the default
    // virtualizing stack, a TreeView's flat rows) flows vertically.
    private static Orientation ItemFlowOrientation(ItemsControl list) => list.ItemsHostPanel switch
    {
        WrapPanel wp => wp.Orientation,
        StackPanel sp => sp.Orientation,
        _ => Orientation.Vertical,
    };

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

    // Insert index + the caret's rect (in the LIST's local coords). Two steps: (1) the nearest realized container + which
    // side of its mid-line gives the insertion INDEX - nearest, not "directly under", because a WrapPanel's margins let a
    // gap point fall through hit-test; (2) the caret is placed in the SEAM between item[index-1] and item[index] - the
    // midpoint of their gap - so its position depends only on the index, not on which plate is nearest. That is what stops
    // it sticking to one plate's edge then flipping to the other's a few px later. Mouse.GetPosition(c) gives each
    // container's rect in list coords, so no TransformToVisual is needed.
    private static int ComputeInsertion(ItemsControl list, Orientation flow, out Rect caret)
    {
        const double thickness = 8.0;   // the caret's thin dimension (fits the theme template's end-cap dots)
        var horizontal = flow == Orientation.Horizontal;
        var listSize = list.RenderSize;
        var count = list.Items?.Count ?? 0;
        var pList = Mouse.GetPosition((IInputComponent)list);

        IUIComponent nearest = null;
        var nearestIndex = -1;
        Vector2 nearestPItem = default;
        var nearestSize = default(Size);
        var best = double.MaxValue;
        foreach (var i in list.ItemContainerGenerator.RealizedIndices)
        {
            if (list.ItemContainerGenerator.ContainerFromIndex(i) is not { } c) continue;
            var pItem = Mouse.GetPosition((IInputComponent)c);
            var size = c.RenderSize;
            // Squared distance from the cursor to this container's rect (0 while inside it).
            var dx = pItem.X < 0 ? -pItem.X : pItem.X > size.Width ? pItem.X - size.Width : 0;
            var dy = pItem.Y < 0 ? -pItem.Y : pItem.Y > size.Height ? pItem.Y - size.Height : 0;
            var dist = dx * dx + dy * dy;
            if (dist < best) { best = dist; nearest = c; nearestIndex = i; nearestPItem = pItem; nearestSize = size; }
        }

        if (nearest == null)   // nothing realized (empty list)
        {
            caret = horizontal ? new Rect(0, 0, thickness, listSize.Height) : new Rect(0, 0, listSize.Width, thickness);
            return count;
        }

        var after = horizontal ? nearestPItem.X > nearestSize.Width / 2.0 : nearestPItem.Y > nearestSize.Height / 2.0;
        var index = nearestIndex + (after ? 1 : 0);   // insert BEFORE item `index`
        caret = SeamCaret(horizontal, thickness, RealizedRect(list, index - 1, pList), RealizedRect(list, index, pList));
        return index;
    }

    // The container at `index` as a rect in the LIST's local coords (via Mouse.GetPosition), or null if not realized.
    private static Rect? RealizedRect(ItemsControl list, int index, Vector2 pList)
    {
        if (index < 0 || list.ItemContainerGenerator.ContainerFromIndex(index) is not { } c) return null;
        var pItem = Mouse.GetPosition((IInputComponent)c);
        var size = c.RenderSize;
        return new Rect(pList.X - pItem.X, pList.Y - pItem.Y, size.Width, size.Height);
    }

    // The caret's rect for the seam between `before` and `after`. Both on the same line -> the MIDPOINT of their gap (so it
    // never sticks to one plate's edge and flips). A wrap/line boundary or a list end -> the single neighbour's leading /
    // trailing edge. The caret runs ACROSS the flow, spanning that neighbour's cross-axis extent.
    private static Rect SeamCaret(bool horizontal, double t, Rect? before, Rect? after)
    {
        if (before is { } b && after is { } a)
        {
            var sameLine = horizontal ? Math.Abs(b.Y - a.Y) < 1.0 : Math.Abs(b.X - a.X) < 1.0;
            if (sameLine)
            {
                return horizontal
                    ? new Rect((b.X + b.Width + a.X) / 2.0 - t / 2.0, b.Y, t, b.Height)
                    : new Rect(b.X, (b.Y + b.Height + a.Y) / 2.0 - t / 2.0, b.Width, t);
            }

            // Different lines (a wrap boundary): sit at the leading edge of `after` - the start of its line.
            return horizontal ? new Rect(a.X - t / 2.0, a.Y, t, a.Height) : new Rect(a.X, a.Y - t / 2.0, a.Width, t);
        }

        if (after is { } af)    // index == 0: before the first item
            return horizontal ? new Rect(af.X - t / 2.0, af.Y, t, af.Height) : new Rect(af.X, af.Y - t / 2.0, af.Width, t);
        if (before is { } bf)   // index == count: after the last item
            return horizontal ? new Rect(bf.X + bf.Width - t / 2.0, bf.Y, t, bf.Height) : new Rect(bf.X, bf.Y + bf.Height - t / 2.0, bf.Width, t);
        return default;
    }

    private static void ClearIndicator()
    {
        if (_indicator != null) _indicatorLayer?.Remove(_indicator);
        _indicator = null;
        _indicatorLayer = null;
        _indicatorList = null;
        _indicatorRect = Rect.Empty;
        _insertIndex = -1;
        _insertBefore = null;
    }

    private static IUIComponent FindAllowDropAncestor(IUIComponent hit)
    {
        for (var c = hit; c != null; c = c.VisualParent)
        {
            if (GetAllowDrop((AdamantiumComponent)c)) return c;
        }
        return null;
    }

    // Auto-scroll: when the cursor sits in the top/bottom edge band of the nearest ScrollViewer, scroll it steadily via a
    // TIMER (not per-move), so the speed is time-based (independent of how fast the mouse moves) and holding still at the
    // edge keeps scrolling. Speed = DragDrop.AutoScrollSpeed (px/sec) on the ScrollViewer, ramped by how deep into the band.
    private static void AutoScroll(IUIComponent hit)
    {
        ScrollViewer sv = null;
        for (var c = hit; c != null; c = c.VisualParent)
        {
            if (c is ScrollViewer found) { sv = found; break; }
        }

        if (sv == null) { StopAutoScroll(); return; }

        var local = Mouse.GetPosition((IInputComponent)sv);
        var height = sv.RenderSize.Height;
        double dir = local.Y < AutoScrollBand ? -1 : local.Y > height - AutoScrollBand ? 1 : 0;
        if (dir == 0) { StopAutoScroll(); return; }

        // 0 at the band's inner edge → 1 at the very edge, so it eases in rather than jumping to full speed.
        var depth = dir < 0 ? (AutoScrollBand - local.Y) / AutoScrollBand : (local.Y - (height - AutoScrollBand)) / AutoScrollBand;
        depth = depth < 0 ? 0 : depth > 1 ? 1 : depth;
        var speed = GetAutoScrollSpeed((AdamantiumComponent)sv);   // px/sec

        _autoScrollViewer = sv;
        _autoScrollPerTick = dir * speed * depth * (AutoScrollTickMs / 1000.0);
        _autoScrollTimer ??= CreateAutoScrollTimer();
        if (!_autoScrollTimer.IsEnabled) _autoScrollTimer.Start();
    }

    private static DispatcherTimer CreateAutoScrollTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(AutoScrollTickMs) };
        timer.Tick += (_, _) =>
        {
            if (!_dragging || _autoScrollViewer == null || _autoScrollPerTick == 0) { StopAutoScroll(); return; }
            _autoScrollViewer.SetScrollOffset(_autoScrollViewer.ScrollOffset + new Vector2(0, _autoScrollPerTick));
        };
        return timer;
    }

    private static void StopAutoScroll()
    {
        _autoScrollTimer?.Stop();
        _autoScrollViewer = null;
        _autoScrollPerTick = 0;
    }

    // Spring-loading: find the nearest ISpringLoadable under the cursor. While the cursor rests over the SAME one the
    // armed timer keeps counting; move to a different one (or off any) and it restarts/stops. On fire the target activates.
    private static void UpdateSpringLoad(IUIComponent hit)
    {
        ISpringLoadable target = null;
        for (var c = hit; c != null; c = c.VisualParent)
        {
            if (c is ISpringLoadable s) { target = s; break; }
        }

        if (ReferenceEquals(target, _springTarget)) return;   // same element - let the running dwell continue
        _springTarget = target;
        _springTimer ??= CreateSpringTimer();
        _springTimer.Stop();
        if (target != null) _springTimer.Start();
    }

    private static DispatcherTimer CreateSpringTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(SpringLoadDwellMs) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();   // one-shot per dwell
            if (_dragging) _springTarget?.SpringLoad();
        };
        return timer;
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
            InsertIndex = _insertIndex,     // where the insertion line was; -1 if not over an items host
            InsertBefore = _insertBefore    // the item after the caret (survives the source removal - use for a stable reorder)
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
        _springTimer?.Stop();
        _springTarget = null;
        StopAutoScroll();
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
