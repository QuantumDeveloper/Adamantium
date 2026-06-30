using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.Templates;
using NUnit.Framework;

namespace Adamantium.UITests;

// Pure-CPU (no GPU) tests for the ItemsControl core: item -> container generation through the ItemContainerGenerator,
// and that the ItemTemplate's bindings resolve against each item. Non-virtualizing path (Phase 1).
[TestFixture]
public class ItemsControlTests
{
    private sealed class Person
    {
        public string Name { get; init; }
        public double Size { get; init; }
    }

    // A minimal control template whose root is the ItemsPresenter the control looks up by PART name.
    private static ItemsControl ArrangedItemsControl(System.Collections.IEnumerable source, DataTemplate itemTemplate = null)
    {
        var ic = new ItemsControl();
        if (itemTemplate != null) ic.ItemTemplate = itemTemplate;
        ic.ItemsSource = source;
        ic.Template = new ControlTemplate(() =>
        {
            var presenter = new ItemsPresenter();
            var result = new TemplateResult { RootComponent = presenter };
            result.RegisterName("PART_ItemsPresenter", presenter);
            return result;
        });
        ic.Measure(new Size(500, 500));
        ic.Arrange(new Rect(0, 0, 500, 500));
        return ic;
    }

    private static ControlTemplate ItemsPresenterTemplate() => new(() =>
    {
        var presenter = new ItemsPresenter();
        var result = new TemplateResult { RootComponent = presenter };
        result.RegisterName("PART_ItemsPresenter", presenter);
        return result;
    });

    [Test]
    public void VirtualizingStackPanelRealizesOnlyVisibleWindow()
    {
        var items = Enumerable.Range(0, 10000).Cast<object>().ToList();
        // Uniform fixed-height item template -> item extent 20px.
        var template = new DataTemplate(() => new TemplateResult { RootComponent = new Border { Height = 20 } });

        var ic = new ItemsControl { ItemTemplate = template, ItemsSource = items };
        ic.Template = ItemsPresenterTemplate();              // default ItemsPanel = vertical (virtualizing) StackPanel
        ic.Measure(new Size(100, 300));
        ic.Arrange(new Rect(0, 0, 100, 300));

        var gen = ic.ItemContainerGenerator;
        var panel = ic.ItemsHostPanel;
        var scroll = (IScrollableContent)panel;

        Assert.Multiple(() =>
        {
            Assert.That(gen.RealizedCount, Is.LessThan(60), "only the visible window (+buffer) is realized, not 10000");
            Assert.That(scroll.Extent.Height, Is.EqualTo(10000 * 20).Within(1), "extent spans all items");
            Assert.That(gen.ContainerFromIndex(0), Is.Not.Null, "top realized at offset 0");
            Assert.That(gen.ContainerFromIndex(9999), Is.Null, "far end not realized");
        });

        // Scroll to the middle (item ~250 = 5000/20): the window must move there and stay bounded.
        scroll.SetOffset(new Vector2(0, 5000));
        panel.Measure(new Size(100, 300), true);
        panel.Arrange(new Rect(0, 0, 100, 300), true);

        var realized = gen.RealizedIndices.ToList();
        Assert.Multiple(() =>
        {
            Assert.That(gen.RealizedCount, Is.LessThan(60), "window stays bounded after scrolling");
            Assert.That(realized, Has.Some.InRange(248, 252), "window now centered near item 250");
            Assert.That(gen.ContainerFromIndex(0), Is.Null, "the top is recycled away after scrolling");
        });
    }

