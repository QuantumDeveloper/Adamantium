using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Core.Commands;
using Adamantium.UI.Core.Dispatcher;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Adorners;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Imaging;
using Adamantium.UI.Core.Rendering;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Core.Templates;

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

    // Custom ghost: a DataTemplate to picture the drag with, bound to the payload (DragData). When set on a source, the
    // ghost is a snapshot of THIS built template instead of the dragged element(s) - e.g. a compact chip rather than a full
    // row. Unset = the default (a snapshot of the dragged element/selection).
    public static readonly AdamantiumProperty DragTemplateProperty = AdamantiumProperty.RegisterAttached(
        "DragTemplate", typeof(DataTemplate), typeof(AdamantiumComponent), new PropertyMetadata(null));

    public static DataTemplate GetDragTemplate(AdamantiumComponent e) => e.GetValue(DragTemplateProperty) as DataTemplate;
    public static void SetDragTemplate(AdamantiumComponent e, DataTemplate value) => e.SetValue(DragTemplateProperty, value);

    // ------------------------------------------------------------------ session state (one drag at a time, app-global)
    private static IInputComponent _source;      // the pressed source; armed until the threshold, then dragging
    private static Vector2 _startScreen;
    private static bool _dragging;
    private static IDataPackage _data;
    private static byte[] _ghostBgra;
    private static int _ghostW, _ghostH;
    private static bool _ghostShown;
    private static double _ghostBakeScale = 1.0;   // the DPI scale the ghost bitmap was baked at (the source monitor)
    private static double _presentedScale = 1.0;    // the scale currently shown - re-upload only when the monitor DPI changes

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
    private static bool _indicatorFrame;   // frame (drop-into) vs caret - part of the "nothing moved" cache key
    private static int _insertIndex = -1;
    private static object _insertBefore;   // the item at the insertion point - insert BEFORE it (robust to same-list shifts)

    // Hybrid tree-drop: the hovered node + where the drop lands relative to it (before/after sibling, or into as a child).
    private static object _dropTarget;
    private static DropPlacement _placement = DropPlacement.None;

    // Drag heartbeat: a light timer that runs for the whole gesture so Esc cancels + the cursor tracks a Ctrl press/release
    // _dragEffects is the last effect the DragOver settled on - it drives BOTH the cursor feedback and whether the drop
    // actually lands (None = the payload can't go here).
    private static DragDropEffects _dragEffects = DragDropEffects.None;
    private static int _dragCount = 1;   // how many items are being dragged - drives the multi-drag count badge on the ghost
    private static (byte[] bgra, int w, int h) _badgePart;   // the rendered count-badge snapshot (empty for a single-item drag)

    private static IVisualRenderer _renderer;
    private static IDragGhost _ghost;
    private static IVisualRenderer Renderer => _renderer ??= UIApplication.Current?.Container.Resolve<IVisualRenderer>();
    private static IDragGhost Ghost
    {
        get
        {
            if (_ghost == null)
            {
                _ghost = UIApplication.Current?.Container.Resolve<IDragGhost>();
                if (_ghost != null) _ghost.DpiChanged += OnGhostDpiChanged;
            }
            return _ghost;
        }
    }

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

        // Effects -> cursor feedback. Default to the modifier's gesture (Ctrl = Copy, else Move) over a valid target, None
        // when there's no target; the target's DragOverCommand may DOWNGRADE it (e.g. to None: this payload can't land here
        // right now). The resulting effect drives the cursor AND the eventual drop.
        var effects = DragDropEffects.None;
        if (target != null)
        {
            var args = new DragDropEventArgs(_data, _source, Mouse.GetPosition((IInputComponent)target)) { Effects = DefaultEffects() };
            if (GetDragOverCommand((AdamantiumComponent)target) is { } over && over.CanExecute(args)) over.Execute(args);
            effects = args.Effects;
        }
        _dragEffects = effects;
        Mouse.OverrideCursor = CursorFor(effects);
    }

    // Ctrl = Copy, otherwise Move (WPF/Explorer convention). The per-target DragOver can override this. Read the PHYSICAL
    // key state (GetAsyncKeyState) - GetKeyState / Keyboard.Modifiers returns a stale value while the mouse is captured.
    private const int VkControl = 0x11;
    private static DragDropEffects DefaultEffects() =>
        (Adamantium.Win32.Win32Interop.GetAsyncKeyState(VkControl) & 0x8000) != 0
            ? DragDropEffects.Copy : DragDropEffects.Move;

    private static Cursor CursorFor(DragDropEffects effects) => effects switch
    {
        DragDropEffects.None => Cursors.No,        // ⊘ can't drop here
        DragDropEffects.Copy => Cursors.DragCopy,  // arrow + green plus
        DragDropEffects.Link => Cursors.Hand,
        _ => Cursors.SizeAll,                       // Move
    };

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

        if (list is TreeView tree)   // hierarchy: sibling-vs-child by the row zone (the hybrid tree-drop)
        {
            UpdateTreeDrop(tree, hit, host);
            return;
        }

        // Flat list: a caret at the insertion seam; no hierarchical placement.
        _placement = DropPlacement.None;
        _dropTarget = null;
        var flow = ItemFlowOrientation(list);
        var index = ComputeInsertion(list, flow, out var caret);
        _insertIndex = index;
        // Anchor to insert BEFORE must SURVIVE the source removal: skip items that are being dragged (they're the ListBox
        // selection - still present during the drag), so a reorder landing INSIDE the moved pack keeps a real anchor instead
        // of losing it and appending to the end. First non-dragged item at/after the caret; null (append) if there is none.
        _insertBefore = null;
        var items = list.Items;
        var dragged = (list as ListBox)?.SelectedItems;
        if (items != null)
            for (int i = index; i >= 0 && i < items.Count; i++)
                if (dragged?.Contains(items[i]) != true) { _insertBefore = items[i]; break; }
        // The bar runs PERPENDICULAR to the item flow.
        var barOrientation = flow == Orientation.Vertical ? Orientation.Horizontal : Orientation.Vertical;
        ShowIndicator(list, host, caret, barOrientation, frame: false);
    }

    // Hybrid tree drop: the row zone under the cursor decides sibling-vs-child. Top third -> caret ABOVE (Before), bottom
    // third -> caret BELOW (After), middle -> a FRAME around the row (Into, as a child). DropTarget = the hovered node.
    private static void UpdateTreeDrop(TreeView tree, IUIComponent hit, IAdornerHost host)
    {
        TreeViewItem container = null;
        for (var c = hit; c != null && !ReferenceEquals(c, tree); c = c.VisualParent)
        {
            if (c is TreeViewItem tvi) { container = tvi; break; }
        }

        _insertIndex = -1;
        _insertBefore = null;

        if (container == null)   // empty area -> drop into the root, no cue
        {
            _placement = DropPlacement.Into;
            _dropTarget = null;
            ClearIndicatorVisual();
            return;
        }

        const double thickness = 8.0;
        var pItem = Mouse.GetPosition((IInputComponent)container);
        var top = new Vector2(Mouse.GetPosition((IInputComponent)tree).X - pItem.X,
                              Mouse.GetPosition((IInputComponent)tree).Y - pItem.Y);   // container top-left in tree coords
        var size = container.RenderSize;
        _dropTarget = container.DataContext;   // the node (BindRow sets DataContext = node)

        Rect rect;
        bool frame;
        if (pItem.Y < size.Height / 3.0)
        {
            _placement = DropPlacement.Before;
            rect = new Rect(top.X, top.Y - thickness / 2.0, size.Width, thickness);
            frame = false;
        }
        else if (pItem.Y > size.Height * 2.0 / 3.0)
        {
            _placement = DropPlacement.After;
            rect = new Rect(top.X, top.Y + size.Height - thickness / 2.0, size.Width, thickness);
            frame = false;
        }
        else
        {
            _placement = DropPlacement.Into;
            rect = new Rect(top.X, top.Y, size.Width, size.Height);
            frame = true;
        }

        ShowIndicator(tree, host, rect, Orientation.Horizontal, frame);
    }

    // Create (or reuse) the themed cue and place it. Recreated only when the host, rect, or frame/caret mode changes.
    private static void ShowIndicator(ItemsControl list, IAdornerHost host, Rect rect, Orientation orientation, bool frame)
    {
        if (ReferenceEquals(list, _indicatorList) && ReferenceEquals(host.AdornerLayer, _indicatorLayer)
            && rect == _indicatorRect && frame == _indicatorFrame && _indicator != null)
        {
            return;   // nothing moved
        }

        ClearIndicatorVisual();
        _indicatorList = list;
        _indicatorLayer = host.AdornerLayer;
        _indicatorRect = rect;
        _indicatorFrame = frame;

        // A real, themed control: its look is a ControlTemplate from the active theme (restyle the drop cue in the theme,
        // not here). Orientation drives the caret axis; IsFrame swaps the caret for a "drop into" outline.
        var indicator = new DropInsertionIndicator { AdornedElement = list, Orientation = orientation, IsFrame = frame };
        if (UIApplication.Current?.ThemeManager is { CurrentTheme: { } theme } manager) manager.ApplyTheme(theme, indicator);
        ((IMeasurableComponent)indicator).Measure(rect.Size, true);
        ((IMeasurableComponent)indicator).Arrange(rect, true);
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
        var nearestRect = new Rect(pList.X - nearestPItem.X, pList.Y - nearestPItem.Y, nearestSize.Width, nearestSize.Height);
        caret = SeamCaret(horizontal, thickness, RealizedRect(list, index - 1), RealizedRect(list, index), nearestRect, after);
        return index;
    }

    // The container at `index` as a rect in the LIST's local coords, via TransformBoundsToVisual (element-to-element, no
    // cursor bridge needed). Null if not realized.
    private static Rect? RealizedRect(ItemsControl list, int index)
    {
        if (index < 0 || list.ItemContainerGenerator.ContainerFromIndex(index) is not { } c) return null;
        return c.TransformBoundsToVisual(list);
    }

    // The caret's rect for the seam at the insertion index. When the two neighbours share a LINE -> the MIDPOINT of their
    // gap (never sticks to one plate's edge and flips). At a WRAP BOUNDARY or a list END the two representations (trailing
    // edge of the previous column vs leading edge of the next) are far apart, so anchor to `nearest` - the item UNDER THE
    // CURSOR - on the side the cursor is on (afterSide). That keeps the caret where the pointer is instead of jumping to the
    // far column's/row's edge. Runs ACROSS the flow, spanning the anchor's cross-axis extent.
    private static Rect SeamCaret(bool horizontal, double t, Rect? before, Rect? after, Rect nearest, bool afterSide)
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
        }

        // Boundary / end: the seam is at the nearest item's TRAILING edge (cursor in its second half) or LEADING edge (first).
        if (horizontal)
        {
            var x = afterSide ? nearest.X + nearest.Width : nearest.X;
            return new Rect(x - t / 2.0, nearest.Y, t, nearest.Height);
        }

        var y = afterSide ? nearest.Y + nearest.Height : nearest.Y;
        return new Rect(nearest.X, y - t / 2.0, nearest.Width, t);
    }

    // Remove the cue adorner + reset its visual state, but KEEP the resolved drop target (the tree path clears the visual
    // over empty space yet still wants Placement/DropTarget for the drop).
    private static void ClearIndicatorVisual()
    {
        if (_indicator != null)
        {
            _indicatorLayer?.Remove(_indicator);
            _indicator.AdornedElement = null;   // drop the inheritance link so the host doesn't retain the cue
        }
        _indicator = null;
        _indicatorLayer = null;
        _indicatorList = null;
        _indicatorRect = Rect.Empty;
        _indicatorFrame = false;
    }

    private static void ClearIndicator()
    {
        ClearIndicatorVisual();
        _insertIndex = -1;
        _insertBefore = null;
        _dropTarget = null;
        _placement = DropPlacement.None;
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
        var startArgs = new DragDropEventArgs(_data, _source, _startScreen)
        {
            // The origin collection (the source list's ItemsSource), so the VM can identify WHERE the drag came from by
            // reference - unambiguous after a Copy duplicates a value across lists.
            SourceItemsSource = (_source as IUIComponent)?.GetVisualAncestors().OfType<ItemsControl>().FirstOrDefault()?.ItemsSource
        };
        var started = GetDragStartedCommand((AdamantiumComponent)_source);
        if (started != null && started.CanExecute(startArgs)) started.Execute(startArgs);

        // The ghost is a snapshot of every dragged element (the whole multi-selection), composited into a stack. Each bake
        // runs OFF the render thread (queued); the ghost appears once they've all arrived (a frame or two later).
        _dragCount = GetGhostElements(_source).Count;   // selection size -> the multi-drag count badge
        _ghostBakeScale = SourceRenderScale((IUIComponent)_source);   // the snapshot bakes at the source window's density

        // Ghost body: ONE representative snapshot - the source's custom DragTemplate if it declares one, else the dragged
        // element itself (an exact copy of the row). A multi-selection shows this single ghost plus a count badge, not a
        // stack of every selected row.
        IReadOnlyList<IUIComponent> body = BuildDragTemplate(_source) is { } custom ? [custom] : [(IUIComponent)_source];
        _badgePart = default;
        var badge = _dragCount > 1 ? BuildCountBadge(_dragCount) : null;

        _ghostParts = new (byte[], int, int)[body.Count];
        _ghostPending = body.Count + (badge != null ? 1 : 0);
        for (int i = 0; i < body.Count; i++)
        {
            int index = i;
            Renderer?.RequestSnapshot(body[i], img => OnGhostPartReady(index, img));
        }
        if (badge != null) Renderer?.RequestSnapshot(badge, OnBadgeReady);
    }

    // The device scale the snapshot bakes at = the source window's RenderScale (BuildLiveCache records at that density), so the
    // ghost bitmap is ALREADY physical-pixel sized. A monitor crossing re-scales RELATIVE to this, so it must be known - else
    // the re-scale double-applies the source density (ghost ends up bake x monitor too big on an already-high-DPI window).
    private static double SourceRenderScale(IUIComponent source)
    {
        for (IUIComponent n = source; n != null; n = n.VisualParent)
            if (n is IWindow { Renderer: { } r }) return r.RenderScale;
        return 1.0;
    }

    // Esc cancels ANY in-progress drag. A class handler on WindowBase's tunnelling PreviewKeyDown catches it before a
    // control consumes the key, in whichever window has focus - an event, not a poll. Registered once.
    static DragDrop()
    {
        Keyboard.PreviewKeyDownEvent.RegisterClassHandler<WindowBase>(new KeyEventHandler(OnGlobalKeyDown));
    }

    private static void OnGlobalKeyDown(object sender, KeyEventArgs e)
    {
        if (_dragging && e.Key == Key.Escape)
        {
            e.Handled = true;
            CancelDrag();
        }
    }

    // Build the source's DragTemplate (if any) into a detached, themed, arranged visual bound to the payload; the async
    // snapshot path then bakes it into the ghost. Null when no template is set - the caller snapshots the element instead.
    private static IUIComponent BuildDragTemplate(IInputComponent source)
    {
        if (source is not AdamantiumComponent c || GetDragTemplate(c) is not { } template) return null;
        try
        {
            if (template.Build(source as IUIComponent)?.RootComponent is not IUIComponent root) return null;
            if (root is FundamentalUIComponent fu)
            {
                fu.DataContext = _data?.Get<object>();   // the payload - the template's {Binding}s resolve against it
                fu.ApplyCurrentTheme();
            }
            // Size the custom ghost to the SOURCE ROW's width, not to its own text: measure with that width available, then
            // arrange AT it - a stretching template fills the row (matching what it replaces) instead of shrinking to ~50px.
            var rowWidth = (source as IUIComponent)?.RenderSize.Width ?? 0;
            var measurable = (IMeasurableComponent)root;
            measurable.Measure(new Size(rowWidth > 0 ? rowWidth : 4096, double.PositiveInfinity));
            var arrangeWidth = rowWidth > 0 ? rowWidth : measurable.DesiredSize.Width;
            measurable.Arrange(new Rect(new Size(arrangeWidth, measurable.DesiredSize.Height)));
            return root;
        }
        catch
        {
            return null;   // a broken/failed template must not break the drag - fall back to snapshotting the element
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

        ComposeGhost();
    }

    // The multi-drag count badge arrived (a snapshot of a real, themed Border+TextBlock control).
    private static void OnBadgeReady(ImageSource img)
    {
        if (!_dragging) return;
        if (img is BitmapSource bs)
            _badgePart = (DragGhostPixels.ToPremultipliedBgra(bs), (int)bs.PixelWidth, (int)bs.PixelHeight);
        if (--_ghostPending > 0) return;
        ComposeGhost();
    }

    // All parts in: stack the body, then drop the count badge onto its top-right corner (canvas expands so the badge
    // sits at the corner, not over the content).
    private static void ComposeGhost()
    {
        var parts = _ghostParts.Where(p => p.bgra != null).ToList();
        if (parts.Count == 0) return;
        var body = parts.Count == 1 ? parts[0] : DragGhostPixels.StackVertical(parts, 4);
        var (fb, fw, fh) = _badgePart.bgra != null ? DragGhostPixels.WithCornerBadge(body, _badgePart) : body;
        _ghostBgra = fb;
        _ghostW = fw;
        _ghostH = fh;
        ShowOrMoveGhost();
    }

    // A real badge control (themed accent pill + the count) - snapshotted like any other ghost part, so the text is crisp.
    private static IUIComponent BuildCountBadge(int count)
    {
        try
        {
            var theme = UIApplication.Current?.ThemeManager?.CurrentTheme;
            var text = new TextBlock
            {
                Text = count > 99 ? "99+" : count.ToString(),
                Foreground = theme?.AccentForegroundColor ?? new SolidColorBrush(Colors.White),
                FontSize = 11,
            };
            var badge = new Border
            {
                Background = theme?.AccentFillColorDefault ?? new SolidColorBrush(Color.FromRgba(0x00, 0x91, 0xF7)),
                BorderBrush = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(11),
                Padding = new Thickness(7, 2, 7, 2),
                Child = text,
            };
            var m = (IMeasurableComponent)badge;
            m.Measure(new Size(4096, 4096));
            m.Arrange(new Rect(m.DesiredSize));
            return badge;
        }
        catch { return null; }
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
    private static void OnLostCapture(object sender, MouseEventArgs e) => CancelDrag();

    // Abort the drag with NO drop - Esc, or capture yanked away (a screenshot overlay, Alt-Tab, another control grabbing
    // it). The source's DragCompleted still runs with None so it can undo any provisional state; then everything resets.
    private static void CancelDrag()
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
        if (_ghostShown)
        {
            Ghost?.Move(x, y);                                  // same monitor -> cheap reposition; a DPI crossing re-scales via OnGhostDpiChanged
            return;
        }
        // First show: the bitmap is already baked at the source-monitor scale, so present it as-is. The window is now created
        // on the cursor's monitor; if that isn't the bake monitor (a fast cross before the snapshot baked), correct it once.
        Ghost?.Show(_ghostBgra, _ghostW, _ghostH, x, y);
        _ghostShown = true;
        _presentedScale = _ghostBakeScale;
        OnGhostDpiChanged(Ghost?.Dpi ?? 96);
    }

    // Re-scale the ghost the instant it crosses onto a monitor of a different DPI (driven by IDragGhost.DpiChanged, event-based
    // - no per-frame polling). Re-uploads the resampled bitmap at the current cursor so the ghost keeps its physical size.
    private static void OnGhostDpiChanged(uint dpi)
    {
        if (!_ghostShown || _ghostBgra == null) return;
        var scale = dpi / 96.0;
        if (Math.Abs(scale - _presentedScale) < 0.001) return;
        var s = Mouse.ScreenCoordinates;
        int x = (int)s.X + GhostCursorOffset, y = (int)s.Y + GhostCursorOffset;
        var (bgra, w, h) = ScaleGhost(scale);
        Ghost?.Show(bgra, w, h, x, y);
        _presentedScale = scale;
    }

    // The baked ghost bitmap re-scaled from its bake DPI to a target monitor scale (unchanged when they match).
    private static (byte[] bgra, int w, int h) ScaleGhost(double scale)
    {
        if (_ghostBakeScale <= 0 || Math.Abs(scale - _ghostBakeScale) < 0.001) return (_ghostBgra, _ghostW, _ghostH);
        int w = Math.Max(1, (int)Math.Round(_ghostW * scale / _ghostBakeScale));
        int h = Math.Max(1, (int)Math.Round(_ghostH * scale / _ghostBakeScale));
        return (DragGhostPixels.Resample(_ghostBgra, _ghostW, _ghostH, w, h), w, h);
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

        // Decide the effect FRESH at release: over a target, read Ctrl now (Copy) else Move; None with no target (incl. a
        // drop-disabled panel, which isn't an AllowDrop target). Honour a per-target DragOver that downgraded to None. Using
        // the live Ctrl here - not the last DragOver's cached value - is what makes Move actually remove from the source.
        var target = HitTestDropTarget(out var positionInTarget);
        var effects = target != null && _dragEffects != DragDropEffects.None ? DefaultEffects() : DragDropEffects.None;
        var args = new DragDropEventArgs(_data, _source, positionInTarget)
        {
            Effects = effects,
            InsertIndex = _insertIndex,     // where the insertion line was; -1 if not over an items host
            InsertBefore = _insertBefore,   // the item after the caret (survives the source removal - use for a stable reorder)
            DropTarget = _dropTarget,       // hybrid tree-drop: the hovered node
            Placement = _placement          // before/after sibling, or into as a child (None for a flat list)
        };

        // Order matters: the SOURCE removes (on Move) FIRST, then the TARGET adds. Dropping back into the SAME collection
        // then nets to a re-add (a no-op / reorder), not a delete - which the reverse order would cause (the target's add is
        // a no-op because the item is still there, then the source's remove would wipe it). This split is also what makes
        // cross-window / cross-VM Move work: the target can't touch a collection it doesn't own, so the source removes.
        var completed = GetDragCompletedCommand((AdamantiumComponent)_source);
        if (completed != null && completed.CanExecute(args)) completed.Execute(args);   // source REMOVES on Move

        if (target != null && effects != DragDropEffects.None)
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
        Mouse.OverrideCursor = null;   // restore normal per-element cursors
        _dragEffects = DragDropEffects.None;
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
        _presentedScale = _ghostBakeScale;   // next drag re-bakes _ghostBakeScale; keep them in step so first show doesn't resample
        _ghostParts = null;
        _ghostPending = 0;
        _badgePart = default;
    }

    private static IDataPackage ResolveData(AdamantiumComponent source)
    {
        var d = GetDragData(source);
        return d as IDataPackage ?? new DataPackage(d);
    }
}
