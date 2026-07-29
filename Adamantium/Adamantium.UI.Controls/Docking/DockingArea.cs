using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;

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
    /// found again when the window is docked back.</para></summary>
    private readonly Dictionary<string, Pane> _panesById = new();

    private PaneNode RootContent => Layout.Main?.Content;

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

        SyncLengthsToModel();
        return finalSize;
    }

    /// <summary>Copies the lengths the controls now carry back into the model.
    /// <para>A divider drag writes onto the CONTROLS - that is what makes the boundary move under the pointer. Without
    /// this the model never learned of it, so the next rebuild (any drop, any tear-off) handed the controls the sizes
    /// from before the drag and everything the user had arranged with the mouse was silently undone. The model is what
    /// gets saved, so it has to be the one that knows.</para></summary>
    private void SyncLengthsToModel()
    {
        foreach (var pair in _groupsByNode) pair.Key.Length = PaneHost.GetPaneLength(pair.Value);
        foreach (var pair in _hostsByNode) pair.Key.Length = PaneHost.GetPaneLength(pair.Value);
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
        foreach (var group in _groupsByNode.Values)
        {
            group.InvalidateMeasure();
            (group.ItemsHostPanel as IMeasurableComponent)?.InvalidateMeasure();
        }

        InvalidateMeasure();
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

    /// <summary>Where the group under a point sits, in THIS area's coordinates, and what a drop there would do. The
    /// point is in the area's own space.</summary>
    private DockTarget Resolve(Vector2 point)
    {
        var origin = this.PointToScreen(Vector2.Zero);
        var scale = DpiScale;
        var area = new Rect(0, 0, RenderSize.Width, RenderSize.Height);

        foreach (var pair in _groupsByNode)
        {
            var group = pair.Value;
            // Physical difference back to LOGICAL, to be in the same units as the point and the group's own size.
            var at = (group.PointToScreen(Vector2.Zero) - origin) / scale;
            var size = group.RenderSize;
            if (point.X < at.X || point.Y < at.Y || point.X > at.X + size.Width || point.Y > at.Y + size.Height) continue;

            var bounds = new Rect(at.X, at.Y, size.Width, size.Height);

            // The EDGE anchors first. They belong to the area and sit at its sides, the cross belongs to whichever group
            // is under the pointer - and where the two could overlap, the edge is the more specific answer. An edge drop
            // is aimed at the ROOT, which is what makes it span the whole side; it is the same move, not another kind.
            var edge = DockCompass.EdgeZoneAt(area, point, _compass.IndicatorSize, _compass.EdgeIndicatorInset);
            if (edge != DockZone.None)
                return new DockTarget(RootContent, bounds, edge,
                    DockCompass.PreviewOf(area, edge, EdgeDockSize), isEdge: true);

            var zone = DockCompass.ZoneAt(bounds, point, _compass.IndicatorSize, _compass.IndicatorGap);
            return new DockTarget(pair.Key, bounds, zone, DockCompass.PreviewOf(bounds, zone));
        }

        return default;
    }

    /// <summary>
    /// A pane was dragged clear of this area: it becomes a window of its own. Owned HERE rather than left to the
    /// application, because a floating pane is not a loose window that happens to hold a panel - it is another ROOT of
    /// this same layout, and the layout is what has to know about it in order to save it or dock it back.
    /// </summary>
    internal bool TearOff(Pane pane, TabTearOffEventArgs e)
    {
        if (pane?.Id == null || !Layout.RemovePane(pane.Id)) return false;

        var node = new PaneGroupNode();
        node.Add(pane.Id);
        var root = new DockingRoot(node, isMain: false);
        Layout.Roots.Add(root);

        // How far along its own tab the pointer took hold. Read BEFORE the rebuild, while the tab is still in the tree
        // and has a position at all - the window is then placed so the pointer keeps that same grip on its caption,
        // instead of jumping to the middle of a window it never grabbed there.
        var grabX = e.ScreenPosition.X - pane.PointToScreen(Vector2.Zero).X;

        // This area first: the pane must leave the tree it is in before the window claims its content, or one component
        // would have two parents for as long as it took the window to appear.
        var content = pane.Content;
        Rebuild();

        var window = new Window
        {
            Title = pane.Header?.ToString() ?? "Pane",
            ClientWidth = 480,
            ClientHeight = 360
        };

        // Showing a window belongs to the UI thread while the gesture runs on the loop thread, where the visual tree
        // lives. InvokeAsync, not Invoke: blocking into the pump would stall the very frame the drag is still in.
        UIAppContext.Current.Dispatcher.InvokeAsync(() =>
        {
            // Just the pane's CONTENT. A floating pane already has a caption naming it - the window's own - and a tab
            // strip holding a single tab beneath that caption says the same thing twice.
            window.Content = content;
            window.Show();

            _overlayShown = false;

            // The cursor is read HERE, live: it has moved on since the threshold was crossed, and aiming at where it
            // WAS is what puts the caption out from under the pointer.
            // Both sides PHYSICAL: the cursor is a desktop point and so is a window's position (see WindowBase.Left).
            // The caption height is logical, so it - and only it - is converted.
            var cursor = Mouse.ScreenCoordinates;
            window.Left = cursor.X - grabX;
            window.Top = cursor.Y - window.TitleBarHeight * DpiScale.Y / 2;

            window.WindowMoving += (_, _) => TrackWindow(window);
            window.WindowMoveCompleted += (_, _) => DropWindow(window, pane.Id, root);

            // Hand the still-held button to the platform's own move loop, so the window rides under the cursor with
            // Aero Snap intact instead of a position recomputed per mouse event.
            window.DragMove();
        });

        e.TornWindow = window;
        return true;
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

    private void TrackWindow(WindowBase window)
    {
        var point = PointerIn();
        var size = RenderSize;
        _target = point.X < 0 || point.Y < 0 || point.X > size.Width || point.Y > size.Height
            ? default
            : Resolve(point);

        var target = _target;

        // The window covers the area, so it is touched only when the pointer crosses in or out of it - and touching a
        // window is UI-thread work while this runs on the LOOP thread (WindowMoving arrives through LoopSignal.Drain,
        // not from the message pump: measured, every Show from here threw a DispatcherException).
        var inside = target.Node != null;
        if (inside != _overlayShown)
        {
            _overlayShown = inside;
            UIAppContext.Current.Dispatcher.InvokeAsync(() =>
            {
                if (_overlayShown) ShowOverlay();
                else HideOverlay();
            });
        }

        // The group's rectangle in the AREA's coordinates, which are the compass's own: it covers the whole area.
        if (!inside) _compass.Clear();
        else _compass.AimAt(target.Bounds, target.Zone, target.IsEdge, EdgeDockSize);

        if (LogDocking)
        {
            System.Console.WriteLine($"[DockTrack] mouse={Mouse.ScreenCoordinates} area=({point.X:F0},{point.Y:F0}) " +
                                     $"size=({size.Width:F0}x{size.Height:F0}) node={_target.Node != null} edge={_target.IsEdge} zone={_target.Zone}");
        }
    }

    /// <summary>The floating window was let go. If it was over an indicator, its pane comes back into the tree and the
    /// window closes - the same <see cref="DockingLayout.MovePane"/> every other gesture ends in.</summary>
    private void DropWindow(WindowBase window, string paneId, DockingRoot root)
    {
        TrackWindow(window);

        var target = _target;
        _target = default;
        _overlayShown = false;
        _compass.Clear();
        UIAppContext.Current.Dispatcher.InvokeAsync(HideOverlay);

        if (!target.IsValid) return;

        // Hand the content back before the pane is re-hosted: it belongs to the window's tree until this line.
        window.Content = null;

        if (!Layout.MovePane(paneId, target.Node, target.Zone,
                size: target.IsEdge ? PaneLength.Pixels(EdgeDockSize) : null)) return;

        Layout.Roots.Remove(root);
        Rebuild();
        window.Close();
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
                if (group.IsEmpty) return null;

                var control = GroupFor(group);
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
                foreach (var child in split.Children)
                {
                    var visual = BuildVisual(child);
                    if (visual == null) continue;

                    if (wanted.Count > 0) wanted.Add(new PaneSplitter());
                    wanted.Add(visual);
                }

                if (wanted.Count == 0) return null;

                // Touch the children only when they actually differ - an unchanged split must not disturb its subtree.
                if (!SameChildren(host, wanted))
                {
                    host.Children.Clear();
                    foreach (var visual in wanted) host.Children.Add(visual);
                }

                return host;
            }

            default:
                return null;
        }
    }

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
        _groupsByNode[node] = created;

        if (LogDocking) System.Console.WriteLine($"[DockingArea] NEW PaneGroup #{created.GetHashCode()} for node #{node.GetHashCode()}");

        return created;
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
            control.SelectedIndex = System.Math.Clamp(node.ActiveIndex, 0, control.Items.Count - 1);
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

            _groupsByNode[node] = group;
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