    [Test]
    public void VirtualizingWrapPanelRealizesOnlyVisibleGrid()
    {
        var items = Enumerable.Range(0, 10000).Cast<object>().ToList();
        var ic = new ItemsControl
        {
            ItemsSource = items,
            ItemsPanel = new ItemsPanelTemplate(() => new TemplateResult
            {
                RootComponent = new WrapPanel
                {
                    Orientation = Orientation.Horizontal,
                    ItemWidth = 50,
                    ItemHeight = 50
                }
            })
        };
        ic.Template = ItemsPresenterTemplate();
        ic.Measure(new Size(200, 300));            // 4 columns (200/50), 6 visible rows (300/50)
        ic.Arrange(new Rect(0, 0, 200, 300));

        var gen = ic.ItemContainerGenerator;
        var panel = ic.ItemsHostPanel;
        var scroll = (IScrollableContent)panel;

        Assert.Multiple(() =>
        {
            Assert.That(gen.RealizedCount, Is.LessThan(60), "only the visible grid window realized, not 10000");
            Assert.That(scroll.Extent.Height, Is.EqualTo(2500 * 50).Within(1), "extent height = ceil(10000/4) lines * 50");
            Assert.That(scroll.Extent.Width, Is.EqualTo(4 * 50).Within(1), "extent width = 4 columns * 50");
            Assert.That(gen.ContainerFromIndex(0), Is.Not.Null);
            Assert.That(gen.ContainerFromIndex(9999), Is.Null);
        });

        // Scroll down to line 20 (1000 / 50): the realized grid window moves there.
        scroll.SetOffset(new Vector2(0, 1000));
        panel.Measure(new Size(200, 300), true);
        panel.Arrange(new Rect(0, 0, 200, 300), true);

        var realized = gen.RealizedIndices.ToList();
        Assert.Multiple(() =>
        {
            Assert.That(gen.RealizedCount, Is.LessThan(60), "window stays bounded after scrolling");
            Assert.That(realized, Has.Some.InRange(80, 84), "window around line 20 (index ~80 = 20*4)");
            Assert.That(gen.ContainerFromIndex(0), Is.Null, "top rows recycled away");
        });
    }

    [Test]
    public void VirtualizingWrapPanel_AutoCell_VariableWidthItems_StableGridAndContent()
    {
        // Variable-width string items with NO ItemWidth/ItemHeight -> the panel resolves a UNIFORM cell itself and must
        // keep it stable across scrolls. Regression: the old code re-probed ONE item's width every pass, so the cell and
        // column count flickered (extent oscillated) and the grid reflowed into overlapping/garbled rows + scroll churn.
        var items = Enumerable.Range(0, 3000).Select(i => $"Item {i}").Cast<object>().ToList();
        var ic = new ItemsControl
        {
            ItemsSource = items,
            ItemsPanel = new ItemsPanelTemplate(() => new TemplateResult
            {
                RootComponent = new WrapPanel { Orientation = Orientation.Horizontal }   // auto cell (no ItemWidth/Height)
            })
        };
        ic.Template = ItemsPresenterTemplate();
        ic.Measure(new Size(320, 240));
        ic.Arrange(new Rect(0, 0, 320, 240));

        var gen = ic.ItemContainerGenerator;
        var panel = ic.ItemsHostPanel;
        var scroll = (IScrollableContent)panel;

        void LayoutAt(double y)
        {
            scroll.SetOffset(new Vector2(0, y));
            panel.Measure(new Size(320, 240), true);
            panel.Arrange(new Rect(0, 0, 320, 240), true);
        }

        void AssertContentBound(string where)
        {
            foreach (var i in gen.RealizedIndices)
                Assert.That(((ContentPresenter)gen.ContainerFromIndex(i)).Content, Is.EqualTo(items[i]),
                    $"index {i} shows its own item ({where}) - recycling rebinds in place, no stale/duplicate text");
        }

        // Scroll forward: the cell may grow to fit wider items (more digits); track the peak row extent.
        var peakHeight = scroll.Extent.Height;
        var maxRealized = 0;
        for (double y = 0; y <= 4000; y += 173)
        {
            LayoutAt(y);
            peakHeight = Math.Max(peakHeight, scroll.Extent.Height);
            maxRealized = Math.Max(maxRealized, gen.RealizedCount);
            AssertContentBound($"forward y={y}");
        }

        // Scroll back over the SAME items: a grow-only cached cell must not shrink, so the extent stays at its peak. The
        // old per-pass re-probe shrank the cell here (narrow items at the top) -> column count jumped -> grid reflowed.
        for (double y = 4000; y >= 0; y -= 173)
        {
            LayoutAt(y);
            Assert.That(scroll.Extent.Height, Is.EqualTo(peakHeight).Within(0.5),
                $"row extent stays stable scrolling back (y={y}) - the cell does not flicker");
            AssertContentBound($"back y={y}");
        }

        Assert.That(maxRealized, Is.LessThan(150), "the realized grid window stays bounded at every scroll position");
    }

