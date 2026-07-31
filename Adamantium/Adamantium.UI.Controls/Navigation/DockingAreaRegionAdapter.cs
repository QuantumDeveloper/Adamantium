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
            foreach (var viewModel in region.ActiveViewModels)
            {
                if (panesByViewModel.ContainsKey(viewModel)) continue;

                var placement = viewModel as IDockablePane;
                panesByViewModel[viewModel] = area.AddPane(PaneFor(viewModel), placement?.PaneZone ?? DockZone.Center);
            }

            if (region.CurrentViewModel != null && panesByViewModel.TryGetValue(region.CurrentViewModel, out var current))
            {
                area.Activate(current);
            }

            syncing = false;
        }

        region.ActiveViewsChanged += (_, _) => Sync();

        region.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(IRegion.CurrentViewModel) || syncing) return;
            if (region.CurrentViewModel == null || !panesByViewModel.TryGetValue(region.CurrentViewModel, out var id)) return;

            syncing = true;
            area.Activate(id);
            syncing = false;
        };

        // Closing a pane is the user saying that view is done with, so the region must forget it too. Otherwise the
        // region still holds the view model, the next navigation to it REUSES that instance, sees it already "open"
        // and opens nothing at all - a name that can never be reached again once it has been closed.
        area.PaneClosed += (_, e) =>
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
        };

        // ...and the other way: the user clicking a tab IS navigation, so the region has to hear about it or the two
        // answers - what is on screen and what the journal thinks - drift apart.
        area.ActivePaneChanged += (_, _) =>
        {
            if (syncing) return;

            var active = ActiveViewModel(area, panesByViewModel);
            if (active == null || ReferenceEquals(active, region.CurrentViewModel)) return;

            syncing = true;
            region.Activate(active);
            syncing = false;
        };

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
    private Pane PaneFor(object viewModel)
    {
        var placement = viewModel as IDockablePane;

        return new Pane
        {
            Id = placement?.PaneId ?? viewModel.GetType().Name,
            Header = placement?.PaneTitle ?? (object)viewModel,
            Allowed = placement?.PaneAllowed ?? DockZone.All,
            Content = viewModel,
            ContentTemplateSelector = new ViewLocatorTemplateSelector(_viewLocator)
        };
    }
}
