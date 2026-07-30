using System.Collections.Generic;
using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.Docking;

/// <summary>
/// The area panes are docked in: it owns the layout and lays its groups out by it.
/// <para>The author writes WHERE, never how much - groups declare a <see cref="PaneGroup.Zone"/> and the split tree is
/// derived from them (<see cref="DockingLayout.FromZones"/>). A share written by hand would be a share of a split the
/// author cannot see, and would stop being true the first time a divider is dragged.</para>
/// <para>The layout is DATA and lives in <see cref="Layout"/>: it can be built, changed and asserted on without a
/// window, which is why the model is a separate thing from these controls. The controls are a VIEW of it - change the
/// model and call <see cref="Rebuild"/>, and the visual tree follows. No gesture edits controls directly, or the model
/// and the screen would be two answers to the same question.</para>
/// </summary>
public class DockingArea : Panel
{
    /// <summary>The layout this area shows. Rebuilt from the authored zones when they change; from then on it is what
    /// gestures edit and what a save writes.</summary>
    public DockingLayout Layout { get; private set; } = new();

    /// <summary>Every pane this area knows, by id. The model refers to panes by id and nothing else, so this is the one
    /// place an id turns back into a control.
    /// <para>A pane torn into a window is still the SAME control and stays registered here, which is what lets it be
    /// found again when the window is docked back. SHARED with every floating area of the same layout, for exactly that
    /// reason: a pane that moved between two windows must not be two entries.</para></summary>
    private readonly Dictionary<string, Pane> _panesById;

    /// <summary>The root this area shows: the layout's main one, or - for a floating window's area - the root that was
    /// torn off into it.
    /// <para>This is what makes a floating panel dockable INTO. The model was always a forest of roots and never knew
    /// about windows; giving each root an area of its own means a floating window has a tab strip, a compass and the same
    /// gestures as the main one, instead of being a box with a panel in it.</para></summary>
    private readonly DockingRoot _root;

    /// <summary>The main area, when this is a floating one. The family shares a layout, a pane registry and a drag.</summary>
    private readonly DockingArea _owner;

    /// <summary>The floating areas opened from this one. Only the main area has any.</summary>
    private readonly List<DockingArea> _satellites = [];

    /// <summary>The window a floating area lives in, so it can be closed when its root is docked away.</summary>
    private WindowBase _window;

    public DockingArea() => _panesById = new Dictionary<string, Pane>();

    private DockingArea(DockingArea owner, DockingRoot root)
    {
        _owner = owner;
        _root = root;
        Layout = owner.Layout;
        _panesById = owner._panesById;
        // SetCurrentValue, not the setter: a plain write is LOCAL and would outrank a theme or a binding the owner's own
        // value came from, so a floating area could never be restyled with the rest of them.
        SetCurrentValue(EdgeDockSizeProperty, owner.EdgeDockSize);
        DividerThickness = owner.DividerThickness;

        // Its content came from a tear-off, not from authored children - there are no zones here to build a layout from.
        _layoutBuilt = true;
    }

    private DockingArea Owner => _owner ?? this;

    /// <summary>Every area of this layout, FLOATING ONES FIRST. A floating window sits over the main one, so where both
    /// could claim the pointer the floating one is the answer.</summary>
    private IEnumerable<DockingArea> Family
    {
        get
        {
            var owner = Owner;
            for (var i = owner._satellites.Count - 1; i >= 0; i--) yield return owner._satellites[i];
            yield return owner;
        }
    }

    private PaneNode RootContent => (_root ?? Layout.Main)?.Content;

    /// <summary>The control showing each group node. Kept ACROSS rebuilds so a group that merely moved keeps its own
    /// control - and with it its selection and its scroll position, which a freshly built one would lose.</summary>
    private readonly Dictionary<PaneGroupNode, PaneGroup> _groupsByNode = new();

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureLayout();

        foreach (var child in Children) child.Measure(availableSize);
        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        EnsureLayout();

        var area = new Rect(0, 0, finalSize.Width, finalSize.Height);
        foreach (var child in Children) child.Arrange(area);