    [Test]
    public void RecyclingBoundsContainerAllocation()
    {
        var items = Enumerable.Range(0, 10000).Cast<object>().ToList();
        var ic = new ItemsControl
        {
            ItemsSource = items,
            ItemTemplate = new DataTemplate(() => new TemplateResult { RootComponent = new Border { Height = 20 } })
        };
        ic.Template = ItemsPresenterTemplate();
        ic.Measure(new Size(100, 300));
        ic.Arrange(new Rect(0, 0, 100, 300));

        var gen = ic.ItemContainerGenerator;
        var panel = ic.ItemsHostPanel;
        var scroll = (IScrollableContent)panel;

        var seen = new HashSet<IUIComponent>();
        var maxRealized = 0;

        // Scroll through the entire 10k list a viewport at a time.
        for (double y = 0; y <= scroll.Extent.Height - 300; y += 300)
        {
            scroll.SetOffset(new Vector2(0, y));
            panel.Measure(new Size(100, 300), true);
            panel.Arrange(new Rect(0, 0, 100, 300), true);

            foreach (var i in gen.RealizedIndices)
                seen.Add(gen.ContainerFromIndex(i));
            maxRealized = Math.Max(maxRealized, gen.RealizedCount);
        }

        Assert.Multiple(() =>
        {
            Assert.That(maxRealized, Is.LessThan(60), "the realized window is bounded at every scroll position");
            Assert.That(seen.Count, Is.LessThan(100),
                "containers are recycled from a small pool, not allocated per item (would be ~10000 without recycling)");
        });
    }

    [Test]
    public void ScrollContentPresenterDelegatesToVirtualizingPanel()
    {
        var ic = new ItemsControl
        {
            ItemsSource = Enumerable.Range(0, 10000).Cast<object>().ToList(),
            ItemTemplate = new DataTemplate(() => new TemplateResult { RootComponent = new Border { Height = 20 } })
        };
        ic.Template = ItemsPresenterTemplate();

        // A ScrollContentPresenter (CanContentScroll) hosting the items control must delegate its scroll surface to the
        // inner virtualizing panel - WPF's CanContentScroll on the IScrollableContent seam.
        var scp = new ScrollContentPresenter { CanContentScroll = true, Content = ic };
        scp.Measure(new Size(100, 300));
        scp.Arrange(new Rect(0, 0, 100, 300));

        var surface = (IScrollableContent)scp;
        var gen = ic.ItemContainerGenerator;
        var panel = ic.ItemsHostPanel;

        Assert.That(surface.Extent.Height, Is.EqualTo(10000 * 20).Within(1), "SCP delegates extent to the inner panel");

        // Scrolling via the SCP reaches the panel; drive the panel layout to realize the new window.
        surface.SetOffset(new Vector2(0, 5000));
        panel.Measure(new Size(100, 300), true);
        panel.Arrange(new Rect(0, 0, 100, 300), true);

        Assert.Multiple(() =>
        {
            Assert.That(surface.Offset.Y, Is.EqualTo(5000).Within(0.5), "SCP offset reads from the panel");
            Assert.That(gen.RealizedIndices.ToList(), Has.Some.InRange(248, 252), "SetOffset via the SCP moved the panel window");
            Assert.That(gen.ContainerFromIndex(0), Is.Null);
        });
    }

    [Test]
    public void RealizesOneContainerPerItem()
    {
        var ic = ArrangedItemsControl(new[] { "a", "b", "c" });
        var gen = ic.ItemContainerGenerator;

        Assert.Multiple(() =>
        {
            Assert.That(gen.ContainerFromIndex(0), Is.InstanceOf<ContentPresenter>());
            Assert.That(gen.ContainerFromIndex(1), Is.InstanceOf<ContentPresenter>());
            Assert.That(gen.ContainerFromIndex(2), Is.InstanceOf<ContentPresenter>());
            Assert.That(gen.ContainerFromIndex(3), Is.Null, "only as many containers as items");
            // Each container projects its own item.
            Assert.That(((ContentPresenter)gen.ContainerFromIndex(0)).Content, Is.EqualTo("a"));
            Assert.That(((ContentPresenter)gen.ContainerFromIndex(2)).Content, Is.EqualTo("c"));
        });
    }

    [Test]
    public void ContainerIndexRoundTrips()
    {
        var ic = ArrangedItemsControl(new[] { "x", "y" });
        var gen = ic.ItemContainerGenerator;
        var c1 = gen.ContainerFromIndex(1);
        Assert.That(gen.IndexFromContainer(c1), Is.EqualTo(1));
    }

