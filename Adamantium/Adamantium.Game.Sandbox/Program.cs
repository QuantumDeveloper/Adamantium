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
                System.Threading.Thread.Sleep(6000);   // let the first tab settle: we are measuring the steady state
                var startFrames = Adamantium.UI.Core.Diagnostics.RuntimeStats.PresentedFrames;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var limit = Environment.GetEnvironmentVariable("ADAM_PROBE_SECONDS") is { } sec ? double.Parse(sec) : 20;
                double layout = 0;
                double sumRecord = 0, sumApply = 0, sumProc = 0, sumDraw = 0, sumProcessors = 0, sumLayout = 0;
                double maxDraw = 0, maxApply = 0, maxRecord = 0;
                long samples = 0;
                while (sw.Elapsed.TotalSeconds < limit)
                {
                    var st = typeof(Adamantium.UI.Core.Diagnostics.RuntimeStats);
                    if (Adamantium.UI.Core.Diagnostics.RuntimeStats.LastLayoutPassMs > layout) layout = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastLayoutPassMs;
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
                }
                var inv = samples > 0 ? 1.0 / samples : 0;

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
                    $"layout peak {layout:0} ms | presented {frames} in {sw.Elapsed.TotalSeconds:0.0} s = {frames / sw.Elapsed.TotalSeconds:0} fps" + System.Environment.NewLine
                    + $"sampled avg ms: layout {sumLayout * inv:0.00} record {sumRecord * inv:0.00} apply {sumApply * inv:0.00} proc {sumProc * inv:0.00} draw {sumDraw * inv:0.00} processors {sumProcessors * inv:0.00}" + System.Environment.NewLine
                    + $"sampled max ms: record {maxRecord:0.0} apply {maxApply:0.0} draw {maxDraw:0.0} | frame budget at {frames / sw.Elapsed.TotalSeconds:0} fps = {1000.0 / (frames / sw.Elapsed.TotalSeconds):0.00} ms" + System.Environment.NewLine
                    + Adamantium.UI.Core.Diagnostics.FrameTrace.Percentiles() + System.Environment.NewLine
                    + Adamantium.UI.Rendering.LayerProbe.Dump() + System.Environment.NewLine
                    + "theme swap: " + stripReport + System.Environment.NewLine);
                if (Environment.GetEnvironmentVariable("ADAM_PROBE_EXIT") == "1") Environment.Exit(0);
            }) { IsBackground = true };
            t.Start();
        }
        gameApp.IsFixedTimeStep = false;
        SetUp(gameApp);
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