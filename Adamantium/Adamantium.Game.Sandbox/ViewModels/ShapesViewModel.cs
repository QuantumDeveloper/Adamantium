using Adamantium.MVVM;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Shapes tab: a single <see cref="StrokeThickness"/> is bound to every shape's stroke, so one slider retraces
/// them all at once. The animated dashed Path keeps its own hover animation.</summary>
[ViewModel]
public partial class ShapesViewModel : TabPageViewModel
{
    public ShapesViewModel() : base("Shapes") { }

    [Bindable] private double _strokeThickness = 3;
}