        return finalSize;
    }

    /// <summary>Copies the lengths the controls now carry back into the model - called when a DIVIDER DRAG ends, and at
    /// no other time.
    /// <para>A drag writes onto the controls, because that is what moves the boundary under the pointer, and the model
    /// has to learn of it or the next rebuild would hand back the sizes from before the drag. But ONLY then: the model
    /// is the truth and the controls are a view of it, so copying every layout pass let the view answer back - and a
    /// freshly built control still carrying the default Star wrote that default over a real size. Measured: after a
    /// tear-off the console went from a 160-pixel band to a star and took two thirds of the window.</para></summary>
    private void SyncLengthsToModel()
    {
        // A folded group's length is Auto and belongs to the fold, not to anything the user dragged - copying it back
        // would overwrite the size it is supposed to return to.
        foreach (var pair in _groupsByNode)
        {
            if (pair.Key.OwnsLength) pair.Key.Length = PaneHost.GetPaneLength(pair.Value);
        }
        foreach (var pair in _hostsByNode) pair.Key.Length = PaneHost.GetPaneLength(pair.Value);
    }

    /// <summary>The pin button: puts a docked group away to its strip, or pins a folded one (from either folded state)
    /// back into the layout. Like every other gesture this edits the MODEL and rebuilds - which state a panel is in is
    /// part of a layout, and the layout is what gets saved.</summary>
    internal void TogglePinned(PaneGroup group)
    {
        var node = NodeOf(group);
        if (node == null) return;

        var changed = node.State == PaneGroupState.Docked
            ? Layout.CollapseGroup(node)
            : Layout.ExpandGroup(node);

        if (changed) Rebuild();
    }

    /// <summary>Shows a put-away group's body without pinning it back - the strip stays and the body is drawn over the
    /// neighbours. Clicking a tab in a folded strip lands here.</summary>
    internal void Reveal(PaneGroup group)
    {
        var node = NodeOf(group);
        if (node != null && Layout.RevealGroup(node)) Rebuild();
    }

    /// <summary>Puts a revealed body away again, leaving the strip.</summary>
    internal void Hide(PaneGroup group)
    {
        var node = NodeOf(group);
        if (node != null && Layout.HideGroup(node)) Rebuild();
    }

    // Click-outside-to-put-away, the same shape a Popup uses (see Popup.HookLightDismiss): a revealed panel is a GLANCE at
    // a tool, so the moment attention goes elsewhere it goes away again - and only PINNING keeps it. The handler lives on
    // the window, because the press that dismisses it is by definition not inside this panel; it is unhooked as soon as
    // nothing is revealed, so a docked layout pays nothing for it.
    private MouseButtonEventHandler _lightDismiss;
    private IInputComponent _dismissHost;

    private void SyncLightDismiss()
    {
        var wanted = false;
        foreach (var node in _groupsByNode.Keys)
        {
            if (node.State == PaneGroupState.Revealed) wanted = true;
        }

        if (wanted) HookLightDismiss();
        else UnhookLightDismiss();
    }

    private void HookLightDismiss()
    {
        if (_dismissHost != null) return;
        if (WindowRoot() is not { } root) return;

        _lightDismiss ??= OnGlobalPreviewDown;
        _dismissHost = root;
        root.AddHandler(Mouse.PreviewMouseDownEvent, _lightDismiss, handledEventsToo: true);
    }

    private void UnhookLightDismiss()
    {
        if (_dismissHost == null) return;

        _dismissHost.RemoveHandler(Mouse.PreviewMouseDownEvent, _lightDismiss);
        _dismissHost = null;
    }

    private void OnGlobalPreviewDown(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as IUIComponent;

        // Collected first: putting one away rebuilds the tree, and the dictionary must not be walked while that happens.
        List<PaneGroup> leaving = null;
        foreach (var pair in _groupsByNode)
        {
            if (pair.Key.State != PaneGroupState.Revealed) continue;
            if (source != null && IsWithin(source, pair.Value)) continue;   // pressed inside the panel itself - keep it

            leaving ??= [];
            leaving.Add(pair.Value);
        }

        if (leaving == null) return;
        foreach (var group in leaving) Hide(group);
    }

    private static bool IsWithin(IUIComponent node, IUIComponent ancestor)
    {
        for (var n = node; n != null; n = n.VisualParent)
        {
            if (ReferenceEquals(n, ancestor)) return true;
        }
        return false;
    }

    /// <summary>The window this area sits in - the topmost visual ancestor, which is where a press anywhere can be seen
    /// from.</summary>
    private IInputComponent WindowRoot()
    {
        IUIComponent node = this;
        while (node.VisualParent != null) node = node.VisualParent;
        return node as IInputComponent;
    }

    // --- Closing: what it MEANS differs by pane -----------------------------------------------------------------------
    // A document closed is gone - it was part of the session, and the file may not even exist next time. A tool closed is
    // PUT AWAY: it is part of the workspace, and every editor brings it back from a menu. One button, two verbs, and the
    // pane's own Kind is what says which - that is policy, and policy belongs to the pane (rule 1.5).

    /// <summary>Where a put-away tool was standing, so it can be brought back to the same place.</summary>
    private readonly struct HiddenSpot
    {
        public HiddenSpot(PaneGroupNode group, int index, DockZone zone)
        {
            Group = group;
            Index = index;
            Zone = zone;
        }

        public PaneGroupNode Group { get; }

        public int Index { get; }

        /// <summary>Where the author said this pane belongs - the fallback when the group it was in has since died.</summary>
        public DockZone Zone { get; }
    }

    private readonly Dictionary<string, HiddenSpot> _hidden = new();

    /// <summary>Tools that have been put away and can be brought back. The application shows them in a "Windows" menu -
    /// without such a list a closed tool is unreachable, which is why closing one may not simply delete it.</summary>
    public IReadOnlyCollection<string> HiddenPanes => Owner._hidden.Keys;

    internal void ClosePane(Pane pane)
    {
        if (pane?.Id is not { } id) return;

        var group = Layout.FindGroup(id);
        if (group == null) return;

        if (pane.Kind == PaneKind.Tool)
        {
            Owner._hidden[id] = new HiddenSpot(group, group.PaneIds.IndexOf(id), pane.Zone);
        }
        else
        {
            // A document is gone for good, so nothing should still be able to find its control by id.
            _panesById.Remove(id);
        }

        Layout.RemovePane(id);
        RebuildFamily();
    }

    /// <summary>Brings a put-away tool back - to the group it was in, or, if that group has since died, to the zone its
    /// author gave it. Returns false for an id that is not put away.</summary>
    public bool RestorePane(string paneId)
    {
        if (paneId == null || !Owner._hidden.Remove(paneId, out var spot)) return false;

        var home = spot.Group;
        if (home is { Parent: not null } || ReferenceEquals(home, Layout.DocumentWell))
        {
            home.Insert(System.Math.Min(spot.Index, home.PaneIds.Count), paneId);
        }
        else if (spot.Zone is DockZone.Center && Layout.DocumentWell != null)
        {
            Layout.DocumentWell.Add(paneId);
        }
        else
        {
            var group = new PaneGroupNode();
            group.Add(paneId);
            Layout.Split(RootContent, spot.Zone is DockZone.None or DockZone.Center ? DockZone.Right : spot.Zone, group);
        }

        Layout.Normalize();
        RebuildFamily();
        return true;
    }

    /// <summary>The model node a group control stands for.</summary>
    private PaneGroupNode NodeOf(PaneGroup group)
    {
        foreach (var pair in _groupsByNode)
        {
            if (ReferenceEquals(pair.Value, group)) return pair.Key;
        }

        return null;
    }

    /// <summary>Regenerates the visual tree from the model. Every change to the layout ends here.</summary>
    public void Rebuild()
    {
        // Take the LEAVERS out of every group first: a pane is one control and can only be in one group, so a group
        // must not still be holding a pane another is about to receive.
        // Removed one by one, never with Clear(). Clear() raises a Reset, and on a Reset the items control rebuilds its
        // items panel wholesale - the tabs then go into a brand-new panel while the visual tree carries on arranging
        // the old one. Measured by object identity: the arranged panel (#66552671, two tabs) was never the panel the
        // group reported holding three.
        foreach (var pair in _groupsByNode)
        {
            var items = pair.Value.Items;
            for (var i = items.Count - 1; i >= 0; i--)
            {
                if (items[i] is Pane pane && !pair.Key.PaneIds.Contains(pane.Id)) items.RemoveAt(i);
            }
        }

        var visual = BuildVisual(RootContent);

        // Only swap the top of the tree when it actually changed; an unchanged root must not be torn down and rebuilt.
        var current = Children.Count == 1 ? Children[0] : null;
        if (!ReferenceEquals(current, visual))
        {
            Children.Clear();
            if (visual != null) Children.Add(visual);
        }

        Prune();

        // Invalidate the groups AFTER the tree is back together. The panel does mark itself when a child is added, but
        // FillPanes runs while this subtree is detached, and a dirty mark raised then is lost - the dirty queue belongs
        // to the visual root, and re-attaching does not re-register anything.
        // Measured: three tabs, all in the panel's children, all visible, desired widths 52/64/52 - laid out at 0/0/52,
        // which is the arrangement of the TWO tabs the panel had when it was last measured.
        // The STRIP PANEL as well as the group. Invalidating only the group is not enough: the group re-arranges, but
        // its size has not changed, so the pass never descends into a panel that is not itself marked - and that panel
        // is exactly the thing holding the new tab. Measured: the group's own ArrangeOverride ran while the third tab
        // kept the bounds of its previous life.
        // And the TABS themselves, for the same reason one level down: a fold changes each pane's label rotation, which
        // changes its header template and so its own size - and that mark is raised while the pane is detached, so it is
        // lost exactly like the panel's was. Measured: in a folded column of three, only the last tab carried the turned
        // footprint (41x70); the first two kept the 78x29 they had lying flat and the three drew on top of each other.
        foreach (var group in _groupsByNode.Values)
        {
            group.InvalidateMeasure();
            (group.ItemsHostPanel as IMeasurableComponent)?.InvalidateMeasure();

            foreach (var item in group.Items)
            {
                if (item is IMeasurableComponent pane) pane.InvalidateMeasure();
            }
        }

        InvalidateMeasure();

        // Every model change ends here, so this is the one place where "is anything revealed?" is always true or false
        // for the whole area - hooking it at each gesture instead left a reveal made by any other route undismissable.
        SyncLightDismiss();
        SyncWindowTitle();
    }

    /// <summary>The group whose name the floating window is currently wearing, so it can be let go of when another takes
    /// over.</summary>
    private PaneGroup _titleSource;

    /// <summary>A floating window showing ONE panel is named by that panel - the panel has given up its own caption for
    /// exactly this, so the title bar is now the only thing saying which it is. It follows the ACTIVE tab: with several
    /// panes in the group, a title fixed at the tear-off would name whichever happened to be showing then.</summary>
    private void SyncWindowTitle()
    {
        if (_window == null) return;

        var group = RootContent is PaneGroupNode node && _groupsByNode.TryGetValue(node, out var control) ? control : null;

        if (!ReferenceEquals(group, _titleSource))
        {
            if (_titleSource != null) _titleSource.PropertyChanged -= OnTitleSourceChanged;
            _titleSource = group;
            if (_titleSource != null) _titleSource.PropertyChanged += OnTitleSourceChanged;
        }

        // Split into several panels, the window keeps the name it was torn off with: each panel wears its own caption
        // again, and picking one of them to name the window would be an arbitrary choice.
        if (group?.Title is { } title) _window.Title = title.ToString();
    }

    private void OnTitleSourceChanged(object sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (e.Property == PaneGroup.TitleProperty) SyncWindowTitle();
    }

    private readonly DockCompass _compass = new();
    private DockCompassWindow _compassWindow;

    /// <summary>Whether the overlay is up. It covers the WHOLE area and is shown for as long as the pointer is inside
    /// it, so this is the only thing that ever changes about it during a drag.</summary>
    private bool _overlayShown;

    /// <summary>The compass shown during a docking drag. Exposed so a theme can style it - it is a control, not an
    /// internal drawing.</summary>
    public DockCompass Compass => _compass;

    private DockTarget _target;

    /// <summary>Builds the two overlay windows, once, WITHOUT showing them. Constructed here, on the UI thread, while it
    /// is still ours - the drag that uses them runs inside the platform's move loop, which owns that thread.
    /// <para>Deliberately not shown-then-hidden to "warm them up": that leaves two full-size windows on screen for as
    /// long as it takes the hide to land, which is exactly long enough to see.</para></summary>
    /// <summary>Puts the overlay window over the WHOLE area and shows it. Everything the gesture draws lives inside as
    /// ordinary controls, in the area's own coordinates.
    /// <para>One window for the area, not one per group: it is put up when the pointer enters and taken down when it
    /// leaves, and in between it neither moves nor resizes. A window sized to the aimed-at GROUP had to be moved and
    /// resized mid-gesture, which raced the compass laid out inside it - and it also made the area's own edges
    /// unreachable, so there was nowhere to put an edge anchor.</para></summary>
    private void ShowOverlay()
    {
        // Position PHYSICAL (a desktop point, like PointToScreen answers), size LOGICAL - see WindowBase.Left for why
        // those two differ.
        var origin = this.PointToScreen(Vector2.Zero);
        var bounds = new Rect(0, 0, RenderSize.Width, RenderSize.Height);

        // Built at its FULL size, before Show. A window sized after creation was never laid out at all - measured:
        // DockCompass.ArrangeOverride ran zero times across a whole drag while the window was placed nine times.
        _compassWindow ??= new DockCompassWindow
        {
            Content = _compass,
            Left = origin.X + bounds.X,
            Top = origin.Y + bounds.Y,
            ClientWidth = bounds.Width,
            ClientHeight = bounds.Height
        };

        _compassWindow.Left = origin.X + bounds.X;
        _compassWindow.Top = origin.Y + bounds.Y;
        _compassWindow.ClientWidth = bounds.Width;
        _compassWindow.ClientHeight = bounds.Height;
        _compassWindow.Show();

        if (LogDocking)
        {
            System.Console.WriteLine($"[DockOverlay] at=({_compassWindow.Left:F0},{_compassWindow.Top:F0}) " +
                                     $"size=({bounds.Width:F0}x{bounds.Height:F0}) " +
                                     $"winTemplate={_compassWindow.Template != null} " +
                                     $"compassSize=({_compass.RenderSize.Width:F0}x{_compass.RenderSize.Height:F0}) " +
                                     $"indicatorBrush={_compass.IndicatorBrush != null} " +
                                     $"previewBrush={_compass.PreviewBrush != null}");
        }
    }

    private void HideOverlay() => _compassWindow?.Hide();

    /// <summary>What the thing currently being dragged is allowed to do. Read once when its window is put up, because it
    /// cannot change during the drag - and asking it per mouse move would walk the tree hundreds of times a second.</summary>
    private DockZone _dragAllowed = DockZone.All;

    /// <summary>Where a node may be docked: what EVERY pane in it allows. One pane forbidding a side forbids it for the
    /// panel - dropping the group would put that pane there too, and a permission that is ignored when the pane travels
    /// with company is not a permission.</summary>
    private DockZone AllowedFor(PaneNode node)
    {
        var allowed = DockZone.All;
        foreach (var id in DockingLayout.PanesIn(node))
        {
            if (_panesById.TryGetValue(id, out var pane)) allowed &= pane.Allowed;
        }

        return allowed;
    }

    /// <summary>Where the group under a point sits, in THIS area's coordinates, and what a drop there would do. The
    /// point is in the area's own space; <paramref name="allowed"/> is what the dragged panes permit.</summary>
    private DockTarget Resolve(Vector2 point, DockZone allowed)
    {
        var origin = this.PointToScreen(Vector2.Zero);
        var scale = DpiScale;
        var area = new Rect(0, 0, RenderSize.Width, RenderSize.Height);

        PaneGroupNode node = null;
        var bounds = area;

        foreach (var pair in _groupsByNode)
        {
            // A PUT-AWAY panel is not a place to drop into: all that is left of it is a strip of buttons, and there is no
            // body there to be tabbed into or split. Worse, those strips sit along the edges - exactly where the real
            // panel behind them is being aimed at - so a cross drawn on the strip lands on top of the cross of the panel
            // it covers, and neither of them says any more where the drop would go.
            if (pair.Key.State == PaneGroupState.Collapsed) continue;

            var group = pair.Value;
            // Physical difference back to LOGICAL, to be in the same units as the point and the group's own size.
            var at = (group.PointToScreen(Vector2.Zero) - origin) / scale;
            var size = group.RenderSize;
            if (point.X < at.X || point.Y < at.Y || point.X > at.X + size.Width || point.Y > at.Y + size.Height) continue;

            node = pair.Key;
            bounds = new Rect(at.X, at.Y, size.Width, size.Height);
            break;
        }

        // The EDGE anchors first. They belong to the AREA and sit at its sides, the cross belongs to whichever group is
        // under the pointer - and where the two could overlap, the edge is the more specific answer. An edge drop is
        // aimed at the ROOT, which is what makes it span the whole side; it is the same move, not another kind.
        // Asked of the area rather than from inside the group loop: the edges are exactly where the put-away strips are,
        // so tying the anchors to "found a group under the pointer" made the area's own edge unreachable behind one.
        var edge = DockCompass.EdgeZoneAt(area, point, _compass.IndicatorSize, _compass.EdgeIndicatorInset);
        if (edge != DockZone.None && (allowed & edge) != 0)
            return new DockTarget(RootContent, bounds, edge,
                DockCompass.PreviewOf(area, edge, EdgeDockSize), isEdge: true);

        if (node == null) return default;

        var zone = DockCompass.ZoneAt(bounds, point, _compass.IndicatorSize, _compass.IndicatorGap);

        // A zone the panes forbid arms nothing: the indicator does not light up and the drop does nothing, because
        // DockTarget is only valid with a zone. A permission that merely un-does the move AFTER it happened would
        // show the user a landing that is not going to be honoured.
        if ((allowed & zone) == 0) zone = DockZone.None;

        return new DockTarget(node, bounds, zone, DockCompass.PreviewOf(bounds, zone));
    }

    /// <summary>
    /// A pane was dragged clear of this area: it becomes a window of its own. Owned HERE rather than left to the
    /// application, because a floating pane is not a loose window that happens to hold a panel - it is another ROOT of
    /// this same layout, and the layout is what has to know about it in order to save it or dock it back.
    /// </summary>
    internal bool TearOff(Pane pane, TabTearOffEventArgs e)
    {
        // A pane that is not allowed to float does not tear off at all: the strip keeps it and goes on reordering, which
        // is exactly what happened before anybody listened to the event.
        if (pane == null || (pane.Allowed & DockZone.Floating) == 0) return false;
        if (pane.Id == null || !Layout.RemovePane(pane.Id)) return false;

        var node = new PaneGroupNode();
        node.Add(pane.Id);
        var root = new DockingRoot(node, isMain: false);
        Layout.Roots.Add(root);

        // How far along its own tab the pointer took hold. Read BEFORE the rebuild, while the tab is still in the tree
        // and has a position at all - the window is then placed so the pointer keeps that same grip on its caption,
        // instead of jumping to the middle of a window it never grabbed there.
        var grabX = e.ScreenPosition.X - pane.PointToScreen(Vector2.Zero).X;

        // This area first: the pane must leave the tree it is in before the floating area claims it, or one component
        // would have two parents for as long as it took the window to appear.
        Rebuild();

        var floating = Float(root, pane.Header?.ToString() ?? "Pane", out var pieceWindow);
        Show(floating, pieceWindow, grabX);

        // The pane may have been the LAST one in a floating window - that window has nothing left to show and goes.
        Owner.CloseEmptyWindows();

        e.TornWindow = pieceWindow;
        return true;
    }

    /// <summary>
    /// A whole tool panel was dragged by its CAPTION: the group leaves with every pane it holds, in order, and becomes a
    /// window of its own. Dragging a tab moves ONE pane; dragging the caption moves the panel - they are different moves,
    /// and which one happened must not depend on how many tabs are open.
    /// </summary>
    internal bool TearOffGroup(PaneGroup control, Vector2 screenPosition)
    {
        var node = NodeOf(control);
        if (node == null) return false;
        if ((AllowedFor(node) & DockZone.Floating) == 0) return false;   // one pane refusing to float holds the panel

        var title = control.Title?.ToString() ?? "Panel";
        var grabX = screenPosition.X - control.PointToScreen(Vector2.Zero).X;

        var root = Layout.TearOffGroup(node);
        if (root == null) return false;

        var area = Float(root, title, out var window);

        // The group CONTROL travels with its node, so the panel keeps its tabs, its selection and its scroll position -
        // and its panes never have to be taken out of one items panel and put into another.
        _groupsByNode.Remove(node, out var moved);
        if (moved != null) area._groupsByNode[node] = moved;

        Rebuild();   // detaches it from this tree, so the floating area can take it
        Show(area, window, grabX);
        Owner.CloseEmptyWindows();
        return true;
    }

    /// <summary>Opens a floating window for a root of this layout, showing it through an AREA of its own.</summary>
    private DockingArea Float(DockingRoot root, string title, out Window window)
    {
        var area = new DockingArea(Owner, root);
        Owner._satellites.Add(area);

        window = new Window
        {
            Title = title,
            ClientWidth = 480,
            ClientHeight = 360
        };

        area._window = window;
        return area;
    }

    /// <summary>Puts the floating window on screen under the pointer and hands the still-held button to the platform's
    /// own move loop. <paramref name="grabX"/> is how far along the caption the pointer took hold.</summary>
    private void Show(DockingArea area, Window window, double grabX)
    {
        var root = area._root;

        // What the travelling panes permit, read once: it cannot change while the drag runs, and asking per mouse move
        // would walk the tree hundreds of times a second.
        _dragAllowed = AllowedFor(root.Content);

        // Showing a window belongs to the UI thread while the gesture runs on the loop thread, where the visual tree
        // lives. InvokeAsync, not Invoke: blocking into the pump would stall the very frame the drag is still in.
        UIAppContext.Current.Dispatcher.InvokeAsync(() =>
        {
            // The window holds an AREA, not the bare content: that is the whole reason a floating panel can be docked
            // INTO. It is a docking area with its own root, its own tab strip and its own compass, so a tab dragged out
            // of any other window can land in it.
            window.Content = area;
            window.Show();
            area.Rebuild();

            // Nothing is aimed at yet. Clearing the AREA rather than just this flag: after the family was introduced the
            // compass that is up may belong to another window, and dropping the flag alone would leave it on screen.
            _targetArea?.ClearAim();
            _targetArea = null;

            // The cursor is read HERE, live: it has moved on since the threshold was crossed, and aiming at where it
            // WAS is what puts the caption out from under the pointer.
            // Both sides PHYSICAL: the cursor is a desktop point and so is a window's position (see WindowBase.Left).
            // The caption height is logical, so it - and only it - is converted.
            var cursor = Mouse.ScreenCoordinates;
            window.Left = cursor.X - grabX;
            window.Top = cursor.Y - window.TitleBarHeight * DpiScale.Y / 2;

            window.WindowMoving += (_, _) => TrackWindow(window);
            window.WindowMoveCompleted += (_, _) => DropWindow(window, root);

            // Hand the still-held button to the platform's own move loop, so the window rides under the cursor with
            // Aero Snap intact instead of a position recomputed per mouse event.
            window.DragMove();
        });
    }

    /// <summary>Where the pointer is, in this area's coordinates, while a floating window is being dragged.
    /// <para>Taken from the MOUSE, not from the window's position. Measured: during the platform's move loop
    /// <c>WindowMoving</c> fires for every step, but the window's own Left/Top stay at the value they had when the loop
    /// started - 813 events, one unchanging position - while the screen cursor tracks perfectly. Deriving the pointer
    /// from the window is what froze the compass where the drag began.</para></summary>
    private Vector2 PointerIn()
    {
        // Screen coordinates are PHYSICAL pixels and so is what PointToScreen answers, while everything this area
        // measures itself in - RenderSize, a group's bounds, the compass geometry - is LOGICAL. They are the same number
        // only at 100%; on a scaled display the difference put the pointer nowhere near where it actually was, which is
        // why the compass could not be aimed at all on a 4K monitor. Divide once, here, at the boundary.
        return (Mouse.ScreenCoordinates - this.PointToScreen(Vector2.Zero)) / DpiScale;
    }

    /// <summary>Physical pixels per logical unit for the window this area is in. One when it has no window yet (nothing
    /// to convert against) or on an unscaled display.</summary>
    private Vector2 DpiScale => RootVisual is IWindow window ? window.DpiScale : Vector2.One;

    /// <summary>Which area of the family the pointer is over, and what a drop there would do. EVERY area is asked, not
    /// just this one - that is what lets a panel be dropped into a floating window as well as into the main one, and it
    /// is the same question in each of them because they are areas of the same layout.</summary>
    private void TrackWindow(WindowBase window)
    {
        DockingArea hit = null;
        var target = default(DockTarget);
        var point = Vector2.Zero;

        foreach (var area in Family)
        {
            // The window being dragged is not somewhere to drop it - its own area travels with it.
            if (ReferenceEquals(area._window, window)) continue;
            if (area.RootVisual == null) continue;   // not on screen yet: it has no coordinates to convert against

            var at = area.PointerIn();
            var size = area.RenderSize;
            if (at.X < 0 || at.Y < 0 || at.X > size.Width || at.Y > size.Height) continue;

            var resolved = area.Resolve(at, _dragAllowed);
            if (resolved.Node == null) continue;

            hit = area;
            target = resolved;
            point = at;
            break;
        }

        // The area that WAS aimed at takes its compass down: crossing from one window into another must not leave two up.
        if (!ReferenceEquals(hit, _targetArea))
        {
            _targetArea?.ClearAim();
            _targetArea = hit;
        }

        _target = target;
        hit?.Aim(target);

        if (LogDocking)
        {
            System.Console.WriteLine($"[DockTrack] mouse={Mouse.ScreenCoordinates} area=({point.X:F0},{point.Y:F0}) " +
                                     $"hit={(hit == null ? "none" : hit == Owner ? "main" : "floating")} " +
                                     $"node={_target.Node != null} edge={_target.IsEdge} zone={_target.Zone}");
        }
    }

    /// <summary>The area currently aimed at, which is the one whose compass is up. Held by the area DRIVING the drag.</summary>
    private DockingArea _targetArea;

    /// <summary>Puts this area's compass up (once) and points it at the target. The bounds are in this area's own
    /// coordinates, which are the compass's own: it covers the whole area.</summary>
    private void Aim(DockTarget target)
    {
        // The window covers the area, so it is touched only when the pointer crosses in or out of it - and touching a
        // window is UI-thread work while this runs on the LOOP thread (WindowMoving arrives through LoopSignal.Drain,
        // not from the message pump: measured, every Show from here threw a DispatcherException).
        if (!_overlayShown)
        {
            _overlayShown = true;
            UIAppContext.Current.Dispatcher.InvokeAsync(ShowOverlay);
        }

        _compass.AimAt(target.Bounds, target.Zone, target.IsEdge, EdgeDockSize);
    }

    private void ClearAim()
    {
        _compass.Clear();
        if (!_overlayShown) return;

        _overlayShown = false;
        UIAppContext.Current.Dispatcher.InvokeAsync(HideOverlay);
    }

    /// <summary>The floating window was let go. If it was over an indicator, WHATEVER it holds - a single pane, a panel,
    /// or a whole split built up inside it - comes back into the tree it was dropped on and the window closes.</summary>
    private void DropWindow(WindowBase window, DockingRoot root)
    {
        TrackWindow(window);

        var target = _target;
        var area = _targetArea;
        _target = default;
        _targetArea = null;
        area?.ClearAim();

        if (!target.IsValid || area == null) return;
        if (!Layout.MoveNode(root.Content, target.Node, target.Zone,
                size: target.IsEdge ? PaneLength.Pixels(EdgeDockSize) : null)) return;

        // The controls the window was showing are given up BEFORE anything rebuilds: their panes are about to be built
        // into the target area's tree, and a component belongs to one tree.
        window.Content = null;
        FloatingArea(window)?.Release();

        RebuildFamily();
    }

    private DockingArea FloatingArea(WindowBase window)
    {
        foreach (var area in Owner._satellites)
        {
            if (ReferenceEquals(area._window, window)) return area;
        }
        return null;
    }

    /// <summary>Gives up every control this area holds. Called when its root has been docked into another area: the panes
    /// in those controls are about to be built into that area's tree, and a component belongs to ONE tree.</summary>
    private void Release()
    {
        foreach (var pair in _groupsByNode)
        {
            var items = pair.Value.Items;
            for (var i = items.Count - 1; i >= 0; i--) items.RemoveAt(i);
        }

        _groupsByNode.Clear();
        _hostsByNode.Clear();
        Children.Clear();
    }

    /// <summary>Rebuilds every area of the layout and closes the floating windows whose roots have gone. One drop can
    /// change two windows - the one the panel left and the one it landed in - so both are rebuilt from the model rather
    /// than one of them being patched.</summary>
    private void RebuildFamily()
    {
        foreach (var area in Family) area.Rebuild();
        Owner.CloseEmptyWindows();
    }

    private void CloseEmptyWindows()
    {
        for (var i = _satellites.Count - 1; i >= 0; i--)
        {
            var area = _satellites[i];
            if (Layout.Roots.Contains(area._root)) continue;   // still a root of the layout: its window stays

            _satellites.RemoveAt(i);

            var window = area._window;
            if (window == null) continue;

            area._window = null;
            UIAppContext.Current.Dispatcher.InvokeAsync(window.Close);
        }
    }

    internal static readonly bool LogDocking = System.Environment.GetEnvironmentVariable("ADAMANTIUM_DOCK_LOG") == "1";

    /// <summary>Forgets the controls of nodes the layout no longer contains. Groups die - the last pane is dragged out
    /// of one, or two centre groups are merged into one on load - and holding their controls would keep emptying them
    /// on every rebuild forever.</summary>
    private void Prune()
    {
        // Asked of the MODEL, not of the visual tree: whether a control has a parent yet depends on when attachment
        // happens, and a rule that depends on timing is a rule that will one day delete a live group.
        // Only THIS area's root counts: a group that moved to a floating window belongs to that window's area now, and
        // holding its control here would empty it on every rebuild of a tree it has left.
        var alive = new HashSet<PaneGroupNode>();
        CollectGroups(RootContent, alive);

        List<PaneGroupNode> gone = null;
        foreach (var node in _groupsByNode.Keys)
        {
            if (!alive.Contains(node)) (gone ??= []).Add(node);
        }

        if (gone == null) return;
        foreach (var node in gone) _groupsByNode.Remove(node);
    }

    private static void CollectGroups(PaneNode node, HashSet<PaneGroupNode> into)
    {
        switch (node)
        {
            case PaneGroupNode group:
                into.Add(group);
                break;
            case PaneSplitNode split:
                foreach (var child in split.Children) CollectGroups(child, into);
                break;
        }
    }

    /// <summary>Turns a node into controls: a split becomes a <see cref="PaneHost"/> holding its children with a
    /// <see cref="PaneSplitter"/> in every gap, a group becomes a <see cref="PaneGroup"/> holding its panes. Building
    /// real controls (rather than arranging the groups by hand from the model) is what makes the boundaries draggable -
    /// the splitter needs neighbours in a panel to resize.</summary>
    private IMeasurableComponent BuildVisual(PaneNode node)
    {
        switch (node)
        {
            case PaneGroupNode group:
            {
                // An empty group is gone - except the document well, which stays as empty space. Closing the last
                // document must not take the centre of the layout with it.
                var isWell = ReferenceEquals(group, Layout.DocumentWell);
                if (group.IsEmpty && !isWell) return null;

                var control = GroupFor(group);

                // STATE FIRST, panes second. Filling them decides the selection, and whether a strip may have none is
                // part of the state (RequiresSelection follows the fold). Done the other way round, a folding panel had
                // its selection cleared while the control still insisted on having one - so the strip put the highlight
                // back on the FIRST tab, and that answer was then written into the model as the active pane. Pinning the
                // panel open afterwards duly restored it: whichever tab you revealed, you got the first one back.
                control.State = group.State;
                control.Edge = DockingLayout.EdgeOf(group);
                control.IsFloatingRoot = _root is { IsMain: false } && ReferenceEquals(RootContent, group);

                // Looks follow the PLACE: the well is the documents, everything else is a tool. A tool dropped into the
                // centre is dressed as a document - it has no edge there to fold against, so a caption with a pin on it
                // would be a button that cannot do anything.
                control.Kind = isWell ? PaneKind.Document : PaneKind.Tool;

                FillPanes(control, group);
                PaneHost.SetPaneLength(control, group.Length);
                return control;
            }

            case PaneSplitNode split:
            {
                // The host for this NODE, kept across rebuilds like the groups are. Building a fresh one every time
                // re-parents every group under it, and re-attaching a control re-applies its template: the group then
                // ends up with a NEW items panel holding the tabs while the visual tree still arranges the old one.
                // Measured: the panel being arranged (#40088089, 2 children) was never the one the group reported.
                var host = HostFor(split);
                host.Orientation = split.Orientation;
                host.DividerThickness = DividerThickness;
                PaneHost.SetPaneLength(host, split.Length);

                var wanted = new List<IMeasurableComponent>();
                PaneNode previous = null;

                foreach (var child in split.Children)
                {
                    var visual = BuildVisual(child);
                    if (visual == null) continue;

                    if (wanted.Count > 0)
                    {
                        // The one moment the controls get to answer back: a finished drag is the user stating a size, and
                        // the model is what a save reads. See SyncLengthsToModel for why it is not done every pass.
                        var grip = new PaneSplitter();
                        grip.DragCompleted += (_, _) => SyncLengthsToModel();

                        // A divider next to a PUT-AWAY panel does not drag. That panel's size is MEASURED - its strip and
                        // nothing more - not assigned, so a drag writes pixels onto a control whose model still says Auto;
                        // the model refuses them (see SyncLengthsToModel), nothing rebuilds, and the panel simply stays
                        // stretched at whatever it was dragged to, caption and body still hidden. The gap stays for the
                        // seam; it just is not a handle.
                        grip.IsHitTestVisible = !IsPutAway(previous) && !IsPutAway(child);

                        wanted.Add(grip);
                    }

                    wanted.Add(visual);
                    previous = child;
                }

                if (wanted.Count == 0) return null;

                // Touch the children only when they actually differ - an unchanged split must not disturb its subtree.
                if (!SameChildren(host, wanted))
                {
                    host.Children.Clear();
                    foreach (var visual in wanted) host.Children.Add(visual);
                }
                else
                {
                    // The live splitters are KEPT (SameChildren treats them as interchangeable - they carry no identity,
                    // only a position), so the one thing that does change about them has to be carried across: folding a
                    // panel leaves the split's shape untouched, and without this its divider stayed draggable.
                    for (var i = 0; i < wanted.Count; i++)
                    {
                        if (wanted[i] is PaneSplitter fresh && host.Children[i] is PaneSplitter live)
                            live.IsHitTestVisible = fresh.IsHitTestVisible;
                    }
                }

                return host;
            }

            default:
                return null;
        }
    }

    /// <summary>A panel that is put away answers for its own size (its strip, and nothing else), so nobody may state one
    /// for it.</summary>
    private static bool IsPutAway(PaneNode node) => node is PaneGroupNode { State: PaneGroupState.Collapsed };

    private readonly Dictionary<PaneSplitNode, PaneHost> _hostsByNode = new();

    private PaneHost HostFor(PaneSplitNode node)
    {
        if (_hostsByNode.TryGetValue(node, out var existing)) return existing;

        var created = new PaneHost();
        _hostsByNode[node] = created;
        return created;
    }

    private static bool SameChildren(PaneHost host, List<IMeasurableComponent> wanted)
    {
        if (host.Children.Count != wanted.Count) return false;

        for (var i = 0; i < wanted.Count; i++)
        {
            // Splitters are interchangeable - they carry no identity, only a position between two neighbours.
            if (wanted[i] is PaneSplitter && host.Children[i] is PaneSplitter) continue;
            if (!ReferenceEquals(host.Children[i], wanted[i])) return false;
        }

        return true;
    }

    /// <summary>The control for a group node - the one it already had, or a new one. A node created by a split has no
    /// control yet, and that is the only case where one is made.</summary>
    private PaneGroup GroupFor(PaneGroupNode node)
    {
        if (_groupsByNode.TryGetValue(node, out var existing)) return existing;

        var created = new PaneGroup();
        Track(node, created);

        if (LogDocking) System.Console.WriteLine($"[DockingArea] NEW PaneGroup #{created.GetHashCode()} for node #{node.GetHashCode()}");

        return created;
    }

    /// <summary>Registers a group control against its node - the ONE place a pair is made, so that everything a pair
    /// needs is done exactly once and in both cases: a node created by a split, and a group written in markup.
    /// <para>Which tab is active is part of the LAYOUT, so picking one has to reach the model - every rebuild reads the
    /// selection back out of it (see FillPanes). Missing it for authored groups meant the model kept whatever it was
    /// built with: reveal a put-away panel by clicking its third tab, press the pin, and the first one came back.</para>
    /// <para>Closed over the node rather than looked up: the control travels WITH its node into a floating window, and
    /// this area's dictionary no longer knows either of them by then. And a -1 is not an opinion about which tab is
    /// active - it is a folded panel saying none is - so it is not recorded, and the panel returns to the tab it was
    /// last showing.</para></summary>
    private void Track(PaneGroupNode node, PaneGroup control)
    {
        _groupsByNode[node] = control;

        control.SelectionChanged += (_, _) =>
        {
            if (control.SelectedIndex >= 0) node.ActiveIndex = control.SelectedIndex;
        };
    }

    private void FillPanes(PaneGroup control, PaneGroupNode node)
    {
        // Bring the group's tabs to exactly the model's order, moving only what is out of place. Whatever is already
        // where it belongs is left untouched - the panel keeps its children, and its identity, and so does everything
        // measured against them.
        for (var i = 0; i < node.PaneIds.Count; i++)
        {
            if (!_panesById.TryGetValue(node.PaneIds[i], out var pane)) continue;

            var at = control.Items.IndexOf(pane);
            if (at == i) continue;

            // It must not arrive wearing the offset the drag-reorder animation left on it (measured at 215px).
            if (pane.RenderTransform is Core.Media.Transform transform) transform.TranslateX = transform.TranslateY = 0;

            if (at >= 0) control.Items.RemoveAt(at);
            control.Items.Insert(System.Math.Min(i, control.Items.Count), pane);
        }

        while (control.Items.Count > node.PaneIds.Count) control.Items.RemoveAt(control.Items.Count - 1);

        if (control.Items.Count > 0)
        {
            // A PUT-AWAY panel has no selection: its strip is a row of buttons, and a highlighted one would claim a panel
            // is open when none is. Said HERE as well as in SyncFold, because a rebuild that does not CHANGE the state
            // does not re-run the fold - and this line would then put the selection straight back. Measured: a folded
            // strip kept its accent bar under the first tab after any later rebuild.
            control.SelectedIndex = node.State == PaneGroupState.Collapsed
                ? -1
                : System.Math.Clamp(node.ActiveIndex, 0, control.Items.Count - 1);
        }

        control.InvalidateMeasure();
    }

    /// <summary>Space left between two neighbours for the divider that will sit there.</summary>
    public double DividerThickness { get; set; } = 4.0;

    /// <summary>How wide a pane docked to an EDGE of the area starts out, in pixels along that edge's axis.
    /// <para>A band, not half the area: an edge anchor is a side panel, and half the editor is a partition rather than
    /// an anchor. In pixels because a side panel should keep its width while the window resizes around it - it stays
    /// freely draggable, that is what the divider is for; pixels only mean it does not scale with the window.</para></summary>
    public static readonly AdamantiumProperty EdgeDockSizeProperty = AdamantiumProperty.Register(
        nameof(EdgeDockSize), typeof(double), typeof(DockingArea), new PropertyMetadata(240.0));

    public double EdgeDockSize
    {
        get => GetValue<double>(EdgeDockSizeProperty);
        set => SetValue(EdgeDockSizeProperty, value);
    }

    private bool _layoutBuilt;

    /// <summary>Builds the layout from the authored groups, once. Everything after that is the layout's own history -
    /// rebuilding it from markup later would throw away what the user arranged.</summary>
    private void EnsureLayout()
    {
        if (_layoutBuilt) return;
        _layoutBuilt = true;

        var declarations = new List<ZoneDeclaration>();

        foreach (var child in Children)
        {
            if (child is not PaneGroup group) continue;

            // A group node holds the ids of its PANES, not the group's own name: a gesture moves one pane, so a pane is
            // the smallest thing the model has to be able to name.
            var node = new PaneGroupNode();
            foreach (var item in group.Items)
            {
                if (item is not Pane pane) continue;

                var id = EnsureId(pane);
                _panesById[id] = pane;
                node.Add(id);
            }

            if (node.IsEmpty) continue;

            Track(node, group);
            declarations.Add(new ZoneDeclaration(group.Zone, node, group.Size));
        }

        if (declarations.Count == 0) return;

        Layout = DockingLayout.FromZones(declarations);

        // The authored groups are children of THIS panel until now; the built tree is about to take them, and a
        // component belongs to one visual tree. Cleared here rather than inside Rebuild, which must not tear the tree
        // down on later calls - that is what re-applies templates and orphans the items panel.
        Children.Clear();

        Rebuild();
    }

    /// <summary>A pane's id, made from its header when the author did not give one. Derived from the header rather than
    /// from its position, because a saved layout has to survive the panes being declared in a different order.</summary>
    private string EnsureId(Pane pane)
    {
        if (!string.IsNullOrEmpty(pane.Id)) return pane.Id;

        var stem = pane.Header?.ToString();
        if (string.IsNullOrEmpty(stem)) stem = "pane";

        var id = stem;
        var next = 2;
        while (_panesById.ContainsKey(id)) id = $"{stem}{next++}";

        pane.Id = id;
        return id;
    }
}
