namespace Adamantium.Navigation;

/// <summary>Implemented by a view model that wants the navigation lifecycle: notification when it is navigated to/from,
/// and a say in whether an existing instance can serve a new navigation (view reuse).</summary>
public interface INavigationAware
{
    void OnNavigatedTo(NavigationContext context);
    void OnNavigatedFrom(NavigationContext context);

    /// <summary>Return true to REUSE this instance for <paramref name="context"/> instead of resolving a fresh one
    /// (e.g. the same page with different parameters).</summary>
    bool IsNavigationTarget(NavigationContext context);
}
