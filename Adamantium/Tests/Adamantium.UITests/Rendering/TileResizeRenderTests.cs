using System.Collections.Generic;
using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Templates;
using Adamantium.UI.Extensions;
using Adamantium.UI.Rendering;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>
/// The tile-resize artefacts (gaps + overlapping tiles while the size slider is dragged, healed by scrolling new cells in).
/// Reproduced HEADLESS, through the real layout pass and the real RenderCache, so the mechanism can be named instead of
/// guessed at from a screenshot.
///
/// The picture is drawn from the applier's FROZEN layout replica, never from the live tree - so the tiles are drawn where and
/// at the size that replica says. Gaps and overlaps therefore mean exactly one thing: an entry in it is STALE - a tile that
/// resized or moved, and whose new geometry never reached the draw side. That is the invariant asserted here, per component,
/// and it is asserted on the SAME frames the app is broken on: while the resize is still in flight.
/// </summary>
[TestFixture]
public class TileResizeRenderTests
{
    private const double ViewportW = 1200;
    private const double ViewportH = 550;

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
                RootComponent = new Border { Margin = new Thickness(3), Background = Brushes.Red }   // a tile that DRAWS
            }),
            ItemsPanel = new ItemsPanelTemplate(() => new TemplateResult
            {
                RootComponent = new WrapPanel { Orientation = Orientation.Horizontal, ItemWidth = cell, ItemHeight = cell }
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
        WindowExtension.UpdateTree(root);
        return (root, ic, (WrapPanel)ic.ItemsHostPanel);
    }

    // Every component the draw pass will draw must be drawn with the geometry LAYOUT gave it. A mismatch is a tile rendered
    // at its previous size or previous slot - a gap or an overlap, exactly what the slider produces on screen.
    private static void AssertDrawnGeometryIsFresh(RenderCache cache, string when)
    {
        var stale = new List<string>();
        var snapshot = cache.AppliedSnapshot;

        foreach (var component in cache.DrawnComponents)
        {
            if (!snapshot.TryGetValue(component, out var frozen))
            {
                stale.Add($"{Name(component)}: DRAWN but the draw side has no layout for it at all");
                continue;
            }

            if (frozen.RenderSize != component.RenderSize)
                stale.Add($"{Name(component)}: drawn at size {frozen.RenderSize}, laid out at {component.RenderSize}");
            else if (frozen.LocalTransform != component.LocalTransform)
                stale.Add($"{Name(component)}: drawn at a stale position (transform differs from the laid-out one)");
            else if (!ReferenceEquals(frozen.RenderParent, component.RenderParent))
                stale.Add($"{Name(component)}: drawn under a stale parent - its whole composed position is wrong");
        }

        Assert.That(stale, Is.Empty,
            $"{when}: the draw side is drawing {stale.Count} component(s) from stale layout - that IS the gap/overlap:\n"
            + string.Join("\n", stale.Take(10)));
    }

    private static string Name(IUIComponent c) => $"{c.GetType().Name}#{c.RenderId.ToString()[..8]}";

    // The slider: the cell size changes and the layout is driven ONE pass at a time, with the frame RECORDED after each pass -
    // which is what the app does, and why the artefacts are visible mid-drag rather than only at the end.
    [Test]
    public void TileResize_DrawnGeometryNeverGoesStale()
    {
        var (root, ic, panel) = BuildTiles(6000, 24);
        var cache = new RenderCache(new DrawingContext(), new FakeRenderUnitFactory());

        for (var i = 0; i < 40; i++) { WindowExtension.UpdateTree(root); cache.BuildFromVisualTree(root); }
        AssertDrawnGeometryIsFresh(cache, "after the initial fill");

        // Drag the slider: every step resizes the cells, re-lays out ONCE, and records ONE frame - no settling in between.
        double cell = 24;
        for (var step = 0; step < 25; step++)
        {
            cell += step % 2 == 0 ? 9 : -4;   // grow and shrink, like a hand on a slider
            panel.ItemWidth = cell;
            panel.ItemHeight = cell;

            WindowExtension.UpdateTree(root);
            cache.BuildFromVisualTree(root);

            AssertDrawnGeometryIsFresh(cache, $"mid-drag, step {step} (cell={cell})");
        }
    }

    // ...and the paint ORDER must still be the one a full walk derives, all the way through the drag: a resize realizes and
    // recycles containers, which is precisely when a spliced order can drift out of the tree's.
    [Test]
    public void TileResize_KeepsPaintOrder()
    {
        var (root, ic, panel) = BuildTiles(2000, 24);
        var cache = new RenderCache(new DrawingContext(), new FakeRenderUnitFactory());

        for (var i = 0; i < 40; i++)
        {
            WindowExtension.UpdateTree(root);
            cache.BuildFromVisualTree(root);
            AssertOrderMatchesTree(cache, root, $"during the initial fill, frame {i} ({cache.LastBuildKind})");
        }

        double cell = 24;
        for (var step = 0; step < 15; step++)
        {
            cell += step % 2 == 0 ? 9 : -4;
            panel.ItemWidth = cell;
            panel.ItemHeight = cell;

            var marks0 = RenderDirty.TotalStructuralMarks;
            WindowExtension.UpdateTree(root);
            var marks = RenderDirty.TotalStructuralMarks - marks0;   // did the collapse NAME anyone?

            cache.BuildFromVisualTree(root);
            TestContext.WriteLine($"step {step,2} cell={cell,-4} kind={cache.LastBuildKind,-10} structuralMarks={marks,-5} drawn={cache.PaintOrder.Count}");
            AssertOrderMatchesTree(cache, root, $"mid-drag, step {step} (cell={cell}, {cache.LastBuildKind})");
        }
    }

    // The VIEWPORT resize (a drag-resize / maximize): the heaviest thing the app does - the visible grid changes shape and
    // thousands of tiles are realized over the following frames. It used to force a whole-tree re-record on EVERY frame until
    // the layout settled (WindowBase: "parts of that settle never mark the render dirty"), which is exactly when the splice
    // would pay most. Those unmarked settle writes were the mark holes - a container hidden without naming itself, an auto-hide
    // scrollbar collapsing from its never-assigned default. With the marks honest, a resize is just structure changing, and it
    // must splice: no full walk, and nothing drawn from stale layout.
    [Test]
    public void ViewportResize_Splices_AndKeepsDrawnGeometryFresh()
    {
        var (root, ic, panel) = BuildTiles(6000, 24);
        var cache = new RenderCache(new DrawingContext(), new FakeRenderUnitFactory());
        for (var i = 0; i < 40; i++) { WindowExtension.UpdateTree(root); cache.BuildFromVisualTree(root); }

        var fullWalks = 0;
        double w = ViewportW;
        for (var step = 0; step < 20; step++)
        {
            w += step % 2 == 0 ? 140 : -60;          // drag the window edge
            root.ClientWidth = w;
            root.Width = w;

            WindowExtension.UpdateTree(root);
            cache.BuildFromVisualTree(root);
            if (cache.LastBuildKind == RenderBuildKind.Full) fullWalks++;

            AssertDrawnGeometryIsFresh(cache, $"mid-resize, step {step} (width={w}, {cache.LastBuildKind})");
            AssertOrderMatchesTree(cache, root, $"mid-resize, step {step} (width={w}, {cache.LastBuildKind})");
        }

        Assert.That(fullWalks, Is.Zero,
            $"a resize re-recorded the WHOLE tree on {fullWalks} of 20 frames - the splice was refused exactly where it matters most");
    }

    // The spliced paint order, held against the one a full walk of the SAME tree derives.
    private static void AssertOrderMatchesTree(RenderCache cache, TestWindowRoot root, string when)
    {
        var actual = cache.PaintOrder;

        var reference = new RenderCache(new DrawingContext(), new FakeRenderUnitFactory());
        root.InvalidateRender(true);
        reference.BuildFromVisualTree(root);
        var expected = reference.PaintOrder;
        if (actual.SequenceEqual(expected)) return;

        var inTree = reference.DrawnComponents.Select(c => c.RenderId).ToHashSet();
        var gone = cache.DrawnComponents.Where(c => !inTree.Contains(c.RenderId)).ToList();
        var missing = expected.Except(actual).Count();
        var at = Enumerable.Range(0, System.Math.Min(actual.Count, expected.Count)).First(i => actual[i] != expected[i]);

        // WHY is each one gone? That is the whole question - the splice frees a component only when a mark NAMED it.
        var why = gone.Take(6).Select(c =>
        {
            var hidden = "";
            for (var a = c.VisualParent; a != null; a = a.VisualParent)
                if (a.Visibility != Visibility.Visible) { hidden = $" (ancestor {a.GetType().Name} is {a.Visibility})"; break; }
            return $"    {c.GetType().Name}: visibility={c.Visibility} attached={c.IsAttachedToVisualTree}{hidden}";
        });

        Assert.Fail($"{when}: the spliced paint order drifted from the tree's.\n" +
                    $"  counts: spliced={actual.Count} tree={expected.Count}\n" +
                    $"  drawn-but-gone={gone.Count}, in-tree-but-never-drawn={missing}\n" +
                    $"  first divergence at rank {at}\n" +
                    $"  why they are gone:\n" + string.Join("\n", why));
    }
}
