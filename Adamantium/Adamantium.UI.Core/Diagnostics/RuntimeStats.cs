namespace Adamantium.UI.Core.Diagnostics;

/// <summary>
/// Lightweight live counters for a runtime diagnostics overlay, so the otherwise-invisible work of the layout manager,
/// the binding batcher and the animation heartbeat can be SEEN at runtime (verification, not just unit tests). All
/// writes are cheap field updates on the UI thread; a reader (the overlay) samples them once per frame. The last-pass
/// fields are snapshots of the most recent layout pass; the cumulative counters are meant to be sampled by per-frame
/// delta.
/// </summary>
public static class RuntimeStats
{
    /// <summary>Wall-clock duration of the most recent layout pass, in milliseconds (~0 on an idle frame, which is the
    /// whole point of the dirty-queue model: no per-frame tree walk).</summary>
    public static double LastLayoutPassMs;

    /// <summary>True if the most recent layout pass hit the frame budget and deferred work to a later frame.</summary>
    public static bool LastPassBudgetDeferred;

    // Per-frame render-pipeline phase timings (ms), snapshots of the most recent frame. Phase 0 of the render-cache
    // redesign (docs/RENDER_CACHE_REDESIGN.md): make the otherwise-invisible per-frame render cost measurable, so the
    // retained rewrite can be aimed at the phase that actually dominates (today the per-frame cache REBUILD, not layout).
    /// <summary>RenderCache.BuildFromVisualTree - the per-frame walk that re-records every component's draw commands.</summary>
    public static double LastRenderBuildMs;
    /// <summary>RenderCache.ProcessCommands - re-bake every cached unit's world transform.</summary>
    public static double LastRenderProcMs;
    /// <summary>RenderCache.Render - the content draw pass (batch + command recording).</summary>
    public static double LastRenderDrawMs;
    /// <summary>Overlay stages (adorner + popup) draw.</summary>
    public static double LastProcessorsMs;

    /// <summary>Cumulative count of frames actually PRESENTED. With a dedicated render thread this is the only honest frame
    /// rate: the loop's own rate measures Update + record, and the two are deliberately decoupled - a heavy Update must not
    /// drag the presented frame rate down with it (that is the entire point of the split). Sample by delta.</summary>
    public static long PresentedFrames;

    // Transform table (see Rendering/TransformTable). Since nothing is world-baked into an instance any more, EVERY drawn
    // element resolves a slot each walk - so the load-bearing number is how many of those actually WRITE. A settled scene
    // should sit at zero writes; a steady non-zero means slots are churning (released and re-acquired), and each write is
    // its own 64-byte upload. Slot/acquire/release are per-frame; Recreations is cumulative (buffer reallocations).
    public static int TransformSlots;
    public static int TransformWrites;
    public static int TransformAcquires;
    public static int TransformReleases;
    public static int TransformRecreations;

    /// <summary>Cumulative count of binding target writes - every time a <c>{Binding}</c> pushes a value to its target:
    /// the initial connect, a DataContext re-resolve (e.g. a recycled list container rebinding on scroll), AND a batched
    /// source-property change. Sample by delta to see how many landed this frame (idle ~0; spikes on scroll rebinds and
    /// on a binding storm, where the per-flush cap bounds it).</summary>
    public static long BindingUpdatesApplied;
}
