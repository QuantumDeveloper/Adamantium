using System.Collections.ObjectModel;
using System.Linq;
using Adamantium.MVVM;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Instancing tab: a large virtualized grid of NON-SDF star polygons. Every star is the SAME shape at the SAME
/// size, so its tessellated LOCAL mesh is byte-identical across all items - only the fill colour and grid position vary.
/// That is exactly the retained geometry-instancing case: with RETAINED_INSTANCING=1 the whole visible window of stars
/// collapses to ONE instanced draw (shared mesh + a per-instance world-matrix/colour SSBO), instead of one draw per star.
/// The rectangle Layout tab can't prove this - a rounded rect is an SDF batch, not tessellated fill.</summary>
[ViewModel]
public partial class InstancingViewModel : TabPageViewModel
{
    public InstancingViewModel() : base("Instancing") { }

    private static readonly string[] Palette =
        ["#3B82F6", "#22C55E", "#F59E0B", "#EF4444", "#8B5CF6", "#14B8A6", "#EC4899", "#EAB308"];

    public ObservableCollection<ColorRect> Stars { get; } =
        new(Enumerable.Range(0, 600).Select(i => new ColorRect { Color = Palette[i % Palette.Length] }));
}
