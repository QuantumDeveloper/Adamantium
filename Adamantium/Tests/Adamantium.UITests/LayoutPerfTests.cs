using System.Diagnostics;
using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Templates;
using Adamantium.UI.Extensions;
using NUnit.Framework;

namespace Adamantium.UITests;

// Headless reproduction of the Sandbox Layout-tab freeze: dragging the size slider shrinks the tiles, so a virtualizing
// WrapPanel realizes MORE tiles per frame and re-measures the whole visible grid. Driven through the REAL layout path
// (LayoutManager via WindowExtension.UpdateTree, i.e. MeasureDirty measures the invalidated panel directly) so it matches
// the app, not a top-down Measure the validity gates would short-circuit. Guards on measure/arrange counts (deterministic)
// and prints wall-time for reference, so the layout optimisation can be iterated + regression-guarded without the GPU.
[TestFixture]
public class LayoutPerfTests
{
    private const double ViewportW = 1200;
    private const double ViewportH = 550;

    // Counts how many times the panel's Measure/Arrange OVERRIDE actually runs in one layout pass - i.e. how many times
    // the pass looped and re-did the whole grid (the re-measure-in-arrange cascade multiplier).
    private sealed class CountingWrapPanel : WrapPanel
    {
        public int MeasureOverrides, ArrangeOverrides;
        protected override Size MeasureOverride(Size availableSize) { MeasureOverrides++; return base.MeasureOverride(availableSize); }
        protected override Size ArrangeOverride(Size finalSize) { ArrangeOverrides++; return base.ArrangeOverride(finalSize); }
    }

    // A visual root with a client viewport, mirroring a window (so the WrapPanel gets a finite scroll viewport and
    // virtualizes to the visible grid instead of realizing all items).
    private sealed class TestWindowRoot : Grid, IRootVisualComponent
    {
        public Vector2 PointToClient(Vector2 point) => point;
        public Vector2 PointToScreen(Vector2 point) => point;
        public void AttachContextAndInitialize(IUIContext context) { }
        public double Left { get; set; }
        public double Top { get; set; }
        public string Title { get; set; }
        public double ClientWidth { get; set; }
        public double ClientHeight { get; set; }
        public IUIContext UIContext => null;
    }

    private static (TestWindowRoot root, ItemsControl ic, WrapPanel panel) BuildTiles(int count, double cell)
    {
        var items = Enumerable.Range(0, count).Cast<object>().ToList();
        var ic = new ItemsControl
        {
            ItemsSource = items,
            ItemTemplate = new DataTemplate(() => new TemplateResult
            {
                RootComponent = new Border { Margin = new Thickness(3) }   // margined tile, like LayoutView.auml
            }),
            ItemsPanel = new ItemsPanelTemplate(() => new TemplateResult
            {
                RootComponent = new CountingWrapPanel { Orientation = Orientation.Horizontal, ItemWidth = cell, ItemHeight = cell }
            }),
            Template = new ControlTemplate(() =>
            {
                var presenter = new ItemsPresenter();
                var result = new TemplateResult { RootComponent = presenter };
                result.RegisterName("PART_ItemsPresenter", presenter);
                return result;
            })
        };
        var root = new TestWindowRoot { ClientWidth = ViewportW, ClientHeight = ViewportH };
        root.Children.Add(ic);
        WindowExtension.UpdateTree(root);   // settle the initial layout
        return (root, ic, (WrapPanel)ic.ItemsHostPanel);
    }