    [Test]
    public void ItemTemplateBindingResolvesAgainstItem()
    {
        var people = new List<Person>
        {
            new() { Name = "A", Size = 40 },
            new() { Name = "B", Size = 120 },
            new() { Name = "C", Size = 250 }
        };

        var template = new DataTemplate(() =>
        {
            var border = new Border();
            var result = new TemplateResult { RootComponent = border };
            result.AddBinding(border, "Width", new Binding("Size"));
            return result;
        });

        var ic = ArrangedItemsControl(people, template);
        var gen = ic.ItemContainerGenerator;

        Assert.Multiple(() =>
        {
            for (var i = 0; i < people.Count; i++)
            {
                var container = (ContentPresenter)gen.ContainerFromIndex(i);
                var border = container.VisualChildren.OfType<Border>().First();
                Assert.That(border.Width, Is.EqualTo(people[i].Size).Within(0.5),
                    $"item {i}: ItemTemplate {{Binding Size}} must resolve against the item");
            }
        });
    }

    // Drives the REAL window layout pass (separate measure-then-arrange phases over the whole tree, like the app) rather
    // than calling panel.Measure/Arrange back-to-back. This is what exposes a container whose arrange aborts (its measure
    // got re-invalidated between the two phases) -> it keeps a STALE position -> items render out of order.
    [Test]
    public void VirtualizedContainersStayValidThroughWindowLayoutPass()
    {
        var people = Enumerable.Range(0, 500)
            .Select(i => new Person { Name = $"P{i}", Size = 20 + i % 13 }).ToList();
        var template = new DataTemplate(() =>
        {
            var border = new Border { Height = 20 };
            var result = new TemplateResult { RootComponent = border };
            result.AddBinding(border, "Width", new Binding("Size"));
            return result;
        });

        var ic = new ItemsControl { ItemTemplate = template, ItemsSource = people };
        ic.Template = ItemsPresenterTemplate();
        // Host in a fixed-size Border so the window-style pass measures the panel at the 100x300 viewport (not Infinity).
        var host = new Border { Width = 100, Height = 300, Child = ic };

        Adamantium.UI.Extensions.WindowExtension.UpdateTree(host);

        var gen = ic.ItemContainerGenerator;
        var panel = ic.ItemsHostPanel;
        var scroll = (IScrollableContent)panel;

        void Scroll(double y) { scroll.SetOffset(new Vector2(0, y)); Adamantium.UI.Extensions.WindowExtension.UpdateTree(host); }

        Assert.Multiple(() =>
        {
            foreach (var y in new double[] { 0, 1000, 5000, 200, 3333, 80, 9000, 0 })
            {
                Scroll(y);
                var actual = scroll.Offset.Y;
                foreach (var i in gen.RealizedIndices.ToList())
                {
                    var c = (IMeasurableComponent)gen.ContainerFromIndex(i);
                    Assert.That(c.IsMeasureValid, Is.True, $"offset {actual}, index {i}: measure valid (not aborted)");
                    Assert.That(c.IsArrangeValid, Is.True, $"offset {actual}, index {i}: arrange APPLIED (not aborted -> stale position)");
                    var border = (c as ContentPresenter)?.VisualChildren.OfType<Border>().FirstOrDefault();
                    Assert.That(border?.Width, Is.EqualTo(people[i].Size).Within(0.5), $"offset {actual}, index {i}: correct data");
                }
            }
        });
    }

    // The closest headless mirror of the app: a delegating ScrollContentPresenter hosts the ItemsControl, driven through
    // the real window layout pass. Checks the APPLIED render position (WorldTransform) of every realized container is
    // monotonic by index and spaced by the item extent - i.e. nothing renders out of order / outside the viewport.
    [Test]
    public void VirtualizedItemsRenderInOrderThroughScrollContentPresenter()
    {
        var people = Enumerable.Range(0, 500)
            .Select(i => new Person { Name = $"P{i}", Size = 20 + i % 13 }).ToList();
        var template = new DataTemplate(() =>
        {
            var border = new Border { Height = 20 };
            var result = new TemplateResult { RootComponent = border };
            result.AddBinding(border, "Width", new Binding("Size"));
            return result;
        });

        var ic = new ItemsControl { ItemTemplate = template, ItemsSource = people };
        ic.Template = ItemsPresenterTemplate();
        var scp = new ScrollContentPresenter { CanContentScroll = true, Content = ic };
        var host = new Border { Width = 100, Height = 300, Child = scp };

        Adamantium.UI.Extensions.WindowExtension.UpdateTree(host);

        var gen = ic.ItemContainerGenerator;
        var surface = (IScrollableContent)scp;

        void Scroll(double y) { surface.SetOffset(new Vector2(0, y)); Adamantium.UI.Extensions.WindowExtension.UpdateTree(host); }

        static double WorldY(IUIComponent c) =>
            new Rect(0, 0, c.RenderSize.Width, c.RenderSize.Height).TransformToAABB(c.WorldTransform).Y;

        Assert.Multiple(() =>
        {
            foreach (var y in new double[] { 0, 1000, 5000, 200, 3333, 80, 9000, 0 })
            {
                Scroll(y);
                var actual = surface.Offset.Y;

                foreach (var i in gen.RealizedIndices.ToList())
                {
                    var c = gen.ContainerFromIndex(i);
                    // The renderer positions each container by its WorldTransform; it must equal index*extent - offset.
                    Assert.That(WorldY(c), Is.EqualTo(i * 20 - actual).Within(1.0),
                        $"offset {actual}, index {i}: rendered position must follow index order (not jumbled/stale)");
                    var border = (c as ContentPresenter)?.VisualChildren.OfType<Border>().FirstOrDefault();
                    Assert.That(border?.Width, Is.EqualTo(people[i].Size).Within(0.5),
                        $"offset {actual}, index {i}: correct item data");
                }
            }
        });
    }

