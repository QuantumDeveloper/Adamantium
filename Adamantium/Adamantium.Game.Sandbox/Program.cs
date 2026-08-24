using Adamantium.UI;
using System;

namespace Adamantium.Game.Sandbox;

public class Program
{
    // OLE (the OS drag-drop bridge) requires the UI thread to be a single-threaded apartment - the same requirement
    // WPF/WinForms put on their entry point. Without it the app still runs; only drags to/from other applications are off.
    [STAThread]
    public static void Main(string[] args)
    {
        // A dragged picture should also travel as a file: many targets (Paint 3D, packaged apps) ask for a file list
        // and never look at a bitmap. Off by default in the engine because it writes to disk - an application opts in.
        UI.Input.DragDropOptions.OfferImagesAsFiles = true;

        var gameApp = new AdamantiumGameApplication();
        if (Environment.GetEnvironmentVariable("ADAM_PROBE_LOG") is { } log)
        {
            var t = new System.Threading.Thread(() =>
            {
                // Let the first tab SETTLE. Six seconds was enough once and is not any more - a heavy tab now builds for
                // longer than that, and a probe that starts inside the build measures the build. Configurable, because
                // "how long does this take to settle" is exactly what changes.
                System.Threading.Thread.Sleep(Environment.GetEnvironmentVariable("ADAM_PROBE_SETTLE") is { } warm ? int.Parse(warm) : 6000);

                // WHO enters or leaves the drawn set while the probe runs. Counted for the whole window, not just the
                // self-driven pan: a spike a hand reproduces has to be attributable the same way one the harness makes is.
                var churn = new System.Collections.Generic.Dictionary<string, int>();
                void Note(string what, Adamantium.UI.Core.IUIComponent c)
                {
                    var key = what + " " + c.GetType().Name + " '" + (c as Adamantium.UI.Controls.Base.UIComponent)?.Name + "' -> "
                              + c.Visibility;
                    lock (churn) churn[key] = churn.TryGetValue(key, out var had) ? had + 1 : 1;
                }
                Adamantium.UI.Core.VisualTreeNotifications.Attached += c => Note("attached", c);
                Adamantium.UI.Core.VisualTreeNotifications.Detached += c => Note("detached", c);
                Adamantium.UI.Core.VisualTreeNotifications.VisibilityChanged += c => Note("collapsed-flip", c);
                Adamantium.UI.Core.VisualTreeNotifications.ShownOrHidden += c => Note("hidden-flip", c);
                Adamantium.UI.Core.VisualTreeNotifications.ClipChanged += c => Note("clip", c);

                var startFrames = Adamantium.UI.Core.Diagnostics.RuntimeStats.PresentedFrames;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var limit = Environment.GetEnvironmentVariable("ADAM_PROBE_SECONDS") is { } sec ? double.Parse(sec) : 20;
                double layout = 0;
                double sumBegin = 0, sumEnd = 0, sumSubmit = 0, sumPresent = 0, sumFence = 0, sumAcquire = 0, sumSetup = 0;
                double sumPre = 0;
                double sumRecord = 0, sumApply = 0, sumProc = 0, sumDraw = 0, sumProcessors = 0, sumLayout = 0;
                double maxDraw = 0, maxApply = 0, maxRecord = 0;
                long samples = 0;
                var secondStart = System.Diagnostics.Stopwatch.GetTimestamp();
                var secondFrames = Adamantium.UI.Core.Diagnostics.RuntimeStats.PresentedFrames;
                var worstSecond = long.MaxValue;

                // LOOP responsiveness, measured from OUTSIDE the loop: post a no-op and time how long it takes to come
                // back. Frames-per-second cannot see this - the RENDER thread goes on presenting a replayed stream while
                // the loop is stuck, so a window that answers nothing for ten seconds still reports hundreds of frames a
                // second. That is exactly the report "the colour picker hangs the app", and it is why the first probe
                // found nothing: it was watching the wrong thread.
                var loopDispatcher = Adamantium.UI.Threading.Dispatcher.CurrentDispatcher;
                var loopSentAt = 0L;         // when the outstanding no-op was posted; 0 = none in flight
                double loopWorstMs = 0;      // the longest the loop took to answer during this second
                var loopBindings = Adamantium.UI.Core.Diagnostics.RuntimeStats.BindingUpdatesApplied;
                var loopMeasures = Adamantium.UI.Controls.Base.MeasurableUIComponent.TotalMeasureCalls;
                var loopArranges = Adamantium.UI.Controls.Base.MeasurableUIComponent.TotalArrangeCalls;
                while (sw.Elapsed.TotalSeconds < limit)
                {
                    var st = typeof(Adamantium.UI.Core.Diagnostics.RuntimeStats);
                    if (Adamantium.UI.Core.Diagnostics.RuntimeStats.LastLayoutPassMs > layout) layout = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastLayoutPassMs;
                    sumPre += Adamantium.UI.Core.Diagnostics.RuntimeStats.LastPreRenderMs;
                    sumBegin += Adamantium.UI.Core.Diagnostics.RuntimeStats.LastBeginDrawMs;
                    sumEnd += Adamantium.UI.Core.Diagnostics.RuntimeStats.LastEndDrawMs;
                    sumSubmit += Adamantium.UI.Core.Diagnostics.RuntimeStats.LastSubmitMs;
                    sumPresent += Adamantium.UI.Core.Diagnostics.RuntimeStats.LastPresentMs;
                    sumFence += Adamantium.Graphics.GraphicsDevice.LastFenceWaitMs;
                    sumAcquire += Adamantium.Graphics.GraphicsDevice.LastAcquireMs;
                    sumSetup += Adamantium.Graphics.GraphicsDevice.LastBeginSetupMs;
                    sumLayout += Adamantium.UI.Core.Diagnostics.RuntimeStats.LastLayoutPassMs;
                    sumRecord += Adamantium.UI.Core.Diagnostics.RuntimeStats.LastRecordMs;
                    sumApply += Adamantium.UI.Core.Diagnostics.RuntimeStats.LastApplyMs;
                    sumProc += Adamantium.UI.Core.Diagnostics.RuntimeStats.LastRenderProcMs;
                    sumDraw += Adamantium.UI.Core.Diagnostics.RuntimeStats.LastRenderDrawMs;
                    sumProcessors += Adamantium.UI.Core.Diagnostics.RuntimeStats.LastProcessorsMs;
                    if (Adamantium.UI.Core.Diagnostics.RuntimeStats.LastRenderDrawMs > maxDraw) maxDraw = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastRenderDrawMs;
                    if (Adamantium.UI.Core.Diagnostics.RuntimeStats.LastApplyMs > maxApply) maxApply = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastApplyMs;
                    if (Adamantium.UI.Core.Diagnostics.RuntimeStats.LastRecordMs > maxRecord) maxRecord = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastRecordMs;
                    samples++;

                    // One no-op in flight at a time. While it is outstanding the loop has not answered, so the stall is
                    // reported LIVE rather than only once it ends - a hang that outlasts the run would otherwise vanish.
                    if (loopSentAt == 0)
                    {
                        var sentAt = System.Diagnostics.Stopwatch.GetTimestamp();
                        loopSentAt = sentAt;
                        loopDispatcher?.Post(() =>
                        {
                            var ms = System.Diagnostics.Stopwatch.GetElapsedTime(sentAt).TotalMilliseconds;
                            if (ms > loopWorstMs) loopWorstMs = ms;
                            loopSentAt = 0;
                        });
                    }
                    else
                    {
                        // Read ONCE: the dispatcher clears this field from the other thread, and reading it twice let a
                        // zero land in the elapsed call - which is how a stall came out as 994 560 350 ms.
                        var outstanding = loopSentAt;
                        if (outstanding != 0)
                        {
                            var waiting = System.Diagnostics.Stopwatch.GetElapsedTime(outstanding).TotalMilliseconds;
                            if (waiting > loopWorstMs) loopWorstMs = waiting;
                        }
                    }

                    System.Threading.Thread.Sleep(15);

                    // The WORST SECOND, not the average: a drop that lasts a few seconds disappears into a 40-second
                    // mean, and the per-frame ring only holds the last couple of thousand frames - at a thousand a
                    // second that is the last two. A minimum survives both.
                    if (System.Diagnostics.Stopwatch.GetElapsedTime(secondStart).TotalSeconds >= 1.0)
                    {
                        var thisSecond = Adamantium.UI.Core.Diagnostics.RuntimeStats.PresentedFrames - secondFrames;
                        if (thisSecond < worstSecond) worstSecond = thisSecond;

                        // TEMP: one line PER SECOND. A single "worst second" over a long window cannot say WHEN it
                        // happened, so a drop while building the tab and a drop while dragging a slider read the same -
                        // and three times in a row I explained the wrong moment. The timeline tells them apart: find the
                        // seconds where the fps matches what the plate showed, and read what those seconds were doing.
                        var bindsNow = Adamantium.UI.Core.Diagnostics.RuntimeStats.BindingUpdatesApplied;
                        var measuresNow = Adamantium.UI.Controls.Base.MeasurableUIComponent.TotalMeasureCalls;
                        var arrangesNow = Adamantium.UI.Controls.Base.MeasurableUIComponent.TotalArrangeCalls;

                        System.IO.File.AppendAllText(log + ".seconds.txt",
                            $"t={sw.Elapsed.TotalSeconds:00} fps={thisSecond,5} " +
                            $"loopMs={loopWorstMs,7:0} " +
                            $"binds={bindsNow - loopBindings,8} " +
                            $"measure={measuresNow - loopMeasures,8} arrange={arrangesNow - loopArranges,8} " +
                            $"walks={Adamantium.UI.Core.Diagnostics.FrameTrace.Walks} " +
                            $"layoutMs={Adamantium.UI.Core.Diagnostics.RuntimeStats.LastLayoutPassMs:0.0} " +
                            $"drawMs={Adamantium.UI.Core.Diagnostics.RuntimeStats.LastRenderDrawMs:0.00}" + Environment.NewLine);

                        loopWorstMs = 0;
                        loopBindings = bindsNow;
                        loopMeasures = measuresNow;
                        loopArranges = arrangesNow;

                        secondFrames = Adamantium.UI.Core.Diagnostics.RuntimeStats.PresentedFrames;
                        secondStart = System.Diagnostics.Stopwatch.GetTimestamp();
                    }
                }
                var inv = samples > 0 ? 1.0 / samples : 0;

                // TEMP (ADAM_VISIT_TABS=1): open every tab in turn, then report which ones the app survived.
                // Shader objects are created LAZILY, at the first draw that needs a pass - so a run that never leaves the
                // home tab never creates the gradient / pattern / fractal / image shaders at all, and "it started" proves
                // nothing about them. Anything that only breaks on those passes needs the tab to be visited.
                if (Environment.GetEnvironmentVariable("ADAM_VISIT_TABS") == "1")
                {
                    var win = Adamantium.UI.UIApplication.Current?.MainWindow;
                    var tabs = win?.Content is Adamantium.UI.Core.IUIComponent c ? Find<Adamantium.UI.Controls.TabControl>(c) : null;
                    var visited = new System.Text.StringBuilder();

                    if (tabs != null)
                    {
                        for (var i = 0; i < tabs.Items.Count; i++)
                        {
                            var index = i;
                            Adamantium.UI.Threading.Dispatcher.CurrentDispatcher?.Post(() => tabs.SelectedIndex = index);
                            System.Threading.Thread.Sleep(1500);   // let it build, lay out and DRAW at least once
                            visited.Append(index).Append(':')
                                   .Append(Adamantium.UI.Core.Diagnostics.RuntimeStats.PresentedFrames).Append(' ');
                            System.IO.File.WriteAllText(log + ".tabs.txt", visited.ToString());
                        }
                    }

                    System.IO.File.WriteAllText(log + ".tabs.txt",
                        (tabs == null ? "no TabControl found" : "survived tabs " + visited) + Environment.NewLine);
                }


                // TEMP (ADAM_STRIP_SCROLL=1): pan the tab STRIP back and forth while a heavy tab is open - the reported
                // drop from ~700 fps to ~100. Driven from here so the cost can be attributed without a hand on the mouse.
                if (Environment.GetEnvironmentVariable("ADAM_STRIP_SCROLL") == "1")
                {
                    var win = Adamantium.UI.UIApplication.Current?.MainWindow;
                    var strip = win?.Content is Adamantium.UI.Core.IUIComponent c ? FindStrip(c) : null;
                    if (strip != null)
                    {
                        var startedAt = Adamantium.UI.Core.Diagnostics.RuntimeStats.PresentedFrames;
                        var clock = System.Diagnostics.Stopwatch.StartNew();
                        // Park just inside the far end, so the small oscillation below cannot reach either end whatever
                        // the strip's range turns out to be (it differs run to run with the headers' widths).
                        Adamantium.UI.Threading.Dispatcher.CurrentDispatcher?.Post(() => { strip.Pan(1e6); strip.Pan(-24); });
                        System.Threading.Thread.Sleep(300);
                        Adamantium.UI.Core.Diagnostics.LayoutTrace.Counting = true;

                        // TWO costs, measured apart, because they answer different questions and only one of them is the
                        // strip's own. MOVING: a small oscillation that stays inside the range - nothing crosses the clip,
                        // no chevron flips, so what it costs is the move itself. TRAVELLING: full sweeps that reach both
                        // ends - headers cross the clip and the chevrons appear and disappear, each of which is a
                        // structural change. Mixed into one number they hid each other, and the mix swung run to run with
                        // however long the strip happened to be.
                        var moveFrames = Run(4, () => strip.Pan(_pan = -_pan));
                        var moveFps = moveFrames / 4.0;
                        var movePans = _panned;

                        var travelFrames = Run(4, () =>
                        {
                            if (strip.Pan(_sweep)) return true;
                            _sweep = -_sweep;   // reached an end - turn around
                            return strip.Pan(_sweep);
                        });
                        var travelFps = travelFrames / 4.0;

                        var panned = Adamantium.UI.Core.Diagnostics.RuntimeStats.PresentedFrames - startedAt;
                        Adamantium.UI.Core.Diagnostics.LayoutTrace.Counting = false;
                        System.IO.File.AppendAllText(log + ".strip.txt",
                            $"moving the strip (no clip crossings): {moveFps:0} fps, {movePans} pans" + Environment.NewLine
                            + $"travelling end to end: {travelFps:0} fps, {_panned - movePans} pans" + Environment.NewLine
                            + Adamantium.UI.Core.Diagnostics.FrameTrace.Percentiles() + Environment.NewLine
                            + Adamantium.UI.Rendering.LayerProbe.Dump() + Environment.NewLine
                            + Adamantium.UI.Core.Diagnostics.LayoutTrace.DumpCounts() + Environment.NewLine);
                    }
                }

                // TEMP (ADAM_LIST_SCROLL=1): scroll the heavy tab's own list, which is the workload the per-layer arena
                // (§5a phase 3) was argued from - slot renumbering, segment cuts, layers relocated out of their room.
                // Panning the tab strip barely touches any of that, so it cannot answer whether that rewrite is worth it.
                if (Environment.GetEnvironmentVariable("ADAM_LIST_SCROLL") == "1")
                {
                    var win = Adamantium.UI.UIApplication.Current?.MainWindow;
                    var viewer = win?.Content is Adamantium.UI.Core.IUIComponent c ? Find<Adamantium.UI.Controls.ScrollViewer>(c) : null;
                    if (viewer != null)
                    {
                        Adamantium.UI.Rendering.LayerProbe.Reset();
                        var down = 40.0;
                        var listFrames = Run(8, () =>
                        {
                            var at = viewer.ScrollOffset;
                            if (at.Y + down < 0) down = -down;
                            viewer.SetScrollOffset(new Adamantium.Mathematics.Vector2(at.X, at.Y + down));
                            if (viewer.ScrollOffset.Y == at.Y) down = -down;   // hit an end - turn around
                            return true;
                        });
                        System.IO.File.AppendAllText(log + ".list.txt",
                            $"scrolling the list: {listFrames / 8.0:0} fps" + Environment.NewLine
                            + Adamantium.UI.Core.Diagnostics.FrameTrace.Percentiles() + Environment.NewLine
                            + Adamantium.UI.Rendering.LayerProbe.Dump() + Environment.NewLine);
                    }
                }

                // TEMP (ADAM_CLOSE_FLIP=1): show and hide a tab's close button, which is what HOVERING one does - the
                // reported "the plate stops updating and the frame gets worse when the pointer rests on a close button".
                // Driven from here so it can be attributed without a hand on the mouse.
                if (Environment.GetEnvironmentVariable("ADAM_CLOSE_FLIP") == "1")
                {
                    var win = Adamantium.UI.UIApplication.Current?.MainWindow;
                    var button = win?.Content is Adamantium.UI.Core.IUIComponent c ? FindNamed(c, "PART_CloseButton") : null;
                    if (button != null)
                    {
                        Adamantium.UI.Rendering.LayerProbe.Reset();
                        var show = true;
                        var flipFrames = Run(8, () =>
                        {
                            button.Visibility = show ? Adamantium.UI.Core.Visibility.Visible : Adamantium.UI.Core.Visibility.Hidden;
                            show = !show;
                            return true;
                        });
                        System.IO.File.AppendAllText(log + ".flip.txt",
                            $"flipping a close button: {flipFrames / 8.0:0} fps" + Environment.NewLine
                            + Adamantium.UI.Core.Diagnostics.FrameTrace.Percentiles() + Environment.NewLine
                            + Adamantium.UI.Rendering.LayerProbe.Dump() + Environment.NewLine);
                    }
                }

                // TEMP (ADAM_SPINNER_KIND=Dots|Ripple|...): press "+25 of that kind" and report the frames it costs.
                // The A/B the Animations tab was built for: equal counts of indicators that animate DIFFERENT things -
                // Dots move transforms only (composited), Ripple also animates element Opacity (no channel yet). Driven
                // from here so the two numbers are measured the same way rather than read off a plate by eye.
                if (Environment.GetEnvironmentVariable("ADAM_SPINNER_KIND") is { } kind)
                {
                    // Reached through the BUTTON rather than the view-model: the button is what a hand would press, and
                    // it needs nothing to be visible from here that the markup does not already expose.
                    var win = Adamantium.UI.UIApplication.Current?.MainWindow;
                    var button = win?.Content is Adamantium.UI.Core.IUIComponent root
                        ? FindButton(root, "+25 " + kind)
                        : null;

                    if (button != null)
                    {
                        // 25 at a time is below the noise - two runs of the same kind differed more than the two kinds
                        // did. Pressed repeatedly instead, because the difference between a composited channel and a
                        // re-bake is a PER-INSTANCE cost and only shows once there are enough instances to see it.
                        var presses = Environment.GetEnvironmentVariable("ADAM_SPINNER_PRESSES") is { } p ? int.Parse(p) : 10;
                        var idle = Run(3, () => false);

                        for (var i = 0; i < presses; i++)
                        {
                            Adamantium.UI.Threading.Dispatcher.CurrentDispatcher?.Post(() => button.Command?.Execute(null));
                            System.Threading.Thread.Sleep(400);
                        }

                        System.Threading.Thread.Sleep(3000);   // let them realize and settle before counting

                        var busy = Run(8, () => false);
                        System.IO.File.AppendAllText(log + ".spinner.txt",
                            $"{kind} x{presses * 25}: idle {idle / 3.0:0} fps -> running {busy / 8.0:0} fps " +
                            $"({1000.0 / Math.Max(1, idle / 3.0):0.00} -> {1000.0 / Math.Max(1, busy / 8.0):0.00} ms)" + Environment.NewLine);
                    }
                    else
                    {
                        System.IO.File.AppendAllText(log + ".spinner.txt",
                            $"{kind}: button not found - is the Animations tab open?" + Environment.NewLine);
                    }
                }

                // TEMP (ADAM_OPACITY_FADE=1): fade the DEEPEST-rooted container on this tab and report what one Opacity
                // change costs against the size of the subtree under it. This is the case element Opacity is actually
                // about: the value multiplies down the whole chain and is baked into every descendant's colour, so one
                // write re-bakes N units. Measuring it on flat leaf spinners - as the first attempt did - measures the
                // one shape where the cost cannot appear.
                if (Environment.GetEnvironmentVariable("ADAM_OPACITY_FADE") == "1")
                {
                    var win = Adamantium.UI.UIApplication.Current?.MainWindow;
                    var target = win?.Content is Adamantium.UI.Core.IUIComponent root ? Heaviest(root) : null;

                    if (target != null)
                    {
                        var under = Descendants(target);
                        var still = Run(4, () => false);

                        var phase = 0.0;
                        var fading = Run(8, () =>
                        {
                            phase += 0.08;
                            target.Opacity = 0.55 + 0.45 * Math.Sin(phase);
                            return true;
                        });

                        System.IO.File.AppendAllText(log + ".fade.txt",
                            $"faded {target.GetType().Name} over {under} descendants: " +
                            $"still {still / 4.0:0} fps ({1000.0 / Math.Max(1, still / 4.0):0.00} ms) -> " +
                            $"fading {fading / 8.0:0} fps ({1000.0 / Math.Max(1, fading / 8.0):0.00} ms)" + Environment.NewLine);
                    }
                }

                // TEMP self-check (ADAM_THEME_SWAP=1): swap the theme from here and report the tab strip's height after
                // each swap. ~36 is a strip; anything larger is the "page inside a tab header" fault this hunt closed.
                var stripReport = "(not asked)";
                if (Environment.GetEnvironmentVariable("ADAM_THEME_SWAP") == "1")
                {
                    var themes = Adamantium.UI.Core.UIAppContext.Current?.ThemeManager;
                    var win = Adamantium.UI.UIApplication.Current?.MainWindow;
                    if (themes != null && win != null)
                    {
                        stripReport = string.Empty;
                        for (var lap = 0; lap < 2; lap++)
                        {
                            var wanted = themes.CurrentTheme?.Name == "FluentLight" ? "FluentDark" : "FluentLight";
                            var next = themes[wanted];
                            Adamantium.UI.Threading.Dispatcher.CurrentDispatcher?.Post(() => themes.SetTheme(next));
                            System.Threading.Thread.Sleep(6000);
                            stripReport += $"after {wanted}: strip {StripHeight(win):0} px; ";
                        }
                    }
                }

                var frames = Adamantium.UI.Core.Diagnostics.RuntimeStats.PresentedFrames - startFrames;
                var s = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastRenderDrawMs;
                System.IO.File.WriteAllText(log,
                    $"layout peak {layout:0} ms | WORST SECOND {(worstSecond == long.MaxValue ? 0 : worstSecond)} fps | presented {frames} in {sw.Elapsed.TotalSeconds:0.0} s = {frames / sw.Elapsed.TotalSeconds:0} fps" + System.Environment.NewLine
                    + $"sampled avg ms: prerender {sumPre * inv:0.00} layout {sumLayout * inv:0.00} record {sumRecord * inv:0.00} apply {sumApply * inv:0.00} proc {sumProc * inv:0.00} draw {sumDraw * inv:0.00} processors {sumProcessors * inv:0.00}" + System.Environment.NewLine
                    + $"frame steps avg ms: beginDraw {sumBegin * inv:0.00} = fence {sumFence * inv:0.00} + setup {sumSetup * inv:0.00} + record/apply/prerender {(sumRecord + sumApply + sumPre) * inv:0.00} + acquire {sumAcquire * inv:0.00}" + System.Environment.NewLine
                    + $"                    endDraw {sumEnd * inv:0.00} submit {sumSubmit * inv:0.00} present {sumPresent * inv:0.00}" + System.Environment.NewLine
                    + $"sampled max ms: record {maxRecord:0.0} apply {maxApply:0.0} draw {maxDraw:0.0} | frame budget at {frames / sw.Elapsed.TotalSeconds:0} fps = {1000.0 / (frames / sw.Elapsed.TotalSeconds):0.00} ms" + System.Environment.NewLine
                    + Adamantium.UI.Core.Diagnostics.FrameTrace.Percentiles() + System.Environment.NewLine
                    + Adamantium.UI.Rendering.LayerProbe.Dump() + System.Environment.NewLine
                    + "theme swap: " + stripReport + System.Environment.NewLine
                    + "churn:" + System.Environment.NewLine
                    + string.Join(System.Environment.NewLine, System.Linq.Enumerable.Select(
                        System.Linq.Enumerable.OrderByDescending(churn, p => p.Value), p => $"  {p.Value,5}  {p.Key}"))
                    + System.Environment.NewLine);

                // Every frame that ran LONG, one line each: what kind of build it was, why it could not replay, and how
                // much of it was layout and record. A spike a hand reproduces is only worth anything if it names itself.
                System.IO.File.WriteAllText(log + ".frames.txt",
                    "presentation extensions: " + DescribePresentationSupport() + System.Environment.NewLine
                    + "patch STILL re-bakes (by unit type, whole run):" + System.Environment.NewLine
                    + string.Join(System.Environment.NewLine, System.Linq.Enumerable.Select(
                        System.Linq.Enumerable.Take(
                            System.Linq.Enumerable.OrderByDescending(Adamantium.UI.Core.Diagnostics.FrameTrace.Patched, p => p.Value), 8),
                        p => $"  {p.Value,7}  {p.Key}")) + System.Environment.NewLine
                    + "patch refusals by reason:" + System.Environment.NewLine
                    + string.Join(System.Environment.NewLine, System.Linq.Enumerable.Select(
                        System.Linq.Enumerable.OrderByDescending(Adamantium.UI.Core.Diagnostics.FrameTrace.Refusals, p => p.Value),
                        p => $"  {p.Value,5}  {p.Key}")) + System.Environment.NewLine
                    + "not node-aware:" + System.Environment.NewLine
                    + string.Join(System.Environment.NewLine, System.Linq.Enumerable.Select(
                        System.Linq.Enumerable.OrderByDescending(Adamantium.UI.Core.Diagnostics.FrameTrace.NotAware, p => p.Value),
                        p => $"  {p.Value,5}  {p.Key}")) + System.Environment.NewLine
                    + Adamantium.UI.Core.Diagnostics.FrameTrace.DumpIncidents());
                if (Environment.GetEnvironmentVariable("ADAM_PROBE_EXIT") == "1") Environment.Exit(0);
            }) { IsBackground = true };
            t.Start();
        }
        gameApp.IsFixedTimeStep = false;
        SetUp(gameApp);
    }

