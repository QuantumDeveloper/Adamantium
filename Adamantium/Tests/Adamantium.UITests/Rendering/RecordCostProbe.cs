using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Diagnostics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Templates;
using Adamantium.UI.Extensions;
using Adamantium.UI.Rendering;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>
/// Where the RECORD half of a tile-grid resize goes, headless. The record is device-free by design, so the whole of it
/// runs here - the same RecordFrame the app calls, with the same counters (RuntimeStats.LastRecord*) read back per frame.
///
/// Written because attributing it on the live stand cost a hand-drag per hypothesis, and three of those hypotheses were
/// wrong. Here a hypothesis costs a test run.
/// </summary>
[TestFixture]
[Explicit("Measurement probe - run it deliberately and read the numbers")]
public class RecordCostProbe
{
    private const double ViewportW = 3840;
    private const double ViewportH = 2160;

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

    // The sandbox tile is a CHAIN, not one element: container -> Border -> ContentPresenter -> Rectangle. Depth is most of
    // what the record pays (each level is its own record), so the probe has to have it.
    private static (TestWindowRoot root, WrapPanel panel) BuildScene(int itemCount, double cell)
    {
        var items = Enumerable.Range(0, itemCount).Cast<object>().ToList();
        WrapPanel panel = null;
        var ic = new ItemsControl
        {
            ItemsSource = items,
            ItemTemplate = new DataTemplate(() => new TemplateResult
            {
                RootComponent = new Border
                {
                    Margin = new Thickness(3),
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
                RootComponent = panel = new WrapPanel { Orientation = Orientation.Horizontal, ItemWidth = cell, ItemHeight = cell }
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
        return (root, panel);
    }

    private sealed class Totals
    {
        public int Frames;
        public double RecordMs, RenderMs, CopyMs, PlanMs, SnapMs, SnapDrawsMs, SnapDirtyMs, SnapOpacityMs, SnapTailMs;
        public int Dirty, Skips, Empty, Published;
        public long AllocBytes, RenderBytes, CopyBytes, SnapBytes;
        public double LayoutMs, ApplyMs, PlanOnlyMs;
        public int Marks;
        public readonly Dictionary<string, double> ByType = new();

        public void Add(double recordMs, long allocBytes)
        {
            Frames++;
            AllocBytes += allocBytes;
            RecordMs += recordMs;
            RenderMs += RuntimeStats.LastRecordRenderMs;
            CopyMs += RuntimeStats.LastRecordCopyMs;
            PlanMs += RuntimeStats.LastRecordPlanMs;
            SnapMs += RuntimeStats.LastRecordSnapMs;
            SnapDrawsMs += RuntimeStats.LastSnapDrawsMs;
            SnapDirtyMs += RuntimeStats.LastSnapDirtyMs;
            SnapOpacityMs += RuntimeStats.LastSnapOpacityMs;
            SnapTailMs += RuntimeStats.LastSnapTailMs;
            RenderBytes += RuntimeStats.LastRecordRenderBytes;
            CopyBytes += RuntimeStats.LastRecordCopyBytes;
            SnapBytes += RuntimeStats.LastSnapBytes;
            PlanOnlyMs += RuntimeStats.LastRecordPlanOnlyMs;
            Marks += RuntimeStats.LastRecordStructuralMarks;
            Dirty += RuntimeStats.LastRecordDirty;
            Skips += RuntimeStats.LastRecordClassifySkips;
            Empty += RuntimeStats.LastRecordEmptyDraws;
            Published += RuntimeStats.LastSnapPublished;
            foreach (var pair in RuntimeStats.RecordMsByType)
            {
                ByType.TryGetValue(pair.Key.Name, out var t);
                ByType[pair.Key.Name] = t + pair.Value;
            }
            RuntimeStats.RecordMsByType.Clear();
        }

        public void Report(string title)
        {
            var n = Math.Max(1, Frames);
            TestContext.Out.WriteLine($"--- {title}: {Frames} recorded frames, per frame ---");
            TestContext.Out.WriteLine($"  layout   {LayoutMs / n,8:F1} ms");
            TestContext.Out.WriteLine($"  record   {RecordMs / n,8:F1} ms");
            TestContext.Out.WriteLine($"  apply    {ApplyMs / n,8:F1} ms");
            TestContext.Out.WriteLine($"    render {RenderMs / n,8:F1} ms   copy {CopyMs / n,6:F1}   plan {PlanMs / n,6:F1} (place {PlanOnlyMs / n,5:F1} for {Marks / n,6} marks)");
            TestContext.Out.WriteLine($"    snap   {SnapMs / n,8:F1} ms   (draws {SnapDrawsMs / n,5:F1}, dirty {SnapDirtyMs / n,5:F1}, opacity {SnapOpacityMs / n,5:F1}, tail {SnapTailMs / n,5:F1})");
            TestContext.Out.WriteLine($"  dirty {Dirty / n,7}   skipped {Skips / n,7}   empty {Empty / n,7}   published {Published / n,7}");
            TestContext.Out.WriteLine($"  alloc  {AllocBytes / n / 1024.0,8:F0} KB/frame   (render {RenderBytes / n / 1024.0,6:F0}, copy {CopyBytes / n / 1024.0,6:F0}, snap {SnapBytes / n / 1024.0,6:F0})");
            foreach (var pair in ByType.OrderByDescending(p => p.Value).Take(8))
                TestContext.Out.WriteLine($"    render<{pair.Key}> {pair.Value / n,7:F2} ms/frame");
        }
    }

    /// <summary>The gesture: drag the size slider, one layout pass and one recorded frame per step - which is what the app
    /// does, and why the cost is visible mid-drag rather than at the end.</summary>
    [Test]
    public void DragBreakdown()
    {
        var (root, panel) = BuildScene(60000, 24);
        var cache = new RenderCache(new DrawingContext(), new FakeRenderUnitFactory());

        for (var i = 0; i < 40; i++) { WindowExtension.UpdateTree(root); cache.BuildFromVisualTree(root); }

        var totals = new Totals();
        double cell = 24;
        for (var step = 0; step < 20; step++)
        {
            cell += step % 2 == 0 ? 9 : -4;
            panel.ItemWidth = cell;
            panel.ItemHeight = cell;

            var layout0 = System.Diagnostics.Stopwatch.GetTimestamp();
            WindowExtension.UpdateTree(root);
            var layoutMs = System.Diagnostics.Stopwatch.GetElapsedTime(layout0).TotalMilliseconds;

            var alloc0 = GC.GetAllocatedBytesForCurrentThread();
            var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            cache.RecordFrame(root);
            var ms = System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
            var alloc = GC.GetAllocatedBytesForCurrentThread() - alloc0;
            var apply0 = System.Diagnostics.Stopwatch.GetTimestamp();
            cache.ApplyFrame();
            var applyMs = System.Diagnostics.Stopwatch.GetElapsedTime(apply0).TotalMilliseconds;

            // let the first steps settle the container pool
            if (step >= 4) { totals.Add(ms, alloc); totals.LayoutMs += layoutMs; totals.ApplyMs += applyMs; }
        }

        totals.Report($"{ViewportW}x{ViewportH} tile-size drag");
    }

    /// <summary>Content ARRIVING - what a tab entry does. Modelled on what the live stand measured: a container holding
    /// thousands of children, of which a few hundred each receive one new child. The plan then has to place a few hundred
    /// marks, and it re-read 1.18 MILLION children to do it.</summary>
    [Test]
    public void AttachBreakdown()
    {
        const int Siblings = 5000;
        const int Receivers = 228;

        var big = new StackPanel();
        var hosts = new List<StackPanel>();
        for (var i = 0; i < Siblings; i++)
        {
            var host = new StackPanel();
            big.Children.Add(new Border { Background = Brushes.Red, Child = host });
            hosts.Add(host);
        }

        var root = new TestWindowRoot { ClientWidth = ViewportW, ClientHeight = ViewportH };
        root.Children.Add(big);

        var cache = new RenderCache(new DrawingContext(), new FakeRenderUnitFactory());
        for (var i = 0; i < 6; i++) { WindowExtension.UpdateTree(root); cache.BuildFromVisualTree(root); }

        // ...and now the content arrives, spread across many parents - exactly the shape the stand showed.
        for (var i = 0; i < Receivers; i++)
            hosts[i * (Siblings / Receivers)].Children.Add(new Border { Background = Brushes.Blue });

        WindowExtension.UpdateTree(root);

        RuntimeStats.ScansSuccessor = RuntimeStats.ScansLastRank = RuntimeStats.ScansCollect = RuntimeStats.ScansParent = 0;
        var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        cache.RecordFrame(root);
        var ms = System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
        cache.ApplyFrame();

        TestContext.Out.WriteLine($"--- {Receivers} parents each gain a child, among {Siblings} siblings ---");
        TestContext.Out.WriteLine($"  record   {ms,8:F1} ms   (place {RuntimeStats.LastRecordPlanOnlyMs,7:F1})");
        TestContext.Out.WriteLine($"  marks {RuntimeStats.LastRecordStructuralMarks,6}   parents {RuntimeStats.LastRecordPlanParents,6}   runs {RuntimeStats.LastRecordPlanRuns,6}");
        TestContext.Out.WriteLine($"  scans total {RuntimeStats.LastRecordPlanScans,10}");
        TestContext.Out.WriteLine($"    successor {RuntimeStats.ScansSuccessor,10}   lastRank {RuntimeStats.ScansLastRank,9}   collect {RuntimeStats.ScansCollect,8}   parent {RuntimeStats.ScansParent,8}");
    }

    /// <summary>What CONSTRUCTING one component costs. A tab build allocates 12.8KB per attached component - measured
    /// three times on three different seconds and identical each time - which is orders of magnitude more than the
    /// object itself should weigh. This says which type, and how much of it is the base class everything shares.</summary>
    [Test]
    public void ComponentConstructionCost()
    {
        // Bytes AND time, because they are not the same story: 2.8 KB of allocation does not take 16 microseconds, so
        // whatever makes construction slow is mostly NOT what makes it fat, and a fix aimed at one may not touch the other.
        void Cost(string what, Func<object> make)
        {
            for (var i = 0; i < 200; i++) GC.KeepAlive(make());   // warm: statics, type init, property registration
            const int N = 2000;
            var a0 = GC.GetAllocatedBytesForCurrentThread();
            var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            for (var i = 0; i < N; i++) GC.KeepAlive(make());
            var ns = System.Diagnostics.Stopwatch.GetElapsedTime(t0, System.Diagnostics.Stopwatch.GetTimestamp()).TotalMilliseconds * 1_000_000 / N;
            TestContext.Out.WriteLine(
                $"  {what,-34} {(GC.GetAllocatedBytesForCurrentThread() - a0) / N,7} B {ns,9:F0} ns");
        }

        Cost("Rectangle", () => new Adamantium.UI.Controls.Shapes.Rectangle());
        Cost("Border", () => new Border());
        Cost("TextBlock", () => new Adamantium.UI.Controls.Text.TextBlock());
        Cost("ContentPresenter", () => new ContentPresenter());
        Cost("StackPanel", () => new StackPanel());
        Cost("Grid", () => new Grid());
        Cost("ListBoxItem", () => new ListBoxItem());
        Cost("the whole tile chain", () => new Border
        {
            Margin = new Thickness(3),
            Child = new Adamantium.UI.Controls.Shapes.Rectangle { Fill = Brushes.Blue }
        });

        // AdamantiumComponent WITHOUT the UIComponent half: same property store, no visual-tree machinery. The difference
        // between this and a Border is what being an ELEMENT costs on top of being a property owner.
        Cost("SolidColorBrush (AdamantiumComponent)", () => new SolidColorBrush());

        // The broad sweep: what an EMPTY instance of each control costs before anyone has done anything with it. Ordered
        // by nothing in particular on purpose - the point is to let the numbers pick the targets rather than to confirm a
        // guess about which control is fat. A LEAF (Rectangle, TextBlock, Ellipse) is the interesting shape: it can never
        // have visual children, so anything it builds for them is spent on nothing.
        // A bare instance is not what a template produces - it produces one with several properties SET, and each write is
        // what brings the value map and a container into being. The difference between these two rows is the real cost of
        // "this element has properties", multiplied by every element a template stamps out.
        TestContext.Out.WriteLine("  -- what WRITING properties adds on top of a bare instance --");
        Cost("Border, bare", () => new Border());
        // Different properties carry different metadata FLAGS, and the flags run different code on write. Measured side
        // by side these separate "the value map came into being" from "the write cascaded somewhere".
        Cost("Border + ZIndex (AffectsRender)", () => new Border { ZIndex = 1 });
        Cost("Border + UseAnalyticAA (Render)", () => new Border { UseAnalyticAA = false });
        Cost("Border + Margin (AffectsMeasure)", () => new Border { Margin = new Thickness(3) });
        Cost("Border + Width (AffectsMeasure)", () => new Border { Width = 10 });
        Cost("Border + 1 property", () => new Border { Margin = new Thickness(3) });
        Cost("Border + 4 properties", () => new Border
        {
            Margin = new Thickness(3), Width = 10, Height = 10, ZIndex = 1
        });
        Cost("Rectangle + Fill", () => new Adamantium.UI.Controls.Shapes.Rectangle { Fill = Brushes.Blue });

        TestContext.Out.WriteLine("  -- an empty instance of each control --");
        Cost("Ellipse", () => new Adamantium.UI.Controls.Shapes.Ellipse());
        Cost("Line", () => new Adamantium.UI.Controls.Shapes.Line());
        Cost("Path", () => new Adamantium.UI.Controls.Shapes.Path());
        Cost("Button", () => new Adamantium.UI.Controls.Buttons.Button());
        Cost("CheckBox", () => new CheckBox());
        Cost("ToggleButton", () => new Adamantium.UI.Controls.Primitives.ToggleButton());
        Cost("Slider", () => new Slider());
        Cost("ScrollBar", () => new Adamantium.UI.Controls.Primitives.ScrollBar());
        Cost("ScrollViewer", () => new ScrollViewer());
        Cost("ScrollContentPresenter", () => new ScrollContentPresenter());
        Cost("ItemsPresenter", () => new ItemsPresenter());
        Cost("ItemsControl", () => new ItemsControl());
        Cost("ListBox", () => new ListBox());
        Cost("ContentControl", () => new ContentControl());
        Cost("WrapPanel", () => new WrapPanel());
        Cost("Canvas", () => new Canvas());
        Cost("DockPanel", () => new DockPanel());
        Cost("TextBox", () => new Adamantium.UI.Controls.Text.TextBox());
        Cost("Image", () => new Image());
        Cost("Thumb", () => new Adamantium.UI.Controls.Primitives.Thumb());
        Cost("Track", () => new Adamantium.UI.Controls.Primitives.Track());

        TestContext.Out.WriteLine("  -- what a component's base costs to build --");
        Cost("ConcurrentDictionary (default)", () => new System.Collections.Concurrent.ConcurrentDictionary<object, object>());
        Cost("ConcurrentDictionary (1 lock, 4)", () => new System.Collections.Concurrent.ConcurrentDictionary<object, object>(1, 4));
        Cost("ConcurrentDictionary (1, 31) = ours", () => new System.Collections.Concurrent.ConcurrentDictionary<object, object>(1, 31));
        Cost("ConcurrentDictionary (1, 16)", () => new System.Collections.Concurrent.ConcurrentDictionary<object, object>(1, 16));
        Cost("ConcurrentDictionary (2, 8)", () => new System.Collections.Concurrent.ConcurrentDictionary<object, object>(2, 8));
        Cost("Dictionary<string,object>", () => new Dictionary<string, object>());
        Cost("TrackingCollection<IUIComponent>", () => new Adamantium.Core.Collections.TrackingCollection<IUIComponent>());

        // The FundamentalUIComponent constructor builds FIVE collections unconditionally - ClassNames, Styles,
        // _attachedStyles, Behaviors, Triggers - and three of them go through SetValue, so each also seeds a container in
        // the property store. For a Border stamped out by a template all five are EMPTY: no classes, no local styles, no
        // behaviors, no triggers. Every AdamantiumCollection pays for itself plus a `new object()` lock plus a
        // `new T[5]` backing array before a single item exists. This is the same shape as the styleValues/triggerValues
        // dictionaries that were already made lazy - measured here rather than assumed.
        TestContext.Out.WriteLine("  -- the five collections every component builds whether or not it uses them --");
        Cost("Classes", () => new Adamantium.UI.Core.Resources.Classes());
        Cost("StylesCollection", () => new Adamantium.UI.Core.Resources.StylesCollection());
        Cost("BehaviorCollection", () => new Adamantium.UI.Core.Collections.BehaviorCollection(null));
        Cost("TriggerCollection", () => new Adamantium.UI.Core.Resources.TriggerCollection());
        Cost("bare AdamantiumCollection<object>", () => new Adamantium.Core.Collections.AdamantiumCollection<object>());
        Cost("just `new object()` (the lock)", () => new object());
        Cost("just `new object[5]` (the items)", () => new object[5]);

        // How many property slots a component ACTUALLY ends up with, once it has been through a real layout - the number
        // that says whether the map's capacity of 31 is anywhere near right, and whether making it lazy helps in the app
        // or only on a bare `new`. Measured on the same tile scene the rest of this fixture uses, not on a fresh object.
        TestContext.Out.WriteLine("  -- property slots per component, after a real layout --");
        {
            var (root, _) = BuildScene(400, 24);
            WindowExtension.UpdateTree(root);
            WindowExtension.UpdateTree(root);

            var byType = new Dictionary<string, (int Count, int Slots, int Max)>();
            void Walk(IUIComponent c)
            {
                var slots = ((AdamantiumComponent)c).ValueSlotCount;
                byType.TryGetValue(c.GetType().Name, out var acc);
                byType[c.GetType().Name] = (acc.Count + 1, acc.Slots + slots, Math.Max(acc.Max, slots));
                foreach (var child in c.VisualChildren) Walk(child);
            }
            Walk(root);

            foreach (var pair in byType.OrderByDescending(p => p.Value.Count).Take(10))
                TestContext.Out.WriteLine(
                    $"  {pair.Key,-28} n={pair.Value.Count,-6} slots avg={(double)pair.Value.Slots / pair.Value.Count,5:F1} max={pair.Value.Max}");
        }

        // `capacity` on a ConcurrentDictionary is the INITIAL bucket count, not a ceiling: it grows and rehashes on its
        // own. Asserted rather than asserted-in-prose, because the whole point of lowering it from 31 to 16 rests on it -
        // if it were a cap, a component with a seventeenth property would silently lose it.
        {
            var probe = new System.Collections.Concurrent.ConcurrentDictionary<int, int>(concurrencyLevel: 1, capacity: 4);
            for (var i = 0; i < 500; i++) probe[i] = i;
            Assert.That(probe.Count, Is.EqualTo(500), "capacity is an initial size, not a limit");
            TestContext.Out.WriteLine($"  capacity(4) holds {probe.Count} entries -> capacity is a starting size, not a cap");
        }

        TestContext.Out.WriteLine($"  processors: {Environment.ProcessorCount}");
    }

    /// <summary>What the hand-rolled float math costs, against the SIMD-accelerated System.Numerics equivalents. The
    /// engine detects AVX2/SSE4.2 (AcceleratedMathConfig) and then uses it nowhere - Matrix4x4F.Multiply is 64 scalar
    /// multiplies and 48 adds. Whether that matters is a question of how often it runs, which the frame counters answer;
    /// this says what one operation costs.</summary>
    [Test]
    public void VectorMathCost()
    {
        void Time(string what, Action body)
        {
            for (var i = 0; i < 1000; i++) body();
            const int N = 200000;
            var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            for (var i = 0; i < N; i++) body();
            TestContext.Out.WriteLine($"  {what,-40} {System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalMilliseconds * 1000000 / N,8:F1} ns");
        }

        var a = Matrix4x4F.Translation(3, 4, 0);
        var b = Matrix4x4F.Scaling(2f, 2f, 1f);
        Matrix4x4F sink = default;
        Time("Matrix4x4F.Multiply (ours, scalar)", () => Matrix4x4F.Multiply(ref a, ref b, out sink));
        Time("Matrix4x4F operator * (ours)", () => { sink = a * b; });

        var na = System.Numerics.Matrix4x4.CreateTranslation(3, 4, 0);
        var nb = System.Numerics.Matrix4x4.CreateScale(2f, 2f, 1f);
        System.Numerics.Matrix4x4 nsink = default;
        Time("System.Numerics.Matrix4x4 * (SIMD)", () => { nsink = na * nb; });

        Time("Matrix4x4F.Translation (build)", () => { sink = Matrix4x4F.Translation(1, 2, 0); });
        TestContext.Out.WriteLine($"  Avx2={System.Runtime.Intrinsics.X86.Avx2.IsSupported} Sse41={System.Runtime.Intrinsics.X86.Sse41.IsSupported} HW-accel Vector={System.Numerics.Vector.IsHardwareAccelerated}");
        GC.KeepAlive(sink); GC.KeepAlive(nsink);
    }

    /// <summary>ONE Rectangle, recorded over and over: the drag probe says a Rectangle's record is ~21us, which is far more
    /// than a payload allocation, so the parts have to be timed apart. RenderReadOnly runs OnRender WITHOUT the clean-frame
    /// gate, so the loop measures the real thing rather than an early return.</summary>
    [Test]
    public void OneRectangleRecord()
    {
        var rect = new Adamantium.UI.Controls.Shapes.Rectangle { Fill = Brushes.Blue, Width = 40, Height = 40 };
        var host = new TestWindowRoot { ClientWidth = 200, ClientHeight = 200 };
        host.Children.Add(rect);
        WindowExtension.UpdateTree(host);

        var context = new DrawingContext();
        var internalContext = (Adamantium.UI.Core.Graphics.IDrawingContextInternal)context;
        const int N = 20000;

        double Time(string what, Action body)
        {
            body();   // warm
            var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            for (var i = 0; i < N; i++) body();
            var us = System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalMilliseconds * 1000 / N;
            TestContext.Out.WriteLine($"  {what,-34} {us,8:F3} us");
            return us;
        }

        Time("whole OnRender + Clear", () => { internalContext.Clear(); rect.RenderReadOnly(context); });
        Time("Clear only", () => internalContext.Clear());
        Time("read Fill", () => { var _ = rect.Fill; });
        Time("read CornerRadius", () => { var _ = rect.CornerRadius; });
        Time("read StrokeThickness", () => { var _ = rect.StrokeThickness; });
        Time("GetPen()", () => { var _ = rect.GetPen(); });
        Time("ForControl", () => context.ForControl(rect));
        var fill = rect.Fill; var corners = rect.CornerRadius; var dst = new Rect(new Size(40, 40));
        Time("DrawRectangle (values prefetched)", () =>
        {
            internalContext.Clear();
            ((Adamantium.UI.Core.Graphics.IDrawingSession)context.ForControl(rect)).DrawRectangle(fill, dst, corners, null);
        });
        long Bytes(string what, Action body)
        {
            body();
            var a0 = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++) body();
            var per = (GC.GetAllocatedBytesForCurrentThread() - a0) / 1000;
            TestContext.Out.WriteLine($"  {what,-34} {per,8} B/call");
            return per;
        }

        Bytes("alloc: whole OnRender", () => { internalContext.Clear(); rect.RenderReadOnly(context); });
        Bytes("alloc: DrawRectangle (prefetched)", () =>
        {
            internalContext.Clear();
            ((Adamantium.UI.Core.Graphics.IDrawingSession)context.ForControl(rect)).DrawRectangle(fill, dst, corners, null);
        });
        Bytes("alloc: GetPen()", () => { var _ = rect.GetPen(); });

        Time("WorldTransform (depth 1)", () => { var _ = rect.WorldTransform; });
        Time("ClipToBounds (unset)", () => { var _ = rect.ClipToBounds; });
        Time("Aura (unset)", () => { var _ = rect.Aura; });
    }

    /// <summary>What a property READ costs, set against unset - the lazy-container change moved every unset read onto the
    /// cold path (no container -> UnsetValue -> registration check + default-metadata resolve), and the record does ten of
    /// them per draw command. Times the cold path's parts so the fix aims at the expensive one.</summary>
    [Test]
    public void PropertyReadCost()
    {
        var rect = new Adamantium.UI.Controls.Shapes.Rectangle { Fill = Brushes.Blue };
        var type = rect.GetType();
        const int N = 200000;

        void Time(string what, Action body)
        {
            body();
            var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            for (var i = 0; i < N; i++) body();
            TestContext.Out.WriteLine($"  {what,-40} {System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalMilliseconds * 1000000 / N,8:F1} ns");
        }

        Time("GetValue: SET property (Fill)", () => { var _ = rect.GetValue(Adamantium.UI.Controls.Shapes.Shape.FillProperty); });
        Time("GetValue: UNSET (CornerRadius)", () => { var _ = rect.GetValue(Adamantium.UI.Controls.Shapes.Rectangle.CornerRadiusProperty); });
        Time("  part: GetType()", () => { var _ = rect.GetType(); });
        Time("  part: IsRegistered", () => { var _ = AdamantiumPropertyMap.IsRegistered(rect, Adamantium.UI.Controls.Shapes.Rectangle.CornerRadiusProperty); });
        Time("  part: GetDefaultMetadata", () => { var _ = Adamantium.UI.Controls.Shapes.Rectangle.CornerRadiusProperty.GetDefaultMetadata(type); });
        Time("GetValue: UNSET inheriting (DataContext)", () => { var _ = rect.GetValue(FundamentalUIComponent.DataContextProperty); });
        TestContext.Out.WriteLine("  -- what one LayoutSnapshot is made of --");
        Time("LocalTransform", () => { var _ = rect.LocalTransform; });
        Time("RenderSize", () => { var _ = rect.RenderSize; });
        Time("ClipToBounds", () => { var _ = rect.ClipToBounds; });
        Time("RenderParent", () => { var _ = rect.RenderParent; });
        Time("Bounds", () => { var _ = rect.Bounds; });
        Time("RenderTransform (unset)", () => { var _ = rect.RenderTransform; });

        // The recorder's rank/unit maps are keyed by RenderId (a Guid). HasRank/RankOf/HoldsUnits are asked several times
        // per component per frame, so what a Guid key costs against a reference key is worth knowing.
        TestContext.Out.WriteLine("  -- map key: Guid vs component reference --");
        var byGuid = new Dictionary<Guid, long>();
        var byRef = new Dictionary<IUIComponent, long>();
        var keys = new List<IUIComponent>();
        for (var i = 0; i < 4000; i++)
        {
            var c = new Adamantium.UI.Controls.Shapes.Rectangle();
            keys.Add(c);
            byGuid[c.RenderId] = i;
            byRef[c] = i;
        }
        var k = 0;
        Time("Dictionary<Guid,long> lookup", () => { byGuid.TryGetValue(keys[k++ & 4095 & 3999].RenderId, out _); });
        k = 0;
        Time("Dictionary<IUIComponent,long> lookup", () => { byRef.TryGetValue(keys[k++ & 4095 & 3999], out _); });
    }
}
