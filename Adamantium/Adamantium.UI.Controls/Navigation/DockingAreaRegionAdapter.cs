using System.Collections.Generic;
using Adamantium.Navigation;
using Adamantium.UI.Controls.Docking;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Navigation;

/// <summary>
/// Region on a <see cref="DockingArea"/>: a view model navigated to becomes a <see cref="Pane"/>, and the region's
/// current view is whichever pane is active.
/// <para>Without this the docking area is an island - the main part of an editor's UI, reachable only by hand, while
/// the whole navigation layer (<see cref="INavigationService"/>, <see cref="IViewLocator"/>) goes past it. A
/// <see cref="Selector"/> adapter already covers ONE group of tabs; what an area adds is the choice of PLACE, which a
/// tab strip does not have.</para>
/// <para>Where a view model lands: the DOCUMENT WELL, because that is what opening a document means - unless the view
/// model says otherwise via <see cref="IDockablePane"/>, which is how a tool gets opened against an edge. A pane that is
/// already open is ACTIVATED rather than opened a second time, wherever the user has since moved it.</para>
/// </summary>
public sealed class DockingAreaRegionAdapter : IRegionAdapter
{
    private readonly IViewLocator _viewLocator;

    public DockingAreaRegionAdapter(IViewLocator viewLocator)
    {
        _viewLocator = viewLocator;
    }

    public void Attach(IRegion region, IUIComponent host)
    {
        if (host is not DockingArea area) return;

        // Tabs accumulate: opening a second document does not close the first. That is the whole difference between a
        // docking area and a ContentControl, and stating it here keeps the region from removing what it did not open.
        region.SingleActiveView = false;

        var panesByViewModel = new Dictionary<object, string>();
        var syncing = false;

        void Sync()
        {
            if (syncing) return;
            syncing = true;

            var wanted = new HashSet<object>(region.ActiveViewModels);

            // Gone from the region -> gone from the layout.
            List<object> removed = null;
            foreach (var pair in panesByViewModel)
            {
                if (wanted.Contains(pair.Key)) continue;

                (removed ??= []).Add(pair.Key);
            }

            if (removed != null)
            {
                foreach (var viewModel in removed)
                {
                    area.RemovePane(panesByViewModel[viewModel]);
                    panesByViewModel.Remove(viewModel);
                }
            }

            // New in the region -> a pane in the document well.
            var opened = false;
            foreach (var viewModel in region.ActiveViewModels)
            {
                if (panesByViewModel.ContainsKey(viewModel)) continue;

                var placement = viewModel as IDockablePane;
                panesByViewModel[viewModel] = area.AddPane(PaneFor(viewModel), placement?.PaneZone ?? DockZone.Center);
                opened = true;
            }

            // Putting the region's current view on top is for a sync that OPENED NOTHING - a re-entry, a restore, a
            // region rebuilt around panes that already exist. When this sync has just opened one, the opening already
            // decided what is on top, and the region's "current" is still the view it was on a moment ago: the list of
            // active views changes BEFORE CurrentViewModel does, so activating it here reaches into whatever panel that
            // older view lives in and turns it to that tab. Measured: opening a document in one zone moved a zone
            // nobody had touched, and the change of CurrentViewModel then arrived and put the new document on top
            // anyway - so the line achieved nothing except disturbing the other panel.
            if (!opened
                && region.CurrentViewModel != null
                && panesByViewModel.TryGetValue(region.CurrentViewModel, out var current))
            {
                area.Activate(current);
            }

            syncing = false;
        }

        // NAMED handlers, every one of them, so that all of this can be taken off again. It used to be five lambdas and
        // no way to remove any: a view rebuilt on re-entry hands the region a NEW area while the old one - detached from
        // the tree, but still subscribed - goes on syncing into its own layout. Measured on the stand: two areas alive,
        // and every document opened after that arrived in BOTH, so a zone nobody touched moved to the tab it had just
        // been given.
        void OnActiveViewsChanged(object s, EventArgs e) => Sync();

        void OnRegionPropertyChanged(object s, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(IRegion.CurrentViewModel) || syncing) return;
            if (region.CurrentViewModel == null || !panesByViewModel.TryGetValue(region.CurrentViewModel, out var id)) return;

            syncing = true;
            area.Activate(id);
            syncing = false;
        }

        // A saved layout names panes this region opened, and at start-up none of them exist yet. The key written with
        // them is the view model's TYPE, so the region can make the very same thing again, put it back in itself, and
        // hand the area the pane - which the layout then finds by id like any other.
        void OnPaneRestoring(object s, PaneRestoringEventArgs e)
        {
            if (e.Pane != null || string.IsNullOrEmpty(e.RestoreKey)) return;

            var viewModel = Recreate(e.RestoreKey, e.PaneId);
            if (viewModel == null) return;

            syncing = true;                       // the pane is being made for a layout, not opened into one
            region.Add(viewModel);
            syncing = false;

            var pane = PaneFor(viewModel);
            pane.Id = e.PaneId;
            panesByViewModel[viewModel] = e.PaneId;
            e.Pane = pane;
        }