    // One "slider step": change the uniform cell size and run ONE layout pass, reporting realized count + measure/arrange
    // calls + wall-time. Returns the pass's measure count (the number the layout fix should shrink).
    private static long ResizeStep(TestWindowRoot root, ItemsControl ic, WrapPanel panel, double newCell)
    {
        var m0 = MeasurableUIComponent.TotalMeasureCalls;
        var a0 = MeasurableUIComponent.TotalArrangeCalls;

        var cp = (CountingWrapPanel)panel;
        cp.MeasureOverrides = 0; cp.ArrangeOverrides = 0;

        panel.ItemWidth = newCell;
        panel.ItemHeight = newCell;   // AffectsMeasure/Arrange -> LayoutManager enqueues the panel

        var sw = Stopwatch.StartNew();
        WindowExtension.UpdateTree(root);
        sw.Stop();

        var measures = MeasurableUIComponent.TotalMeasureCalls - m0;
        var arranges = MeasurableUIComponent.TotalArrangeCalls - a0;
        var realized = ic.ItemContainerGenerator.RealizedCount;
        TestContext.WriteLine($"cell->{newCell,-4} realized={realized,-4} measures={measures,-5} arranges={arranges,-5} panelMeasure={cp.MeasureOverrides} panelArrange={cp.ArrangeOverrides} {sw.Elapsed.TotalMilliseconds,7:F2} ms");
        return measures;
    }

    // One scroll step: move the virtualizing panel's offset and run ONE real layout pass, reporting realized count +
    // measure/arrange calls + wall-time. SetOffset InvalidateMeasure()s the panel, so this exercises the exact per-scroll
    // path the Sandbox hits. Returns the pass's measure count.
    private static long ScrollStep(TestWindowRoot root, ItemsControl ic, WrapPanel panel, double y)
    {
        var m0 = MeasurableUIComponent.TotalMeasureCalls;
        var a0 = MeasurableUIComponent.TotalArrangeCalls;
        var cp = (CountingWrapPanel)panel;
        cp.MeasureOverrides = 0; cp.ArrangeOverrides = 0;

        panel.SetOffset(new Vector2(0, (float)y));

        var sw = Stopwatch.StartNew();
        WindowExtension.UpdateTree(root);
        sw.Stop();

        var measures = MeasurableUIComponent.TotalMeasureCalls - m0;
        var arranges = MeasurableUIComponent.TotalArrangeCalls - a0;
        var realized = ic.ItemContainerGenerator.RealizedCount;
        TestContext.WriteLine($"scrollY->{y,-6:F0} realized={realized,-4} measures={measures,-6} arranges={arranges,-6} panelMeasure={cp.MeasureOverrides} panelArrange={cp.ArrangeOverrides} {sw.Elapsed.TotalMilliseconds,7:F2} ms");
        return measures;
    }

    // Reproduce the Sandbox Layout-tab scroll freeze: 6000 tiles at 24px, scroll a row at a time and a fast jump, and
    // report the per-step measure/arrange cost. This is the number the transform-only-scroll fix must collapse.
    // Pump layout passes (as the app's per-frame loop would) until the virtualized window stops growing - i.e. the burst
    // fill of the initially-visible grid has completed. Returns the settled realized count.
    private static int Settle(TestWindowRoot root, ItemsControl ic)
    {
        var prev = -1;
        for (var i = 0; i < 60 && ic.ItemContainerGenerator.RealizedCount != prev; i++)
        {
            prev = ic.ItemContainerGenerator.RealizedCount;
            WindowExtension.UpdateTree(root);
        }
        return ic.ItemContainerGenerator.RealizedCount;
    }

    [Test]
    public void Scroll_ReLayoutCost()
    {
        var (root, ic, panel) = BuildTiles(6000, 24);
        var settled = Settle(root, ic);
        TestContext.WriteLine($"=== Layout-tab scroll re-layout cost (6000 tiles @24, {ViewportW}x{ViewportH} viewport, settled realized={settled}) ===");
        double y = 0;
        TestContext.WriteLine("-- STEADY-STATE slow scroll (one 24px row per step, grid pre-filled) --");
        for (var i = 0; i < 10; i++) { ScrollStep(root, ic, panel, y += 24); Settle(root, ic); }
        TestContext.WriteLine("-- fast scroll (one viewport-height jump per step) --");
        for (var i = 0; i < 5; i++) { ScrollStep(root, ic, panel, y += ViewportH); Settle(root, ic); }
    }

