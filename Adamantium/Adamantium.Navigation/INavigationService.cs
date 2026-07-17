using System;
using System.Threading;
using System.Threading.Tasks;

namespace Adamantium.Navigation;

/// <summary>The view-model-facing navigation façade: region/view navigation (delegating to <see cref="DefaultRegion"/>),
/// and window navigation that opens a new window from a view model without it ever touching a UI type. A permanent member
/// of the application; view models inject it.</summary>
public interface INavigationService
{
    IRegion DefaultRegion { get; }
    IRegionManager Regions { get; }

    Task<NavigationResult> NavigateToAsync(Type viewModelType, NavigationParameters parameters = null, CancellationToken cancellationToken = default);
    Task<NavigationResult> NavigateToAsync<TViewModel>(NavigationParameters parameters = null, CancellationToken cancellationToken = default);
    Task<NavigationResult> GoBackAsync(CancellationToken cancellationToken = default);
    Task<NavigationResult> GoForwardAsync(CancellationToken cancellationToken = default);
    bool CanGoBack { get; }
    bool CanGoForward { get; }

    /// <summary>Open <paramref name="contentViewModelType"/> in its own window. <paramref name="windowShell"/> names a
    /// registered custom window shell (null = default); the framework creates the window and loads the content view. With
    /// <paramref name="singleInstance"/> = true, if a window for this content type is already open it is brought to the
    /// front instead of opening another (default false = a new instance every call).</summary>
    Task<NavigationResult> OpenWindowAsync(Type contentViewModelType, NavigationParameters parameters = null, string windowShell = null, bool singleInstance = false, CancellationToken cancellationToken = default);
    Task<NavigationResult> OpenWindowAsync<TContentViewModel>(NavigationParameters parameters = null, string windowShell = null, bool singleInstance = false, CancellationToken cancellationToken = default);
    Task CloseWindowAsync(object contentViewModel);
}