        // Closing a pane is the user saying that view is done with, so the region must forget it too. Otherwise the
        // region still holds the view model, the next navigation to it REUSES that instance, sees it already "open"
        // and opens nothing at all - a name that can never be reached again once it has been closed.
        void OnPaneClosed(object s, PaneClosedEventArgs e)
        {
            foreach (var pair in panesByViewModel)
            {
                if (pair.Value != e.PaneId) continue;

                panesByViewModel.Remove(pair.Key);
                syncing = true;                 // the pane is already out of the layout; Sync must not take it out twice
                region.Remove(pair.Key);
                syncing = false;
                break;
            }
        }

        // ...and the other way: the user clicking a tab IS navigation, so the region has to hear about it or the two
        // answers - what is on screen and what the journal thinks - drift apart.
        void OnActivePaneChanged(object s, EventArgs e)
        {
            if (syncing) return;

            var active = ActiveViewModel(area, panesByViewModel);
            if (active == null || ReferenceEquals(active, region.CurrentViewModel)) return;

            syncing = true;
            region.Activate(active);
            syncing = false;
        }

        region.ActiveViewsChanged += OnActiveViewsChanged;
        region.PropertyChanged += OnRegionPropertyChanged;
        area.PaneRestoring += OnPaneRestoring;
        area.PaneClosed += OnPaneClosed;
        area.ActivePaneChanged += OnActivePaneChanged;

        // ...and taken off again the moment this area is gone for good. Without it the adapter outlives its control: the
        // region goes on calling into an area nobody can see, which keeps its own layout, its own panes and its own idea
        // of what is open - and every document opened afterwards is added to that one as well as to the live one.
        void Release(ReadOnlySpan<IFundamentalUIComponent> gone)
        {
            // The batch is everything discarded together - our area is at most one of them, so this only asks whether it
            // is in there at all.
            var ours = false;
            foreach (var component in gone)
            {
                if (!ReferenceEquals(component, area)) continue;
                ours = true;
                break;
            }
            if (!ours) return;

            region.ActiveViewsChanged -= OnActiveViewsChanged;
            region.PropertyChanged -= OnRegionPropertyChanged;
            area.PaneRestoring -= OnPaneRestoring;
            area.PaneClosed -= OnPaneClosed;
            area.ActivePaneChanged -= OnActivePaneChanged;
            DiscardedVisuals.Discarded -= Release;
        }

        DiscardedVisuals.Discarded += Release;

        Sync();
    }

    /// <summary>The view model behind the pane the user is looking at, or null when it is a pane the region never
    /// opened (an authored tool panel, say).</summary>
    private static object ActiveViewModel(DockingArea area, Dictionary<object, string> panes)
    {
        foreach (var pane in area.Panes)
        {
            if (!pane.IsSelected) continue;

            foreach (var pair in panes)
            {
                if (pair.Value == pane.Id) return pair.Key;
            }
        }

        return null;
    }

    /// <summary>Wraps a view model in a pane. The BODY is resolved by the view locator - the pane holds the view model
    /// itself and lets the template selector turn it into a view, which is what keeps the region free of UI types.</summary>
    // Makes a view model again from what was saved with its pane. The key is the type; anything the instance itself
    // knew (which page it was showing, say) it restores from its own id - see IRestorablePane.
    private static object Recreate(string restoreKey, string paneId)
    {
        var type = Type.GetType(restoreKey, throwOnError: false);
        if (type == null) return null;

        var context = UIAppContext.Current?.UIContext;
        var viewModel = context != null ? context.Resolve(type) : Activator.CreateInstance(type);

        (viewModel as IRestorablePane)?.RestoreFrom(paneId);
        return viewModel;
    }

    private Pane PaneFor(object viewModel)
    {
        var placement = viewModel as IDockablePane;

        return new Pane
        {
            Id = placement?.PaneId ?? viewModel.GetType().Name,
            Header = placement?.PaneTitle ?? (object)viewModel,
            Allowed = placement?.PaneAllowed ?? DockZone.All,

            // What it takes to make this one again if a saved layout is loaded when it does not exist: its TYPE. The
            // instance restores the rest of itself from its own id.
            RestoreKey = viewModel.GetType().AssemblyQualifiedName,
            Content = viewModel,
            ContentTemplateSelector = new ViewLocatorTemplateSelector(_viewLocator)
        };
    }
}
