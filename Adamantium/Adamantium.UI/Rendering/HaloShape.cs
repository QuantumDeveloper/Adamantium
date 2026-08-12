namespace Adamantium.UI.Rendering;

/// <summary>How the halo pass gets the signed distance it shapes its band from. The values are the shader's contract
/// (<c>HaloRectData.Params.z</c>), so they are fixed, not free to renumber.</summary>
internal enum HaloShape
{
    /// <summary>A rounded rectangle - computed in closed form.</summary>
    RoundedRect = 0,

    /// <summary>A full ellipse - computed in closed form.</summary>
    Ellipse = 1,

    /// <summary>Arbitrary tessellated geometry - READ from a distance field baked per shape. Same pass, same falloff:
    /// the only difference is where the distance comes from.</summary>
    Field = 2
}