    // Continuous (incremental) scroll, like the wheel in the app: tiny offset deltas, many frames, so containers are
    // recycled 1-2 at a time and the pool state builds up - the regime a big-jump test never reaches. After EVERY step
    // every realized container must still show its own index's data at its own (index-ordered) position.
    [Test]
    public void VirtualizedListStaysCorrectUnderContinuousScroll()
    {
        var people = Enumerable.Range(0, 500)
            .Select(i => new Person { Name = $"P{i}", Size = 20 + i % 13 }).ToList();
        var template = new DataTemplate(() =>
        {
            var border = new Border { Height = 20 };
            var result = new TemplateResult { RootComponent = border };
            result.AddBinding(border, "Width", new Binding("Size"));
            return result;
        });

        var ic = new ItemsControl { ItemTemplate = template, ItemsSource = people };
        ic.Template = ItemsPresenterTemplate();
        var scp = new ScrollContentPresenter { CanContentScroll = true, Content = ic };
        var host = new Border { Width = 100, Height = 300, Child = scp };
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(host);

        var gen = ic.ItemContainerGenerator;
        var panel = ic.ItemsHostPanel;
        var surface = (IScrollableContent)scp;
        static double WorldY(IUIComponent c) =>
            new Rect(0, 0, c.RenderSize.Width, c.RenderSize.Height).TransformToAABB(c.WorldTransform).Y;

        Assert.Multiple(() =>
        {
            double off = 0;
            for (var step = 0; step < 600; step++)   // 600 * 17 = 10200 > extent, so it sweeps the whole list and back
            {
                off += step < 300 ? 17 : -17;        // forward then backward, to exercise both recycle directions
                surface.SetOffset(new Vector2(0, off));
                Adamantium.UI.Extensions.WindowExtension.UpdateTree(host);
                var actual = surface.Offset.Y;

                var realized = gen.RealizedIndices.Select(idx => gen.ContainerFromIndex(idx)).ToHashSet();
                foreach (var i in gen.RealizedIndices.ToList())
                {
                    var c = gen.ContainerFromIndex(i);
                    Assert.That(WorldY(c), Is.EqualTo(i * 20 - actual).Within(1.0),
                        $"step {step} (off {actual}), index {i}: position must stay index-ordered");
                    var border = (c as ContentPresenter)?.VisualChildren.OfType<Border>().FirstOrDefault();
                    Assert.That(border?.Width, Is.EqualTo(people[i].Size).Within(0.5),
                        $"step {step} (off {actual}), index {i}: data must stay correct (no stale recycle)");
                }

                // The renderer draws every VISIBLE child of the panel, not just the realized window. So a pooled
                // (recycled, unrealized) container that is still Visible would paint stale data at a stale position
                // ALONGSIDE the correct items - the "вразнобой / за пределами" symptom. Visible must mean realized.
                foreach (var child in panel.VisualChildren)
                    Assert.That(child.Visibility == Visibility.Visible, Is.EqualTo(realized.Contains(child)),
                        $"step {step} (off {actual}): a container is Visible iff it's in the realized window " +
                        $"(visible={child.Visibility == Visibility.Visible}, realized={realized.Contains(child)})");
            }
        });
    }

