using System;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core.Behaviors;
using Adamantium.UI.Core.Diagnostics;
using Adamantium.UI.Core.Media.Animation;

namespace Adamantium.Game.Sandbox.Behaviors;

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

    private long _lastMeasure, _lastArrange, _lastBindings, _lastPresented;
    private double _windowElapsed, _windowMaxLayoutMs;
    private double _sumLayout, _sumBuild, _sumProc, _sumDraw, _sumProcs;   // per-frame sums -> averages over the window
    private int _windowFrames;
    private bool _windowDeferred;
    private bool _running;
    private int _traceWindows;   // TEMP
    private int _peakLayout;     // TEMP: busiest layout-invalidation second seen so far
    private long _peakUnits, _lastCreated, _lastUpdated, _lastCommands;   // TEMP: busiest build second
    private int _second;   // TEMP: index in the appended history
    private double _windowMaxRecord, _windowMaxApply;

    // TEMP: WHO entered or left the drawn set over the last second. Every one of these is a structural change, and a
    // structural change is the one thing that can still cost a walk of the whole window - so when a spike is reproduced
    // by hand, this is what names it. Same shape as the layout counters beside it: reset per dump, busiest second kept.
    private readonly System.Collections.Generic.Dictionary<string, int> _churn = new();
    private int _peakChurn;

    // The fewest frames any one second has presented so far, and where that second started in the incident list.
    private long _worstSecondFrames = long.MaxValue;
    private int _secondLoopFrames;   // loop frames accumulated across this second window
    private long _worstSecondFrom;
    private int _worstSecondMark;

    private void NoteChurn(string what, Adamantium.UI.Core.IUIComponent c)
    {
        if (!_running) return;
        var key = $"{what} {c.GetType().Name} '{(c as UIComponent)?.Name}' -> {c.Visibility}";
        lock (_churn) _churn[key] = _churn.TryGetValue(key, out var had) ? had + 1 : 1;
    }

    private string DumpChurn(out int total)
    {
        var text = new System.Text.StringBuilder();
        total = 0;
        lock (_churn)
        {
            foreach (var pair in System.Linq.Enumerable.OrderByDescending(_churn, p => p.Value))
            {
                text.Append($"  {pair.Value,5}  {pair.Key}\n");
                total += pair.Value;
            }
        }

        return $"tree churn this second: {total}\n{text}";
    }

    protected override void OnAttached(TextBlock target)
    {
        Adamantium.UI.Core.VisualTreeNotifications.Attached += c => NoteChurn("attached", c);
        Adamantium.UI.Core.VisualTreeNotifications.Detached += c => NoteChurn("detached", c);
        Adamantium.UI.Core.VisualTreeNotifications.VisibilityChanged += c => NoteChurn("collapsed-flip", c);
        Adamantium.UI.Core.VisualTreeNotifications.ShownOrHidden += c => NoteChurn("hidden-flip", c);
        Adamantium.UI.Core.VisualTreeNotifications.ClipChanged += c => NoteChurn("clip", c);

        _lastMeasure = MeasurableUIComponent.TotalMeasureCalls;
        _lastArrange = MeasurableUIComponent.TotalArrangeCalls;
        _lastBindings = RuntimeStats.BindingUpdatesApplied;
        _running = true;
        FrameTrace.Enabled = true;      // TEMP
        LayoutTrace.Counting = true;   // TEMP
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
        if (RuntimeStats.LastRecordMs > _windowMaxRecord) _windowMaxRecord = RuntimeStats.LastRecordMs;
        if (RuntimeStats.LastApplyMs > _windowMaxApply) _windowMaxApply = RuntimeStats.LastApplyMs;
        if (_windowElapsed < RefreshSeconds) return false;

        var measure = MeasurableUIComponent.TotalMeasureCalls;
        var arrange = MeasurableUIComponent.TotalArrangeCalls;
        var bindings = RuntimeStats.BindingUpdatesApplied;
        var fps = _windowFrames / _windowElapsed;   // the LOOP's rate: Update + record

        // ...and the PRESENTED rate, which with a render thread is a different number entirely - the compositor keeps
        // presenting while a heavy Update crawls. Equal to the loop's rate in the default inline path.
        var presented = RuntimeStats.PresentedFrames;
        var renderFps = (presented - _lastPresented) / _windowElapsed;
        _lastPresented = presented;


        // Frame breakdown (averages over the window, so they sum to ~frame time). "other" = the residual the render
        // pipeline can't see: GPU-fence wait in BeginDraw + swapchain blit + Present. Phase 0 of the render-cache
        // redesign - shows whether the per-frame cache REBUILD (build+proc) or something else (GPU/present) dominates.
        var f = 1.0 / _windowFrames;
        var frameMs = _windowElapsed * 1000.0 * f;
        var avgLayout = _sumLayout * f;
        var avgBuild = _sumBuild * f; var avgProc = _sumProc * f; var avgDraw = _sumDraw * f; var avgProcs = _sumProcs * f;
        var other = Math.Max(0, frameMs - avgLayout - avgBuild - avgProc - avgDraw - avgProcs);

        target.Text =
            $"render {renderFps,5:F0} fps     loop {fps,5:F0} fps\n" +
            $"frame {frameMs,5:F1} ms   layout {avgLayout,5:F2} (max {_windowMaxLayoutMs,4:F1}){(_windowDeferred ? " [DEFERRED]" : "")}\n" +
            $"build/proc/draw  {avgBuild,4:F1} / {avgProc,4:F1} / {avgDraw,4:F1} ms\n" +
            $"processors {avgProcs,4:F1}    other {other,4:F1} ms\n" +
            $"measure/arrange  {measure - _lastMeasure} / {arrange - _lastArrange}\n" +
            $"bindings {bindings - _lastBindings}    anim {AnimationManager.ActiveCount}";

        // TEMP: dump the in-memory frame ring once a second (four refresh windows) - one file write, not one per frame.
        if (++_traceWindows >= 4)
        {
            _traceWindows = 0;
            System.IO.File.WriteAllText(@"C:\AdamantiumEngine\frames.log", FrameTrace.Dump());
            System.IO.File.WriteAllText(@"C:\AdamantiumEngine\incidents.log", FrameTrace.DumpIncidents());

            // WHO marked layout dirty over the last second, biggest first. Reset per dump, so a drag reads as "this is
            // what one second of dragging costs" rather than as a total that only ever grows - and the BUSIEST second so
            // far is kept beside it, because the second worth reading is never the one that happens to be current when
            // somebody looks.
            var layout = LayoutTrace.DumpCounts();
            var total = LayoutTrace.TotalCount();
            System.IO.File.WriteAllText(@"C:\AdamantiumEngine\layout.log", layout);
            if (total > _peakLayout)
            {
                _peakLayout = total;
                System.IO.File.WriteAllText(@"C:\AdamantiumEngine\layout-peak.log", layout);
            }

            LayoutTrace.ResetCounts();

            var churn = DumpChurn(out var churnTotal);
            System.IO.File.WriteAllText(@"C:\AdamantiumEngine\churn.log", churn);
            if (churnTotal > _peakChurn)
            {
                _peakChurn = churnTotal;
                System.IO.File.WriteAllText(@"C:\AdamantiumEngine\churn-peak.log", churn);
            }

            lock (_churn) _churn.Clear();

            // ...and what the BUILD spent itself on over the same second: which half, and whether the apply was building
            // units from scratch or updating the ones it had. Peak-kept for the same reason.
            var created = RuntimeStats.UnitsCreated - _lastCreated;
            var updated = RuntimeStats.UnitsUpdated - _lastUpdated;
            var commands = RuntimeStats.CommandsApplied - _lastCommands;
            _lastCreated = RuntimeStats.UnitsCreated;
            _lastUpdated = RuntimeStats.UnitsUpdated;
            _lastCommands = RuntimeStats.CommandsApplied;

            // MAXIMA over the window, not the value that happened to be current at the dump - a spike lasts one frame and
            // the dump reads a quiet one.
            var build = $"record max {_windowMaxRecord:F2} ms   apply max {_windowMaxApply:F2} ms\n" +
                        $"units created {created}   updated {updated}   commands {commands}\n" +
                        $"measure {measure - _lastMeasure}   arrange {arrange - _lastArrange}   maxLayout {_windowMaxLayoutMs:F1} ms\n";
            System.IO.File.WriteAllText(@"C:\AdamantiumEngine\build.log", build);
            if (created + updated > _peakUnits)
            {
                _peakUnits = created + updated;
                System.IO.File.WriteAllText(@"C:\AdamantiumEngine\build-peak.log", build);
            }

            // EVERY second, appended. A "peak" file picks one second by one criterion and throws the rest away - and the
            // criterion picked the initial fill, which measures more than any drag ever will, so the file froze on the
            // startup second and the thing being hunted was never written at all. A history cannot lose the event.
            // THE WORST SECOND, kept whole. Every peak file above picks its second by ITS OWN criterion, and the second a
            // tester reports is picked by a different one entirely - the picture stuttered. So keep the one where the
            // FEWEST frames went out, with everything that happened in it side by side: that is the second to read.
            // The first ten are skipped - a cold start beats any stutter and would own this file forever.
            // BOTH rates, and the worse of them decides. The render thread keeps presenting while a heavy Update crawls,
            // so a stalled loop leaves the presented count almost untouched - and a stalled loop is exactly what a
            // stuttering picture is. Judged by the presented count alone, this file never noticed the event at all.
            var framesThisSecond = presented - _worstSecondFrom;
            var loopThisSecond = _secondLoopFrames;
            _worstSecondFrom = presented;
            _secondLoopFrames = 0;
            if (_second > 10 && Math.Min(framesThisSecond, loopThisSecond) < _worstSecondFrames)
            {
                _worstSecondFrames = Math.Min(framesThisSecond, loopThisSecond);
                System.IO.File.WriteAllText(@"C:\AdamantiumEngine\worst-second.log",
                    $"WORST SECOND SO FAR: loop {loopThisSecond} fps, presented {framesThisSecond} fps (second {_second})\n\n"
                    + build + "\n" + layout + "\n" + churn + "\n"
                    + "long frames in it:\n" + FrameTrace.DumpIncidentsSince(_worstSecondMark));
            }
            _worstSecondMark = FrameTrace.IncidentCount;

            _second++;
            // EVERY second gets a line about what entered or left the drawn set, busy or not. The question a churn number
            // answers is "is this still going on?", and a file that keeps only the busy seconds cannot tell a fill that
            // ends from one that never does.
            System.IO.File.AppendAllText(@"C:\AdamantiumEngine\churn-history.log",
                $"second {_second}: churn {churnTotal}, created {created}, updated {updated}, arrange {arrange - _lastArrange}\n");
            if (measure - _lastMeasure > 50 || created + updated > 500)
            {
                System.IO.File.AppendAllText(@"C:\AdamantiumEngine\layout-history.log",
                    $"---- second {_second} ----\n{build}{layout}\n");
            }
        }

        _lastMeasure = measure; _lastArrange = arrange; _lastBindings = bindings;
        _secondLoopFrames += _windowFrames;
        _windowElapsed = 0; _windowFrames = 0; _windowMaxLayoutMs = 0; _windowDeferred = false;
        _windowMaxRecord = 0; _windowMaxApply = 0;
        _sumLayout = _sumBuild = _sumProc = _sumDraw = _sumProcs = 0;
        return false;   // keep ticking
    }
}
