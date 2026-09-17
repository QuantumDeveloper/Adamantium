using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>The COMMENT FRAME: a titled box behind a group of nodes. A graph of forty nodes is unreadable as forty
/// nodes and readable as five labelled areas, and this is the only documentation a graph ever gets.</summary>
public class CanvasFrameTests
{
    private static CanvasFrameItem Frame(Rect world = default) =>
        new(world.Width > 0 ? world : new Rect(0, 0, 400, 300), "Lighting", Brushes.White);

    // UNDER the nodes. Over them it would be a sheet of colour across the thing it is about.
    [Test]
    public void AFrameIsDrawnBehindTheNodes()
    {
        ICanvasItem frame = Frame();

        Assert.That(frame.Band, Is.EqualTo(CanvasBand.Under));
        Assert.That(frame.Mode, Is.EqualTo(CanvasMode.Nodes), "a frame is a way of reading a GRAPH");
    }

    // ONLY THE TITLE STRIP and the outline answer the pointer. A frame is mostly empty by design, and one that could be
    // picked up by its middle would be a sheet nobody could click through to what is inside it.
    [Test]
    public void AFrameIsGrabbedByItsTitleAndItsEdgeAndNotByItsMiddle()
    {
        var frame = Frame();
        frame.TitleHeight = 26;

        Assert.Multiple(() =>
        {
            Assert.That(frame.HitTest(new Vector2(200, 10), 2), Is.True, "the title strip does not take a press");
            Assert.That(frame.HitTest(new Vector2(1, 150), 2), Is.True, "the left edge does not take a press");
            Assert.That(frame.HitTest(new Vector2(200, 150), 2), Is.False,
                "the middle of a frame swallows presses meant for what is inside it");
        });
    }

    // DRAGGED, it takes along what was standing on it - which is the whole reason to draw one round a group of nodes.
    [Test]
    public void AFrameCarriesWhatIsStandingOnIt()
    {
        var frame = Frame();
        var inside = new ShapeItem(CanvasShape.Rectangle, new Rect(40, 60, 80, 40), Brushes.White, 2);
        var outside = new ShapeItem(CanvasShape.Rectangle, new Rect(900, 60, 80, 40), Brushes.White, 2);

        frame.Catch(new ICanvasItem[] { frame, inside, outside });
        frame.Move(new Vector2(100, 50));

        Assert.Multiple(() =>
        {
            Assert.That(inside.World.X, Is.EqualTo(140).Within(0.01), "what was inside stayed behind");
            Assert.That(outside.World.X, Is.EqualTo(900).Within(0.01), "something outside came along");
            Assert.That(frame.World.X, Is.EqualTo(100).Within(0.01));
        });
    }

    // ...and it is asked ONCE, when the drag begins. Asked again on every move, a frame would collect whatever it was
    // pushed over on the way, and a node dragged out of one would be dragged back in by the frame catching up.
    [Test]
    public void AFrameDoesNotCollectWhatItIsPushedOver()
    {
        var frame = Frame();
        var elsewhere = new ShapeItem(CanvasShape.Rectangle, new Rect(500, 60, 80, 40), Brushes.White, 2);

        frame.Catch(new ICanvasItem[] { frame, elsewhere });

        // The frame is dragged over it - and must not pick it up on the way.
        frame.Move(new Vector2(200, 0));
        frame.Move(new Vector2(50, 0));

        Assert.That(elsewhere.World.X, Is.EqualTo(500).Within(0.01), "the frame swept something up as it passed");
    }

    // ...and lets go afterwards, so a frame standing still owns nothing.
    [Test]
    public void AFrameLetsGoWhenTheDragEnds()
    {
        var frame = Frame();
        var inside = new ShapeItem(CanvasShape.Rectangle, new Rect(40, 60, 80, 40), Brushes.White, 2);

        frame.Catch(new ICanvasItem[] { inside });
        frame.Release();
        frame.Move(new Vector2(100, 0));

        Assert.That(inside.World.X, Is.EqualTo(40).Within(0.01), "the frame is still holding on after the drag");
    }

