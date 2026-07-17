using System.Threading.Tasks;
using Adamantium.MVVM;
using Adamantium.Navigation;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Content view model of the SECOND window. It names a custom window shell ("workspace") via IWindowAware - the
/// framework creates that window and loads this view model's view; the VM never touches a Window type. Its TabControl is
/// driven purely by a NAMED region on the navigation manager: OpenDoc/OpenChart activate tabs, CloseTab removes them.</summary>
[ViewModel]
public partial class WorkspaceShellViewModel : IWindowAware
{
    public const string WorkspaceRegion = "workspace";

    private readonly INavigationService _navigation;
    private int _docCounter;
    private int _chartCounter;

    public WorkspaceShellViewModel(INavigationService navigation)
    {
        _navigation = navigation;
        _navigation.Regions.GetOrCreateRegion(WorkspaceRegion);
    }

    // IWindowAware: which shell to host this in, plus its caption/size - all without touching a UI type.
    public string WindowShellKey => WorkspaceRegion;
    public string Title => "Workspace";
    public double Width => 900;
    public double Height => 600;

    private IRegion Region => _navigation.Regions[WorkspaceRegion];

    [Command] private Task OpenDoc() =>
        Region.NavigateToAsync<DocPageViewModel>(new NavigationParameters().Add("n", ++_docCounter));

    [Command] private Task OpenChart() =>
        Region.NavigateToAsync<ChartPageViewModel>(new NavigationParameters().Add("n", ++_chartCounter));

    [Command] private void CloseTab()
    {
        if (Region.CurrentViewModel != null) Region.Remove(Region.CurrentViewModel);
    }
}
