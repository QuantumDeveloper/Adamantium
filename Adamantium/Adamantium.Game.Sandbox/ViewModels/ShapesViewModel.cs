using Adamantium.MVVM;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Shapes tab: a single <see cref="StrokeThickness"/> is bound to every static shape's stroke, so one slider
/// retraces them all at once. A separate <see cref="Stroke"/> settings object drives the full Pen-parameter preview
/// (thickness/dashes/trim/caps/joins/corner/hue) on a big rectangle + ellipse. The animated dashed Line keeps its own hover
/// animation.</summary>
[ViewModel]
public partial class ShapesViewModel : TabPageViewModel
{
    public ShapesViewModel() : base("Shapes") { }

    [Bindable] private double _strokeThickness = 3;

    // Full Pen playground driving the two big preview shapes - moved here from the Layout tab, where it only cluttered the
    // size/template demo. Its sliders mutate this one instance; the rect + ellipse bind the shape's stroke off Stroke.*.
    public StrokeSettings Stroke { get; } = new();
}
