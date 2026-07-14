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

    /// <summary>Content ENTERED or LEFT the drawn set (a virtualizing panel realizing tiles, a control collapsing) and the
    /// change could be attributed to named components, so the paint order was SPLICED: only the added subtrees were
    /// recorded, only the removed ones freed. O(changed), no tree walk.</summary>
    Structural,

    /// <summary>A full tree walk rebuilt the paint-order list: the first build, or a change nothing could attribute to a
    /// component (a theme/DPI swap, an unowned transform) - the fallback that always re-derives everything.</summary>
    Full
}
