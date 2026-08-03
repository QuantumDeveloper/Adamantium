using System.Collections.ObjectModel;
using Adamantium.MVVM;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Keyboard-navigation tab: everything here is meant to be driven WITHOUT the mouse - Tab to move between the
/// blocks, the arrows to move inside one. The data is deliberately dull; what is on show is where the focus goes.</summary>
[ViewModel]
public partial class KeyboardViewModel : TabPageViewModel
{
    public KeyboardViewModel() : base("Keyboard") { }

    public ObservableCollection<string> Rows { get; } = new(
    [
        "Aurora", "Basalt", "Cinder", "Dune", "Ember", "Fjord", "Glacier", "Harbour",
        "Inlet", "Jetty", "Kelp", "Lagoon", "Marsh", "Nimbus", "Onyx", "Prairie",
        "Quarry", "Reef", "Summit", "Tundra", "Vale", "Willow", "Yonder", "Zenith"
    ]);

    public ObservableCollection<string> Tiles { get; } = new(
    [
        "Coral", "Cyan", "Lime", "Pink", "Gold", "Indigo", "Olive", "Peach",
        "Mint", "Rose", "Sky", "Rust", "Plum", "Sand", "Jade", "Slate"
    ]);
}
