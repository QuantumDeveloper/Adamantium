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

    /// <summary>Cumulative count of binding target writes - every time a <c>{Binding}</c> pushes a value to its target:
    /// the initial connect, a DataContext re-resolve (e.g. a recycled list container rebinding on scroll), AND a batched
    /// source-property change. Sample by delta to see how many landed this frame (idle ~0; spikes on scroll rebinds and
    /// on a binding storm, where the per-flush cap bounds it).</summary>
    public static long BindingUpdatesApplied;
}
