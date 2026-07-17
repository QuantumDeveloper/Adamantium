using System.Threading;
using System.Threading.Tasks;
using Adamantium.MVVM;
using Adamantium.Navigation;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Details page: reads a typed <c>id</c> from the navigation parameters (INavigationAware) and can VETO leaving
/// while "unsaved changes" is on (IConfirmNavigation) - the guard runs before any other page is resolved.</summary>
[ViewModel]
public partial class DetailsPageViewModel : INavigationAware, IConfirmNavigation
{
    [Bindable] private int _id;

    // While on, leaving this page is blocked - flip it off to allow navigating away.
    [Bindable] private bool _hasUnsavedChanges;

    public void OnNavigatedTo(NavigationContext context) => Id = context.Parameters.GetValue<int>("id");
    public void OnNavigatedFrom(NavigationContext context) { }
    public bool IsNavigationTarget(NavigationContext context) => true;

    public Task<bool> CanNavigateAwayAsync(NavigationContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(!HasUnsavedChanges);
}
