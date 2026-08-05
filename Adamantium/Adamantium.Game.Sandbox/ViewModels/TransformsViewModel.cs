using Adamantium.MVVM;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Transforms tab: what the transform TABLE buys. Every tile is a rounded, stroked rect - SDF-batch content -
/// drawn under its own rotation/shear. Instances carry local geometry plus a slot index and the vertex shader fetches the
/// matrix, so an arbitrary affine (or 3D) transform no longer disqualifies an element from the batch: the bake used to
/// fold the world into axis-aligned bounds, which a rotation or a shear simply cannot be written as, so every turned tile
/// fell back to its own draw. Turn Spread up and the grid draws hundreds of DIFFERENT matrices in one instanced draw.</summary>
[ViewModel]
public partial class TransformsViewModel : TabPageViewModel
{
    public TransformsViewModel() : base("Transforms") { }

    public TransformSettings Transforms { get; } = new();
}
