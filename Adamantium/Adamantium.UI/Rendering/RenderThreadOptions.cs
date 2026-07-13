namespace Adamantium.UI.Rendering;

/// <summary>
/// Render-thread rollout flag (docs/RENDER_THREAD_PLAN.md, Phase 3.2/3.3). Default ON = the device-free RECORD and the
/// GPU APPLY both run inline on the loop thread, exactly as before the split - the safe, verified path. When later turned
/// OFF (Phase 3.3), the applier moves to a dedicated render thread with a double-buffered packet; kept as an A/B fallback
/// while that stabilises. In Phase 3.2 it only decides WHERE the record runs (loop-level pre-Draw vs inline in BeginDraw).
/// </summary>
public static class RenderThreadOptions
{
    public static bool SingleThreaded = true;

    /// <summary>Phase 3.3: run the APPLY + Render + Present half (ExecuteDrawSequence) on a dedicated render thread while
    /// the loop thread does Update + Record. Requires the decoupled record path (implies <see cref="SingleThreaded"/> =
    /// false - the record must already run at loop level). Off by default; the render thread is only spawned when this is
    /// set at startup. 3.3a hands off serialized (loop waits for the render frame); 3.3b relaxes it to overlapped.</summary>
    public static bool RenderThreadEnabled = false;
}
