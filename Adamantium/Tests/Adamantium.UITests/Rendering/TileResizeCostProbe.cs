using System;
using System.Linq;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Templates;
using Adamantium.UI.Extensions;
using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>
/// What a tile-size drag costs, headless: the same gesture the sandbox reproduces by hand, run here so it can be measured
/// as often as needed without a person at the keyboard. Not an assertion - it prints counters and passes; read the output.
///
/// A caution learned the hard way: this harness has no theme, no styles and no renderer, so it UNDERSTATES the real thing.
/// It is good for shape (does the cost scale with the window, or worse?) and useless for "is the app fast now" - that only
/// the live stand answers.
/// </summary>
[TestFixture]
[Explicit("Measurement probe - run it deliberately and read the numbers")]
public class TileResizeCostProbe
{
    // The scenario the freeze was reported on: 4K, maximised, tiles at their minimum.
    private const double BigViewportW = 3840;
    private const double BigViewportH = 2100;

    private sealed class TestWindowRoot : Grid, IRootVisualComponent
    {
        public Vector2 PointToClient(PixelPoint point) => new((float)point.X, (float)point.Y);
        public PixelPoint PointToScreen(Vector2 point) => new(point.X, point.Y);
        public PixelPoint Position { get; set; }
        public void AttachContextAndInitialize(IUIContext context) { }
        public double Left { get; set; }
        public double Top { get; set; }
        public string Title { get; set; }
        public double ClientWidth { get; set; }
        public double ClientHeight { get; set; }
        public IUIContext UIContext => null;
    }

    /// <summary>The tile grid the sandbox's Layout tab builds, settled and ready to be dragged.</summary>
    private static TestWindowRoot BuildScene(int itemCount, double width, double height, out WrapPanel wrapPanel)
    {
        var items = Enumerable.Range(0, itemCount).Cast<object>().ToList();
        WrapPanel panel = null;
        var ic = new ItemsControl
        {
            ItemsSource = items,
            // The sandbox's tile is a chain, not one element: container -> Border -> ContentPresenter -> Rectangle. Depth
            // is part of the cost (each level runs its own ArrangeCore), so the probe has to have it too.
            ItemTemplate = new DataTemplate(() => new TemplateResult
            {
                RootComponent = new Border
                {
                    Margin = new Thickness(3),
                    Background = Brushes.Red,
                    Child = new Adamantium.UI.Controls.Shapes.Rectangle
                    {
                        Fill = Brushes.Blue,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch
                    }
                }
            }),
            ItemsPanel = new ItemsPanelTemplate(() => new TemplateResult
            {
                RootComponent = panel = new WrapPanel { Orientation = Orientation.Horizontal, ItemWidth = 24, ItemHeight = 24 }
            }),
            Template = new ControlTemplate(() =>
            {
                var presenter = new ItemsPresenter();
                var result = new TemplateResult { RootComponent = presenter };
                result.RegisterName("PART_ItemsPresenter", presenter);
                return result;
            })
        };

        var root = new TestWindowRoot { ClientWidth = width, ClientHeight = height };
        root.Children.Add(ic);

        for (var i = 0; i < 60; i++) { WindowExtension.UpdateTree(root); RenderDirty.Clear(); }

        wrapPanel = panel;
        return root;
    }

