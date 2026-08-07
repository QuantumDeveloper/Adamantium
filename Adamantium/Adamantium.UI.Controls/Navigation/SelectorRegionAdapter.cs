using System.Collections.Generic;
using System.Collections.ObjectModel;
using Adamantium.Navigation;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.Navigation;

/// <summary>Region on a <see cref="Selector"/> (covers <see cref="TabControl"/>): the region's
/// <see cref="IRegion.ActiveViewModels"/> are the items/tabs, and its <see cref="IRegion.CurrentViewModel"/> is kept in
/// two-way sync with <see cref="Selector.SelectedItem"/>. For a TabControl the tab bodies are resolved view-by-view-model.
/// This is "a TabControl driven purely by the navigation manager".</summary>
public sealed class SelectorRegionAdapter : IRegionAdapter
{
    private readonly IViewLocator _viewLocator;

    public SelectorRegionAdapter(IViewLocator viewLocator)
    {
        _viewLocator = viewLocator;
    }

    public void Attach(IRegion region, IUIComponent host)
    {
        if (host is not Selector selector) return;

        var items = new ObservableCollection<object>();
        var activeSet = new HashSet<object>();   // reused each sync - reconcile is O(N), not O(N^2) (matters for big lists)
        var presentSet = new HashSet<object>();
        var syncing = false;

        void SyncItems()
        {
            var active = region.ActiveViewModels;
            activeSet.Clear();
            foreach (var vm in active) activeSet.Add(vm);

            // Drop items that are no longer active (O(1) membership per check).
            for (var i = items.Count - 1; i >= 0; i--)
                if (!activeSet.Contains(items[i])) items.RemoveAt(i);

            // Append newly-active view models in order (O(1) membership via presentSet).
            presentSet.Clear();
            foreach (var it in items) presentSet.Add(it);
            foreach (var vm in active)
                if (presentSet.Add(vm)) items.Add(vm);
        }

        selector.ItemsSource = items;
        // A TabControl renders the selected view model as its BODY (ContentTemplateSelector); its tab HEADERS stay the
        // app's ItemTemplate. Any other Selector (ListBox, ListView, ...) renders each view model as an ITEM, so the
        // view-locator drives ItemTemplateSelector instead - unless the app already provided an item template.
        if (selector is TabControl tab)
            tab.ContentTemplateSelector = new ViewLocatorTemplateSelector(_viewLocator);
        else if (selector.ItemTemplate == null && selector.ItemTemplateSelector == null)
            selector.ItemTemplateSelector = new ViewLocatorTemplateSelector(_viewLocator);

        // A region is resolved by NAME and outlives any host bound to it (GetOrCreateRegion hands back the same instance),
        // so these handlers MUST come off when this host goes away. Without that, re-opening the window left the previous
        // TabControl still subscribed: it kept syncing its own dead item list, and - through SelectionChanged below - kept
        // ACTIVATING view models on the live region, so two controls drove one region and the new tabs came out wrong.
        void OnActiveViewsChanged(object sender, System.EventArgs e)
        {
            if (syncing) return;
            syncing = true;
            SyncItems();
            syncing = false;
        }

        void OnRegionPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(IRegion.CurrentViewModel) || syncing) return;
            syncing = true;
            selector.SelectedItem = region.CurrentViewModel;
            syncing = false;
        }

        void OnSelectionChanged(object sender, System.EventArgs e)
        {
            if (syncing || selector.SelectedItem == null) return;
            syncing = true;
            region.Activate(selector.SelectedItem);
            syncing = false;
        }

        region.ActiveViewsChanged += OnActiveViewsChanged;
        region.PropertyChanged += OnRegionPropertyChanged;
        selector.SelectionChanged += OnSelectionChanged;

        selector.DetachedFromVisualTreeEvent += OnDetached;

        void OnDetached(object sender, VisualTreeAttachmentEventArgs e)
        {
            selector.DetachedFromVisualTreeEvent -= OnDetached;
            region.ActiveViewsChanged -= OnActiveViewsChanged;
            region.PropertyChanged -= OnRegionPropertyChanged;
            selector.SelectionChanged -= OnSelectionChanged;
        }

        SyncItems();
        selector.SelectedItem = region.CurrentViewModel;
    }
}
