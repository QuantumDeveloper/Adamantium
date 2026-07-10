using System.Collections.Generic;

namespace Adamantium.UI.Core;

/// <summary>
/// Per-frame render dirty registry for the render-cache redesign (docs/RENDER_CACHE_REDESIGN.md §4a/§4i). Instead of
/// re-walking the whole visual tree every frame, invalidation records WHAT changed here, and the render cache re-does
/// only that:
/// <list type="bullet">
/// <item><b>Geometry</b> - components whose RECORDED draw output changed (colour/size/shape/opacity/visibility). Only
/// these re-render (a partial rebuild); every other unit is kept as-is.</item>
/// <item><b>Transform</b> - something MOVED (a <c>Bounds</c> / <c>RenderTransform</c> change). The recorded geometry is
/// unchanged, so nothing re-renders; the render pass just re-bakes the world transforms (drop the frame-scoped memo).</item>
/// <item><b>Structural</b> - a visual child was added/removed, or a partial re-render changed a component's draw-command
/// COUNT (so the retained paint-order list no longer matches). Forces a full walk to rebuild that list.</item>
/// </list>
/// A frame with none of these set is fully clean: the cache re-draws last frame's retained units with ~0 CPU.
/// </summary>
/// <remarks>
/// CONSERVATIVE by construction: over-marking only costs a redundant re-render/rebuild (correct, just slower); the only
/// unsafe outcome - a real change marking NOTHING - can't happen because every mutation path marks at least one of the
/// three (and a running property animation marks Geometry each tick). Single-threaded (UI/render thread).
/// </remarks>
public static class RenderDirty
{
    private static readonly HashSet<IUIComponent> GeometrySet = new();
    private static bool _transform;
    private static bool _structural;

    // Monotonic test hook (like MeasurableUIComponent.TotalMeasureCalls): recycling-ring tests assert ZERO structural
    // marks per continuous-scroll step, which is how they catch attach/detach/Visibility churn headlessly.
    public static long TotalStructuralMarks;

    /// <summary>Records that <paramref name="component"/>'s recorded geometry changed - it will re-render.</summary>
    public static void MarkGeometry(IUIComponent component)
    {
        // Locked: a PARALLEL arrange pass (VirtualizingPanel) calls this concurrently as each tile's size settles;
        // HashSet is not thread-safe so a lock-free Add could corrupt it. Uncontended in the single-threaded case (the
        // common path). The scalar counters below stay lock-free (a lost increment only mis-counts a diagnostic).
        if (component == null) return;
        lock (GeometrySet) GeometrySet.Add(component);
    }

    /// <summary>Records that something moved (world transforms must be re-baked; no re-record).</summary>
    public static void MarkTransform() => _transform = true;

    /// <summary>Records a structural change (add/remove/command-count change) - the paint-order list must be rebuilt.</summary>
    public static void MarkStructural() { _structural = true; TotalStructuralMarks++; }

    /// <summary>Any dirty state at all (else the frame is fully clean).</summary>
    public static bool HasWork => _structural || _transform || GeometrySet.Count > 0;

    public static bool IsStructural => _structural;
    public static bool IsTransform => _transform;

    /// <summary>The geometry-dirty components to re-render this build (only valid until <see cref="Clear"/>).</summary>
    public static IReadOnlyCollection<IUIComponent> Geometry => GeometrySet;

    /// <summary>Reset after a build has consumed the dirty state.</summary>
    public static void Clear()
    {
        GeometrySet.Clear();
        _transform = false;
        _structural = false;
    }
}
