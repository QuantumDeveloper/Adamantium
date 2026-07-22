using Adamantium.MVVM;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Splitter tab: a GridSplitter dragging to resize two panes (a Thumb-based control that now carries its own
/// style under exact-type matching), and a UniformGrid tiling its children into equal cells. Pure markup - no VM state.</summary>
[ViewModel]
public partial class SplitterViewModel : TabPageViewModel
{
    public SplitterViewModel() : base("Splitter")
    {
    }
}
