using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Adamantium.Navigation;

/// <summary>A control-agnostic navigation target. It tracks view MODELS (never a UI type); a region ADAPTER binds it to a
/// concrete host control (ContentControl, TabControl/Selector, a future DockingControl). Navigation is typed
/// (<c>NavigateToAsync&lt;TViewModel&gt;()</c>) and driven entirely from view models.</summary>
public interface IRegion : INotifyPropertyChanged
{
    string Name { get; }

    /// <summary>The selected/shown view model. A ContentControl adapter renders exactly this; a Selector adapter renders
    /// <see cref="ActiveViewModels"/> and keeps its selection synced to this.</summary>
    object CurrentViewModel { get; }

    /// <summary>Every view model present in the region (for a TabControl: the open tabs).</summary>
    IReadOnlyList<object> ActiveViewModels { get; }

    /// <summary>Single-content behaviour: navigating REPLACES (removes the previous active) instead of accumulating. A
    /// ContentControl adapter sets this true on attach; a Selector adapter leaves it false (tabs accumulate).</summary>
    bool SingleActiveView { get; set; }

    INavigationJournal Journal { get; }
    bool CanGoBack { get; }
    bool CanGoForward { get; }

    Task<NavigationResult> NavigateToAsync(Type viewModelType, NavigationParameters parameters = null, CancellationToken cancellationToken = default);
    Task<NavigationResult> NavigateToAsync<TViewModel>(NavigationParameters parameters = null, CancellationToken cancellationToken = default);
    Task<NavigationResult> GoBackAsync(CancellationToken cancellationToken = default);
    Task<NavigationResult> GoForwardAsync(CancellationToken cancellationToken = default);

    void Add(object viewModel);
    void Remove(object viewModel);
    void Activate(object viewModel);
    void Deactivate(object viewModel);

    event EventHandler<RegionNavigationEventArgs> Navigated;
    event EventHandler ActiveViewsChanged;
}
