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
}
