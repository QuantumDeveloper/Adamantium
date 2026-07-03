using System;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core.Behaviors;
using Adamantium.UI.Core.Diagnostics;
using Adamantium.UI.Core.Media.Animation;

namespace Adamantium.Game.Sandbox;

/// <summary>
/// Attaches to a <see cref="TextBlock"/> and rewrites its text a few times a second with live engine diagnostics, so the
/// work that is normally invisible (the layout manager, the binding batcher, the inertia heartbeat) can be SEEN while
/// exercising the running app - the runtime counterpart to the headless unit tests.
/// <list type="bullet">
/// <item>layout pass max (ms) over the refresh window - ~0 when idle (no per-frame tree walk), spikes on scroll/resize;</item>
/// <item>measure/arrange calls over the window - 0 when idle, bounded by virtualization while scrolling;</item>
/// <item>[DEFERRED] - a pass in the window hit the frame budget and pushed work to a later frame;</item>
/// <item>binding target writes over the window - 0 when idle, spikes on scroll (recycled rows rebinding) and on a
/// binding storm (where the per-flush cap bounds it);</item>
/// <item>active animations - &gt; 0 while scroll inertia (or any animation) is coasting.</item>
/// </list>
/// It rides the same <see cref="AnimationManager"/> heartbeat as everything else. It samples every frame (cheap field
/// math) but only rewrites the TextBlock ~4x/sec: re-rastering the glyphs every frame is what actually costs FPS, so the
/// overlay amortises it over a refresh window instead.
/// </summary>
public class DiagnosticsOverlayBehavior : Behavior<TextBlock>
{
    private const double RefreshSeconds = 0.25;   // rewrite the text ~4x/sec - readable, and cheap (no per-frame raster)

    private long _lastMeasure, _lastArrange, _lastBindings;
    private double _windowElapsed, _windowMaxLayoutMs;
    private double _sumLayout, _sumBuild, _sumProc, _sumDraw, _sumProcs;   // per-frame sums -> averages over the window
    private int _windowFrames;
    private bool _windowDeferred;
    private bool _running;

    protected override void OnAttached(TextBlock target)
    {
        _lastMeasure = MeasurableUIComponent.TotalMeasureCalls;
        _lastArrange = MeasurableUIComponent.TotalArrangeCalls;
        _lastBindings = RuntimeStats.BindingUpdatesApplied;
        _running = true;
        AnimationManager.AddTicker(dt => Advance(target, dt));
    }

    protected override void OnDetached(TextBlock target) => _running = false;

    private bool Advance(TextBlock target, double dt)
    {
        if (!_running) return true;   // detached -> let the heartbeat drop this ticker

        // Accumulate this frame's samples cheaply; only rewrite the TextBlock once the refresh window elapses.
        _windowElapsed += dt;
        _windowFrames++;
        if (RuntimeStats.LastLayoutPassMs > _windowMaxLayoutMs) _windowMaxLayoutMs = RuntimeStats.LastLayoutPassMs;
        if (RuntimeStats.LastPassBudgetDeferred) _windowDeferred = true;
        _sumLayout += RuntimeStats.LastLayoutPassMs;
        _sumBuild  += RuntimeStats.LastRenderBuildMs;
        _sumProc   += RuntimeStats.LastRenderProcMs;
        _sumDraw   += RuntimeStats.LastRenderDrawMs;
        _sumProcs  += RuntimeStats.LastProcessorsMs;
        if (_windowElapsed < RefreshSeconds) return false;

        var measure = MeasurableUIComponent.TotalMeasureCalls;
        var arrange = MeasurableUIComponent.TotalArrangeCalls;
        var bindings = RuntimeStats.BindingUpdatesApplied;
        var fps = _windowFrames / _windowElapsed;

        // Frame breakdown (averages over the window, so they sum to ~frame time). "other" = the residual the render
        // pipeline can't see: GPU-fence wait in BeginDraw + swapchain blit + Present. Phase 0 of the render-cache
        // redesign - shows whether the per-frame cache REBUILD (build+proc) or something else (GPU/present) dominates.
        var f = 1.0 / _windowFrames;
        var frameMs = _windowElapsed * 1000.0 * f;
        var avgLayout = _sumLayout * f;
        var avgBuild = _sumBuild * f; var avgProc = _sumProc * f; var avgDraw = _sumDraw * f; var avgProcs = _sumProcs * f;
        var other = Math.Max(0, frameMs - avgLayout - avgBuild - avgProc - avgDraw - avgProcs);

        target.Text =
            $"FPS   {fps,5:F0}     frame {frameMs,5:F1} ms\n" +
            $"layout  {avgLayout,5:F2} (max {_windowMaxLayoutMs,4:F1}){(_windowDeferred ? " [DEFERRED]" : "")}\n" +
            $"build/proc/draw  {avgBuild,4:F1} / {avgProc,4:F1} / {avgDraw,4:F1} ms\n" +
            $"processors {avgProcs,4:F1}    other {other,4:F1} ms\n" +
            $"measure/arrange  {measure - _lastMeasure} / {arrange - _lastArrange}\n" +
            $"bindings {bindings - _lastBindings}    anim {AnimationManager.ActiveCount}";

        _lastMeasure = measure; _lastArrange = arrange; _lastBindings = bindings;
        _windowElapsed = 0; _windowFrames = 0; _windowMaxLayoutMs = 0; _windowDeferred = false;
        _sumLayout = _sumBuild = _sumProc = _sumDraw = _sumProcs = 0;
        return false;   // keep ticking
    }
}
