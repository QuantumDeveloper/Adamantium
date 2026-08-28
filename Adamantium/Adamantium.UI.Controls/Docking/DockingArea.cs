using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.Docking;

/// <summary>
/// The area panes are docked in: it owns the layout and lays its groups out by it.
/// <para>The author writes WHERE, never how much - groups declare a <see cref="PaneGroup.Zone"/> and the split tree is
/// derived from them (<see cref="DockingLayout.FromZones"/>).</para>
/// <para>The layout is DATA (<see cref="Layout"/>) and these controls are a VIEW of it: change the model and call
/// <see cref="Rebuild"/>. No gesture edits controls directly, or the model and the screen become two answers to one
/// question.</para>
/// </summary>
public class DockingArea : Panel
{
    /// <summary>The layout this area shows - what gestures edit and what a save writes.</summary>
    public DockingLayout Layout { get; private set; } = new();

    // Panes by id - the one place an id turns back into a control. Shared with every floating area of the layout: a
    // pane that moved between two windows must not be two entries.
    private readonly Dictionary<string, Pane> _panesById;

    // The root this area shows: the layout's main one, or the one torn off into this floating window. Giving each root
    // an area of its own is what makes a floating panel dockable INTO.
    private readonly DockingRoot _root;

    private readonly DockingArea _owner;              // the main area, when this is a floating one
    private readonly List<DockingArea> _satellites = [];
    private WindowBase _window;                       // the window a floating area lives in
    private EventHandler<EventArgs> _onWindowClosed;  // "the user closed it" - taken back when WE close it ourselves

    public DockingArea() => _panesById = new Dictionary<string, Pane>();

    private DockingArea(DockingArea owner, DockingRoot root)
    {
        _owner = owner;
        _root = root;
        Layout = owner.Layout;
        _panesById = owner._panesById;
        // SetCurrentValue: a plain write is LOCAL and would outrank the theme or binding the owner's own value came from.
        SetCurrentValue(EdgeDockSizeProperty, owner.EdgeDockSize);
        DividerThickness = owner.DividerThickness;

        _layoutBuilt = true;   // its content came from a tear-off - there are no authored zones to build from
    }

    private DockingArea Owner => _owner ?? this;

    /// <summary>Every area of this layout, FLOATING ONES FIRST: a floating window sits over the main one, so where both
    /// could claim the pointer the floating one wins.</summary>
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

    /// <summary>THIS window's document area, or null if it has none - a floating window of tools does not.</summary>
    private PaneNode Well => (_root ?? Layout.Main)?.DocumentWell;

    // The control per group node, kept ACROSS rebuilds: a group that merely moved keeps its selection and scroll.
    private readonly Dictionary<PaneGroupNode, PaneGroup> _groupsByNode = new();

    // The area is TWO things: the split tree in the middle, and the four strips of PUT-AWAY panels along its edges. The
    // strips are not in the tree (rule 3b), so they are laid out around it and nothing inside the tree can move them.

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureLayout(fromLayoutPass: true);
        FollowWindowActivation(HostWindow);

        // Strips first, each asked what it needs across its own edge; the rest is the tree's.
        var left = MeasureBar(DockZone.Left, availableSize);
        var right = MeasureBar(DockZone.Right, availableSize);
        var top = MeasureBar(DockZone.Top, availableSize);
        var bottom = MeasureBar(DockZone.Bottom, availableSize);

        var middle = new Size(
            System.Math.Max(0, availableSize.Width - left - right),
            System.Math.Max(0, availableSize.Height - top - bottom));

        _tree?.Measure(middle);
        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        EnsureLayout(fromLayoutPass: true);

        // Sides take the FULL height, bands what is left between them - the order the layout itself uses (rule 2.3).
        var left = BarExtent(DockZone.Left);
        var right = BarExtent(DockZone.Right);
        var top = BarExtent(DockZone.Top);
        var bottom = BarExtent(DockZone.Bottom);

        var bandWidth = System.Math.Max(0, finalSize.Width - left - right);

        // TREE first: a strip's flyout is placed from the zone it would come back to, which is a control inside the tree.
        _tree?.Arrange(new Rect(left, top, bandWidth,
            System.Math.Max(0, finalSize.Height - top - bottom)));

        ArrangeBar(DockZone.Left, new Rect(0, 0, left, finalSize.Height));
        ArrangeBar(DockZone.Right, new Rect(finalSize.Width - right, 0, right, finalSize.Height));
        ArrangeBar(DockZone.Top, new Rect(left, 0, bandWidth, top));
        ArrangeBar(DockZone.Bottom, new Rect(left, finalSize.Height - bottom, bandWidth, bottom));