    // The mature-virtualizer contract, asserted on CONTINUOUS scroll (one row per step, NO settle between steps - the app
    // scrolls continuously, which is exactly what my earlier Settle()-per-step bench masked). A correct recycling ring:
    //  - realized container count is CONSTANT (a fixed ring, not a growing/oscillating window);
    //  - ZERO structural marks per scroll step (containers are rebound in place - no attach/detach, no Visibility toggle);
    //  - measure calls per step are O(one row), not O(whole window) (rebind is data-only; stable tiles don't re-measure).
    // This test is expected to FAIL on the pre-fix code (it oscillates + churns) - it defines "correct" so drift can't hide.
    [Test]
    public void Scroll_RecyclingRingInvariants()
    {
        // This is about the RING (does a scroll step reuse containers in place?), not about the per-frame bind budget. With
        // the real time budget in play a loaded machine can spend its 6 ms mid-window and DEFER the rest of the slots - the
        // ring then reads short by exactly the deferred slots, and the test fails on how busy the box was rather than on
        // anything the panel did wrong. Bind the whole window every pass so what is asserted below is the ring itself; the
        // budget's own slicing behaviour is covered by VirtualizingWrapPanel_HugeWindow_RealizesInCappedSlices...
        var savedBind = WrapPanel.BindBudgetMs;
        var savedFill = WrapPanel.FillBudgetMs;
        WrapPanel.BindBudgetMs = double.MaxValue;
        WrapPanel.FillBudgetMs = double.MaxValue;
        try
        {
            RunRecyclingRingInvariants();
        }
        finally
        {
            WrapPanel.BindBudgetMs = savedBind;
            WrapPanel.FillBudgetMs = savedFill;
        }
    }

    private void RunRecyclingRingInvariants()
    {
        var (root, ic, panel) = BuildTiles(6000, 24);
        Settle(root, ic);
        // Warm up with FRACTIONAL (sub-cell) scroll steps: real inertia scrolls by fractional pixels, so the offset sits
        // mid-cell where floor(top)/ceil(bottom) diverge - the exact condition my earlier whole-cell (y+=24) steps land ON
        // the boundary and hid. A mid-cell offset also reveals one more PARTIAL row than a whole-cell one, so the working
        // set legitimately expands ONCE, with the attach marks that go with it, before it reaches its steady size. The
        // contract below is about the STEADY state, so warm up until the ring stops growing rather than for a fixed number
        // of steps (a fixed count locked the baseline mid-expansion and then flagged the expansion itself as a violation).
        double y = 500;
        var stable = 0;
        var previous = -1;
        for (var i = 0; i < 40 && stable < 3; i++)
        {
            y += 7;
            panel.SetOffset(new Vector2(0, (float)y));
            WindowExtension.UpdateTree(root);
            var count = ic.ItemContainerGenerator.RealizedCount;
            stable = count == previous ? stable + 1 : 0;
            previous = count;
        }
        var baseline = ic.ItemContainerGenerator.RealizedCount;
        var rowStride = baseline / 6;                         // generous O(one row) bound
        TestContext.WriteLine($"=== Recycling-ring invariants (6000 @24, fractional scroll, ring={baseline}) ===");

        var failures = new System.Collections.Generic.List<string>();
        for (var step = 0; step < 30; step++)
        {
            var m0 = MeasurableUIComponent.TotalMeasureCalls;
            var s0 = RenderDirty.TotalStructuralMarks;

            y += 7;                                           // fractional (sub-cell) scroll, CONTINUOUS (no Settle)
            panel.SetOffset(new Vector2(0, (float)y));
            WindowExtension.UpdateTree(root);

            var measures = MeasurableUIComponent.TotalMeasureCalls - m0;
            var structs = RenderDirty.TotalStructuralMarks - s0;
            var realized = ic.ItemContainerGenerator.RealizedCount;
            TestContext.WriteLine($"step {step,2}: realized={realized,-5} measures={measures,-5} struct={structs}");

            if (realized != baseline) failures.Add($"step {step}: realized {realized} != {baseline} (ring oscillates/not constant)");
            if (structs != 0)         failures.Add($"step {step}: struct {structs} != 0 (attach/Visibility churn)");
            if (measures > rowStride)  failures.Add($"step {step}: measures {measures} > {rowStride} (~O(one row)) - re-measuring the window");
        }

        Assert.That(failures, Is.Empty, "recycling-ring invariants violated:\n" + string.Join("\n", failures.Take(8)));
    }