    private static int _panned;   // TEMP: pans that actually moved the strip - a harness that pans nothing measures nothing
    private static double _pan = 8;      // the small oscillation's current direction
    private static double _sweep = 48;   // the end-to-end sweep's current direction (a wheel notch)

    // TEMP: post one pan per frame-ish for the given seconds, and report the frames presented while doing it.
    // TEMP (ADAM_OPACITY_FADE): how many visual descendants a node carries, and the node carrying the most of them -
    // the subtree whose fade is worth timing.
    private static int Descendants(Adamantium.UI.Core.IUIComponent node)
    {
        var count = 0;
        var stack = new System.Collections.Generic.Stack<Adamantium.UI.Core.IUIComponent>();
        stack.Push(node);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            foreach (var child in n.VisualChildren) { count++; stack.Push(child); }
        }

        return count;
    }

    private static Adamantium.UI.Controls.Base.UIComponent Heaviest(Adamantium.UI.Core.IUIComponent root)
    {
        Adamantium.UI.Controls.Base.UIComponent best = null;
        var bestCount = 0;
        var stack = new System.Collections.Generic.Stack<Adamantium.UI.Core.IUIComponent>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            foreach (var child in n.VisualChildren) stack.Push(child);

            // The tab's CONTENT, not the shell around it. Picking "whatever has the most descendants" picked a Grid of
            // 379 - the tab's own frame - while the thing worth fading is the panel holding the items. An ItemsControl
            // is that panel by construction, so the search is restricted to one.
            if (n is not Adamantium.UI.Controls.ItemsControl ui || ReferenceEquals(n, root)) continue;

            var under = Descendants(n);
            if (under <= bestCount) continue;

            best = ui;
            bestCount = under;
        }

        return best;
    }

    // TEMP (ADAM_SPINNER_KIND): the button whose Content reads exactly this, anywhere under root.
    private static Adamantium.UI.Controls.Primitives.ButtonBase FindButton(Adamantium.UI.Core.IUIComponent root, string content)
    {
        var stack = new System.Collections.Generic.Stack<Adamantium.UI.Core.IUIComponent>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is Adamantium.UI.Controls.Primitives.ButtonBase b && Equals(b.Content, content)) return b;
            foreach (var child in node.VisualChildren) stack.Push(child);
        }

        return null;
    }

    private static long Run(double seconds, Func<bool> pan)
    {
        var from = Adamantium.UI.Core.Diagnostics.RuntimeStats.PresentedFrames;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (clock.Elapsed.TotalSeconds < seconds)
        {
            Adamantium.UI.Threading.Dispatcher.CurrentDispatcher?.Post(() => { if (pan()) System.Threading.Interlocked.Increment(ref _panned); });
            System.Threading.Thread.Sleep(16);
        }

        return Adamantium.UI.Core.Diagnostics.RuntimeStats.PresentedFrames - from;
    }

    // TEMP: the first control of a kind under a root - the harnesses need to reach a viewer or a strip by type.
    private static T Find<T>(Adamantium.UI.Core.IUIComponent root) where T : class
    {
        var stack = new System.Collections.Generic.Stack<Adamantium.UI.Core.IUIComponent>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is T hit) return hit;
            foreach (var child in node.VisualChildren) stack.Push(child);
        }

        return null;
    }

    // TEMP: the first control with this Name under a root.
    /// <summary>Which presentation extensions this MACHINE turned out to have - asked of the device, so a report from a
    /// different GPU or a Mac says what was true there rather than what the wish list hoped for.</summary>
    private static string DescribePresentationSupport()
    {
        var service = Adamantium.UI.UIApplication.Current?.Container
            ?.Resolve<Adamantium.Graphics.Core.IGraphicsDeviceService>();
        var main = (service as Adamantium.UI.Services.GraphicsDeviceService)?.MainGraphicsDevice;
        if (main == null) return "no device";

        return $"swapchainMaintenance {main.SupportsSwapchainMaintenance} | presentWait {main.SupportsPresentWait}"
             + $" | incrementalPresent {main.SupportsIncrementalPresent}";
    }

    private static Adamantium.UI.Controls.Base.UIComponent FindNamed(Adamantium.UI.Core.IUIComponent root, string name)
    {
        var stack = new System.Collections.Generic.Stack<Adamantium.UI.Core.IUIComponent>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is Adamantium.UI.Controls.Base.UIComponent ui && ui.Name == name) return ui;
            foreach (var child in node.VisualChildren) stack.Push(child);
        }

        return null;
    }

    // TEMP: the tab strip, for the pan self-check above.
    private static Adamantium.UI.Controls.TabStripScroller FindStrip(Adamantium.UI.Core.IUIComponent root)
    {
        var stack = new System.Collections.Generic.Stack<Adamantium.UI.Core.IUIComponent>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is Adamantium.UI.Controls.TabStripScroller strip) return strip;
            foreach (var child in node.VisualChildren) stack.Push(child);
        }

        return null;
    }

    // TEMP: the tab strip's height, for the self-check above.
    private static double StripHeight(object win)
    {
        var content = win.GetType().GetProperty("Content")?.GetValue(win) as Adamantium.UI.Core.IUIComponent;
        if (content == null) return -1;

        var stack = new System.Collections.Generic.Stack<Adamantium.UI.Core.IUIComponent>();
        stack.Push(content);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node.GetType().Name == "TabStripScroller") return node.RenderSize.Height;
            foreach (var child in node.VisualChildren) stack.Push(child);
        }

        return -1;
    }

    private static void SetUp(AdamantiumGameApplication gameApp)
    {
        gameApp.EnableGraphicsDebug = Environment.GetEnvironmentVariable("ADAM_VK_DEBUG") == "1";
        gameApp.DesiredFPS = 300;
        gameApp.StartupType = typeof(MainWindow);
        gameApp.Run();
    }
}