        return finalSize;
    }

    /// <summary>The window this area lives in - its own for a floating one, the walk up the tree for the main one.</summary>
    private WindowBase HostWindow
    {
        get
        {
            if (_window != null) return _window;

            for (IUIComponent node = this; node != null; node = node.VisualParent)
            {
                if (node is WindowBase window) return window;
            }

            return null;
        }
    }

    /// <summary>The visual for the split tree - one child among the strips, and the only one that grows.</summary>
    private IMeasurableComponent _tree;

    private double MeasureBar(DockZone edge, Size availableSize)
    {
        var across = 0.0;

        foreach (var group in BarControls(edge))
        {
            group.Measure(availableSize);
            var wanted = edge is DockZone.Left or DockZone.Right ? group.DesiredSize.Width : group.DesiredSize.Height;
            across = System.Math.Max(across, wanted);
        }

        return across;
    }

    private double BarExtent(DockZone edge)
    {
        var across = 0.0;

        foreach (var group in BarControls(edge))
        {
            across = System.Math.Max(across, edge is DockZone.Left or DockZone.Right
                ? group.DesiredSize.Width
                : group.DesiredSize.Height);
        }

        return across;
    }

    /// <summary>Stacks the strips of one edge ALONG it, each taking what it asked for - two put-away panels on the same
    /// side sit one after the other, exactly as their tabs would.</summary>
    private void ArrangeBar(DockZone edge, Rect bounds)
    {
        var vertical = edge is DockZone.Left or DockZone.Right;
        var offset = 0.0;

        foreach (var group in BarControls(edge))
        {
            var along = vertical ? group.DesiredSize.Height : group.DesiredSize.Width;

            group.Arrange(vertical
                ? new Rect(bounds.X, bounds.Y + offset, bounds.Width, along)
                : new Rect(bounds.X + offset, bounds.Y, along, bounds.Height));

            // The flyout takes the shape of the ZONE THE PANEL WOULD COME BACK TO - not of this strip (as long as its
            // few captions) and not of the whole edge (which runs under the side panels and off the window).
            var zone = ZoneBounds(edge);
            var thickness = vertical ? bounds.Width : bounds.Height;
            var extent = FlyoutExtent(NodeOf(group));

            group.RevealLength = vertical ? zone.Height : zone.Width;

            // ...and the flyout's own thickness, from the SAME number the offset above is derived from. It used to be
            // written on the fold and on a layout build instead, so the two halves of one geometry were computed in
            // different places at different moments: the flyout opens at "the strip's edge MINUS extent" and is then
            // drawn "RevealExtent" wide, so the moment those disagree it stops reaching the strip and a strip-width gap
            // opens between the panel and the edge - which reads as the strip sitting outside the docking area.
            group.RevealExtent = extent;

            // Placed from the strip's own corner, both axes stated outright (see PaneGroup.RevealOffsetX): ALONG the
            // edge it lines up with the zone, and ACROSS it opens away from the edge - past the strip on a Left/Top,
            // back by its own size on a Right/Bottom.
            group.RevealOffsetX = edge switch
            {
                DockZone.Left => thickness,
                DockZone.Right => -extent,
                _ => zone.X - (bounds.X + offset)
            };

            group.RevealOffsetY = edge switch
            {
                DockZone.Top => thickness,
                DockZone.Bottom => -extent,
                _ => zone.Y - (bounds.Y + offset)
            };

            offset += along;
        }
    }

    // Where a put-away panel would come back to, in this area's coordinates: the centre column for a band, the whole
    // tree for a side - the same targets ExpandGroup pins against, so the flyout shows the shape it will take.
    private Rect ZoneBounds(DockZone edge)
    {
        var root = _root ?? Layout.Main;
        if (root == null) return default;

        var node = edge is DockZone.Top or DockZone.Bottom ? Layout.BandTarget(root) : root.Content;

        if (VisualOf(node) is not IUIComponent visual || visual.RenderSize.Width <= 0) return default;

        var at = (visual.PointToScreen(Vector2.Zero) - this.PointToScreen(Vector2.Zero)).ToLogical(DpiScale);
        return new Rect(at.X, at.Y, visual.RenderSize.Width, visual.RenderSize.Height);
    }

    private IMeasurableComponent VisualOf(PaneNode node)
    {
        switch (node)
        {
            case PaneSplitNode split when _hostsByNode.TryGetValue(split, out var host):
                return host;

            case PaneGroupNode group when _groupsByNode.TryGetValue(group, out var control):
                return control;

            default:
                return _tree;
        }
    }

    private IEnumerable<PaneGroup> BarControls(DockZone edge)
    {
        var root = _root ?? Layout.Main;
        if (root == null) yield break;

        foreach (var node in root.Bars[edge])
        {
            if (_groupsByNode.TryGetValue(node, out var control)) yield return control;
        }
    }

    // Copies the controls' lengths back into the model - when a DIVIDER DRAG ENDS and at no other time. Copying every
    // layout pass let the view answer back: a freshly built control still carrying the default Star wrote it over a
    // real size (measured: the console went from a 160px band to two thirds of the window after a tear-off).
    private void SyncLengthsToModel()
    {
        // A folded group's length is Auto and belongs to the fold, not to a drag - copying it back overwrites what it
        // is supposed to return to.
        foreach (var pair in _groupsByNode)
        {
            if (pair.Key.OwnsLength) pair.Key.Length = PaneHost.GetPaneLength(pair.Value);
        }
        foreach (var pair in _hostsByNode) pair.Key.Length = PaneHost.GetPaneLength(pair.Value);
    }

    /// <summary>Auto-hide: puts a docked group away to its strip, or brings a folded one back into the layout. The
    /// button is a thumbtack, but the word "pin" belongs to a TAB (<see cref="Pane.IsPinned"/>) - what this does to a
    /// PANEL is hide it until it is wanted.</summary>
    internal void ToggleAutoHide(PaneGroup group)
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

    /// <summary>Makes a pane THE active one - what is being worked in. One across the whole layout, floating windows
    /// included, so the accent border says where the work is rather than where a pointer once passed. The GROUP holding
    /// it is what wears the accent.
    /// <para>A PANE and not the group, and not a control: controls are thrown away and rebuilt from the model, and a
    /// GROUP is not permanent either - dropping a tab into another panel merges it away. Measured: a tab torn into a
    /// window and then dropped onto another one left the accent pointing at a group that no longer existed, so nothing
    /// was lit until the next click. A pane id survives every move, which is exactly what "what am I working in" needs.
    /// </para></summary>
    internal void MakeActive(PaneGroup group) => MakeActive(NodeOf(group));

    private void MakeActive(PaneGroupNode node) => MakeActive(ActivePaneOf(node));

    private void MakeActive(string paneId)
    {
        if (paneId == null) return;

        // Remembered PER WINDOW as well: coming back to a window has to restore the pane that was being worked in
        // THERE, not hand the accent to whichever panel that window happens to build first.
        if (Layout.FindGroup(paneId) is { } home && Layout.RootOf(home) is { } root)
        {
            Owner._activeByRoot[root] = paneId;
        }

        if (paneId == Owner._activePane) return;

        Owner._activePane = paneId;
        Owner.SyncActive();
    }

    // The pane a group is CURRENTLY showing - what activating that panel means.
    private static string ActivePaneOf(PaneGroupNode node)
    {
        if (node == null || node.IsEmpty) return null;

        var index = node.ActiveIndex;
        return index >= 0 && index < node.PaneIds.Count ? node.PaneIds[index] : node.PaneIds[0];
    }

    // Whether this group is the one holding the active pane.
    private bool IsActiveGroup(PaneGroupNode group)
    {
        return Owner._activePane != null && group != null && group.PaneIds.Contains(Owner._activePane);
    }

    // A window becoming the active one makes its panel the active panel: an accent border that only answers to a click
    // INSIDE the panel leaves the whole window looking inactive after it is raised by its caption or by Alt-Tab.
    private void FollowWindowActivation(WindowBase window)
    {
        if (window == null || !_watchedWindows.Add(window)) return;

        window.PropertyChanged += (_, e) =>
        {
            if (e.Property != WindowBase.IsActiveProperty || !window.IsActive) return;

            MakeActive(ActivePaneOf(_root ?? Layout.Main));
        };
    }

    // Which pane of a window wears the accent when that window is raised: the one last worked in there, or - the first
    // time, or after that one has left - whatever the window holds.
    private string ActivePaneOf(DockingRoot root)
    {
        if (root == null) return null;

        if (Owner._activeByRoot.TryGetValue(root, out var remembered)
            && Layout.FindGroup(remembered) is { } home && ReferenceEquals(Layout.RootOf(home), root))
        {
            return remembered;
        }

        foreach (var group in DockingLayout.GroupsIn(root.Content))
        {
            if (ActivePaneOf(group) is { } pane) return pane;
        }

        return null;
    }

    private readonly Dictionary<DockingRoot, string> _activeByRoot = new();

    // Subscribed once per window. The MAIN one is only reachable once this area is in a tree, so it is wired from the
    // layout pass rather than from a constructor that runs before there is a window to speak of.
    private readonly HashSet<WindowBase> _watchedWindows = [];

    // Paints the flag onto whatever controls exist right now, in every window. Not a rebuild: which panel is active
    // changes nothing about the layout, and rebuilding on a mouse press would throw the strip's scroll away mid-click.
    private void SyncActive()
    {
        foreach (var area in Family)
        {
            foreach (var pair in area._groupsByNode)
            {
                pair.Value.IsActive = IsActiveGroup(pair.Key);
            }
        }
    }

    // The pane being worked in - an ID, because it outlives the group holding it (see MakeActive).
    private string _activePane;

    /// <summary>Puts a revealed body away again, leaving the strip.</summary>
    internal void Hide(PaneGroup group)
    {
        var node = NodeOf(group);
        if (node != null && Layout.HideGroup(node)) Rebuild();
    }

    // Click-outside-to-put-away belongs to the FLYOUT (a Popup with KeepOpen=false, see PaneGroup): once the revealed
    // body lives in the window's popup layer, "was the press inside this group" is no longer the right question.

    // --- Closing: what it MEANS differs by pane ---------------------------------------------------------------------
    // A document closed is gone; a tool closed is PUT AWAY and comes back from a menu. One button, two verbs, and the
    // pane's own Kind says which (rule 1.5).

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

    /// <summary>Tools put away and bringable back - what an application lists in a "Windows" menu. Without such a list
    /// a closed tool is unreachable, which is why closing one does not delete it.</summary>
    public IReadOnlyCollection<string> HiddenPanes => Owner._hidden.Keys;

    /// <summary>Raised after a pane has been closed. Anything keeping its own account of what is open (a navigation
    /// region, a "Windows" menu) has to hear it, or a region still believing a closed pane open reuses its view model
    /// on the next navigation and opens nothing.</summary>
    public event EventHandler<PaneClosedEventArgs> PaneClosed;

    /// <summary>Asked BEFORE a pane is closed; set <see cref="PaneClosingEventArgs.Cancel"/> to refuse. Every close goes
    /// through here - the tab's own button, the caption's, and each pane of a bulk close - so "do not close unsaved
    /// work" is stated once and holds everywhere.
    /// <para>The handler returns a TASK, and the area waits for it. That is the whole point: an application that wants
    /// to ASK THE USER answers only when the user has answered, and a plain <c>EventHandler</c> - which must return at
    /// once - has nowhere to wait. A handler with nothing to wait for returns <c>Task.CompletedTask</c>.</para></summary>
    public event Func<object, PaneClosingEventArgs, Task> PaneClosing;

    // Handlers are asked ONE AT A TIME, not with WhenAll: each may put a dialog on screen, and two dialogs at once is
    // not a question, it is a pile-up.
    private async Task AskHandlers(PaneClosingEventArgs args)
    {
        if (Owner.PaneClosing is not { } subscribers) return;

        foreach (var handler in subscribers.GetInvocationList())
        {
            await ((Func<object, PaneClosingEventArgs, Task>)handler)(Owner, args);
            if (args.CancelAll) args.Cancel = true;
            if (args.Cancel) return;   // refused - no point asking anyone else about this pane
        }
    }

    internal async Task<bool> ClosePaneAsync(Pane pane) => (await ClosePaneAsync(pane, null)).closed;

    // The shared body. `stop` reports a CancelAll back to a bulk operation, which is the difference between "this one
    // stays" and "stop asking me about the rest".
    private async Task<(bool closed, bool stop)> ClosePaneAsync(Pane pane, object _)
    {
        if (pane?.Id is not { } id) return (false, false);

        var group = Layout.FindGroup(id);
        if (group == null) return (false, false);

        var isTool = pane.Kind == PaneKind.Tool;

        // On the OWNER, like PaneClosed: a pane closes in any window of the layout, and listeners attach to the area.
        var closing = new PaneClosingEventArgs(id, isTool);
        await AskHandlers(closing);
        if (closing.Cancel) return (false, closing.CancelAll);

        // The layout may have moved on while the question was on screen - a dialog is a long time in UI terms.
        if (Layout.FindGroup(id) is not { } stillThere) return (false, false);
        group = stillThere;

        if (isTool)
        {
            Owner._hidden[id] = new HiddenSpot(group, group.PaneIds.IndexOf(id), pane.Zone);
        }
        else
        {
            // A document is gone for good, so nothing should still be able to find its control by id.
            _panesById.Remove(id);
        }

        Layout.RemovePane(id);
        HandOffActive(id, group);
        RebuildFamily();

        // On the OWNER: a pane can be closed in any window of the layout, and listeners attached to the area.
        Owner.PaneClosed?.Invoke(Owner, new PaneClosedEventArgs(id, isTool));
        return (true, false);
    }

    // --- Closing in bulk ----------------------------------------------------------------------------------------------
    // What a tab's context menu is made of. The MENU is the application's - it holds "save", source control, whatever
    // that application has - but these operations are not: without them the application would have to walk the layout
    // itself, and a second way to close a pane is a second set of rules about what closing means.
    //
    // Every one of them closes ONE PANE AT A TIME, in order, awaiting each: the policy (rule 3a) and the refusal
    // (PaneClosing) apply per pane, so a document that says no stays open while the rest still close - and if the
    // application puts a dialog up, the questions come one after another instead of all at once. Each returns how many
    // actually went: "close all" that closed three of five is not a failure, and the caller may want to say so.

    /// <summary>Closes one pane by id. False if there is no such pane or the application refused.</summary>
    public async Task<bool> ClosePaneAsync(string paneId)
    {
        return paneId != null && _panesById.TryGetValue(paneId, out var pane) && await ClosePaneAsync(pane);
    }

    /// <summary>Closes every pane of the PANEL holding <paramref name="paneId"/> - the "close all tabs" of that
    /// panel.</summary>
    public Task<int> ClosePanesOfGroupAsync(string paneId) => CloseAllAsync(PanesBesideAndIncluding(paneId, keep: null));

    /// <summary>Closes every pane of that panel EXCEPT the named one.</summary>
    public Task<int> CloseOtherPanesAsync(string paneId) => CloseAllAsync(PanesBesideAndIncluding(paneId, keep: paneId));

    /// <summary>Closes every UNPINNED pane of that panel (see <see cref="TabItem.IsPinned"/>); pinned tabs stay, which
    /// is what pinning them was for.</summary>
    public Task<int> CloseUnpinnedPanesAsync(string paneId)
    {
        var ids = PanesBesideAndIncluding(paneId, keep: null);
        ids?.RemoveAll(id => _panesById.TryGetValue(id, out var pane) && pane.IsPinned);
        return CloseAllAsync(ids);
    }

    /// <summary>Closes every pane of this layout, in every window of it. Panes still refuse one by one.</summary>
    public Task<int> CloseAllPanesAsync() => CloseAllAsync([.. Owner._panesById.Keys]);

    // A COPY of the ids, never the model's own list: closing rewrites it as we go.
    private List<string> PanesBesideAndIncluding(string paneId, string keep)
    {
        if (paneId == null || Layout.FindGroup(paneId) is not { } group) return null;

        var ids = new List<string>(group.PaneIds);
        if (keep != null) ids.Remove(keep);
        return ids;
    }

    private async Task<int> CloseAllAsync(List<string> ids)
    {
        if (ids == null) return 0;

        var closed = 0;
        foreach (var id in ids)
        {
            if (!_panesById.TryGetValue(id, out var pane)) continue;

            var (wentAway, stop) = await ClosePaneAsync(pane, null);
            if (wentAway) closed++;
            if (stop) break;   // the user said stop, not "keep this one" - asking about the rest would be badgering
        }
        return closed;
    }

    /// <summary>Brings a put-away tool back - to the group it was in, or, if that group has since died, to the zone its
    /// author gave it. Returns false for an id that is not put away.</summary>
    public bool RestorePane(string paneId)
    {
        if (paneId == null || !Owner._hidden.Remove(paneId, out var spot)) return false;

        var home = spot.Group;
        if (home is { Parent: not null } || Layout.IsDocument(home))
        {
            home.Insert(System.Math.Min(spot.Index, home.PaneIds.Count), paneId);
        }
        else if (spot.Zone is DockZone.Center && Layout.ActiveWellGroup(Owner._activePane) is { } documents)
        {
            documents.Add(paneId);
        }
        else
        {
            var group = new PaneGroupNode();
            group.Add(paneId);
            Layout.DockBeside(RootContent, spot.Zone is DockZone.None or DockZone.Center ? DockZone.Right : spot.Zone,
                group, PaneLength.Pixels(EdgeDockSize));
        }

        Layout.Normalize();
        RebuildFamily();
        return true;
    }

    // --- Opening and closing panes from CODE ------------------------------------------------------------------------
    // The same operations the gestures use, reachable by a view model - there is deliberately no second path in.

    /// <summary>Puts a pane into the layout: the DOCUMENT WELL for <see cref="DockZone.Center"/>, the panel already on
    /// that side otherwise, or a new one there. Returns the pane's id.</summary>
    public string AddPane(Pane pane, DockZone zone = DockZone.Center)
    {
        if (pane == null) return null;

        EnsureLayout();

        var id = EnsureId(pane);
        RegisterPane(id, pane);

        // Asked before this area has read its own markup (a region adapter attaches first - see EnsureLayout): the pane
        // waits for it rather than founding a layout of its own that the authored one would then replace.
        if (!_layoutBuilt)
        {
            _deferredPanes.Add((pane, zone));
            return id;
        }

        // Already here: opening it again ACTIVATES it rather than making a second copy of the same thing.
        if (Layout.FindGroup(id) is { } existing)
        {
            Activate(id);
            return id;
        }

        // A pane that may not be docked cannot START docked: the group holding it inherits the refusal and becomes
        // undockable itself (a group goes only where every pane in it may). It opens in a window of its own instead.
        if (zone is DockZone.Floating || (pane.Allowed & (DockZone.Center | DockZone.Edges)) == 0)
        {
            FloatNew(id, pane.Header?.ToString() ?? "Panel");
            return id;
        }

        // No room for a band down that side and no panel there to join: the well takes it. A place that always exists
        // beats carving a sliver out of a centre already at its floor (rule 7.6).
        if (zone is not DockZone.Center && (RoomFor() & zone) == 0
            && Layout.GroupAt(_root ?? Layout.Main, zone) == null)
        {
            zone = DockZone.Center;
        }

        // Into the ACTIVE group of the document area - which is the area itself until it has been split (rule 1.6.3).
        if (zone is DockZone.Center && Layout.ActiveWellGroup(Owner._activePane) is { } documents)
        {
            documents.Add(id);
            documents.ActiveIndex = documents.PaneIds.Count - 1;
        }
        else if (Layout.GroupAt(_root ?? Layout.Main, zone) is { } side)
        {
            // That side already has a panel: this becomes a TAB in it. Opening from code cannot see what a new column
            // would cost, and each one took its band off the centre until the layout was a row of slivers.
            side.Add(id);
            side.ActiveIndex = side.PaneIds.Count - 1;

            // A panel that silently gains a tab nobody can see has opened nothing.
            if (side.State == PaneGroupState.Collapsed) Layout.RevealGroup(side);
        }
        else
        {
            var group = new PaneGroupNode();
            group.Add(id);

            var target = RootContent;
            if (target == null)
            {
                Layout.Main.Content = group;
                Layout.DocumentWell ??= group;
            }
            else
            {
                // Aimed at the ROOT, so whichever side was docked LAST is the outer one - the layout's own history,
                // and what Telerik does. A band, not half the area (rule 7.6).
                Layout.DockBeside(target, zone is DockZone.Center or DockZone.None ? DockZone.Right : zone, group,
                    PaneLength.Pixels(EdgeDockSize));
            }

            Layout.Normalize();
        }

        // What has just been opened is what is being worked in.
        Owner._activePane = id;

        RebuildFamily();
        return id;
    }

    /// <summary>Brings a pane to the front of whatever group it is in, revealing that group if it is put away.</summary>
    public bool Activate(string paneId)
    {
        if (Layout.FindGroup(paneId) is not { } group) return false;

        group.ActiveIndex = group.PaneIds.IndexOf(paneId);
        Owner._activePane = paneId;   // navigating to a pane is what makes its panel the one being worked in

        // Shown, not pinned back: navigating to a tool is a glance at it, like clicking its tab on the strip.
        if (group.State == PaneGroupState.Collapsed) Layout.RevealGroup(group);

        RebuildFamily();
        return true;
    }

    /// <summary>Takes a pane out of the layout entirely, by id. What a region removing a view model means.</summary>
    public bool RemovePane(string paneId)
    {
        if (paneId == null) return false;

        // WHERE it lived, asked BEFORE the model forgets: if the accent is on this pane, the panel holding it is still
        // the one being worked in and has to keep it.
        var wasActive = paneId == Owner._activePane;
        var home = wasActive ? Layout.FindGroup(paneId) : null;

        if (!Layout.RemovePane(paneId)) return false;

        _panesById.Remove(paneId);
        if (wasActive) HandOffActive(paneId, home);
        RebuildFamily();
        return true;
    }

    /// <summary>Passes the accent on when the pane carrying it goes away. Called from BOTH ways a pane can leave -
    /// this one and the close path, which removes from the layout itself - because a rule about what closing means has
    /// to hold whichever door was used.
    /// <para>The pane being worked in is remembered as an ID, since the id outlives its group. A CLOSED pane leaves
    /// that id naming nothing at all: no group contains it, so every accent goes out at once while the panel underneath
    /// is plainly still the one in use - its strip has already moved to the next tab, so the tab looks selected inside
    /// a panel that looks inactive, and only a click brings the frame back.</para></summary>
    private void HandOffActive(string paneId, PaneGroupNode home)
    {
        if (paneId == null || paneId != Owner._activePane) return;

        Owner._activePane = null;
        var next = home is { IsEmpty: false } ? ActivePaneOf(home) : null;

        // Its last pane went with it: there is no panel left to be working in, so the accent stays out rather than
        // jumping to some unrelated panel the user never touched.
        if (next != null) Activate(next);
        else Owner.SyncActive();
    }

    // The one place a pane becomes known by id, and where the layout starts listening to it: Allowed is an ordinary
    // property and can be set - or bound - at any moment, including while the pane sits docked.
    private void RegisterPane(string id, Pane pane)
    {
        _panesById[id] = pane;

        // Idempotent: the same pane is registered again on every rebuild that touches it.
        pane.PropertyChanged -= Owner.OnPanePropertyChanged;
        pane.PropertyChanged += Owner.OnPanePropertyChanged;
    }

    private void OnPanePropertyChanged(object sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (e.Property != Pane.AllowedProperty || sender is not Pane pane) return;
        if ((pane.Allowed & (DockZone.Center | DockZone.Edges)) != 0) return;   // still dockable somewhere
        if (pane.Id == null || Layout.FindGroup(pane.Id) == null) return;       // not in the tree: nothing to undo

        // Just said it may not be docked, and it IS docked - so out, the same answer markup and AddPane give. Left
        // alone it would make its whole panel undockable: pullable out, never puttable back.
        var id = pane.Id;
        if (!Layout.RemovePane(id)) return;

        System.Console.WriteLine($"[DockingArea] '{pane.Header}' is now allowed to float and nothing else, so it cannot " +
                                 "stay docked - moving it to a window of its own.");

        RebuildFamily();
        FloatNew(id, pane.Header?.ToString() ?? "Panel");
    }

    // --- Saving and restoring the arrangement -----------------------------------------------------------------------

    /// <summary>The whole arrangement as text: the tree, the edge bars, which panel is put away, which tab is on top,
    /// and where each floating window sits. Panes that say they do not come back (<see cref="Pane.Restore"/> - a
    /// document belongs to a session, not to the workspace) are left out, and so are the groups they emptied.</summary>
    public string SaveLayout()
    {
        EnsureLayout();
        SyncBoundsToModel();

        return DockingLayoutSerializer.Save(Layout,
            keepPane: id => PaneById(id)?.Restore != false,
            restoreKeyOf: id => PaneById(id)?.RestoreKey);
    }

    /// <summary>The view model's handle on this area's arrangement: <c>Workspace="{Binding Workspace}"</c>. The view
    /// model owns the object and calls Save/Load on it; the area never learns WHERE a layout is kept, which is the
    /// application's business.</summary>
    public static readonly AdamantiumProperty WorkspaceProperty = AdamantiumProperty.Register(nameof(Workspace),
        typeof(DockingWorkspace), typeof(DockingArea), new PropertyMetadata(null, OnWorkspaceChanged));

    public DockingWorkspace Workspace
    {
        get => GetValue<DockingWorkspace>(WorkspaceProperty);
        set => SetValue(WorkspaceProperty, value);
    }

    private static void OnWorkspaceChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not DockingArea area) return;

        (e.OldValue as DockingWorkspace)?.Detach(area);
        (e.NewValue as DockingWorkspace)?.Attach(area);
    }

    /// <summary>Raised while a layout is being loaded, for every pane it names that this area does not have. The
    /// application makes the pane from the key it saved, or leaves it null and the layout is applied without it.
    /// <para>Panes opened by CODE - a navigation region, an "open this tool" command - do not exist at start-up, so
    /// without this everything but the authored panels quietly vanished from a restored arrangement.</para></summary>
    public event EventHandler<PaneRestoringEventArgs> PaneRestoring;

    /// <summary>Puts a saved arrangement back. False for text this version cannot read or that names no pane this area
    /// has - the caller then keeps whatever is on screen, which is the authored arrangement on a first run.
    /// <para>Only panes this area already knows are placed: a layout names ids, and an id whose pane does not exist is
    /// dropped rather than conjured. Panes it knows that the file does NOT name stay out of the layout - a saved
    /// arrangement is the whole answer, not a suggestion to merge with.</para></summary>
    public bool LoadLayout(string state)
    {
        EnsureLayout();

        // The panes the file expects but this area has not got are asked for FIRST, so that by the time the tree is
        // applied every id in it stands for something. Asking afterwards would mean applying a layout with holes in it
        // and then patching them, which is two arrangements where there should be one.
        RestoreMissingPanes(state);

        var loaded = DockingLayoutSerializer.Load(state, _panesById.ContainsKey);
        if (loaded?.Main == null) return false;

        // Every window of the OLD arrangement goes first: its roots are about to be replaced, and a satellite left
        // behind would be showing a tree that is no longer part of any layout.
        foreach (var area in Owner._satellites.ToArray())
        {
            var window = area.TakeWindow();
            if (window != null) UIAppContext.Current.Dispatcher.InvokeAsync(window.Close);
        }

        Owner._satellites.Clear();

        // The SAME release every other area gets, not a shorter one. Forgetting the node->control pairing is not enough:
        // the old panels go on holding the very same Pane objects as their items, and a pane held by one panel does not
        // move into another - so the panels rebuilt from the restored arrangement came up with tabs that were empty, and
        // then with no tabs at all. Release hands the panes back first.
        Release();
        _tree = null;

        Layout = loaded;
        Layout.Normalize();


        Rebuild();

        // The floating roots come back as windows, each where it was last seen.
        foreach (var root in Layout.Roots)
        {
            if (root.IsMain) continue;

            var ids = string.Join(",", DockingLayout.PanesIn(root.Content));
            OpenWindowFor(root, TitleOf(root), root.Bounds);
        }

        return true;
    }

    // Asks the application for every pane the file names and this area has not got. A pane it makes is registered here
    // and nowhere else - the layout that follows refers to it by id like any other.
    private void RestoreMissingPanes(string state)
    {

        if (PaneRestoring == null && Owner.PaneRestoring == null) return;

        foreach (var pair in DockingLayoutSerializer.ReadRestoreKeys(state))
        {
            if (_panesById.ContainsKey(pair.Key)) continue;

            var args = new PaneRestoringEventArgs(pair.Key, pair.Value);
            Owner.PaneRestoring?.Invoke(Owner, args);

            if (args.Pane == null) continue;

            args.Pane.Id = pair.Key;   // the layout refers to it by THIS id, whatever the maker called it
            RegisterPane(pair.Key, args.Pane);
        }
    }

    private string TitleOf(DockingRoot root)
    {
        foreach (var id in DockingLayout.PanesIn(root.Content))
        {
            if (_panesById.TryGetValue(id, out var pane)) return pane.Header?.ToString() ?? id;
        }

        return "Panel";
    }

    // Where each floating window is NOW - the one piece of absolute geometry a layout keeps, and the reason a panel
    // left on a second monitor comes back to that monitor. Read at save time, because a window moves without the model
    // hearing about it (the platform's own move loop owns the gesture).
    private void SyncBoundsToModel()
    {
        foreach (var area in Owner._satellites)
        {
            if (area._window is not { } window || area._root == null) continue;

            area._root.Bounds = new Rect(window.Left, window.Top, window.ClientWidth, window.ClientHeight);
        }
    }

    /// <summary>The panel controls this area currently shows - one per group of the arrangement. For a host that needs
    /// to reach the panels themselves (a test, a menu that dresses them), not for driving the layout: what a panel IS
    /// belongs to the model, and this is only the view of it.</summary>
    public IEnumerable<PaneGroup> Groups => _groupsByNode.Values;

    /// <summary>The pane a given id stands for, or null.</summary>
    public Pane PaneById(string paneId)
    {
        return paneId != null && _panesById.TryGetValue(paneId, out var pane) ? pane : null;
    }

    /// <summary>Every pane the layout holds, in tree order - across floating windows too, since they are roots of the
    /// same forest.</summary>
    public IEnumerable<Pane> Panes
    {
        get
        {
            foreach (var root in Layout.Roots)
            {
                foreach (var id in DockingLayout.PanesIn(root.Content))
                {
                    if (_panesById.TryGetValue(id, out var pane)) yield return pane;
                }
            }
        }
    }

    /// <summary>Raised when the pane shown in the document well changes - what a region calls its current view.</summary>
    public event EventHandler ActivePaneChanged;

    internal void RaiseActivePaneChanged() => ActivePaneChanged?.Invoke(this, EventArgs.Empty);

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
        // A tool docking beside the well is worth the same band an edge anchor takes - one number, so a drop makes the
        // band its preview drew.
        Layout.BandLength = PaneLength.Pixels(EdgeDockSize);

        // LEAVERS out of every group first: a pane is one control and lives in one group.
        // One by one, never Clear(): a Reset rebuilds the items panel wholesale, so the tabs go into a brand-new panel
        // while the visual tree carries on arranging the old one (measured by object identity).
        foreach (var pair in _groupsByNode)
        {
            var items = pair.Value.Items;
            for (var i = items.Count - 1; i >= 0; i--)
            {
                if (items[i] is Pane pane && !pair.Key.PaneIds.Contains(pane.Id)) items.RemoveAt(i);
            }
        }

        var visual = BuildVisual(RootContent);

        // The children are the TREE plus the put-away panels of the four edges. Rebuilt as one list, because a panel
        // moves between the two by being put away or pinned, and the two must never both hold it.
        var wanted = new List<IMeasurableComponent>();
        if (visual != null) wanted.Add(visual);

        var root = _root ?? Layout.Main;
        if (root != null)
        {
            foreach (var bar in root.Bars)
            {
                foreach (var node in bar.Value)
                {
                    if (node.IsEmpty) continue;

                    // STATE FIRST, panes second - the same order BuildVisual uses, and for the same reason: filling a
                    // strip that does not yet know it is folded makes it insist on a selection, which is written back
                    // into the model as the active pane. Measured on a RESTORED layout: a panel saved looking at its
                    // second tab came back looking at the first.
                    var strip = GroupFor(node);
                    strip.State = node.State;
                    strip.Edge = bar.Key;
                    strip.Kind = PaneKind.Tool;
                    strip.RevealExtent = FlyoutExtent(node);
                    FillPanes(strip, node);
                    wanted.Add(strip);
                }
            }
        }

        _tree = visual;

        // Only touch the children when they actually differ - an unchanged area must not be torn down and rebuilt.
        if (!SameChildren(Children, wanted))
        {
            Children.Clear();
            foreach (var child in wanted) Children.Add(child);
        }

        Prune();

        // Invalidate AFTER the tree is back together: FillPanes runs while this subtree is detached, and a dirty mark
        // raised then is lost - the dirty queue belongs to the visual root and re-attaching re-registers nothing.
        // Three levels, each measured: the GROUP; its STRIP PANEL (an unmarked panel is never descended into, so a new
        // tab kept the bounds of its previous life); and the TABS (a fold changes each label's rotation and size).
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

        SyncWindowTitle();
    }

    private PaneGroup _titleSource;   // the group whose name the floating window wears, so it can be let go of

    // A floating window showing ONE panel is named by it - the panel gave up its own caption for exactly this. Follows
    // the ACTIVE tab: a title fixed at the tear-off would name whichever pane happened to be showing then.
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

    private bool _overlayShown;

    /// <summary>The compass shown during a docking drag - a control, so a theme can style it.</summary>
    public DockCompass Compass => _compass;

    private DockTarget _target;

    // The overlay covers the WHOLE area, one window for all of it: put up when the pointer enters, taken down when it
    // leaves, and in between it neither moves nor resizes. Sized to the aimed-at GROUP instead, it raced the compass
    // laid out inside it and left the area's own edges unreachable, so an edge anchor had nowhere to go.
    private void ShowOverlay()
    {
        // Position PHYSICAL (a desktop point), size LOGICAL - see WindowBase.Left.
        var origin = this.PointToScreen(Vector2.Zero);
        var bounds = new Rect(0, 0, RenderSize.Width, RenderSize.Height);

        // Built at FULL size before Show: a window sized after creation was never laid out at all (measured -
        // DockCompass.ArrangeOverride ran zero times across a whole drag).
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
    }

    private void HideOverlay() => _compassWindow?.Hide();

    // What the current drag may do, read once per DRAG - asking per mouse move would walk the tree hundreds of times a
    // second - and re-read when a DIFFERENT window is picked up: a floating window can be grabbed by its caption at any
    // time, well away from the code that reads permissions, and a float-only panel was then judged by the last drag's.
    private DockZone _dragAllowed = DockZone.All;
    private WindowBase _draggedWindow;

    /// <summary>Where a node may be docked: what EVERY pane in it allows. One pane forbidding a side forbids it for the
    /// panel - dropping the group would put that pane there too.</summary>
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
            // A PUT-AWAY panel is no place to drop into: only its strip is left, and those strips sit exactly where the
            // real panel behind them is being aimed at, so both crosses would land on top of each other (rule 2.6).
            if (pair.Key.State == PaneGroupState.Collapsed) continue;

            var group = pair.Value;
            var at = (group.PointToScreen(Vector2.Zero) - origin).ToLogical(scale);
            var size = group.RenderSize;
            if (point.X < at.X || point.Y < at.Y || point.X > at.X + size.Width || point.Y > at.Y + size.Height) continue;

            node = pair.Key;
            bounds = new Rect(at.X, at.Y, size.Width, size.Height);
            break;
        }

        // EDGE anchors first: they belong to the AREA and win where they could overlap a group's cross. Asked of the
        // area, not from inside the group loop - the edges are where the put-away strips are, and tying anchors to
        // "found a group here" hid the area's own edge behind one. Offered only while the centre can pay (rule 7.6).
        var edge = DockCompass.EdgeZoneAt(area, point, _compass.IndicatorSize, _compass.EdgeIndicatorInset);
        if (edge != DockZone.None && (allowed & edge) != 0 && (RoomFor() & edge) != 0)
        {
            // A SIDE anchor splits the whole root; a TOP/BOTTOM band splits the centre column only, so it does not run
            // under the sides (rule 2.3).
            var anchor = edge is DockZone.Top or DockZone.Bottom
                ? Layout.BandTarget(_root ?? Layout.Main)
                : RootContent;

            return new DockTarget(anchor, bounds, edge,
                DockCompass.PreviewOf(area, edge, EdgeDockSize), isEdge: true);
        }

        if (node == null) return default;

        var zone = DockCompass.ZoneAt(bounds, point, _compass.IndicatorSize, _compass.IndicatorGap);

        // A forbidden zone arms nothing - DockTarget is only valid with a zone - so no indicator promises a landing
        // that would then be undone.
        if ((allowed & zone) == 0) zone = DockZone.None;

        // The centre's floor (rule 7.6) is NOT asked here. A cross aimed inside the document area divides the AREA
        // ITSELF and both halves stay documents (rule 1.6), so the area is worth exactly what it was; a cross aimed at a
        // tool costs that tool, which the centre has no say in. What does cost the centre is a band arriving from
        // OUTSIDE it - that is the edge anchors above, and they are where the floor is spent.
        return new DockTarget(node, bounds, zone, DockCompass.PreviewOf(bounds, zone));
    }

    // --- Asking the application -------------------------------------------------------------------------------------
    // Two events, not one: refusing a move inside the window and refusing a window of its own are different statements
    // about a pane. Raised on the ACTION, never per mouse move - a question asked hundreds of times a second is not one
    // anybody can answer honestly.

    /// <summary>Raised before panes are docked somewhere. Cancel to refuse the move.</summary>
    public event EventHandler<PaneDockingEventArgs> PaneDocking;

    /// <summary>Raised before panes leave for a window of their own. Cancel to refuse the tear-off.</summary>
    public event EventHandler<PaneTearingOffEventArgs> PaneTearingOff;

    // Asked on the OWNER: a floating area is the same docking system in another window, and an application wires one
    // set of handlers, not one per window it never asked to be opened.
    private bool Refuses(PaneDockingEventArgs args)
    {
        Owner.PaneDocking?.Invoke(Owner, args);
        return args.Cancel;
    }

    private bool Refuses(PaneTearingOffEventArgs args)
    {
        Owner.PaneTearingOff?.Invoke(Owner, args);
        return args.Cancel;
    }

    /// <summary>A pane dragged clear of this area becomes a window of its own - another ROOT of this same layout, which
    /// is what lets it be saved and docked back.</summary>
    internal bool TearOff(Pane pane, TabTearOffEventArgs e)
    {
        // Not allowed to float: the strip keeps it and goes on reordering.
        if (pane == null || (pane.Allowed & DockZone.Floating) == 0) return false;
        if (pane.Id == null) return false;
        if (Refuses(new PaneTearingOffEventArgs([pane.Id], isWholePanel: false))) return false;

        // A REVEALED panel is put away first: the glance is over the moment something is carried off it.
        if (Layout.FindGroup(pane.Id) is { State: PaneGroupState.Revealed } showing) Layout.HideGroup(showing);

        // What the panel it is leaving occupies, read while it still has a size: the window opens at exactly that.
        var was = Layout.FindGroup(pane.Id) is { } home && _groupsByNode.TryGetValue(home, out var homeControl)
            ? homeControl.RenderSize
            : default;

        if (!Layout.RemovePane(pane.Id)) return false;

        var node = new PaneGroupNode();
        node.Add(pane.Id);

        // The new window's CENTRE is what was carried into it, whatever it was where it came from - see
        // DockingLayout.TearOffGroup. Alone in a window, a pane stands in that window's centre and is a document there.
        var root = new DockingRoot(node, isMain: false) { DocumentWell = node };
        Layout.Roots.Add(root);

        // How far along its tab the pointer took hold, read BEFORE the rebuild - the window is then placed so the grip
        // on its caption is the same one, instead of jumping to the middle of a window nobody grabbed there.
        var grabX = e.ScreenPosition.X - pane.PointToScreen(Vector2.Zero).X;

        Rebuild();   // the pane leaves this tree before the floating area claims it: one component, one parent

        var floating = Float(root, pane.Header?.ToString() ?? "Pane", out var pieceWindow, was);
        Show(floating, pieceWindow, grabX);

        // What was just carried out IS what is being worked in. Waiting for the window's activation instead left the
        // torn-off pane selected but with no accent anywhere until it was clicked.
        Owner.MakeActive(node);

        Owner.CloseEmptyWindows();   // it may have been the last pane of a floating window

        e.TornWindow = pieceWindow;
        return true;
    }

    /// <summary>
    /// A whole tool panel was dragged by its CAPTION: the group leaves with every pane it holds, in order, and becomes a
    /// window of its own. Dragging a tab moves ONE pane; dragging the caption moves the panel - they are different moves,
    /// and which one happened must not depend on how many tabs are open.
    /// </summary>
    internal bool TearOffGroup(PaneGroup control, PixelPoint screenPosition)
    {
        var node = NodeOf(control);
        if (node == null) return false;
        if ((AllowedFor(node) & DockZone.Floating) == 0) return false;   // one pane refusing to float holds the panel
        if (Refuses(new PaneTearingOffEventArgs([..node.PaneIds], isWholePanel: true))) return false;

        var title = control.Title?.ToString() ?? "Panel";
        var grabX = screenPosition.X - control.PointToScreen(Vector2.Zero).X;

        // The panel keeps the size it was docked at - read before it leaves the tree.
        var was = control.RenderSize;

        var root = Layout.TearOffGroup(node);
        if (root == null) return false;

        var area = Float(root, title, out var window, was);

        // The group CONTROL travels with its node: the panel keeps its tabs, selection and scroll, and its panes are
        // never moved between two items panels.
        _groupsByNode.Remove(node, out var moved);
        if (moved != null)
        {
            area._groupsByNode[node] = moved;
        }

        Rebuild();   // detaches it from this tree, so the floating area can take it
        Show(area, window, grabX);

        // Carried out by hand, so it is what is being worked in - see TearOff.
        Owner.MakeActive(node);

        Owner.CloseEmptyWindows();
        return true;
    }

    // Opens a floating window for a root, shown through an AREA of its own. size = what the panel occupied where it
    // came from, so the tear-off feels like picking that panel up; zero falls back to the authored default.
    private DockingArea Float(DockingRoot root, string title, out DockingWindow window, Size size = default)
    {
        var area = new DockingArea(Owner, root);
        Owner._satellites.Add(area);

        // A DockingWindow, not a bare Window: what a floating panel's window looks like belongs in one selector.
        window = new DockingWindow
        {
            Title = title,
            ClientWidth = size.Width > 0 ? size.Width : FloatingWindowWidth,
            ClientHeight = size.Height > 0 ? size.Height : FloatingWindowHeight,
            Area = area
        };

        // Closed by its own BUTTON, the window takes its panes with it - otherwise they stay in the MODEL with no window
        // to show them, and navigating to one activates a pane that is nowhere on screen. Kept as a field so the area
        // can take it back: a window we close OURSELVES, replacing one arrangement with another, must not carry the
        // panes off - the new arrangement is about to ask for those very panes.
        area._onWindowClosed = (_, _) => CloseRoot(root);
        window.Closed += area._onWindowClosed;

        area._window = window;
        area.FollowWindowActivation(window);
        return area;
    }

    // Closes everything a floating root holds and drops the root. Each pane closes as it would on its own button, so
    // closing the window and closing its panes one by one are the same statement to everything that listens.
    private void CloseRoot(DockingRoot root)
    {
        if (root == null || !Layout.Roots.Contains(root)) return;

        // Collected BEFORE anything closes - closing a pane edits the tree this walks. Edge bars count: a put-away
        // panel is still in that window, just folded to its strip (rule 3b).
        var ids = new List<string>(DockingLayout.PanesIn(root.Content));
        foreach (var bar in root.Bars.Values)
        {
            foreach (var group in bar) ids.AddRange(group.PaneIds);
        }

        // The window is going either way (the platform has already closed it), so this is not a question - it is the
        // same bookkeeping ClosePaneAsync does, minus the asking. Anything else would leave the layout holding panes
        // whose window no longer exists while a dialog was on screen.
        foreach (var id in ids)
        {
            if (!_panesById.TryGetValue(id, out var pane)) continue;

            var isTool = pane.Kind == PaneKind.Tool;
            if (isTool) Owner._hidden[id] = new HiddenSpot(Layout.FindGroup(id), 0, pane.Zone);
            else _panesById.Remove(id);

            Layout.RemovePane(id);
            Owner.PaneClosed?.Invoke(Owner, new PaneClosedEventArgs(id, isTool));
        }

        Layout.Roots.Remove(root);
        RebuildFamily();
    }

    // A pane opened in a window of its OWN with no gesture behind it - DockZone.Floating from code. Same window, wiring
    // and root a tear-off makes, so it can be dragged back in; it is placed rather than grabbed.
    private void FloatNew(string id, string title)
    {
        var group = new PaneGroupNode();
        group.Add(id);
        group.ActiveIndex = 0;

        // Standing alone in a window means standing in that window's centre, exactly as a torn-off pane does.
        var root = new DockingRoot(group, isMain: false) { DocumentWell = group };
        Layout.Roots.Add(root);

        OpenWindowFor(root, title, default);
        RebuildFamily();
    }

    // Puts a floating ROOT on screen. Shared by "open this in its own window" and by a restored layout, so a window
    // that comes back from a saved file is wired exactly like one that was just torn off - it can be dragged, docked
    // back and closed the same way.
    // at = where it was last seen (a saved layout knows); default cascades off this area's corner instead.
    // TEMPORARY: every window a layout opens or lets go, so the count on screen is diagnosed from a record rather than
    // guessed at.
    private void OpenWindowFor(DockingRoot root, string title, Rect at)
    {

        // A remembered place is only worth using while it still exists: a layout saved with a panel on a second monitor
        // is opened on a machine that no longer has one, and a window put back there is one nobody can reach - not even
        // to close it. Then it cascades, exactly like a window that was never anywhere.
        var placed = at.Width > 0 && at.Height > 0 && PlatformSettings.IsOnScreen(at);
        var area = Float(root, title, out var window, placed ? new Size(at.Width, at.Height) : default);

        UIAppContext.Current.Dispatcher.InvokeAsync(() =>
        {
            window.Content = area;
            window.Show();
            area.Rebuild();

            // Physical pixels either way: a window's position is a desktop point. Without a remembered place they
            // cascade off this area's corner, so two of them do not stack exactly.
            if (placed)
            {
                window.Left = at.X;
                window.Top = at.Y;
            }
            else
            {
                var origin = this.PointToScreen(Vector2.Zero);
                var step = 32 * Owner._satellites.Count;
                window.Left = origin.X + 60 + step;
                window.Top = origin.Y + 60 + step;
            }

            // Without this it would be a window that can never come back: dragged by its caption it aims and docks.
            window.WindowMoving += (_, _) => TrackWindow(window, root);
            window.WindowMoveCompleted += (_, _) => DropWindow(window, root);
        });
    }

    /// <summary>How big a floating window starts when the pane has no size to inherit - one opened from code, never
    /// laid out anywhere. A tear-off uses what the panel occupied instead.</summary>
    public static readonly AdamantiumProperty FloatingWindowWidthProperty = AdamantiumProperty.Register(
        nameof(FloatingWindowWidth), typeof(double), typeof(DockingArea), new PropertyMetadata(480.0));

    public double FloatingWindowWidth
    {
        get => GetValue<double>(FloatingWindowWidthProperty);
        set => SetValue(FloatingWindowWidthProperty, value);
    }

    public static readonly AdamantiumProperty FloatingWindowHeightProperty = AdamantiumProperty.Register(
        nameof(FloatingWindowHeight), typeof(double), typeof(DockingArea), new PropertyMetadata(360.0));

    public double FloatingWindowHeight
    {
        get => GetValue<double>(FloatingWindowHeightProperty);
        set => SetValue(FloatingWindowHeightProperty, value);
    }

    // Puts the floating window under the pointer and hands the still-held button to the platform's move loop. grabX is
    // how far along the caption the pointer took hold.
    private void Show(DockingArea area, DockingWindow window, double grabX)
    {
        var root = area._root;

        // Read once per drag, and kept on the OWNER: the drag is aimed at every area of the family.
        Owner._dragAllowed = AllowedFor(root.Content);

        // Showing a window is UI-thread work while the gesture runs on the loop thread. InvokeAsync, not Invoke:
        // blocking into the pump would stall the very frame the drag is in.
        UIAppContext.Current.Dispatcher.InvokeAsync(() =>
        {
            // The window holds an AREA, not the bare content - that is why a floating panel can be docked INTO: it has
            // its own root, tab strip and compass.
            window.Content = area;
            window.Show();
            area.Rebuild();

            // Clearing the AREA, not just the flag: the compass that is up may belong to another window.
            _targetArea?.ClearAim();
            _targetArea = null;

            // The cursor is read HERE, live - it has moved on since the threshold was crossed. Both sides PHYSICAL (see
            // WindowBase.Left); the caption height is logical, so only it is converted.
            var cursor = Mouse.ScreenCoordinates;
            window.Left = cursor.X - grabX;
            window.Top = cursor.Y - window.TitleBarHeight * DpiScale.Y / 2;

            window.WindowMoving += (_, _) => TrackWindow(window, root);
            window.WindowMoveCompleted += (_, _) => DropWindow(window, root);

            // The platform's own move loop: the window rides under the cursor with Aero Snap intact.
            window.DragMove();
        });
    }

    // Where the pointer is, in this area's coordinates, during a window drag. From the MOUSE, not the window: measured,
    // WindowMoving fires per step while the window's own Left/Top stay where the loop started (813 events, one
    // position), which froze the compass at the start of the drag.
    // PHYSICAL to LOGICAL once, here at the boundary - the two are equal only at 100%, and on a scaled display the
    // difference is why the compass could not be aimed at all on 4K.
    private Vector2 PointerIn()
    {
        return (Mouse.ScreenCoordinates - this.PointToScreen(Vector2.Zero)).ToLogical(DpiScale);
    }

    private Vector2 DpiScale => RootVisual is IWindow window ? window.DpiScale : Vector2.One;

    // Which area of the family the pointer is over, and what a drop there would do. EVERY area is asked: that is what
    // lets a panel be dropped into a floating window as well as into the main one.
    private void TrackWindow(WindowBase window, DockingRoot root)
    {
        DockingArea hit = null;
        var target = default(DockTarget);
        var point = Vector2.Zero;

        // First move of THIS window: read what its panes permit. Once per drag, not per move.
        if (!ReferenceEquals(Owner._draggedWindow, window))
        {
            Owner._draggedWindow = window;
            Owner._dragAllowed = AllowedFor(root.Content);
        }

        // Nowhere to dock AT ALL (a float-only pane): no compass anywhere. A cross whose every petal then declines
        // reads as a broken control rather than as a rule.
        if ((Owner._dragAllowed & (DockZone.Center | DockZone.Edges)) == 0)
        {
            _targetArea?.ClearAim();
            _targetArea = null;
            _target = default;
            return;
        }

        foreach (var area in Family)
        {
            // The window being dragged is not somewhere to drop it - its own area travels with it.
            if (ReferenceEquals(area._window, window)) continue;
            if (area.RootVisual == null) continue;   // not on screen yet: it has no coordinates to convert against

            var at = area.PointerIn();
            var size = area.RenderSize;
            if (at.X < 0 || at.Y < 0 || at.X > size.Width || at.Y > size.Height) continue;

            var resolved = area.Resolve(at, Owner._dragAllowed);
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
        SpringLoad(hit, target, point);
    }

    private DockingArea _targetArea;   // the area whose compass is up; held by the area DRIVING the drag

    // --- Spring loading --------------------------------------------------------------------------------------------
    // Dwelling over someone else's TAB brings that pane forward, so its body can be dropped into. Without it a pane can
    // only be dropped into whatever happened to be showing when the drag started.

    private Pane _springTarget;
    private System.DateTime _springSince;

    /// <summary>How long a drag must dwell over a tab before it is brought forward, in ms. A delay, not an instant
    /// swap: a drag CROSSES the strip on its way to the compass, and the layout would flicker through every pane.</summary>
    public static readonly AdamantiumProperty SpringLoadDelayProperty = AdamantiumProperty.Register(
        nameof(SpringLoadDelay), typeof(double), typeof(DockingArea), new PropertyMetadata(600.0));

    public double SpringLoadDelay
    {
        get => GetValue<double>(SpringLoadDelayProperty);
        set => SetValue(SpringLoadDelayProperty, value);
    }

    private void SpringLoad(DockingArea area, DockTarget target, Vector2 point)
    {
        PaneGroup group = null;
        if (area != null && target.Node is PaneGroupNode node) area._groupsByNode.TryGetValue(node, out group);

        // A dragged window pans a strip exactly as a dragged TAB does, or a pane scrolled out of sight is unreachable.
        if (group != null) area.AutoScrollStrip(group, point);

        var pane = group != null ? area.PaneAt(group, point) : null;

        // Moved to a different tab (or off the strip): the clock starts again. Dwelling is about ONE tab.
        if (!ReferenceEquals(pane, _springTarget))
        {
            _springTarget = pane;
            _springSince = System.DateTime.UtcNow;
            return;
        }

        if (pane == null || pane.IsSelected) return;
        if ((System.DateTime.UtcNow - _springSince).TotalMilliseconds < Owner.SpringLoadDelay) return;

        pane.SpringLoad();

        // Restart the clock, don't clear the target: the pointer is still over this tab, and it would otherwise be
        // re-activated on every move for as long as it stayed there.
        _springSince = System.DateTime.UtcNow;
    }

    // Pans a group's strip when the drag is held near its edge. The rule lives in the strip - a dragged tab and a
    // dragged window must feel the same.
    private void AutoScrollStrip(PaneGroup group, Vector2 point)
    {
        if (group.GetTemplateChild("PART_TabStrip") is not TabStripScroller strip) return;

        var at = (strip.PointToScreen(Vector2.Zero) - this.PointToScreen(Vector2.Zero)).ToLogical(DpiScale);
        var along = strip.Orientation == Orientation.Vertical ? point.Y - at.Y : point.X - at.X;

        strip.PanNear(along, strip.AutoScrollMargin, strip.AutoScrollRate);
    }

    // The pane whose TAB is under a point, in this area's coordinates - or null between tabs.
    private Pane PaneAt(PaneGroup group, Vector2 point)
    {
        var origin = this.PointToScreen(Vector2.Zero);
        var scale = DpiScale;

        for (var i = 0; i < group.Items.Count; i++)
        {
            if (group.ItemContainerGenerator.ContainerFromIndex(i) is not Pane pane) continue;
            if (pane.Visibility != Visibility.Visible) continue;

            var at = (pane.PointToScreen(Vector2.Zero) - origin).ToLogical(scale);
            var size = pane.RenderSize;
            if (point.X < at.X || point.Y < at.Y || point.X > at.X + size.Width || point.Y > at.Y + size.Height) continue;

            return pane;
        }

        return null;
    }

    // Puts this area's compass up (once) and points it at the target, in the area's own coordinates.
    private void Aim(DockTarget target)
    {
        // Touched only when the pointer crosses in or out: showing a window is UI-thread work while this runs on the
        // LOOP thread (measured - every Show from here threw a DispatcherException).
        if (!_overlayShown)
        {
            _overlayShown = true;
            UIAppContext.Current.Dispatcher.InvokeAsync(ShowOverlay);
        }

        // The same answers Resolve arms the drop with, so no indicator promises what a drop then declines. The floor
        // speaks only for the EDGE anchors: those are bands taken OUT of the centre. The cross always offers all four
        // sides - over the document area it divides the area into strictly-document parts (rule 1.6), over a tool it
        // costs that tool.
        _compass.AllowedZones = Owner._dragAllowed;
        _compass.AllowedEdgeZones = Owner._dragAllowed & RoomFor();
        _compass.AimAt(target.Bounds, target.Zone, target.IsEdge, EdgeDockSize);
    }

    // Which zones there is still ROOM for: a side is not offered once it would push the centre under DocumentMinSize
    // (rule 7.6). Tabbing into a group and floating cost the centre nothing, so they are always on offer.
    private DockZone RoomFor()
    {
        // The area as a whole, however many groups it has been split into (rule 1.6): what a tool costs is taken from
        // all of it, not from whichever group happens to be first. THIS window's area - a floating one has its own, and
        // a window with none charges nothing.
        if (VisualOf(Well) is not { } well || well.Bounds.Width <= 0) return DockZone.All;

        // The arrival costs only the DIVIDER, not its whole band: a band that does not fit is squeezed, not refused
        // (PaneHost hands every child its minimum first). Charging the full band left top and bottom never on offer on
        // a normal-height window, and a panel sent there quietly opened in the well instead.
        var cost = DividerThickness;
        var zones = DockZone.Center | DockZone.Floating;

        if (well.Bounds.Width - cost > DocumentMinSize) zones |= DockZone.Left | DockZone.Right;
        if (well.Bounds.Height - cost > DocumentMinSize) zones |= DockZone.Top | DockZone.Bottom;

        return zones;
    }

    private void ClearAim()
    {
        _compass.Clear();
        if (!_overlayShown) return;

        _overlayShown = false;
        UIAppContext.Current.Dispatcher.InvokeAsync(HideOverlay);
    }

    // The floating window was let go: over an indicator, WHATEVER it holds - a pane, a panel, a whole split - comes
    // back into the tree it was dropped on and the window closes.
    private void DropWindow(WindowBase window, DockingRoot root)
    {
        TrackWindow(window, root);

        var target = _target;
        var area = _targetArea;
        _target = default;
        _targetArea = null;
        Owner._draggedWindow = null;   // this drag is over; the next one re-reads what its panes permit
        area?.ClearAim();

        if (!target.IsValid || area == null) return;

        // BEFORE the model is touched, and of the RECEIVING area: a veto found afterwards would mean undoing a move.
        if (area.Refuses(new PaneDockingEventArgs([..DockingLayout.PanesIn(root.Content)], target.Node, target.Zone))) return;

        // Read BEFORE the move: dropping onto a centre indicator merges the dragged group away entirely (its panes
        // become tabs of the target), and afterwards there is no node left to ask what was being carried.
        var carried = ActivePaneOf(root.Content as PaneGroupNode)
                      ?? DockingLayout.PanesIn(root.Content).FirstOrDefault();

        // An EDGE anchor lands BESIDE what it is aimed at, never inside it: in a floating window the document area IS
        // the whole content, so without saying so a tool dropped on the rim joined the documents and came out dressed
        // as one (rule 1.6 - the cross divides the area, the rim does not).
        if (!Layout.MoveNode(root.Content, target.Node, target.Zone,
                size: target.IsEdge ? PaneLength.Pixels(EdgeDockSize) : null, beside: target.IsEdge)) return;

        // The window gives up its controls BEFORE anything rebuilds: their panes are about to join another tree.
        window.Content = null;
        var donor = FloatingArea(window);
        donor?.Release();



        // What was just dropped is what is being worked in, wherever it ended up - and its WINDOW is what is being
        // worked in too. Without raising it the accent was handed straight back: the emptied window closes, the OS
        // gives focus to whichever window is next in line, and the activation handler follows it. Measured - a tab
        // dropped into the main window lit up and went dark again as a leftover floating window took the focus.
        Owner.MakeActive(carried);
        area.HostWindow?.Activate();

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

    // Gives up every control this area holds - its root has been docked into another area, and a component belongs to
    // ONE tree.
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

    // Rebuilds every area and closes windows whose roots have gone: one drop changes two windows, and both are rebuilt
    // from the model rather than one of them patched.
    private void RebuildFamily()
    {
        // Windows whose ROOT has left the layout are not rebuilt: their panes are already somewhere else, and building
        // them again would hand the SAME content to a second, doomed set of controls. Content is an element and an
        // element has one parent, so the losing copy takes it out of the live tree - measured on a merge of two floating
        // windows: the emptied window rebuilt itself, its tab body left the surviving window, and the tab went blank.
        // They are closed a line later; this only stops them showing anything on the way out.
        foreach (var area in Family)
        {
            if (area._root != null && !Layout.Roots.Contains(area._root)) 
                continue;

            area.Rebuild();
        }

        Owner.CloseEmptyWindows();
    }

    /// <summary>Takes this floating area's window back from it, ready to be closed BY US - so the window's own "the user
    /// closed me" handler does not run and does not carry the panes off with it. The panes stay in the layout: replacing
    /// one arrangement with another is not the same statement as a person shutting a window.
    /// <para>Measured on a second restore: the panes recreated moments earlier were struck off as those windows closed,
    /// and the arrangement then opened its floating roots with nothing in them.</para></summary>
    private WindowBase TakeWindow()
    {
        var window = _window;
        _window = null;

        if (window != null && _onWindowClosed != null) window.Closed -= _onWindowClosed;
        _onWindowClosed = null;

        Release();
        return window;
    }

    /// <summary>Gives up every floating window this area opened. Called when the area stops being the one its workspace
    /// serves - a view rebuilt on re-entry brings a NEW area, and the old one is still holding the windows it opened.
    /// Without this each visit opened the floating roots again on top of the ones already on screen (measured: three
    /// visits, six windows).</summary>
    internal void ReleaseFloatingWindows()
    {
        foreach (var area in _satellites.ToArray())
        {
            var window = area.TakeWindow();
            if (window != null) UIAppContext.Current.Dispatcher.InvokeAsync(window.Close);
        }

        _satellites.Clear();
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


    // Forgets the controls of nodes the layout no longer holds, or they would be emptied on every rebuild forever.
    // Asked of the MODEL: whether a control has a parent yet depends on timing, and a timing-dependent rule eventually
    // deletes a live group. Only THIS area's root counts - a group that moved to a floating window is that area's now.
    // How many model nodes this area still holds a control for. Tests only: a layout that keeps growing these after
    // panels have come and gone is holding controls for nodes that no longer exist.
    internal int TrackedGroups => _groupsByNode.Count;
    internal int TrackedHosts => _hostsByNode.Count;

    private void Prune()
    {
        var groups = new HashSet<PaneGroupNode>();
        var splits = new HashSet<PaneSplitNode>();
        CollectNodes(RootContent, groups, splits);

        // PUT-AWAY panels are alive too, just not in the tree (rule 3b) - asking only the tree threw away their tabs.
        if ((_root ?? Layout.Main) is { } root)
        {
            foreach (var bar in root.Bars.Values)
            {
                foreach (var node in bar) groups.Add(node);
            }
        }

        Forget(_groupsByNode, groups);
        Forget(_hostsByNode, splits);   // splits die too: one collapses away every time a row is left with one child
    }

    private static void Forget<TNode, TControl>(Dictionary<TNode, TControl> known, HashSet<TNode> alive)
    {
        List<TNode> gone = null;
        foreach (var node in known.Keys)
        {
            if (!alive.Contains(node)) (gone ??= []).Add(node);
        }

        if (gone == null) return;
        foreach (var node in gone) known.Remove(node);
    }

    private static void CollectNodes(PaneNode node, HashSet<PaneGroupNode> groups, HashSet<PaneSplitNode> splits)
    {
        switch (node)
        {
            case PaneGroupNode group:
                groups.Add(group);
                break;
            case PaneSplitNode split:
                splits.Add(split);
                foreach (var child in split.Children) 
                    CollectNodes(child, groups, splits);
                break;
        }
    }

    // Turns a node into controls: a split becomes a PaneHost with a PaneSplitter in every gap, a group becomes a
    // PaneGroup holding its panes. Real controls rather than hand-arranged groups is what makes boundaries draggable.
    private IMeasurableComponent BuildVisual(PaneNode node)
    {
        switch (node)
        {
            case PaneGroupNode group:
            {
                // An empty group is gone - except the LAST one of the document area, which stays as empty space: closing
                // the last document must not take the centre of the layout with it (rule 1.4). One of two editors side
                // by side is ordinary and does die when emptied.
                var isDocument = Layout.IsDocument(group);
                if (group.IsEmpty && !ReferenceEquals(group, Well)) return null;

                var control = GroupFor(group);

                // STATE FIRST, panes second: filling them decides the selection, and whether a strip may have none is
                // part of the state. The other way round, a folding panel's strip put the highlight back on the FIRST
                // tab and wrote that into the model - so whichever tab you revealed, pinning gave you the first one.
                control.State = group.State;
                control.Edge = DockingLayout.EdgeOf(group);
                control.IsFloatingRoot = _root is { IsMain: false } && ReferenceEquals(RootContent, group);

                // Looks follow the PLACE (rule 1.2): anything INSIDE the document area is dressed as a document - it has
                // no edge there to fold against, so a pin would be a button that cannot do anything. After rule 1.6
                // that is every group of a split area, not just the one - and a window documents were carried into,
                // which is the area away from home rather than a tool.
                control.Kind = isDocument ? PaneKind.Document : PaneKind.Tool;
                ApplyTabPolicy(control);   // after Kind: which half of the policy applies follows from it
                control.IsActive = IsActiveGroup(group);
                control.RevealExtent = FlyoutExtent(group);

                FillPanes(control, group);
                PaneHost.SetPaneLength(control, group.Length);
                return control;
            }

            case PaneSplitNode split:
            {
                // Kept across rebuilds like the groups: a fresh host re-parents every group under it, re-applying
                // templates, so the group gets a NEW items panel while the tree still arranges the old one (measured).
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
                        // The one moment the controls answer back: a finished drag is the user stating a size.
                        var grip = new PaneSplitter();
                        grip.DragCompleted += (_, _) => SyncLengthsToModel();

                        // A divider beside a PUT-AWAY panel does not drag: that panel's size is MEASURED (its strip), so
                        // the model refuses the dragged pixels and the panel just stays stretched. The gap is a seam,
                        // not a handle.
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
                    // Live splitters are KEPT (interchangeable - they carry a position, not an identity), so the one
                    // thing that does change is carried across: folding leaves the shape untouched, and the divider of
                    // a folded panel stayed draggable without this.
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

    // A put-away panel answers for its own size (its strip), so nobody may state one for it.
    private static bool IsPutAway(PaneNode node) => node is PaneGroupNode { State: PaneGroupState.Collapsed };

    // How far a revealed flyout reaches across its edge, in PIXELS. A share means nothing to a flyout - it is placed
    // OVER the layout, not laid out inside it - so a share is spent against the area's own size along that axis.
    private double FlyoutExtent(PaneGroupNode group)
    {
        var length = group.RestoreLength;
        if (length.IsPixel) return length.Value;

        var across = DockingLayout.EdgeOf(group) is DockZone.Left or DockZone.Right
            ? RenderSize.Width
            : RenderSize.Height;

        // A star of 0.25 in a row summing to 1 is a quarter of the area; nothing to go on yet falls back to the band.
        var wanted = across * length.Value;
        return wanted > 1 ? wanted : EdgeDockSize;
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
        => SameChildren(host.Children, wanted);

    private static bool SameChildren(IList<IMeasurableComponent> children, List<IMeasurableComponent> wanted)
    {
        if (children.Count != wanted.Count) return false;

        for (var i = 0; i < wanted.Count; i++)
        {
            // Splitters are interchangeable - they carry no identity, only a position between two neighbours.
            if (wanted[i] is PaneSplitter && children[i] is PaneSplitter) continue;
            if (!ReferenceEquals(children[i], wanted[i])) return false;
        }

        return true;
    }

    // The control for a group node - the one it already had, or a new one for a node a split has just created.
    private PaneGroup GroupFor(PaneGroupNode node)
    {
        if (_groupsByNode.TryGetValue(node, out var existing)) return existing;

        var created = new PaneGroup();
        Track(node, created);
        return created;
    }

    // Pairs a group control with its node - the ONE place, so both cases (a node from a split, a group from markup) get
    // everything. Which tab is active is part of the LAYOUT and has to reach the model, or a revealed third tab came
    // back as the first one on pin. Closed over the node: the control travels WITH it into a floating window, where
    // this dictionary knows neither. A -1 is a folded panel saying "none", not an opinion, so it is not recorded.
    private void Track(PaneGroupNode node, PaneGroup control)
    {
        _groupsByNode[node] = control;

        // A NAMED handler that closes over nothing, subscribed with a remove first so it can never stack. Every rebuild
        // that touches a group calls this again; an anonymous lambda would add one more subscription each time and there
        // would be no way to take any of them off.
        // It carries no node either: which node a control stands for is looked up when the event FIRES. A handler closed
        // over the node kept writing into the node its control used to show, so choosing a tab moved the selection of a
        // panel nobody had touched - two zones' indicators sliding together.
        control.SelectionChanged -= OnGroupSelectionChanged;
        control.SelectionChanged += OnGroupSelectionChanged;
    }

    private void OnGroupSelectionChanged(object sender, EventArgs e)
    {
        if (sender is PaneGroup control && control.SelectedIndex >= 0 && NodeOfControl(control) is { } node)
        {
            node.ActiveIndex = control.SelectedIndex;
        }

        Owner.RaiseActivePaneChanged();
    }

    /// <summary>Which node a group control currently stands for - asked at the moment it matters, never remembered in a
    /// closure that a rebuild can leave pointing at the wrong one. A handful of groups, so the walk is nothing.</summary>
    private PaneGroupNode NodeOfControl(PaneGroup control)
    {
        foreach (var pair in _groupsByNode)
        {
            if (ReferenceEquals(pair.Value, control)) return pair.Key;
        }
        return null;
    }

    private void FillPanes(PaneGroup control, PaneGroupNode node)
    {
        // To the model's order, moving only what is out of place: the panel keeps its children and its identity.
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
            // A PUT-AWAY panel has no selection - a highlighted tab would claim a panel is open when none is. Said here
            // as well as in SyncFold: a rebuild that does not CHANGE the state never re-runs the fold, and this line
            // would put the highlight straight back (measured).
            control.SelectedIndex = node.State == PaneGroupState.Collapsed
                ? -1
                : System.Math.Clamp(node.ActiveIndex, 0, control.Items.Count - 1);
        }

        control.InvalidateMeasure();
    }

    /// <summary>Space left between two neighbours for the divider that will sit there.</summary>
    public double DividerThickness { get; set; } = 4.0;

    // --- Tab policy -------------------------------------------------------------------------------------------------
    // How the tabs of a panel behave, stated on the AREA and pushed onto every group it builds (like Kind, Edge and
    // State). Here rather than in the theme because this is a decision about BEHAVIOUR - is a lone tab a title - and the
    // theme should only have to say what that looks like. A host changes it in markup or code without restyling
    // anything or copying a control template. Whether a tab carries a CLOSE button is not here: that belongs to the
    // pane, one answer per panel (Pane.IsClosable), not one answer for every document in the area.

    /// <summary>Whether the ONLY document tab fills its strip, reading as the title of what is open. A second document
    /// makes them ordinary tabs again. On by default; tools keep plain tabs, their name is on their caption.</summary>
    public static readonly AdamantiumProperty StretchSingleDocumentTabProperty = AdamantiumProperty.Register(
        nameof(StretchSingleDocumentTab), typeof(bool), typeof(DockingArea),
        new PropertyMetadata(true, OnTabPolicyChanged));

    // Where the selection bar of a panel's strip runs, and how thick it is - the same two properties every TabControl
    // has, said ONCE for the whole area. Nullable, and that is the point: unset means the area has NO opinion and does
    // not touch its groups, so the theme's look stands and a group that states its own keeps it. Only a value here is
    // pushed down, and then it is pushed to all of them, which is what "the area decides" has to mean.
    public static readonly AdamantiumProperty SelectionIndicatorPlacementProperty = AdamantiumProperty.Register(
        nameof(SelectionIndicatorPlacement), typeof(TabIndicatorPlacement?), typeof(DockingArea),
        new PropertyMetadata(null, OnTabPolicyChanged));

    public static readonly AdamantiumProperty SelectionIndicatorThicknessProperty = AdamantiumProperty.Register(
        nameof(SelectionIndicatorThickness), typeof(double?), typeof(DockingArea),
        new PropertyMetadata(null, OnTabPolicyChanged));

    /// <summary>Whether pinned tabs get a row of their own in every panel of this area, or share the one row. Unset
    /// (default): each panel keeps what the theme says.</summary>
    public static readonly AdamantiumProperty PinnedTabsPlacementProperty = AdamantiumProperty.Register(
        nameof(PinnedTabsPlacement), typeof(PinnedTabsPlacement?), typeof(DockingArea),
        new PropertyMetadata(null, OnTabPolicyChanged));

    public PinnedTabsPlacement? PinnedTabsPlacement
    {
        get => GetValue<PinnedTabsPlacement?>(PinnedTabsPlacementProperty);
        set => SetValue(PinnedTabsPlacementProperty, value);
    }

    public bool StretchSingleDocumentTab
    {
        get => GetValue<bool>(StretchSingleDocumentTabProperty);
        set => SetValue(StretchSingleDocumentTabProperty, value);
    }

    /// <summary>Which side of a panel's tab strip the selection bar runs along, for every panel of this area. Unset
    /// (default): each strip keeps what the theme or the group itself says.</summary>
    public TabIndicatorPlacement? SelectionIndicatorPlacement
    {
        get => GetValue<TabIndicatorPlacement?>(SelectionIndicatorPlacementProperty);
        set => SetValue(SelectionIndicatorPlacementProperty, value);
    }

    /// <summary>How thick that bar is, for every panel of this area. Unset (default): the theme's thickness.</summary>
    public double? SelectionIndicatorThickness
    {
        get => GetValue<double?>(SelectionIndicatorThicknessProperty);
        set => SetValue(SelectionIndicatorThicknessProperty, value);
    }

    // The policy as PLAIN FIELDS, mirrored from the properties above. Building a panel must never READ a property of
    // the area: SetValue holds the component's lock across its callbacks, so while a layout load runs inside
    // Workspace's callback on the loop thread and waits for the dispatcher, the pump thread rebuilding panels would
    // wait for that same lock - measured as a hang the moment the docking view was shown.
    private bool _stretchSingleDocumentTab = true;
    private TabIndicatorPlacement? _indicatorPlacement;
    private double? _indicatorThickness;
    private PinnedTabsPlacement? _pinnedPlacement;

    // Pushed onto the live controls at once - the policy is about panels that already exist, and waiting for the next
    // rebuild would leave the setting apparently ignored.
    private static void OnTabPolicyChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not DockingArea area) return;

        // GetValue, not e.NewValue: that is the raw slot, not the effective value. Re-entrant here - this callback
        // already runs under the area's own lock, on the thread that took it.
        area._stretchSingleDocumentTab = area.StretchSingleDocumentTab;
        area._indicatorPlacement = area.SelectionIndicatorPlacement;
        area._indicatorThickness = area.SelectionIndicatorThickness;
        area._pinnedPlacement = area.PinnedTabsPlacement;

        foreach (var member in area.Family)
        {
            foreach (var pair in member._groupsByNode) member.ApplyTabPolicy(pair.Value);
        }
    }

    // The area's tab policy, as it applies to THIS group - which side of rule 1.2 it is on decides what applies.
    private void ApplyTabPolicy(PaneGroup control)
    {
        var isDocument = control.Kind == PaneKind.Document;
        var owner = Owner;

        // A DOCUMENT group allows close buttons on its tabs; which of its panes actually carry one is each pane's own
        // answer (Pane.IsClosable). A TOOL group never does: its close button is on the CAPTION, and it already closes
        // the selected pane rather than the group - a second button beside it would say the same thing twice.
        control.ShowCloseButton = isDocument;

        // Pinning is a DOCUMENT affordance too: it is about keeping one of many things that come and go. A tool panel
        // holds a handful of tabs that are all part of the workspace, so there is nothing there to single out.
        control.ShowPinButton = isDocument;
        control.StretchSingleTab = isDocument && owner._stretchSingleDocumentTab;

        if (owner._indicatorPlacement is { } placement) control.SelectionIndicatorPlacement = placement;
        if (owner._indicatorThickness is { } thickness) control.SelectionIndicatorThickness = thickness;
        if (owner._pinnedPlacement is { } pinnedPlacement) control.PinnedTabsPlacement = pinnedPlacement;
    }

    /// <summary>How wide a pane docked to an EDGE starts out, in pixels along that edge's axis. A band, not half the
    /// area; in pixels so a side panel keeps its width while the window resizes around it.</summary>
    public static readonly AdamantiumProperty EdgeDockSizeProperty = AdamantiumProperty.Register(
        nameof(EdgeDockSize), typeof(double), typeof(DockingArea), new PropertyMetadata(240.0));

    public double EdgeDockSize
    {
        get => GetValue<double>(EdgeDockSizeProperty);
        set => SetValue(EdgeDockSizeProperty, value);
    }

    /// <summary>The floor under the DOCUMENT WELL along either axis (rule 7.6): the centre pays for every tool that
    /// docks against it, and without a floor enough of them squeeze it out of existence.</summary>
    public static readonly AdamantiumProperty DocumentMinSizeProperty = AdamantiumProperty.Register(
        nameof(DocumentMinSize), typeof(double), typeof(DockingArea), new PropertyMetadata(200.0));

    public double DocumentMinSize
    {
        get => GetValue<double>(DocumentMinSizeProperty);
        set => SetValue(DocumentMinSizeProperty, value);
    }

    private bool _layoutBuilt;

    // Panes opened from code BEFORE the markup's children arrived - see EnsureLayout.
    private readonly List<(Pane Pane, DockZone Zone)> _deferredPanes = [];

    /// <summary>
    /// Builds the layout from the authored groups ONCE - everything after that is the layout's own history, and
    /// rebuilding from markup would throw away what the user arranged.
    /// </summary>
    /// <param name="fromLayoutPass">Called from measure/arrange, where the markup has certainly been applied: an area
    /// with no authored groups at all is then genuinely empty, rather than merely not filled in yet.
    /// <para>The difference is not academic. The generated view sets RegionName on the area and only THEN adds its
    /// panes, so a region adapter attaches to an area that has no children yet - and on a SECOND visit that adapter
    /// already knows which panes are open and opens them immediately. Built from that moment, the layout would be built
    /// out of nothing: no main root (an NRE the first time a pane was opened into it) and the authored panels gone.</para></param>
    private void EnsureLayout(bool fromLayoutPass = false)
    {
        if (_layoutBuilt) return;

        // Nothing authored YET and nobody has laid us out: stay unbuilt and let the caller wait for the markup.
        if (Children.Count == 0 && !fromLayoutPass) return;

        _layoutBuilt = true;

        var declarations = new List<ZoneDeclaration>();

        // Panes the author docked but whose own rules forbid docking: one of them makes the whole panel undockable, so
        // they are taken out and opened where they ARE allowed to be - a window of their own.
        var floatOnly = new List<(PaneGroup Group, Pane Pane)>();

        foreach (var child in Children)
        {
            if (child is not PaneGroup group) continue;

            // A group node holds the ids of its PANES: a gesture moves one pane, so a pane is the smallest nameable
            // thing in the model.
            var node = new PaneGroupNode();
            foreach (var item in group.Items)
            {
                if (item is not Pane pane) continue;

                if ((pane.Allowed & (DockZone.Center | DockZone.Edges)) == 0)
                {
                    floatOnly.Add((group, pane));
                    continue;
                }

                var id = EnsureId(pane);
                RegisterPane(id, pane);
                node.Add(id);
            }

            if (node.IsEmpty) continue;

            Track(node, group);
            declarations.Add(new ZoneDeclaration(group.Zone, node, group.Size));
        }

        // Said out loud, not thrown: a layout that refuses to appear over one misplaced pane helps nobody, and a pane
        // that silently vanished helps less.
        foreach (var (group, pane) in floatOnly)
        {
            group.Items.Remove(pane);
            System.Console.WriteLine($"[DockingArea] '{pane.Header}' is allowed to float and nothing else, so it cannot " +
                                     "be authored inside a docked panel - opening it in a window of its own instead.");
        }

        if (declarations.Count == 0)
        {
            // An area with no authored groups is legitimate - a region fills it and nothing else. It still needs its
            // MAIN root: that is where a pane opened from code goes, and a layout without one is not a layout.
            if (Layout.Main == null) Layout.Roots.Add(new DockingRoot(null, isMain: true));

            OpenDeferredPanes();
            return;
        }

        Layout = DockingLayout.FromZones(declarations);

        // The built tree is about to take the authored groups, and a component belongs to one visual tree. Here rather
        // than in Rebuild, which must not tear the tree down later: that re-applies templates and orphans items panels.
        Children.Clear();

        Rebuild();

        // After the tree exists, so the windows open beside a layout rather than instead of one.
        foreach (var (_, pane) in floatOnly)
        {
            var id = EnsureId(pane);
            RegisterPane(id, pane);
            FloatNew(id, pane.Header?.ToString() ?? "Panel");
        }

        OpenDeferredPanes();
    }

    // The panes that asked to be opened while the area was still empty, now that it knows its own shape.
    private void OpenDeferredPanes()
    {
        if (_deferredPanes.Count == 0) return;

        var waiting = _deferredPanes.ToArray();
        _deferredPanes.Clear();

        foreach (var (pane, zone) in waiting) AddPane(pane, zone);
    }

    // A pane's id, made from its header when the author gave none - from the header, not the position, so a saved
    // layout survives the panes being declared in a different order.
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
