using Adamantium.MVVM;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Shapes tab: a single <see cref="Stroke"/> settings object drives the stroke of EVERY shape in the tab
/// (thickness/dashes/trim/corner/cap/join/colour) - the gallery, the big rect+ellipse preview and the Bézier/NURBS
/// curves all bind their stroke off <c>Stroke.*</c>. The animated dashed Line keeps its own hover animation.</summary>
[ViewModel]
public partial class ShapesViewModel : TabPageViewModel
{
    public ShapesViewModel() : base("Shapes") { }

    public StrokeSettings Stroke { get; } = new();

    /// <summary>The border stand's eight sliders: a thickness per side and a radius per corner.</summary>
    public BorderSettings Border { get; } = new();
}
