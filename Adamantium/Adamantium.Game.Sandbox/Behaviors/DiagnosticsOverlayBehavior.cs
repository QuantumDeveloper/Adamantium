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
    // The PLATE is permanent - it shows what a frame costs, on screen, and that is cheap. Its FILE DUMPS are not: eight
    // writes a second plus a counter on every measure and arrange, which on an otherwise idle scene is the instrument
    // becoming the thing it measures. Off unless asked for, so the numbers on screen are the app's and not mine.
    private static readonly bool Dumps =
        Environment.GetEnvironmentVariable("ADAM_PLATE_DUMPS") == "1" || Environment.GetEnvironmentVariable("ADAM_PROBE_LOG") != null;

    private const double RefreshSeconds = 0.25;   // rewrite the text ~4x/sec - readable, and cheap (no per-frame raster)

    private long _lastMeasure, _lastArrange, _lastBindings, _lastPresented;
    private double _windowElapsed, _windowMaxLayoutMs;
    private double _sumLayout, _sumBuild, _sumProc, _sumDraw, _sumProcs;   // per-frame sums -> averages over the window

    // WHAT "other" WAS MADE OF. It used to be one residual with a comment naming three things at once - the GPU fence
    // wait, the swapchain blit and Present - so a frame that got slower said only "something outside the renderer".
    // Both halves were already measured and neither was shown; the third, the GC, was not measured at all and is the
    // one that decides an FPS figure most often (a loop that never stalls still loses frames to a collection).
    private double _sumWait, _sumPresent;
    private TimeSpan _lastGcPause;
    private int _windowFrames;
    private bool _windowDeferred;
    private bool _running;
    private int _traceWindows;   // TEMP
    private int _peakLayout;     // TEMP: busiest layout-invalidation second seen so far
    private long _peakUnits, _lastCreated, _lastUpdated, _lastCommands;   // TEMP: busiest build second
    private int _second;   // TEMP: index in the appended history
    private double _windowMaxRecord, _windowMaxApply;

    // The same three the plate now names, kept for the FILE as well: a dump that says the build cost nothing and stops
    // there leaves the frame unaccounted for, which is exactly where this measurement kept ending up. Maxima for the
    // two waits (a spike is one frame and an average hides it) and the SUM for the collector, because what a second
    // lost to it is the whole question.
    private double _windowMaxWait, _windowMaxPresent, _windowGcMs;

    // The rest of the RENDER frame, which the loop's budget says nothing about: a presented frame is draw plus these,
    // and while only the fence wait and Present were shown, a draw that halved without the frame rate moving had
    // nowhere to be explained from.
    private double _windowMaxBegin, _windowMaxEnd, _windowMaxSubmit, _windowMaxPre;

    // ...and the two that CLOSE the accounting: what a loop frame cost, and what the entity processors took out of it.
    // Without them the file lists a handful of tenths and stops, while the frame it is describing is eight
    // milliseconds - and a reader is left to assume the rest is the part that was named.
    private double _windowMaxFrame, _windowMaxProcs, _secMaxFrame, _secMaxProcs, _windowMaxDraw;

    // The worst draw's own phases, so the four numbers describe ONE frame - see where they are taken.
    private double _drawSetup, _drawPaint, _drawMoved, _drawOps, _drawWalk, _drawAnim, _drawArena, _drawBrush;
    private bool _drawReplayed;

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
        if (!_running || !Dumps) return;   // counting and writing are for a HUNT, not for every hover
        var key = $"{what} {c.GetType().Name} '{(c as UIComponent)?.Name}' -> {c.Visibility}";
        lock (_churn) _churn[key] = _churn.TryGetValue(key, out var had) ? had + 1 : 1;

        // A SHOW/HIDE is rare, and it is the event being hunted - so it is written the MOMENT it happens rather than
        // counted into a second that may well be read after that second has been reset. A ring, a peak and a per-second
        // tally all throw away exactly the thing somebody is trying to reproduce by hand.
        if (what != "hidden-flip") return;

        try
        {
            System.IO.File.AppendAllText(@"C:\AdamantiumEngine\flips.log", $"{DateTime.Now:HH:mm:ss.fff}  {key}\n");
        }
        catch
        {
            // a diagnostic must never be the reason a frame fails
        }
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
        FrameTrace.Enabled = Dumps;      // TEMP
        LayoutTrace.Counting = Dumps;   // TEMP
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
        _sumWait   += Adamantium.Graphics.GraphicsDevice.LastFenceWaitMs;
        _sumPresent += RuntimeStats.LastPresentMs;
        if (RuntimeStats.LastRecordMs > _windowMaxRecord) _windowMaxRecord = RuntimeStats.LastRecordMs;
        if (RuntimeStats.LastApplyMs > _windowMaxApply) _windowMaxApply = RuntimeStats.LastApplyMs;
        if (Adamantium.Graphics.GraphicsDevice.LastFenceWaitMs > _windowMaxWait)
            _windowMaxWait = Adamantium.Graphics.GraphicsDevice.LastFenceWaitMs;
        if (RuntimeStats.LastPresentMs > _windowMaxPresent) _windowMaxPresent = RuntimeStats.LastPresentMs;
        if (RuntimeStats.LastProcessorsMs > _windowMaxProcs) _windowMaxProcs = RuntimeStats.LastProcessorsMs;
        if (RuntimeStats.LastBeginDrawMs > _windowMaxBegin) _windowMaxBegin = RuntimeStats.LastBeginDrawMs;
        if (RuntimeStats.LastEndDrawMs > _windowMaxEnd) _windowMaxEnd = RuntimeStats.LastEndDrawMs;
        if (RuntimeStats.LastSubmitMs > _windowMaxSubmit) _windowMaxSubmit = RuntimeStats.LastSubmitMs;
        if (RuntimeStats.LastPreRenderMs > _windowMaxPre) _windowMaxPre = RuntimeStats.LastPreRenderMs;
        if (RuntimeStats.LastRenderDrawMs > _windowMaxDraw) _windowMaxDraw = RuntimeStats.LastRenderDrawMs;

        // Taken from the SAME frame as the draw they explain: sampling each phase's own maximum would report four
        // different frames' worst moments as though they were one frame, and their sum would then exceed any draw that
        // ever happened.
        if (RuntimeStats.LastRenderDrawMs >= _windowMaxDraw)
        {
            _drawSetup = RuntimeStats.LastDrawSetupMs;
            _drawPaint = RuntimeStats.LastDrawPaintMs;
            _drawMoved = RuntimeStats.LastDrawMovedMs;
            _drawOps = RuntimeStats.LastDrawOpsMs;
            _drawWalk = RuntimeStats.LastDrawWalkMs;
            _drawReplayed = RuntimeStats.LastDrawReplayed;
            _drawAnim = RuntimeStats.LastDrawAnimMs;
            _drawArena = RuntimeStats.LastDrawArenaPaintMs;
            _drawBrush = RuntimeStats.LastDrawBrushRepaintMs;
        }
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
        var avgWait = _sumWait * f; var avgPresent = _sumPresent * f;

        // The collector's OWN stall, which no per-frame stopwatch in the loop can see: the runtime reports the total
        // time the managed threads were paused, so the delta over this window divided by its frames is what a collection
        // cost each of them. A loop whose every measured part is fast and whose frame rate is still half of what it was
        // is the case this exists for.
        var gcPause = GC.GetTotalPauseDuration();
        var gcWindowMs = (gcPause - _lastGcPause).TotalMilliseconds;
        var avgGc = gcWindowMs * f;
        _lastGcPause = gcPause;
        _windowGcMs += gcWindowMs;
        if (frameMs > _secMaxFrame) _secMaxFrame = frameMs;
        if (_windowMaxProcs > _secMaxProcs) _secMaxProcs = _windowMaxProcs;

        var other = Math.Max(0, frameMs - avgLayout - avgBuild - avgProc - avgDraw - avgProcs - avgWait - avgPresent - avgGc);

        target.Text =
            $"render {renderFps,5:F0} fps     loop {fps,5:F0} fps\n" +
            $"frame {frameMs,5:F1} ms   layout {avgLayout,5:F2} (max {_windowMaxLayoutMs,4:F1}){(_windowDeferred ? " [DEFERRED]" : "")}\n" +
            $"build/proc/draw  {avgBuild,4:F1} / {avgProc,4:F1} / {avgDraw,4:F1} ms\n" +
            $"gpuWait {avgWait,5:F2}  present {avgPresent,5:F2}  gc {avgGc,5:F2} ms\n" +
            $"processors {avgProcs,4:F1}    other {other,4:F1} ms\n" +
            $"measure/arrange  {measure - _lastMeasure} / {arrange - _lastArrange}\n" +
            $"bindings {bindings - _lastBindings}    anim {AnimationManager.ActiveCount}";

        // TEMP: dump the in-memory frame ring once a second (four refresh windows) - one file write, not one per frame.
        if (Dumps && ++_traceWindows >= 4)
        {
            _traceWindows = 0;
            // The whole RING is deliberately NOT dumped. It was 723 KB rebuilt into one string and written to disk every
            // second, on a scene otherwise doing nothing - a large-object allocation and 700 KB of I/O per second, which
            // is the instrument becoming the thing it measures. It showed as a static tab whose frame time wandered
            // between 0.9 and 2.2 ms. Only the LONG frames are kept, which is all anybody has ever read.
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
            // Everything the loop actually DID this second, at its worst. Compared against the pacing budget below.
            var work = _windowMaxRecord + _windowMaxApply + _windowMaxLayoutMs + _windowMaxWait
                       + _windowMaxPresent + _secMaxProcs;

            var build = $"record max {_windowMaxRecord:F2} ms   apply max {_windowMaxApply:F2} ms\n" +
                        $"units created {created}   updated {updated}   commands {commands}\n" +
                        $"measure {measure - _lastMeasure}   arrange {arrange - _lastArrange}   maxLayout {_windowMaxLayoutMs:F1} ms\n" +
                        $"gpuWait max {_windowMaxWait:F2} ms   present max {_windowMaxPresent:F2} ms   " +
                        $"gc {_windowGcMs:F2} ms this second\n" +
                        $"draw max {_windowMaxDraw:F2} ms   presented {renderFps:F0} fps\n" +
                        $"  of that draw ({(_drawReplayed ? "REPLAY" : "WALK")}): setup {_drawSetup:F2}  paint {_drawPaint:F2}  " +
                        $"moved {_drawMoved:F2}  ops {_drawOps:F2}  walk {_drawWalk:F2} ms\n" +
                        $"    paint = anim {_drawAnim:F2} + arena {_drawArena:F2} + brushes {_drawBrush:F2} ms\n" +
                        $"render frame rest: preRender {_windowMaxPre:F2}  beginDraw {_windowMaxBegin:F2}  " +
                        $"overlays {_windowMaxProcs:F2}  endDraw {_windowMaxEnd:F2}  submit {_windowMaxSubmit:F2}  " +
                        $"present {_windowMaxPresent:F2} ms\n" +
                        $"loop frame max {_secMaxFrame:F2} ms (paced to {Adamantium.UI.UIApplication.UpdateRateHz} Hz " +
                        $"= {1000.0 / Math.Max(1, Adamantium.UI.UIApplication.UpdateRateHz):F2} ms)   " +
                        $"processors max {_secMaxProcs:F2} ms\n" +
                        // The remainder against the PACING BUDGET, not against the frame. The update thread is capped, so
                        // a frame that fits inside its budget spends the rest ASLEEP - and subtracting the parts from the
                        // whole frame reports that sleep as eight unexplained milliseconds, which is the wrong thing to
                        // go looking for. What is worth naming is the work that fits nowhere: only if this grows past
                        // the budget is the loop actually behind.
                        $"work {work:F2} ms of budget   over budget {Math.Max(0, work - 1000.0 / Math.Max(1, Adamantium.UI.UIApplication.UpdateRateHz)):F2} ms\n";
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
        _windowMaxWait = 0; _windowMaxPresent = 0; _windowGcMs = 0;
        _secMaxFrame = 0; _secMaxProcs = 0; _windowMaxProcs = 0; _windowMaxFrame = 0; _windowMaxDraw = 0;
        _windowMaxBegin = 0; _windowMaxEnd = 0; _windowMaxSubmit = 0; _windowMaxPre = 0;
        _sumLayout = _sumBuild = _sumProc = _sumDraw = _sumProcs = _sumWait = _sumPresent = 0;
        return false;   // keep ticking
    }
}
