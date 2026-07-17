using Adamantium.MVVM;
using Adamantium.Navigation;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>A document tab in the workspace window's region. Its <see cref="Title"/> (the tab header) is stamped from the
/// navigation parameter, and it declines reuse so each OpenDoc yields a distinct tab.</summary>
[ViewModel]
public partial class DocPageViewModel : INavigationAware
{
    [Bindable] private string _title = "Doc";

    public void OnNavigatedTo(NavigationContext context) => Title = $"Doc {context.Parameters.GetValue<int>("n")}";
    public void OnNavigatedFrom(NavigationContext context) { }
    public bool IsNavigationTarget(NavigationContext context) => false;
}
