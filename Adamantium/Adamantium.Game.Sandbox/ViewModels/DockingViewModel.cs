using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Adamantium.MVVM;
using Adamantium.Navigation;
using Adamantium.UI.Controls.Docking;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Docking tab: an editor-shaped arrangement built from ZONES alone - the markup says where each group goes,
/// never what share of what it takes, and the split tree is derived from that.
/// <para>The area is also a navigation REGION, so panes can be opened and reached by name instead of by hand: see
/// <see cref="Navigate"/>.</para></summary>
[ViewModel]
public partial class DockingViewModel : TabPageViewModel
{
    public const string RegionName = "docking";

    private readonly INavigationService _navigation;
    private int _created;

    public DockingViewModel(INavigationService navigation) : base("Docking")
    {
        _navigation = navigation;
        _navigation.Regions.GetOrCreateRegion(RegionName);
    }

    /// <summary>What the application last answered when the docking area asked it (see
    /// <see cref="Behaviors.DockingPolicyBehavior"/>). Shown above the area, because a refusal that is not said out loud
    /// reads as a gesture that mysteriously did nothing.</summary>
    [Bindable] private string _lastAnswer =
        "Nothing asked yet. Try dropping a fourth tab into a panel, or pulling out a panel's only tab.";

    /// <summary>Every pane the region knows how to reach. New ones join it, so a tab created here can be navigated back
    /// to after it is closed - the name is the identity, not the instance.</summary>
    public ObservableCollection<string> Pages { get; } = ["Assets", "Terminal", "Profiler", "Notes"];

    [Bindable] private string _selectedPage = "Assets";

    /// <summary>Where a NEW pane is created. Centre means the document well, the middle four are the area's own edges,
    /// and Floating opens it in a window of its own - which is also how to get a pane that is only ever floating (see
    /// the Watch pane's Pane.Allowed in the view).</summary>
    public ObservableCollection<DockZone> Places { get; } =
        [DockZone.Center, DockZone.Left, DockZone.Right, DockZone.Top, DockZone.Bottom, DockZone.Floating];

    [Bindable] private DockZone _selectedPlace = DockZone.Center;

    private IRegion Region => _navigation.Regions[RegionName];

    /// <summary>Go to the selected name: open where the place says if it is not there, activate it where it is if it
    /// is. Both are the same call - which of the two happens is the region's business, not the caller's.</summary>
    [Command]
    private Task Navigate()
    {
        return Open(SelectedPage);
    }

    /// <summary>A pane that did not exist before, so there is always something new to place.</summary>
    [Command]
    private Task NewTab()
    {
        var name = $"Page {++_created}";
        Pages.Add(name);
        SelectedPage = name;
        return Open(name);
    }

    private Task Open(string page)
    {
        return Region.NavigateToAsync<DockPageViewModel>(new NavigationParameters()
            .Add(DockPageViewModel.PageKey, page)
            .Add(DockPageViewModel.ZoneKey, SelectedPlace));
    }
}