    // The render walks the VISUAL tree once per node; a node that appears twice in some parent's VisualChildren is
    // walked (and drawn) twice. This reproduces the "every item rendered twice" overdraw by asserting no component
    // has a duplicate in its VisualChildren after realizing + scrolling.
    [Test]
    public void NoDuplicateVisualChildrenInVirtualizedList()
    {
        var people = Enumerable.Range(0, 500).Select(i => new Person { Name = $"P{i}", Size = 28 }).ToList();
        var template = new DataTemplate(() =>
        {
            var border = new Border { Height = 28 };
            var result = new TemplateResult { RootComponent = border };
            result.AddBinding(border, "Width", new Binding("Size"));
            return result;
        });
        var ic = new ItemsControl { ItemTemplate = template, ItemsSource = people };
        ic.Template = ItemsPresenterTemplate();
        var scp = new ScrollContentPresenter { CanContentScroll = true, Content = ic };
        var host = new Border { Width = 100, Height = 300, Child = scp };
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(host);
        var surface = (IScrollableContent)scp;

        static (IUIComponent node, IUIComponent dup) FindDuplicateChild(IUIComponent root)
        {
            var seen = new HashSet<IUIComponent>();
            foreach (var child in root.VisualChildren)
                if (!seen.Add(child)) return (root, child);
            foreach (var child in root.VisualChildren)
            {
                var deeper = FindDuplicateChild(child);
                if (deeper.dup != null) return deeper;
            }
            return (null, null);
        }

        Assert.Multiple(() =>
        {
            foreach (var y in new double[] { 0, 200, 1000, 5000, 200, 0 })
            {
                surface.SetOffset(new Vector2(0, y));
                Adamantium.UI.Extensions.WindowExtension.UpdateTree(host);
                var (node, dup) = FindDuplicateChild(host);
                Assert.That(dup, Is.Null,
                    $"offset {y}: {node?.GetType().Name} has {dup?.GetType().Name} twice in VisualChildren (=> drawn twice)");
            }
        });
    }

    [Test]
    public void RecycledContainerShowsCorrectItemAfterScrolling()
    {
        // Distinct per-item Size so a stale rebind (recycled container still showing the previous item) is detectable.
        var people = Enumerable.Range(0, 500)
            .Select(i => new Person { Name = $"P{i}", Size = 20 + i % 13 }).ToList();
        var template = new DataTemplate(() =>
        {
            var border = new Border { Height = 20 };               // fixed height -> virtualizes, item extent 20
            var result = new TemplateResult { RootComponent = border };
            result.AddBinding(border, "Width", new Binding("Size"));
            return result;
        });

        var ic = new ItemsControl { ItemTemplate = template, ItemsSource = people };
        ic.Template = ItemsPresenterTemplate();
        ic.Measure(new Size(100, 300));
        ic.Arrange(new Rect(0, 0, 100, 300));

        var gen = ic.ItemContainerGenerator;
        var panel = ic.ItemsHostPanel;
        var scroll = (IScrollableContent)panel;

        void ScrollTo(double y)
        {
            scroll.SetOffset(new Vector2(0, y));
            panel.Measure(new Size(100, 300), true);
            panel.Arrange(new Rect(0, 0, 100, 300), true);
        }

        Assert.Multiple(() =>
        {
            foreach (var y in new double[] { 0, 1000, 5000, 200, 9960, 3333, 80, 0 })
            {
                ScrollTo(y);

                // Every index whose row is inside the viewport must be realized (nothing missing). Use the panel's
                // actual (clamped) offset, and never expect an index past the last item.
                var actual = scroll.Offset.Y;
                var firstVisible = (int)(actual / 20);
                var lastVisible = Math.Min(people.Count - 1, (int)((actual + 300) / 20) - 1);
                for (var i = firstVisible; i <= lastVisible; i++)
                    Assert.That(gen.ContainerFromIndex(i), Is.Not.Null, $"offset {actual}: viewport index {i} must be realized");

                // Every realized container must project ITS index's item (no stale/jumbled data).
                foreach (var i in gen.RealizedIndices.ToList())
                {
                    var border = (gen.ContainerFromIndex(i) as ContentPresenter)?.VisualChildren.OfType<Border>().FirstOrDefault();
                    Assert.That(border, Is.Not.Null, $"offset {y}, index {i}: container has its template visual");
                    Assert.That(border.Width, Is.EqualTo(people[i].Size).Within(0.5),
                        $"offset {y}, index {i}: recycled container must show item {i}, not a stale item");
                }
            }
        });
    }

