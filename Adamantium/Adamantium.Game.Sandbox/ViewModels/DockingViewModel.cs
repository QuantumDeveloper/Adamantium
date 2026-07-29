using Adamantium.MVVM;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Docking tab: an editor-shaped arrangement built from ZONES alone - the markup says where each group goes,
/// never what share of what it takes, and the split tree is derived from that.</summary>
[ViewModel]
public partial class DockingViewModel : TabPageViewModel
{
    public DockingViewModel() : base("Docking") { }
}
