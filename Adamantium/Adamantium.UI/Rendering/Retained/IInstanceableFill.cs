using Adamantium.Mathematics;

namespace Adamantium.UI.Rendering.Retained;

/// <summary>
/// A render unit whose SOLID fill body can be drawn instanced (retained geometry-instancing). The render pass asks each
/// unit for its instanced-fill data; if it obliges, the pass registers the instance in the scene and sets
/// <see cref="FillInstanced"/> so the unit's <c>Render</c> SKIPS its per-unit fill body. The unit's AA fringe and stroke
/// still draw per-unit (over the instanced body), so the composite stays pixel-identical to the non-instanced path.
/// </summary>
internal interface IInstanceableFill
{
    /// <summary>Shape key (elements sharing it share one mesh + one instanced draw), the shared LOCAL mesh, and the
    /// baked fill colour. False = not instanceable this frame (non-solid brush, no mesh, ...): draw the fill per-unit.</summary>
    bool TryGetInstancedFill(out GeometryKey key, out object mesh, out Vector4F color);

    /// <summary>Set by the render pass each frame: true = the fill went to the instanced renderer, so Render() skips its
    /// body (fringe/stroke still draw). Persists across clean frames (nothing changed - the retained draw still covers it).</summary>
    bool FillInstanced { get; set; }
}
