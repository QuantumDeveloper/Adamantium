using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>Groups: several things treated as one. A group is an item like any other, so what is asserted here is that
/// it behaves like one - and that making it does not disturb the drawing.</summary>
public class CanvasGroupTests
{
    private static InfiniteCanvas Sized(double width = 400, double height = 300)
    {
        var canvas = new InfiniteCanvas();
        canvas.Measure(new Size(width, height), force: true);
        canvas.Arrange(new Rect(0, 0, width, height));

        return canvas;
    }

    private static ShapeItem Box(double x, double y, double size = 10) =>
        new(CanvasShape.Rectangle, new Rect(x, y, size, size), Brushes.White, 1);

    // The box round everything in it, asked rather than remembered: a child moved through the inspector moves inside
    // the group too, and a remembered box would be wrong from that moment on.
    [Test]
    public void AGroupCoversEverythingInIt()
    {
        var group = new GroupItem([Box(0, 0), Box(90, 40)]);

        Assert.That(group.Bounds, Is.EqualTo(new Rect(0, 0, 100, 50)));

        group.Children[0].Move(new Vector2(-20, 0));

        Assert.That(group.Bounds, Is.EqualTo(new Rect(-20, 0, 120, 50)));
    }

    // Every child keeps its PLACE in the group. Anything else and stretching a group would scatter what is in it.
    [Test]
    public void ResizingAGroupKeepsEveryChildInItsPlace()
    {
        var left = Box(0, 0);
        var right = Box(90, 0);
        var group = new GroupItem([left, right]);

        group.Resize(new Rect(0, 0, 200, 20));

        Assert.Multiple(() =>
        {
            Assert.That(group.Bounds.Width, Is.EqualTo(200).Within(0.001));
            Assert.That(left.Bounds.X, Is.EqualTo(0).Within(0.001));
            Assert.That(right.Bounds.X, Is.EqualTo(180).Within(0.001), "nine tenths across, before and after");
        });
    }

    // Hit by its CHILDREN and not by its box: a group of two things far apart is mostly empty, and picking it up by the
    // emptiness would put everything under it out of reach.
    [Test]
    public void AGroupIsHitWhereItsChildrenAre()
    {
        var group = new GroupItem([Box(0, 0), Box(90, 40)]);

        Assert.Multiple(() =>
        {
            // ON the first box's outline: these are hollow rectangles, so their own middles belong to nobody either.
            Assert.That(group.HitTest(new Vector2(0, 5), 0.5), Is.True);
            Assert.That(group.HitTest(new Vector2(50, 25), 0.5), Is.False, "the empty middle belongs to nobody");
        });
    }

    // Grouping takes the children OUT of the scene and puts the group where the topmost of them was: a group must not
    // jump to the front merely by being made.
    [Test]
    public void GroupingKeepsThePlaceOfTheTopmostChild()
    {
        var canvas = Sized();
        var scene = new CanvasScene();
        var under = Box(0, 0);
        var lower = Box(20, 0);
        var upper = Box(40, 0);
        var over = Box(60, 0);

        foreach (var item in new[] { under, lower, upper, over }) scene.Add(item);
        canvas.Scene = scene;
        canvas.SelectMany([lower, upper], false);

        var group = canvas.GroupSelection();

        Assert.That(group, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(scene.Items, Has.Count.EqualTo(3));
            Assert.That(scene.Items[0], Is.SameAs(under));
            Assert.That(scene.Items[1], Is.SameAs(group), "where the topmost of the two was");
            Assert.That(scene.Items[2], Is.SameAs(over));
            Assert.That(group.Children, Is.EqualTo(new ICanvasItem[] { lower, upper }), "in paint order");
            Assert.That(canvas.Selection, Is.EqualTo(new ICanvasItem[] { group }));
        });
    }

    // ...and breaking it open puts them back exactly there.
    [Test]
    public void UngroupingPutsTheChildrenBackWhereTheGroupWas()
    {
        var canvas = Sized();
        var scene = new CanvasScene();
        var under = Box(0, 0);
        var lower = Box(20, 0);
        var upper = Box(40, 0);
        var over = Box(60, 0);

        foreach (var item in new[] { under, lower, upper, over }) scene.Add(item);
        canvas.Scene = scene;
        canvas.SelectMany([lower, upper], false);
        canvas.GroupSelection();

        Assert.That(canvas.UngroupSelection(), Is.True);
        Assert.That(scene.Items, Is.EqualTo(new ICanvasItem[] { under, lower, upper, over }));
    }

    // One thing is not a group. Without this, "group" on a single selection would wrap it in a box that changes nothing
    // and has to be undone.
    [Test]
    public void OneThingCannotBeGrouped()
    {
        var canvas = Sized();
        var scene = new CanvasScene();
        var only = Box(0, 0);

        scene.Add(only);
        canvas.Scene = scene;
        canvas.Select(only, false);

        Assert.That(canvas.GroupSelection(), Is.Null);
        Assert.That(scene.Items, Has.Count.EqualTo(1));
    }

    // A child reached by entering a group is NOT in the scene, so the scene cannot remove it - and a delete that
    // quietly did nothing would be the worst possible answer.
    [Test]
    public void DeletingSomethingInsideAGroupTakesItOut()
    {
        var canvas = Sized();
        var scene = new CanvasScene();
        var lower = Box(0, 0);
        var upper = Box(40, 0);

        scene.Add(lower);
        scene.Add(upper);
        canvas.Scene = scene;
        canvas.SelectMany([lower, upper], false);

        var group = canvas.GroupSelection();
        canvas.Select(upper, false);
        canvas.DeleteSelection();

        Assert.That(group.Children, Is.EqualTo(new ICanvasItem[] { lower }));
    }

    // ...and a group with nothing left in it goes too: an empty group is an invisible thing in the paint order that can
    // still be picked up by its own empty box.
    [Test]
    public void AGroupEmptiedOfEverythingGoesAway()
    {
        var canvas = Sized();
        var scene = new CanvasScene();
        var lower = Box(0, 0);
        var upper = Box(40, 0);

        scene.Add(lower);
        scene.Add(upper);
        canvas.Scene = scene;
        canvas.SelectMany([lower, upper], false);
        canvas.GroupSelection();

        canvas.Select(lower, false);
        canvas.DeleteSelection();
        canvas.Select(upper, false);
        canvas.DeleteSelection();

        Assert.That(scene.Items, Is.Empty);
    }
}