    [Test]
    public void ObservableCollectionChangesUpdateContainers()
    {
        var data = new ObservableCollection<string> { "a", "b", "c" };
        var ic = ArrangedItemsControl(data);
        var gen = ic.ItemContainerGenerator;

        // Containers are (re)realized on layout. A collection change invalidates the PANEL (Revirtualize); the live
        // framework re-measures it via the layout manager's dirty queue (MeasureDirty), so the headless test drives the
        // panel's layout directly - re-measuring ic alone would skip it, since ic's still-valid ancestors of the panel
        // don't re-cascade into it (finer measure propagation: a parent isn't re-measured unless its own size changed).
        void Relayout()
        {
            var panel = ic.ItemsHostPanel;
            panel.Measure(new Size(500, 500), true);
            panel.Arrange(new Rect(0, 0, 500, 500), true);
        }

        string ContentAt(int i) => (gen.ContainerFromIndex(i) as ContentPresenter)?.Content as string;

        Assert.That(ContentAt(2), Is.EqualTo("c"), "initial");

        data.Add("d");
        Relayout();
        Assert.That(ContentAt(3), Is.EqualTo("d"), "append");

        data.Insert(1, "B2");                       // a, B2, b, c, d
        Relayout();
        Assert.Multiple(() =>
        {
            Assert.That(ContentAt(1), Is.EqualTo("B2"), "inserted in the middle");
            Assert.That(ContentAt(2), Is.EqualTo("b"), "item after the insert shifted up");
        });

        data[0] = "A2";                             // replace head
        Relayout();
        Assert.That(ContentAt(0), Is.EqualTo("A2"), "replace");

        data.RemoveAt(0);                           // A2, B2, b, c, d -> B2, b, c, d
        Relayout();
        Assert.Multiple(() =>
        {
            Assert.That(ContentAt(0), Is.EqualTo("B2"), "remove shifts the rest down");
            Assert.That(gen.ContainerFromIndex(4), Is.Null, "count dropped to 4");
        });
    }

    private sealed class EvenOddSelector : DataTemplateSelector
    {
        public DataTemplate Even { get; init; }
        public DataTemplate Odd { get; init; }
        public override DataTemplate SelectTemplate(object item, AdamantiumComponent container)
            => (int)item % 2 == 0 ? Even : Odd;
    }

    [Test]
    public void ItemTemplateSelectorPicksPerItem()
    {
        var even = new DataTemplate(() => new TemplateResult { RootComponent = new Border() });
        var odd = new DataTemplate(() => new TemplateResult { RootComponent = new TextBlock() });

        var ic = new ItemsControl
        {
            ItemTemplateSelector = new EvenOddSelector { Even = even, Odd = odd },
            ItemsSource = new[] { 0, 1, 2, 3 }.Cast<object>().ToList()
        };
        ic.Template = ItemsPresenterTemplate();
        ic.Measure(new Size(200, 500));
        ic.Arrange(new Rect(0, 0, 200, 500));

        var gen = ic.ItemContainerGenerator;
        Assert.Multiple(() =>
        {
            Assert.That(((ContentPresenter)gen.ContainerFromIndex(0)).VisualChildren.OfType<Border>().Any(), Is.True, "even -> Border template");
            Assert.That(((ContentPresenter)gen.ContainerFromIndex(1)).VisualChildren.OfType<TextBlock>().Any(), Is.True, "odd -> TextBlock template");
            Assert.That(((ContentPresenter)gen.ContainerFromIndex(2)).VisualChildren.OfType<Border>().Any(), Is.True);
        });
    }

    private sealed class MyContainer : ContentPresenter { }

    private sealed class CustomContainerItemsControl : ItemsControl
    {
        protected internal override bool IsItemItsOwnContainer(object item) => false;
        protected internal override IUIComponent GetContainerForItem() => new MyContainer();
    }

    [Test]
    public void ContainerSeamLetsSubclassChooseContainerType()
    {
        var ic = new CustomContainerItemsControl { ItemsSource = new[] { "a", "b" } };
        ic.Template = ItemsPresenterTemplate();
        ic.Measure(new Size(200, 500));
        ic.Arrange(new Rect(0, 0, 200, 500));

        var gen = ic.ItemContainerGenerator;
        Assert.Multiple(() =>
        {
            Assert.That(gen.ContainerFromIndex(0), Is.InstanceOf<MyContainer>(), "subclass container type is used by the generator");
            Assert.That(((MyContainer)gen.ContainerFromIndex(1)).Content, Is.EqualTo("b"), "base PrepareContainer still binds the item");
        });
    }

