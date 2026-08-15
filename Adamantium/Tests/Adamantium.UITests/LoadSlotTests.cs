using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// What <c>x:Load</c> claims, checked by COUNTING rather than by looking: while the condition is false the element is
/// not constructed at all, turning it on constructs it exactly once, and turning it off and on again brings the SAME
/// one back - it is parked, not rebuilt. Nothing in a tree reports construction on its own, which is why the element
/// here counts its own.
/// </summary>
[TestFixture]
public class LoadSlotTests
{
    private int _builds;

    [SetUp]
    public void Reset()
    {
        _builds = 0;
    }

    [Test]
    public void WhileTheConditionIsFalse_NothingIsConstructed()
    {
        var (panel, _) = SlotIn(index: 1);

        Assert.That(_builds, Is.Zero, "an element held back is ABSENT, not hidden");
        Assert.That(panel.Children.Count, Is.EqualTo(2), "and the container must not be holding a placeholder");
    }

    [Test]
    public void TurningItOn_ConstructsItOnce_AtTheIndexItWasWrittenAt()
    {
        var (panel, slot) = SlotIn(index: 1);

        slot.Condition = true;

        Assert.That(_builds, Is.EqualTo(1));
        Assert.That(panel.Children.Count, Is.EqualTo(3));
        Assert.That(panel.Children[1], Is.SameAs(slot.Element), "it goes back where it was written, not on the end");
    }

    [Test]
    public void TurningItOffAndOnAgain_BringsTheSameOneBack_WithoutRebuilding()
    {
        var (panel, slot) = SlotIn(index: 1);

        slot.Condition = true;
        var first = slot.Element;

        slot.Condition = false;
        Assert.That(panel.Children.Count, Is.EqualTo(2), "off means out of the tree");
        Assert.That(_builds, Is.EqualTo(1), "and going out must not destroy it");

        slot.Condition = true;
        Assert.That(_builds, Is.EqualTo(1), "coming back must not construct a second one - it was parked, not thrown away");
        Assert.That(slot.Element, Is.SameAs(first));
        Assert.That(panel.Children[1], Is.SameAs(first), "and it comes back at its own place");
    }

    [Test]
    public void WhileOutOfTheTree_TheSubtreeIsParked_SoTheRendererKeepsWhatItBuilt()
    {
        var (_, slot) = SlotIn(index: 1);
        slot.Condition = true;
        var child = (UIComponent)slot.Element;

        slot.Condition = false;

        Assert.That(child.IsParked, Is.True,
            "an unmarked detach reads as 'thrown away' and its cached units are freed - which is the cost this exists to avoid");
    }

    // A parked mark is what makes the renderer keep a subtree's units. When the slot goes away with its view, nothing
    // can ever show what it was holding - and the mark would keep those units, and the subtree behind them, for good.
    [Test]
    public void WhenTheSlotLeavesItsView_WhatItHeldStopsBeingParked()
    {
        var (panel, slot) = SlotIn(index: 1);
        slot.Condition = true;
        var child = (UIComponent)slot.Element;
        slot.Condition = false;

        panel.RemoveLogicalChild(slot);

        Assert.That(child.IsParked, Is.False,
            "an element nobody can ask for again must not go on claiming its render units");
    }

    [Test]
    public void AskingForItByName_BuildsIt()
    {
        var (panel, slot) = SlotIn(index: 1);

        var element = slot.Element;

        Assert.That(element, Is.Not.Null, "the name must never answer null - asking IS loading");
        Assert.That(_builds, Is.EqualTo(1));
        Assert.That(panel.Children[1], Is.SameAs(element));
    }

    // A panel with two children and a slot for a third, held at `index` - the shape the generator emits.
    private (Panel panel, LoadSlot slot) SlotIn(int index)
    {
        var panel = new StackPanel();
        panel.Children.Add(new Border());
        panel.Children.Add(new Border());

        var slot = new LoadSlot(Build, panel, index);
        panel.AddLogicalChild(slot);
        return (panel, slot);
    }

    private IUIComponent Build()
    {
        _builds++;
        return new Border();
    }
}
