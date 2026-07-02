using System.Linq;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry.Shapes;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Templates;
using Adamantium.UI.Rendering;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

// GPU-free tests for the RenderCache lifecycle and update dispatch. Real controls,
// fake render-unit factory. These are kept as permanent regression coverage.
[TestFixture]
public class RenderCacheTests
{
    private TestRoot _root;
    private FakeRenderUnitFactory _factory;
    private RenderCache _cache;

    private static readonly Rect Box = new Rect(0, 0, 10, 10);

    [SetUp]
    public void SetUp()
    {
        _root = new TestRoot();
        _factory = new FakeRenderUnitFactory();
        _cache = new RenderCache(new DrawingContext(), _factory);
    }

    private void RenderFrame() => _cache.BuildFromVisualTree(_root);

    private static void DrawsRectangle(TestControl c, Brush brush = null) =>
        c.RenderAction = s => s.DrawRectangle(brush ?? Brushes.Red, Box);

    private TestControl AddControl()
    {
        var c = new TestControl();
        _root.Add(c);
        return c;
    }

    // -------- cache correctness --------

    [Test]
    public void Hidden_Control_DoesNotAbortTraversal()
    {
        var a = AddControl(); DrawsRectangle(a);
        var b = AddControl(); DrawsRectangle(b); b.Visibility = Visibility.Hidden;
        var c = AddControl(); DrawsRectangle(c);

        RenderFrame();

        // A and C must have produced units; B (hidden) is skipped. Pre-fix, B's hidden state aborted the
        // whole walk and C was never reached.
        Assert.That(_factory.Created.Select(u => u.Component), Does.Contain(a));
        Assert.That(_factory.Created.Select(u => u.Component), Does.Contain(c));
        Assert.That(_factory.Created.Select(u => u.Component), Does.Not.Contain(b));
    }

    [Test]
    public void GrowingCommandCount_DoesNotThrow_AndCreatesUnit()
    {
        var c = AddControl();
        c.RenderAction = s => s.DrawRectangle(Brushes.Red, Box);
        RenderFrame();
        Assert.That(_factory.Created.Count, Is.EqualTo(1));

        // Frame 2: same control now emits TWO commands. Pre-fix off-by-one indexed units[1] out of range.
        c.RenderAction = s => s.DrawRectangle(Brushes.Red, Box).DrawRectangle(Brushes.Blue, Box);
        c.Invalidate();
        Assert.DoesNotThrow(RenderFrame);
        Assert.That(_factory.Created.Count, Is.EqualTo(2));
    }

    [Test]
    public void DirtyControl_RenderingNothing_ClearsItsUnits()
    {
        var c = AddControl(); DrawsRectangle(c);
        RenderFrame();
        var unit = _factory.Created.Single();

        // Frame 2: control is dirty and now draws nothing -> its stale unit must be released.
        c.RenderAction = null;
        c.Invalidate();
        RenderFrame();

        Assert.That(unit.DeferDisposeCount, Is.EqualTo(1));
    }

    [Test]
    public void CleanControl_WithNoCommands_ReusesCachedUnits()
    {
        var c = AddControl(); DrawsRectangle(c);
        RenderFrame();
        var unit = _factory.Created.Single();

        // Frame 2: NOT invalidated -> Render() is a no-op (no commands) -> cached unit must be reused, not freed.
        RenderFrame();

        Assert.That(unit.DeferDisposeCount, Is.EqualTo(0));
        Assert.That(_factory.Created.Count, Is.EqualTo(1));

        _cache.Render();
        Assert.That(unit.RenderCount, Is.GreaterThan(0));
    }

    // -------- resource lifetime --------

    [Test]
    public void Detach_FromTree_DefersDisposalOfUnits()
    {
        var c = AddControl(); DrawsRectangle(c);
        RenderFrame();
        var unit = _factory.Created.Single();

        _root.Remove(c); // detaches the control from the visual tree

        // Disposal must NOT be triggered by the detach event itself: that event fires during layout/Update,
        // which is the wrong phase for the frame-slot defer queue (BeginDraw would drain it the same frame,
        // disposing a unit still referenced by _renderUnits and still in GPU flight). Disposal happens on
        // the next build, via ReconcileDetachedControls.
        Assert.That(unit.DeferDisposeCount, Is.EqualTo(0));

        RenderFrame(); // build -> reconcile finds the detached control -> defers its units exactly once
        Assert.That(unit.DeferDisposeCount, Is.EqualTo(1));
    }

    [Test]
    public void Hidden_Control_RetainsResources_AndIsNotRecreatedOnShow()
    {
        var c = AddControl(); DrawsRectangle(c);
        RenderFrame();
        var unit = _factory.Created.Single();

        // Hide: skipped from drawing but still attached -> resources kept.
        c.Visibility = Visibility.Collapsed;
        RenderFrame();
        Assert.That(unit.DeferDisposeCount, Is.EqualTo(0));

        // Show again: must reuse the same unit, not allocate a new one.
        c.Visibility = Visibility.Visible;
        RenderFrame();
        Assert.That(_factory.Created.Count, Is.EqualTo(1));
        Assert.That(unit.DeferDisposeCount, Is.EqualTo(0));
    }

