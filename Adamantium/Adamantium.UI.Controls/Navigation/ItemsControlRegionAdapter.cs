using System.Collections.Generic;
using System.Collections.ObjectModel;
using Adamantium.Navigation;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Navigation;

/// <summary>Region on a plain <see cref="ItemsControl"/> (no selection): every active view model is projected to its View
/// as an item. For toolbars / lists of simultaneously-visible views.</summary>
public sealed class ItemsControlRegionAdapter : IRegionAdapter
{
    private readonly IViewLocator _viewLocator;

    public ItemsControlRegionAdapter(IViewLocator viewLocator)
    {
        _viewLocator = viewLocator;
    }

    public void Attach(IRegion region, IUIComponent host)
    {
        if (host is not ItemsControl itemsControl) return;

        var items = new ObservableCollection<object>();
        var activeSet = new HashSet<object>();   // reused each sync - reconcile is O(N), not O(N^2) (matters for big lists)
        var presentSet = new HashSet<object>();

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

        itemsControl.ItemsSource = items;
        if (itemsControl.ItemTemplate == null && itemsControl.ItemTemplateSelector == null)
            itemsControl.ItemTemplateSelector = new ViewLocatorTemplateSelector(_viewLocator);

        region.ActiveViewsChanged += (sender, e) => SyncItems();
        SyncItems();
    }
}
