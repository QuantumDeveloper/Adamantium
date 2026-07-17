using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Adamantium.Core;
using Adamantium.Core.DependencyInjection;

namespace Adamantium.Navigation;

/// <summary>Default region: owns the lifecycle sequence, the journal and the active-view-model set, resolving targets via
/// the DI container. UI-free - an adapter (UI layer) observes CurrentViewModel/ActiveViewModels and drives a control.</summary>
public sealed class Region : PropertyChangedBase, IRegion
{
    private readonly IDependencyResolver _resolver;
    private readonly List<object> _activeViewModels = [];
    private object _currentViewModel;

    public Region(string name, IDependencyResolver resolver, INavigationService navigationService)
    {
        Name = name;
        _resolver = resolver;
        NavigationService = navigationService;
        Journal = new NavigationJournal();
    }

    // The façade a NavigationContext hands to lifecycle callbacks; set by the manager when the service is available.
    internal INavigationService NavigationService { get; set; }

    public string Name { get; }
    public INavigationJournal Journal { get; }
    public IReadOnlyList<object> ActiveViewModels => _activeViewModels;
    public bool SingleActiveView { get; set; }

    public object CurrentViewModel
    {
        get => _currentViewModel;
        private set => SetProperty(ref _currentViewModel, value);
    }

    public bool CanGoBack => Journal.CanGoBack;
    public bool CanGoForward => Journal.CanGoForward;

    public event EventHandler<RegionNavigationEventArgs> Navigated;
    public event EventHandler ActiveViewsChanged;

    public Task<NavigationResult> NavigateToAsync<TViewModel>(NavigationParameters parameters = null, CancellationToken cancellationToken = default)
        => NavigateToAsync(typeof(TViewModel), parameters, cancellationToken);

    public async Task<NavigationResult> NavigateToAsync(Type viewModelType, NavigationParameters parameters = null, CancellationToken cancellationToken = default)
    {
        var context = new NavigationContext(this, NavigationService, viewModelType, _currentViewModel, parameters, NavigationMode.New, cancellationToken);
        try
        {
            if (!await ConfirmLeaveAsync(context, cancellationToken)) return NavigationResult.Vetoed();
            if (cancellationToken.IsCancellationRequested) return NavigationResult.Vetoed();

            var target = FindReusable(viewModelType, context) ?? _resolver.Resolve(viewModelType);
            context.TargetViewModel = target;

            (_currentViewModel as INavigationAware)?.OnNavigatedFrom(context);
            (target as INavigationAware)?.OnNavigatedTo(context);

            Journal.RecordNavigation(new NavigationJournalEntry(viewModelType, target, context.Parameters));
            SetActive(target);
            return Settle(context);
        }
        catch (Exception ex)
        {
            return NavigationResult.Failed(ex);
        }
    }

    public Task<NavigationResult> GoBackAsync(CancellationToken cancellationToken = default) => GoAsync(NavigationMode.Back, cancellationToken);
    public Task<NavigationResult> GoForwardAsync(CancellationToken cancellationToken = default) => GoAsync(NavigationMode.Forward, cancellationToken);

    private async Task<NavigationResult> GoAsync(NavigationMode mode, CancellationToken cancellationToken)
    {
        var peek = mode == NavigationMode.Back
            ? (Journal.CanGoBack ? Journal.BackStack[^1] : null)
            : (Journal.CanGoForward ? Journal.ForwardStack[^1] : null);
        if (peek == null) return NavigationResult.Vetoed();

        var context = new NavigationContext(this, NavigationService, peek.ViewModelType, _currentViewModel, peek.Parameters, mode, cancellationToken);
        try
        {
            if (!await ConfirmLeaveAsync(context, cancellationToken)) return NavigationResult.Vetoed();

            var entry = mode == NavigationMode.Back ? Journal.Back() : Journal.Forward();
            var target = entry.ViewModel ?? _resolver.Resolve(entry.ViewModelType);
            context.TargetViewModel = target;

            (_currentViewModel as INavigationAware)?.OnNavigatedFrom(context);
            (target as INavigationAware)?.OnNavigatedTo(context);

            SetActive(target);
            return Settle(context);
        }
        catch (Exception ex)
        {
            return NavigationResult.Failed(ex);
        }
    }

    public void Add(object viewModel)
    {
        if (viewModel == null || _activeViewModels.Contains(viewModel)) return;
        _activeViewModels.Add(viewModel);
        RaiseActiveViewsChanged();
    }

    public void Remove(object viewModel)
    {
        if (viewModel == null || !_activeViewModels.Remove(viewModel)) return;
        if (ReferenceEquals(viewModel, _currentViewModel))
            CurrentViewModel = _activeViewModels.Count > 0 ? _activeViewModels[^1] : null;
        RaiseActiveViewsChanged();
    }

    public void Activate(object viewModel)
    {
        if (viewModel == null) return;
        Add(viewModel);
        CurrentViewModel = viewModel;
    }

    public void Deactivate(object viewModel)
    {
        if (ReferenceEquals(viewModel, _currentViewModel))
            CurrentViewModel = _activeViewModels.Count > 0 ? _activeViewModels[^1] : null;
    }

    // Reuse the current (or any active) instance of the same type when it says it can serve this navigation.
    private object FindReusable(Type viewModelType, NavigationContext context)
    {
        if (_currentViewModel != null && _currentViewModel.GetType() == viewModelType
            && _currentViewModel is INavigationAware currentAware && currentAware.IsNavigationTarget(context))
            return _currentViewModel;

        foreach (var vm in _activeViewModels)
            if (vm.GetType() == viewModelType && vm is INavigationAware aware && aware.IsNavigationTarget(context))
                return vm;

        return null;
    }

    private void SetActive(object target)
    {
        if (SingleActiveView && _currentViewModel != null && !ReferenceEquals(_currentViewModel, target)
            && _activeViewModels.Remove(_currentViewModel))
            RaiseActiveViewsChanged();

        if (!_activeViewModels.Contains(target))
        {
            _activeViewModels.Add(target);
            RaiseActiveViewsChanged();
        }
        CurrentViewModel = target;
    }

    private NavigationResult Settle(NavigationContext context)
    {
        RaisePropertyChanged(nameof(CanGoBack));
        RaisePropertyChanged(nameof(CanGoForward));
        var result = NavigationResult.Ok(context.TargetViewModel);
        Navigated?.Invoke(this, new RegionNavigationEventArgs(context, result));
        return result;
    }

    private static async Task<bool> ConfirmLeaveAsync(NavigationContext context, CancellationToken cancellationToken)
    {
        if (context.SourceViewModel is IConfirmNavigation guard)
            return await guard.CanNavigateAwayAsync(context, cancellationToken);
        return true;
    }

    private void RaiseActiveViewsChanged() => ActiveViewsChanged?.Invoke(this, EventArgs.Empty);
}
