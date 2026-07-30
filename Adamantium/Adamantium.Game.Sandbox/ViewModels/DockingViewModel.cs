using Adamantium.MVVM;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Docking tab: an editor-shaped arrangement built from ZONES alone - the markup says where each group goes,
/// never what share of what it takes, and the split tree is derived from that.</summary>
[ViewModel]
public partial class DockingViewModel : TabPageViewModel
{
    public DockingViewModel() : base("Docking") { }

    /// <summary>What the application last answered when the docking area asked it (see
    /// <see cref="Behaviors.DockingPolicyBehavior"/>). Shown above the area, because a refusal that is not said out loud
    /// reads as a gesture that mysteriously did nothing.</summary>
    [Bindable] private string _lastAnswer =
        "Nothing asked yet. Try dropping a fourth tab into a panel, or pulling out a panel's only tab.";
}
