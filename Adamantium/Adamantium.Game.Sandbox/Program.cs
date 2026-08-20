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
                double sumPre = 0;
                double sumRecord = 0, sumApply = 0, sumProc = 0, sumDraw = 0, sumProcessors = 0, sumLayout = 0;
                double maxDraw = 0, maxApply = 0, maxRecord = 0;
                long samples = 0;
                var secondStart = System.Diagnostics.Stopwatch.GetTimestamp();
                var secondFrames = Adamantium.UI.Core.Diagnostics.RuntimeStats.PresentedFrames;
                var worstSecond = long.MaxValue;
                while (sw.Elapsed.TotalSeconds < limit)
                {
                    var st = typeof(Adamantium.UI.Core.Diagnostics.RuntimeStats);
                    if (Adamantium.UI.Core.Diagnostics.RuntimeStats.LastLayoutPassMs > layout) layout = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastLayoutPassMs;
                    sumPre += Adamantium.UI.Core.Diagnostics.RuntimeStats.LastPreRenderMs;
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
                    System.Threading.Thread.Sleep(15);

                    // The WORST SECOND, not the average: a drop that lasts a few seconds disappears into a 40-second
                    // mean, and the per-frame ring only holds the last couple of thousand frames - at a thousand a
                    // second that is the last two. A minimum survives both.
                    if (System.Diagnostics.Stopwatch.GetElapsedTime(secondStart).TotalSeconds >= 1.0)
                    {
                        var thisSecond = Adamantium.UI.Core.Diagnostics.RuntimeStats.PresentedFrames - secondFrames;
                        if (thisSecond < worstSecond) worstSecond = thisSecond;
                        secondFrames = Adamantium.UI.Core.Diagnostics.RuntimeStats.PresentedFrames;
                        secondStart = System.Diagnostics.Stopwatch.GetTimestamp();
                    }
                }
                var inv = samples > 0 ? 1.0 / samples : 0;

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
                    "not node-aware:" + System.Environment.NewLine
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