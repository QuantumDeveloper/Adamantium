using System.Collections.Generic;
using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Rendering;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>
/// CLONES (docs/RENDER_CACHE_REDESIGN.md §4o): a prototype's SUBTREE is drawn once per matrix instead of once at its own
/// place, so N copies of a visual cost one real element. Born из the measured disaster it removes: a virtualizing panel
/// built a full template instance per empty slot - 3469 template builds in a 0.25 s window against 147 realized
/// containers, every property write of every build marking layout dirty.
/// <para>The dangerous failure is not "clones missing" - that is visible at once - but a clone run that swallows the
/// groups AFTER the prototype, drawing unrelated content N times. So these are written as NEGATIVE guards: they state
/// what must NOT be multiplied, and one of them pins the TOTAL across the whole tree, which fails on any over-cloning
/// including the kinds nobody thought to name.</para>
/// </summary>
[TestFixture]
public class RenderCloneTests
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

    private void RenderFrame()
    {
        _cache.BuildFromVisualTree(_root);
        RenderDirty.Clear();
        _cache.Render();
    }

    private static void Draws(TestControl c) => c.RenderAction = s => s.DrawRectangle(Brushes.Red, Box);

    private TestControl AddControl()
    {
        var c = new TestControl();
        _root.Add(c);
        Draws(c);
        return c;
    }

    private static IReadOnlyList<Matrix4x4F> Clones(int count) =>
        Enumerable.Range(0, count).Select(i => Matrix4x4F.Translation(i * 20, 0, 0)).ToArray();

    private int RenderCountOf(IUIComponent component) =>
        _factory.Created.Where(u => ReferenceEquals(u.Component, component)).Sum(u => u.RenderCount);

    [Test]
    public void WithoutClones_TheElementDrawsOnce()
    {
        var c = AddControl();

        RenderFrame();

        Assert.That(RenderCountOf(c), Is.EqualTo(1), "no clones declared - the ordinary single draw must be untouched");
    }

    [Test]
    public void APrototypeDrawsOncePerMatrix()
    {
        var prototype = AddControl();
        prototype.RenderClones = Clones(3);

        RenderFrame();

        Assert.That(RenderCountOf(prototype), Is.EqualTo(3));
        Assert.That(_factory.Created.Count(u => ReferenceEquals(u.Component, prototype)), Is.EqualTo(1),
            "three copies must come from ONE unit - building three is the cost this exists to avoid");
    }

    [Test]
    public void TheWHOLE_SubtreeIsCloned_NotJustItsRoot()
    {
        var prototype = AddControl();
        var child = new TestControl();
        prototype.Add(child);
        Draws(child);
        prototype.RenderClones = Clones(3);

        RenderFrame();

        Assert.That(RenderCountOf(child), Is.EqualTo(3),
            "a clone that copied only the prototype's own units would work for a one-Border skeleton and quietly fail " +
            "for any richer template - which is exactly the fragile design this replaces");
    }

    // THE guard: get the end of the subtree wrong and everything painted after the prototype is drawn N times.
    [Test]
    public void WhatComesAFTER_ThePrototypeIsDrawnOnce()
    {
        var before = AddControl();
        var prototype = AddControl();
        prototype.Add(Nested(out var child));
        var after = AddControl();
        prototype.RenderClones = Clones(4);

        RenderFrame();

        Assert.That(RenderCountOf(before), Is.EqualTo(1), "a sibling BEFORE the prototype is not part of the run");
        Assert.That(RenderCountOf(child), Is.EqualTo(4));
        Assert.That(RenderCountOf(after), Is.EqualTo(1),
            "the run must stop at the end of the prototype's subtree - swallowing the rest of the paint order draws " +
            "unrelated content N times");
    }

    // The strongest of the negative guards: pin the TOTAL across the whole tree. Any over-cloning anywhere - the run
    // swallowing a sibling, the root, the next frame's groups - breaks this sum, including failure modes nobody thought
    // to name. A per-element assertion only catches the case it names.
    [Test]
    public void NothingOUTSIDE_ThePrototypeIsMultiplied()
    {
        var before = AddControl();
        var prototype = AddControl();
        var child = new TestControl();
        prototype.Add(child);
        Draws(child);
        var after = AddControl();
        prototype.RenderClones = Clones(5);

        RenderFrame();

        var total = _factory.Created.Sum(u => u.RenderCount);
        Assert.That(total, Is.EqualTo(1 + 5 + 5 + 1),
            $"expected before(1) + prototype(5) + child(5) + after(1); got before={RenderCountOf(before)} " +
            $"prototype={RenderCountOf(prototype)} child={RenderCountOf(child)} after={RenderCountOf(after)}");
    }

    // A prototype that draws NOTHING itself (a bare layout container whose children carry the visual) has no units of
    // its own, so anything that identified the clone host through its first unit would not see it at all.
    [Test]
    public void APrototypeWithNoUnitsOfItsOwn_StillClonesItsChildren()
    {
        var prototype = new TestControl();   // no RenderAction: emits nothing
        _root.Add(prototype);
        var child = new TestControl();
        prototype.Add(child);
        Draws(child);
        prototype.RenderClones = Clones(3);

        RenderFrame();

        Assert.That(RenderCountOf(child), Is.EqualTo(3));
    }

    // A clone run turns recording off while it draws. If it forgot to turn it back on, everything painted after it would
    // stop being recorded - and the next frame's partial/clean fast path would silently lose those units.
    [Test]
    public void AfterACloneRun_TheFollOWINGContentStillDrawsOnTheNextFrame()
    {
        var prototype = AddControl();
        prototype.RenderClones = Clones(2);
        var after = AddControl();

        RenderFrame();
        RenderFrame();

        Assert.That(RenderCountOf(after), Is.EqualTo(2), "one draw per frame, two frames - not zero, not four");
        Assert.That(RenderCountOf(prototype), Is.EqualTo(4));
    }

    [Test]
    public void ClonesSurviveASecondFrame()
    {
        var prototype = AddControl();
        prototype.RenderClones = Clones(2);
        RenderFrame();

        Assert.DoesNotThrow(RenderFrame);
        Assert.That(RenderCountOf(prototype), Is.EqualTo(4), "two frames of two clones");
    }

    [Test]
    public void DroppingTheClones_ReturnsToASingleDraw()
    {
        var prototype = AddControl();
        prototype.RenderClones = Clones(3);
        RenderFrame();

        prototype.RenderClones = null;
        prototype.Invalidate();
        RenderFrame();

        Assert.That(RenderCountOf(prototype), Is.EqualTo(4), "3 cloned + 1 ordinary");
    }

    [Test]
    public void AnEmptyCloneList_IsTheOrdinaryDraw()
    {
        var prototype = AddControl();
        prototype.RenderClones = new Matrix4x4F[0];

        RenderFrame();

        Assert.That(RenderCountOf(prototype), Is.EqualTo(1), "zero matrices must not mean zero draws - it means 'no clones'");
    }

    private TestControl Nested(out TestControl child)
    {
        child = new TestControl();
        Draws(child);
        return child;
    }
}
