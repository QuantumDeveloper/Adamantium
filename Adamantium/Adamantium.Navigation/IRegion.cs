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

    /// <summary>Which VIEW of <see cref="CurrentViewModel"/> is shown, or null for its default one.
    /// <para>A view-model may have several views registered against it - the same object read through different markup,
    /// split up because one file was too long to follow. Navigating between those views changes only this, so an adapter
    /// that watches <see cref="CurrentViewModel"/> alone would see nothing happen and keep showing the old one.</para></summary>
    string CurrentViewKey { get; }

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

    /// <summary>Navigate to a NAMED view of a view-model. Use it when one view-model is read through several views: the
    /// target may be the very same instance that is already shown, and only the view changes.</summary>
    Task<NavigationResult> NavigateToViewAsync(Type viewModelType, string viewKey, NavigationParameters parameters = null, CancellationToken cancellationToken = default);

    /// <summary>Navigate to a NAMED view of a view-model - see <see cref="NavigateToViewAsync(Type, string, NavigationParameters, CancellationToken)"/>.</summary>
    Task<NavigationResult> NavigateToViewAsync<TViewModel>(string viewKey, NavigationParameters parameters = null, CancellationToken cancellationToken = default);

    /// <summary>Show THIS view-model through a named view. The instance is given rather than resolved, which is what an
    /// object that owns the region needs: asking the container for its own type while its constructor is still running
    /// either recurses or hands back a second copy, and the caller already holds the one it means.</summary>
    Task<NavigationResult> NavigateToInstanceAsync(object viewModel, string viewKey, NavigationParameters parameters = null, CancellationToken cancellationToken = default);
    Task<NavigationResult> GoBackAsync(CancellationToken cancellationToken = default);
    Task<NavigationResult> GoForwardAsync(CancellationToken cancellationToken = default);

    void Add(object viewModel);
    void Remove(object viewModel);
    void Activate(object viewModel);
    void Deactivate(object viewModel);

    event EventHandler<RegionNavigationEventArgs> Navigated;
    event EventHandler ActiveViewsChanged;
}
