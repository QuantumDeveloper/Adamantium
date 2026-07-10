using Adamantium.MVVM;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Brushes tab: a showcase of LINEAR and RADIAL gradient brushes - authored entirely in AUML - filling
/// rectangles, ellipses and arbitrary shapes, plus the three spread methods. Proves gradients work on any element (like
/// WPF) and that they batch through the SDF gradient batches.</summary>
[ViewModel]
public partial class BrushesViewModel : TabPageViewModel
{
    public BrushesViewModel() : base("Brushes") { }
}
