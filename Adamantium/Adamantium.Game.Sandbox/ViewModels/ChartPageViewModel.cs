using Adamantium.MVVM;
using Adamantium.Navigation;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>A chart tab in the workspace window's region. Like <see cref="DocPageViewModel"/>, its header comes from the
/// navigation parameter and it declines reuse so each OpenChart yields a distinct tab.</summary>
[ViewModel]
public partial class ChartPageViewModel : INavigationAware
{
    [Bindable] private string _title = "Chart";

    public void OnNavigatedTo(NavigationContext context) => Title = $"Chart {context.Parameters.GetValue<int>("n")}";
    public void OnNavigatedFrom(NavigationContext context) { }
    public bool IsNavigationTarget(NavigationContext context) => false;
}
