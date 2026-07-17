using Adamantium.MVVM;
using Adamantium.Navigation;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Settings page: shows the navigation lifecycle (INavigationAware) by stamping how it was reached.</summary>
[ViewModel]
public partial class SettingsPageViewModel : INavigationAware
{
    [Bindable] private string _status = "Settings";

    public void OnNavigatedTo(NavigationContext context) => Status = $"Settings - arrived via {context.Mode}";
    public void OnNavigatedFrom(NavigationContext context) { }
    public bool IsNavigationTarget(NavigationContext context) => true;
}
