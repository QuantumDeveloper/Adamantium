using System.Linq;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry.Shapes;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
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
