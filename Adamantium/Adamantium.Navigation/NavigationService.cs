using System;
using System.Threading;
using System.Threading.Tasks;
using Adamantium.Core.DependencyInjection;

namespace Adamantium.Navigation;

/// <summary>Default navigation service: delegates region/view navigation to <see cref="DefaultRegion"/> and window
/// navigation to the injected <see cref="IWindowNavigationBackend"/>. UI-free.</summary>
public sealed class NavigationService : INavigationService
{
    private const string DefaultRegionName = "__default";

    private readonly IRegionManager _regions;
    private readonly IDependencyResolver _resolver;
    private readonly IWindowNavigationBackend _windowBackend;

    public NavigationService(IRegionManager regions, IDependencyResolver resolver, IWindowNavigationBackend windowBackend)
    {
        _regions = regions;
        _resolver = resolver;
        _windowBackend = windowBackend;
        // Close the manager <-> service cycle: the manager back-fills the service onto its regions.
        if (regions is RegionManager concrete) concrete.NavigationService = this;
    }

    public IRegionManager Regions => _regions;
    public IRegion DefaultRegion => _regions.GetOrCreateRegion(DefaultRegionName);

    public Task<NavigationResult> NavigateToAsync(Type viewModelType, NavigationParameters parameters = null, CancellationToken cancellationToken = default)
        => DefaultRegion.NavigateToAsync(viewModelType, parameters, cancellationToken);

    public Task<NavigationResult> NavigateToAsync<TViewModel>(NavigationParameters parameters = null, CancellationToken cancellationToken = default)
        => DefaultRegion.NavigateToAsync<TViewModel>(parameters, cancellationToken);

    public Task<NavigationResult> GoBackAsync(CancellationToken cancellationToken = default) => DefaultRegion.GoBackAsync(cancellationToken);
    public Task<NavigationResult> GoForwardAsync(CancellationToken cancellationToken = default) => DefaultRegion.GoForwardAsync(cancellationToken);
    public bool CanGoBack => DefaultRegion.CanGoBack;
    public bool CanGoForward => DefaultRegion.CanGoForward;

    public async Task<NavigationResult> OpenWindowAsync(Type contentViewModelType, NavigationParameters parameters = null, string windowShell = null, bool singleInstance = false, CancellationToken cancellationToken = default)
    {
        if (_windowBackend == null)
            return NavigationResult.Failed(new InvalidOperationException("No IWindowNavigationBackend registered."));
        try
        {
            // Single-instance: an existing window of this content type is brought to the front instead of duplicated. The
            // check runs BEFORE resolving a new view model, so no throwaway VM (and its region) is created when one exists.
            if (singleInstance)
            {
                var existing = _windowBackend.TryActivateExisting(contentViewModelType);
                if (existing != null) return NavigationResult.Ok(existing);
            }
            var contentViewModel = _resolver.Resolve(contentViewModelType);
            await _windowBackend.OpenWindowAsync(contentViewModel, parameters, windowShell, cancellationToken);
            return NavigationResult.Ok(contentViewModel);
        }
        catch (Exception ex)
        {
            return NavigationResult.Failed(ex);
        }
    }

    public Task<NavigationResult> OpenWindowAsync<TContentViewModel>(NavigationParameters parameters = null, string windowShell = null, bool singleInstance = false, CancellationToken cancellationToken = default)
        => OpenWindowAsync(typeof(TContentViewModel), parameters, windowShell, singleInstance, cancellationToken);

    public Task CloseWindowAsync(object contentViewModel)
    {
        _windowBackend?.CloseWindow(contentViewModel);
        return Task.CompletedTask;
    }
}
