using System;
using System.Threading;

namespace Adamantium.Navigation;

/// <summary>Everything about one navigation, handed to the <see cref="INavigationAware"/> lifecycle and the
/// <see cref="IConfirmNavigation"/> guard. Built by the region before the target is resolved.</summary>
public sealed class NavigationContext
{
    public NavigationContext(IRegion region, INavigationService navigationService, Type targetViewModelType,
        object sourceViewModel, NavigationParameters parameters, NavigationMode mode, CancellationToken cancellationToken)
    {
        Region = region;
        NavigationService = navigationService;
        TargetViewModelType = targetViewModelType;
        SourceViewModel = sourceViewModel;
        Parameters = parameters ?? new NavigationParameters();
        Mode = mode;
        CancellationToken = cancellationToken;
    }

    public IRegion Region { get; }
    public INavigationService NavigationService { get; }
    public Type TargetViewModelType { get; }

    /// <summary>The resolved (or reused) target view model. Null until the region resolves it - available to
    /// <see cref="INavigationAware.OnNavigatedTo"/>, not to the outgoing guard.</summary>
    public object TargetViewModel { get; internal set; }

    public object SourceViewModel { get; }
    public NavigationParameters Parameters { get; }
    public NavigationMode Mode { get; }
    public CancellationToken CancellationToken { get; }
}