    /// <summary>Five slider steps, each followed by the passes it takes to settle - what a hand does. The cell is reset
    /// first so every call is handed IDENTICAL work; letting it drift meant a second run always got bigger cells, fewer
    /// tiles, and would have reported that as its own win.</summary>
    private static double Drag(TestWindowRoot root, WrapPanel panel)
    {
        var cell = 24.0;
        panel.ItemWidth = panel.ItemHeight = cell;
        for (var p = 0; p < 8; p++) { WindowExtension.UpdateTree(root); RenderDirty.Clear(); }

        var start = System.Diagnostics.Stopwatch.GetTimestamp();
        for (var step = 0; step < 5; step++)
        {
            cell += step % 2 == 0 ? 6 : -3;
            panel.ItemWidth = cell;
            panel.ItemHeight = cell;
            for (var p = 0; p < 6; p++) { WindowExtension.UpdateTree(root); RenderDirty.Clear(); }
        }
        return (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
    }

    /// <summary>Does the drag cost scale with the number of tiles, or worse? A per-element cost is linear by construction;
    /// a departure from that is a bug, and the us-per-arrange column is where it shows.</summary>
    [Test]
    public void ScalingSweep()
    {
        (double W, double H)[] viewports =
        [
            (960, 540), (1200, 800), (1920, 1080), (2560, 1440), (BigViewportW, BigViewportH)
        ];

        TestContext.Out.WriteLine("viewport        tiles     cores    drag ms   us/core   gen0");
        foreach (var (w, h) in viewports)
        {
            var root = BuildScene(60000, w, h, out var panel);

            var cores0 = MeasurableUIComponent.TotalArrangeCores;
            var gen0 = GC.CollectionCount(0);
            var ms = Math.Min(Drag(root, panel), Drag(root, panel));   // best of two - the machine's worst moment is not a result
            var cores = (MeasurableUIComponent.TotalArrangeCores - cores0) / 2;
            var collections = (GC.CollectionCount(0) - gen0) / 2;

            var tiles = w * h / (24.0 * 24.0);
            TestContext.Out.WriteLine($"{w,5}x{h,-5}  {tiles,8:F0}  {cores,8}   {ms,8:F1}   " +
                                      $"{ms * 1000 / Math.Max(1, cores),7:F2}   {collections,4}");
        }
    }

    /// <summary>The reported gesture: settle at the MINIMUM cell (which realizes a whole 4K screenful of tiny tiles), then
    /// raise the height. Only a few hundred tiles are on screen afterwards - so if this is slow, the cost is not in laying
    /// out what is visible but in what the panel is still holding.</summary>
    [Test]
    public void GrowFromMinimumCell()
    {
        var root = BuildScene(60000, BigViewportW, BigViewportH, out var panel);

        double Settle(double w, double h, int passes = 12)
        {
            panel.ItemWidth = w;
            panel.ItemHeight = h;
            var start = System.Diagnostics.Stopwatch.GetTimestamp();
            for (var p = 0; p < passes; p++) { WindowExtension.UpdateTree(root); RenderDirty.Clear(); }
            return (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }

        // Drive the fill to completion, reporting how long it takes to converge - the freeze is felt while this is still
        // running, so "how many frames does it take" is the question, not "how slow is one".
        panel.ItemWidth = panel.ItemHeight = 24;
        var passes = 0;
        var last = -1;
        var fillStart = System.Diagnostics.Stopwatch.GetTimestamp();
        while (passes < 600 && Realized(panel) != last)
        {
            last = Realized(panel);
            for (var p = 0; p < 10; p++) { WindowExtension.UpdateTree(root); RenderDirty.Clear(); passes++; }
        }
        var fillMs = (System.Diagnostics.Stopwatch.GetTimestamp() - fillStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        TestContext.Out.WriteLine($"fill at minimum cell: {passes} passes, {fillMs:F0}ms, realized {Realized(panel)}");

        // Nothing left to bind: what does a pass still cost with that many containers realized?
        TestContext.Out.WriteLine($"settled pass with {Realized(panel)} realized: {Settle(24, 24, 20) / 20:F1}ms each");

        Settle(24, 200, 20);
        TestContext.Out.WriteLine($"at 24x200: realized {Realized(panel)}");

        for (var step = 0; step < 6; step++)
        {
            var h = 200 + (step % 2 == 0 ? 20 : -10);
            var gen0 = GC.CollectionCount(0);
            var cores0 = MeasurableUIComponent.TotalArrangeCores;
            var ms = Settle(24, h, 6);
            TestContext.Out.WriteLine($"  slider step to 24x{h}: {ms:F0}ms   realized {Realized(panel)}, " +
                                      $"arrangeCores {MeasurableUIComponent.TotalArrangeCores - cores0}, " +
                                      $"gen0 {GC.CollectionCount(0) - gen0}");
        }
    }

    private static int Realized(WrapPanel panel) =>
        panel.Owner?.ItemContainerGenerator.RealizedCount ?? 0;

    /// <summary>WHERE the managed heap goes on a tile grid. A 4K fill takes it from 130MB to 936MB and the collector then
    /// stops every thread for a quarter to a half of each second - so the question "how many bytes and objects does ONE
    /// realized tile cost, and which part of building it spends them" is the one that decides what to fix. Allocation
    /// counters, unlike the per-element stopwatch profiler, do not distort what they measure.</summary>
    [Test]
    public void AllocationPerRealizedTile()
    {
        // A bare component, with nothing built into it: this is what the property system charges per element before any
        // template, binding or child exists.
        TestContext.Out.WriteLine("--- one component, constructed and nothing else");
        ReportCtor("Border", () => new Border());
        ReportCtor("Rectangle", () => new Adamantium.UI.Controls.Shapes.Rectangle());
        ReportCtor("ContentPresenter", () => new ContentPresenter());
        ReportCtor("ListBoxItem", () => new ListBoxItem());
        ReportCtor("TextBlock", () => new Adamantium.UI.Controls.Text.TextBlock());

        // ...and what a whole realized tile costs, measured across a real fill.
        var root = BuildScene(60000, BigViewportW, BigViewportH, out var panel);
        panel.ItemWidth = panel.ItemHeight = 24;

        var settled = LiveHeapBytes();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var realizedBefore = Realized(panel);

        var passes = 0;
        var last = -1;
        while (passes < 600 && Realized(panel) != last)
        {
            last = Realized(panel);
            for (var p = 0; p < 10; p++) { WindowExtension.UpdateTree(root); RenderDirty.Clear(); passes++; }
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        var realized = Realized(panel) - realizedBefore;
        var live = LiveHeapBytes() - settled;

        TestContext.Out.WriteLine($"--- fill at the minimum cell: {realized} containers over {passes} passes");
        TestContext.Out.WriteLine($"allocated total : {allocated / 1048576.0,8:F1} MB   = {allocated / Math.Max(1, realized),8} B per container");
        TestContext.Out.WriteLine($"still LIVE after: {live / 1048576.0,8:F1} MB   = {live / Math.Max(1, realized),8} B per container");
        TestContext.Out.WriteLine($"(live is what the collector must keep walking; allocated-minus-live is per-pass garbage)");
    }

    private static void ReportCtor(string name, Func<object> make)
    {
        make();   // first one pays for JIT + statics; measure the steady state
        const int n = 200;
        var before = GC.GetAllocatedBytesForCurrentThread();
        var kept = new object[n];
        for (var i = 0; i < n; i++) kept[i] = make();
        var bytes = (GC.GetAllocatedBytesForCurrentThread() - before) / n;
        GC.KeepAlive(kept);

        var registered = AdamantiumPropertyMap.GetRegistered(kept[0].GetType()).Count();
        TestContext.Out.WriteLine($"{name,-18} {bytes,7} B   {registered,4} registered properties   " +
                                  $"~{bytes / Math.Max(1, registered),4} B per property");
    }

    /// <summary>Does dragging the slider RETAIN memory, or merely churn it? Reported on the live stand: the managed heap
    /// climbed to 1.3 GB while nothing was happening but the height slider moving back and forth, on a window holding
    /// only ~1700 tiles. Churn is collected; a climb after a forced full collection is something being held. This walks
    /// the same gesture and prints the live heap after every step, so the two are told apart by a number.</summary>
    [Test]
    public void SliderDragRetention()
    {
        var root = BuildScene(60000, BigViewportW, BigViewportH, out var panel);

        void Settle(double w, double h)
        {
            panel.ItemWidth = w;
            panel.ItemHeight = h;
            for (var p = 0; p < 8; p++) { WindowExtension.UpdateTree(root); RenderDirty.Clear(); }
        }

        Settle(24, 240);   // the reported scenario: minimum width, maximum height
        var baseline = LiveHeapBytes();
        TestContext.Out.WriteLine($"settled at 24x240: realized {Realized(panel)}, live {baseline / 1048576.0:F1} MB");

        for (var step = 0; step < 12; step++)
        {
            var h = 240 - step % 4 * 20;   // 240, 220, 200, 180, back to 240 - the slider going to and fro
            Settle(24, h);
            var live = LiveHeapBytes();
            TestContext.Out.WriteLine($"step {step,2} -> 24x{h,3}: realized {Realized(panel),5}, " +
                                      $"live {live / 1048576.0,7:F1} MB, grown {(live - baseline) / 1048576.0,7:F1} MB");
        }
    }

    private static long LiveHeapBytes()
    {
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        return GC.GetTotalMemory(false);
    }
}
