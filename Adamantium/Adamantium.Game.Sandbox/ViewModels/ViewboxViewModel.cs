using Adamantium.MVVM;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Viewbox tab: the same fixed-size vector card shown through a Viewbox in each Stretch mode (and each
/// StretchDirection), so the scaling behaviour is visible side by side.</summary>
[ViewModel]
public partial class ViewboxViewModel : TabPageViewModel
{
    public ViewboxViewModel() : base("Viewbox") { }
}
