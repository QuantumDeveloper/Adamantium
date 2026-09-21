namespace Adamantium.UI.Rendering;

/// <summary>
/// Rendering ALWAYS runs on a dedicated render thread: the loop thread does Update + device-free RECORD, the render thread
/// does the GPU APPLY + Render + Present from a double-buffered packet (docs/RENDER_THREAD_PLAN.md, Phase 3.3). The record
/// runs at loop level; the render thread is spawned at startup.
/// </summary>
public static class RenderThreadOptions
{
    /// <summary>When true, RECORD and GPU APPLY both run inline on the loop thread (the legacy pre-split path). Default
    /// false: the record runs at loop level and the apply moves to <see cref="RenderThread"/>.</summary>
    public static bool SingleThreaded = false;

    /// <summary>The dedicated render thread, once one is running (null otherwise). The RECORD half must NEVER run on it: it
    /// reads the live visual tree, which the loop is concurrently mutating, and it would race the loop's own record on the
    /// same cache. BeginDraw checks this before falling back to its inline record path (see WindowRenderService).</summary>
    public static System.Threading.Thread RenderThread;

    /// <summary>Run the APPLY + Render + Present half (ExecuteDrawSequence) on <see cref="RenderThread"/> while the loop
    /// thread does Update + Record. Requires the decoupled record path (<see cref="SingleThreaded"/> = false). Default on:
    /// the render thread is spawned at startup.</summary>
    public static bool RenderThreadEnabled = true;
}
