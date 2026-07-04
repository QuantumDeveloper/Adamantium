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