    [Test]
    public void PayloadTypeMismatch_ReplacesUnit_DeferringTheOld()
    {
        var c = AddControl();
        c.RenderAction = s => s.DrawRectangle(Brushes.Red, Box);
        RenderFrame();
        var rectUnit = _factory.Created.Single();
        Assert.That(rectUnit.PayloadType.Name, Is.EqualTo("RectanglePayload"));

        // Same slot, different payload type -> Match fails -> old deferred, new created.
        c.RenderAction = s => s.DrawEllipse(Box, Brushes.Red, 0, 360, default(EllipseType));
        c.Invalidate();
        RenderFrame();

        Assert.That(rectUnit.DeferDisposeCount, Is.EqualTo(1));
        Assert.That(_factory.Created.Count, Is.EqualTo(2));
        Assert.That(_factory.Created[1].PayloadType.Name, Is.EqualTo("EllipsePayload"));
    }

    // -------- update dispatch (matching unit is updated, not recreated) --------

    // Repro for "nothing renders inside panels" on a gallery tab. A ContentPresenter attached to the root renders a
    // data item through a DataTemplate whose root is a ContentControl (a <View>) holding a StackPanel > Grid > Border -
    // exactly the tab-body shape, built DYNAMICALLY into an already-attached tree (unlike the old directly-nested views).
    // The deep Border must (a) attach to the tree and (b) produce a render unit that ISN'T freed as detached.
    [Test]
    public void TabBodyStyle_DynamicPanelChild_AttachesAndProducesUnit()
    {
        Border border = null;
        var template = new DataTemplate(() =>
        {
            border = new Border { Width = 56, Height = 34, Background = Brushes.Red };
            var grid = new Grid { Width = 300, Height = 60 };
            ((IContainer)grid).AddOrSetChildComponent(border);
            var stack = new StackPanel { Orientation = Orientation.Vertical };
            ((IContainer)stack).AddOrSetChildComponent(grid);
            var view = new ContentControl { Content = stack };
            return new TemplateResult { RootComponent = view };
        });

        var cp = new ContentPresenter { Content = new object(), ContentTemplate = template };
        _root.Add(cp);
        // Measure/arrange runs the ContentPresenter's BuildCurrent -> builds + attaches the template content.
        cp.Measure(new Size(400, 400));
        cp.Arrange(new Rect(0, 0, 400, 400));

        Assert.That(border, Is.Not.Null, "the template built its Border");
        Assert.That(border.IsAttachedToVisualTree, Is.True, "the panel's Border attached to the visual tree");
        // A unit is created even for a ZERO-size rect, so the real question is whether the dynamically-built content got
        // ARRANGED (RenderSize is set by Arrange). If arrange doesn't reach content built during the measure pass, the
        // Border stays 0x0 and draws nothing - while TextBlocks still show via their own raster path.
        Assert.That(border.RenderSize, Is.EqualTo(new Size(56, 34)), "the Border was ARRANGED to its own size (not left 0x0)");

        RenderFrame();
        Assert.That(_factory.Created.Select(u => u.Component), Does.Contain(border),
            "the Border produced a render unit (so it actually draws) and isn't skipped/freed as detached");
    }

    // Regression: an ARRANGE that changes the size must re-run OnRender. A control first laid out at a stale size (e.g.
    // 0x0 while still unarranged - content built during a measure pass, like a tab body) cached that geometry as valid;
    // MeasureCore invalidates render geometry but ArrangeCore did NOT, so a later size-only arrange left OnRender drawing
    // the old 0x0 rect forever (invisible panels). Re-arrange to a new size WITHOUT re-measuring (measure would mask it).
    [Test]
    public void ArrangeToNewSize_ReRunsOnRender_NotStaleGeometry()
    {
        var b = new Border
        {
            Background = Brushes.Red,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        _root.Add(b);

        b.Measure(new Size(100, 100));
        b.Arrange(new Rect(0, 0, 20, 20));
        RenderFrame();
        var unit = _factory.Created.Single();          // the Border's fill unit
        var updatesBefore = unit.UpdateWithCommandCount;

        b.Arrange(new Rect(0, 0, 60, 40));             // size-only re-arrange (no re-measure)
        RenderFrame();

        Assert.That(unit.UpdateWithCommandCount, Is.GreaterThan(updatesBefore),
            "an arrange-only size change must re-run OnRender (re-record the fill at the new size), not keep stale geometry");
    }

    [Test]
    public void SamePayloadType_UpdatesExistingUnit_NotRecreated()
    {
        var c = AddControl();
        c.RenderAction = s => s.DrawRectangle(Brushes.Red, Box);
        RenderFrame();
        var unit = _factory.Created.Single();

        // Re-render with a different brush (same payload type) -> the cache must reuse + UpdateWithDrawCommand.
        c.RenderAction = s => s.DrawRectangle(Brushes.Blue, Box);
        c.Invalidate();
        RenderFrame();

        Assert.That(_factory.Created.Count, Is.EqualTo(1));
        Assert.That(unit.UpdateWithCommandCount, Is.GreaterThan(0));
        Assert.That(unit.DeferDisposeCount, Is.EqualTo(0));
    }
}
