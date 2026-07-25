namespace Adamantium.UI.Core.Media;

/// <summary>The noise variant a <see cref="NoiseBrush"/> produces. Simplex and Perlin are smooth gradient-style fields
/// (they look similar - Perlin is the classic, Simplex the newer variant); Value is slightly blockier interpolated
/// lattice noise; Worley is cellular (Voronoi distance) - a completely different organic "cells / cracks / scales" look
/// (and the only one that flows under Animate). Ridged and Turbulence are FBM folds over simplex: Turbulence sums the
/// absolute noise (billowy / smoky), Ridged sums the inverted-and-sharpened noise (sharp mountain ridges / marble veins).
/// VoronoiBorders (iq's Xd23Dh) draws the cellular BORDER network - thin glowing cell walls / cracks, not filled cells -
/// and morphs as its feature points orbit under Animate. CombustibleVoronoi (Shane's 4tlSzl) is a 3D-Voronoi fBm through a
/// blackbody FIRE palette - a molten plasma / fireball look; it has its own colour path, so Color1/Color2 don't tint it
/// (only Color1's alpha carries opacity). Best with Animate on.</summary>
public enum NoiseType
{
    Simplex,
    Perlin,
    Value,
    Worley,
    Ridged,
    Turbulence,
    VoronoiBorders,
    CombustibleVoronoi
}
