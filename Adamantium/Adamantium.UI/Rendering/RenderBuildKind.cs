namespace Adamantium.UI.Rendering;

/// <summary>How much work the most recent <see cref="RenderCache.BuildFromVisualTree"/> did this frame
/// (docs/RENDER_CACHE_REDESIGN.md §4a/§4i).</summary>
public enum RenderBuildKind
{
    /// <summary>Nothing changed - the retained units were re-drawn as-is (no walk, no re-record, no transform re-bake).</summary>
    Clean,

    /// <summary>Only the dirty components were re-rendered IN PLACE (or a move re-baked transforms); the retained
    /// paint-order list was untouched - no full tree walk.</summary>
    Partial,

    /// <summary>A full tree walk rebuilt the paint-order list (first build, or a structural change).</summary>
    Full
}