    // The transform-only-scroll invariant: a tile that KEEPS its index across a scroll step must NOT re-run ArrangeCore.
    // With the offset baked into each slot (slot = line*cell - offset) every realized tile got a new rect every step ->
    // ArrangeCore recursed into every ContentPresenter (the freeze). With the offset applied as a single translation of the
    // panel (tiles at ABSOLUTE slots), a staying tile's rect is constant -> Arrange short-circuits, and only the rebound
    // row runs ArrangeCore. Steady-state ArrangeCore work is O(one row), NOT O(whole window). Asserted via TotalArrangeCores
    // (real ArrangeCore runs, not short-circuited calls).
    [Test]
    public void Scroll_StableTilesDoNotReArrange()
    {
        var (root, ic, panel) = BuildTiles(6000, 24);
        Settle(root, ic);
        double y = 500;
        for (var i = 0; i < 5; i++) { y += 7; panel.SetOffset(new Vector2(0, (float)y)); WindowExtension.UpdateTree(root); }
        var realized = ic.ItemContainerGenerator.RealizedCount;
        var bound = realized;   // offset-baked re-arranges the WHOLE window (>= realized); absolute re-arranges ~one row
        TestContext.WriteLine($"=== Arrange short-circuit (6000 @24, fractional scroll, realized={realized}, bound<{bound}) ===");

        var failures = new System.Collections.Generic.List<string>();
        for (var step = 0; step < 20; step++)
        {
            var c0 = MeasurableUIComponent.TotalArrangeCores;
            y += 7;
            panel.SetOffset(new Vector2(0, (float)y));
            WindowExtension.UpdateTree(root);
            var cores = MeasurableUIComponent.TotalArrangeCores - c0;
            TestContext.WriteLine($"step {step,2}: arrangeCores={cores}");
            if (cores >= bound) failures.Add($"step {step}: ArrangeCore ran {cores} times (>= {bound}) - staying tiles re-arranged (offset baked into slots)");
        }
        Assert.That(failures, Is.Empty, "stable tiles re-arranged on scroll:\n" + string.Join("\n", failures.Take(8)));
    }

    [Test]
    public void Shrink_ReLayoutCost()
    {
        var (root, ic, panel) = BuildTiles(600, 60);
        TestContext.WriteLine("=== Layout-tab shrink re-layout cost (600 tiles, 1200x550 viewport) ===");
        foreach (var cell in new[] { 54.0, 48, 42, 36, 30, 24 })
            ResizeStep(root, ic, panel, cell);

        // Regression guard for the WrapPanel pinned-cell measure fix: with the grid already fully realized, ONE more
        // cell change must NOT re-measure the whole visible subtree (the old code did ~3x per tile = the freeze). It
        // should only ARRANGE (tiles move) - so the step's measures stay ~O(1 per realized item), not a multiple.
        var realized = ic.ItemContainerGenerator.RealizedCount;
        var reMeasure = ResizeStep(root, ic, panel, 22);   // one more shrink, grid already realized
        Assert.That(reMeasure, Is.LessThan(realized * 1.5),
            $"pinned-cell WrapPanel must not re-measure the visible grid on a cell change (got {reMeasure} for {realized} tiles)");
    }

}