    [Test]
    public void ItemContainerStyleAppliesToEachContainer()
    {
        var style = new Style { Selector = new Selector { Types = { typeof(ContentPresenter) } } };
        style.Setters.Add(new Setter("Width", "42"));

        var ic = new ItemsControl { ItemContainerStyle = style, ItemsSource = new[] { "a", "b", "c" } };
        ic.Template = ItemsPresenterTemplate();
        ic.Measure(new Size(200, 500));
        ic.Arrange(new Rect(0, 0, 200, 500));

        var gen = ic.ItemContainerGenerator;
        Assert.Multiple(() =>
        {
            Assert.That(((ContentPresenter)gen.ContainerFromIndex(0)).Width, Is.EqualTo(42).Within(0.5));
            Assert.That(((ContentPresenter)gen.ContainerFromIndex(2)).Width, Is.EqualTo(42).Within(0.5));
        });
    }

    private sealed class ItemVm
    {
        public string Name { get; init; }
    }

    private sealed class ListVm
    {
        public ObservableCollection<ItemVm> People { get; } = new();
    }

    [Test]
    public void ViewModelBoundItemsSourceAndItemTemplate()
    {
        var vm = new ListVm();
        vm.People.Add(new ItemVm { Name = "Alice" });
        vm.People.Add(new ItemVm { Name = "Bob" });

        var ic = new ItemsControl
        {
            DataContext = vm,
            ItemTemplate = new DataTemplate(() =>
            {
                var tb = new TextBlock();
                var r = new TemplateResult { RootComponent = tb };
                r.AddBinding(tb, "Text", new Binding("Name"));   // item template binds to ItemVm.Name
                return r;
            })
        };
        // ItemsSource bound to the view-model's collection (resolves against DataContext = vm).
        BindingEngine.SetBinding(ic, ItemsControl.ItemsSourceProperty, new Binding("People"));
        ic.Template = ItemsPresenterTemplate();
        ic.Measure(new Size(200, 500));
        ic.Arrange(new Rect(0, 0, 200, 500));

        var gen = ic.ItemContainerGenerator;
        string NameAt(int i) => ((ContentPresenter)gen.ContainerFromIndex(i)).VisualChildren.OfType<TextBlock>().First().Text;

        Assert.Multiple(() =>
        {
            Assert.That(NameAt(0), Is.EqualTo("Alice"), "VM collection bound to ItemsSource, item template bound to item prop");
            Assert.That(NameAt(1), Is.EqualTo("Bob"));
        });

        // Mutating the VM's collection flows through to a new container after layout. The collection change invalidates
        // the PANEL (Revirtualize); re-measure it directly (the manager does this via MeasureDirty at runtime) - finer
        // measure propagation means re-measuring ic alone wouldn't re-cascade into the still-valid panel ancestors.
        vm.People.Add(new ItemVm { Name = "Carol" });
        var panel = ic.ItemsHostPanel;
        panel.Measure(new Size(200, 500), true);
        panel.Arrange(new Rect(0, 0, 200, 500), true);
        Assert.That(NameAt(2), Is.EqualTo("Carol"), "VM collection add -> new container");
    }

    [Test]
    public void DirectMarkupItemsAreRealized()
    {
        // Items authored directly (no ItemsSource) — the [Content] / IContainer path.
        var ic = new ItemsControl();
        ((IContainer)ic).AddOrSetChildComponent("one");
        ((IContainer)ic).AddOrSetChildComponent("two");
        ic.Template = new ControlTemplate(() =>
        {
            var presenter = new ItemsPresenter();
            var result = new TemplateResult { RootComponent = presenter };
            result.RegisterName("PART_ItemsPresenter", presenter);
            return result;
        });
        ic.Measure(new Size(500, 500));
        ic.Arrange(new Rect(0, 0, 500, 500));

        var gen = ic.ItemContainerGenerator;
        Assert.Multiple(() =>
        {
            Assert.That(((ContentPresenter)gen.ContainerFromIndex(0)).Content, Is.EqualTo("one"));
            Assert.That(((ContentPresenter)gen.ContainerFromIndex(1)).Content, Is.EqualTo("two"));
            Assert.That(gen.ContainerFromIndex(2), Is.Null);
        });
    }
}