    // A WIRE is never carried: it is held by the two sockets at its ends and follows them by itself. Moved as well, it
    // would travel twice.
    [Test]
    public void AFrameNeverCarriesAWire()
    {
        var frame = Frame();
        var left = new ElementItem(new CanvasNode(), new Rect(20, 40, 120, 80));
        var right = new ElementItem(new CanvasNode(), new Rect(200, 40, 120, 80));
        var wire = new ConnectionItem(left, ((CanvasNode)left.Element).OutputPins[0],
            right, ((CanvasNode)right.Element).InputPins[0]);

        frame.Catch(new ICanvasItem[] { left, right, wire });
        frame.Move(new Vector2(10, 0));

        Assert.That(left.World.X, Is.EqualTo(30).Within(0.01), "the nodes did not come along");
    }

    // RESIZING a frame leaves what is inside it WHERE IT IS: a frame is a note about a graph, not a layout, and
    // stretching the note must not move the work.
    [Test]
    public void ResizingAFrameDoesNotMoveWhatIsInIt()
    {
        var frame = Frame();
        var inside = new ShapeItem(CanvasShape.Rectangle, new Rect(40, 60, 80, 40), Brushes.White, 2);

        frame.Catch(new ICanvasItem[] { inside });
        frame.Resize(new Rect(0, 0, 900, 700));

        Assert.That(inside.World, Is.EqualTo(new Rect(40, 60, 80, 40)));
        Assert.That(frame.World.Width, Is.EqualTo(900).Within(0.01));
    }

    // A CONTROL CANNOT BE SQUEEZED TO NOTHING. What is inside it has a size of its own, and pulled below that it does
    // not shrink - it spills: the grips end up inside the node they are meant to hold and the title hangs out of the
    // side. So the item says what it needs and the grip stops there.
    [Test]
    public void AGripStopsWhereTheControlStopsFitting()
    {
        var item = new ElementItem(new Button(), new Rect(0, 0, 200, 80)) { Smallest = new Size(120, 40) };

        item.Resize(new Rect(0, 0, 1, 1));

        Assert.Multiple(() =>
        {
            Assert.That(item.World.Width, Is.EqualTo(120).Within(0.01));
            Assert.That(item.World.Height, Is.EqualTo(40).Within(0.01));
        });
    }

    // ...and the FRAME stops there too, so the grips are never drawn inside what they are holding. The frame scales
    // everything by one factor, so it may go no further than the neediest item can bear.
    [Test]
    public void TheFrameItselfStopsAtTheNeediestItem()
    {
        var canvas = new InfiniteCanvas { Scene = new CanvasScene() };
        canvas.Measure(new Size(800, 600), force: true);
        canvas.Arrange(new Rect(0, 0, 800, 600));

        var roomy = new ElementItem(new Button(), new Rect(0, 0, 200, 80));
        var tight = new ElementItem(new Button(), new Rect(200, 0, 200, 80)) { Smallest = new Size(150, 40) };

        canvas.Scene.Add(roomy);
        canvas.Scene.Add(tight);
        canvas.SelectMany(new ICanvasItem[] { roomy, tight }, false);

        var gesture = new CanvasFrameGesture();
        gesture.Begin(canvas, CanvasHandle.Right, new Vector2(400, 40));

        // Pulled hard enough to collapse it - the frame is 400 wide and the drag takes 10000 off it.
        gesture.MoveTo(canvas, new Vector2(-9600, 40));
        gesture.End();

        Assert.Multiple(() =>
        {
            Assert.That(tight.World.Width, Is.GreaterThanOrEqualTo(150 - 0.01), "the item that could not shrink");
            Assert.That(canvas.SelectionBounds?.Width ?? 0, Is.GreaterThanOrEqualTo(300 - 0.01),
                "the frame went past where its contents stopped, so the grips ended up inside the node");
        });
    }

    [Test]
    public void AFrameCopiesItselfTitleAndAll()
    {
        var frame = Frame();
        frame.TitleHeight = 31;

        var copy = ((ICanvasItem)frame).Copy() as CanvasFrameItem;

        Assert.Multiple(() =>
        {
            Assert.That(copy, Is.Not.Null);
            Assert.That(copy.Title, Is.EqualTo("Lighting"));
            Assert.That(copy.World, Is.EqualTo(frame.World));
            Assert.That(copy.TitleHeight, Is.EqualTo(31));
        });
    }
}
