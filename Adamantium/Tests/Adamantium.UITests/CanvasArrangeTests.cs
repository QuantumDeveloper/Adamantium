using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>Putting the camera where the work is, and putting the work in order - the two things a plane with no edges
/// has to be able to do, because it has no corner to scroll back to and no grid to snap a row against.</summary>
public class CanvasArrangeTests
{
    private static (InfiniteCanvas Canvas, CanvasScene Scene) Stage()
    {
        var canvas = new InfiniteCanvas();
        canvas.Measure(new Size(800, 600), force: true);
        canvas.Arrange(new Rect(0, 0, 800, 600));

        var scene = new CanvasScene();
        canvas.Scene = scene;

        return (canvas, scene);
    }

    private static ShapeItem Box(double x, double y, double w = 60, double h = 40) =>
        new(CanvasShape.Rectangle, new Rect(x, y, w, h), Brushes.White, 2);

    // WHERE IS MY WORK. The plane has no edges, so this is a question it has to be able to answer - and on a big graph
    // it is asked constantly.
    [Test]
    public void FittingBringsWhatIsSelectedIntoView()
    {
        var (canvas, scene) = Stage();
        var far = Box(9000, 7000, 400, 300);
        scene.Add(far);
        canvas.Select(far, false);

        Assert.That(canvas.VisibleWorld.Contains(new Vector2(9200, 7150)), Is.False, "it was in view to begin with");

        Assert.That(canvas.FitSelection(), Is.True);

        var seen = canvas.VisibleWorld;

        Assert.Multiple(() =>
        {
            Assert.That(seen.Contains(new Vector2(far.World.X, far.World.Y)), Is.True, "its corner is off screen");
            Assert.That(seen.Contains(new Vector2(far.World.X + far.World.Width, far.World.Y + far.World.Height)),
                Is.True, "its far corner is off screen");
        });
    }

    // ...with ROOM round it: a drawing pressed against the edges of the screen reads as one that has been cut off.
    [Test]
    public void AFitLeavesRoomRoundWhatItShows()
    {
        var (canvas, scene) = Stage();
        var box = Box(0, 0, 400, 300);
        scene.Add(box);
        canvas.Select(box, false);
        canvas.FitSelection();

        var seen = canvas.VisibleWorld;

        Assert.That(seen.Width, Is.GreaterThan(box.World.Width * 1.05), "it is touching the edges");
    }

    // A single POINT is centred at the zoom already in hand, not zoomed to infinity.
    [Test]
    public void FittingSomethingWithNoSizeDoesNotZoomForEver()
    {
        var (canvas, scene) = Stage();
        var was = canvas.Scale;

        canvas.Fit(new Rect(500, 400, 0, 0));

        Assert.That(canvas.Scale, Is.EqualTo(was).Within(0.001));
        Assert.That(canvas.VisibleWorld.Contains(new Vector2(500, 400)), Is.True);
    }

    // FIT ALL is about what is on the plane OF THE CURRENT MODE: what the other mode holds is not being looked at.
    [Test]
    public void FittingEverythingIsAboutTheModeInHand()
    {
        var (canvas, scene) = Stage();
        scene.Add(Box(0, 0));
        scene.Add(new ElementItem(new CanvasNode(), new Rect(40000, 40000, 190, 110)));

        Assert.That(canvas.FitAll(), Is.True);

        Assert.That(canvas.VisibleWorld.Contains(new Vector2(40000, 40000)), Is.False,
            "the camera flew off to a node the drawing mode does not even show");
    }

    [Test]
    public void FittingNothingIsNotAnAnswer()
    {
        var (canvas, _) = Stage();

        Assert.That(canvas.FitSelection(), Is.False);
        Assert.That(canvas.FitAll(), Is.False);
    }

    // Something in the selection that has NO PLACE OF ITS OWN - a wire, which is wherever the two sockets it joins are.
    // A band takes those along with the nodes, so everything that arranges the plane meets them.
    private sealed class Attached : ICanvasItem
    {
        public Rect Bounds { get; init; }
        public int Order { get; set; }
        public bool IsPlaced => false;
        public CanvasHandles Handles => CanvasHandles.None;
        public bool HitTest(Vector2 world, double tolerance) => false;
        public void Render(Adamantium.UI.Core.Graphics.IDrawingSession session, InfiniteCanvas canvas) { }

        public int Moves { get; private set; }

        public void Move(Vector2 worldDelta) => Moves++;
        public void Resize(Rect world) { }
    }

    // ...and NOTHING THAT ARRANGES THE PLANE MAY TOUCH IT, nor make room for it. Counting the room it covers as taken
    // pushed every node past it, and each node moved dragged the wire's box along - which is how "line these up in a
    // row" spread a graph of seventeen things across a hundred thousand units.
    [Test]
    public void SomethingWithNoPlaceOfItsOwnIsNeitherMovedNorMadeRoomFor()
    {
        var (canvas, scene) = Stage();

        var a = Box(0, 0);
        var b = Box(100, 200);
        var wire = new Attached { Bounds = new Rect(-5000, 0, 10000, 300) };

        scene.Add(a);
        scene.Add(b);
        scene.Add(wire);

        canvas.SelectMany(new ICanvasItem[] { a, b, wire }, false);

        Assert.That(canvas.Align(CanvasAlignment.Top), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(wire.Moves, Is.Zero, "a wire was lined up as though it were somewhere of its own");
            Assert.That(a.World.Y, Is.EqualTo(0).Within(0.01));
            Assert.That(b.World.Y, Is.EqualTo(0).Within(0.01), "the row was not made");

            // The two boxes are 60 wide and were 100 apart, so neither has to move across at all - unless the wire's
            // ten thousand units were counted as occupied.
            Assert.That(a.World.X, Is.EqualTo(0).Within(0.01), "making room for the wire pushed the row apart");
            Assert.That(b.World.X, Is.EqualTo(100).Within(0.01), "making room for the wire pushed the row apart");
        });
    }

    // ...and the FRAME round the selection is round those same things. A wire's box is its whole bend from one node to
    // the other, so a frame that counted it stood far outside everything the person can see they picked.
    [Test]
    public void TheFrameIsRoundWhatHasAPlaceOfItsOwn()
    {
        var (canvas, scene) = Stage();

        var a = Box(0, 0);
        var b = Box(100, 0);
        var wire = new Attached { Bounds = new Rect(-5000, -300, 10000, 900) };

        scene.Add(a);
        scene.Add(b);
        scene.Add(wire);

        canvas.SelectMany(new ICanvasItem[] { a, b, wire }, false);

        Assert.That(canvas.SelectionBounds, Is.Not.Null);

        var frame = canvas.SelectionBounds.Value;

        Assert.Multiple(() =>
        {
            Assert.That(frame.X, Is.EqualTo(0).Within(0.01), "the frame reaches out to where a wire bends");
            Assert.That(frame.Width, Is.EqualTo(160).Within(0.01));
            Assert.That(frame.Y, Is.EqualTo(0).Within(0.01));
            Assert.That(frame.Height, Is.EqualTo(40).Within(0.01));
        });
    }

    // ALIGNING is against the box round the WHOLE selection: "align left" has one obvious meaning, which is that
    // nothing ends up further left than it was.
    [Test]
    public void AligningLeftPutsEveryLeftEdgeOnTheLeftmost()
    {
        var (canvas, scene) = Stage();
        var a = Box(10, 0);
        var b = Box(80, 100);
        var c = Box(200, 200);
        scene.Add(a);
        scene.Add(b);
        scene.Add(c);
        canvas.SelectMany(new ICanvasItem[] { a, b, c }, false);

        Assert.That(canvas.Align(CanvasAlignment.Left), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(a.World.X, Is.EqualTo(10).Within(0.01));
            Assert.That(b.World.X, Is.EqualTo(10).Within(0.01));
            Assert.That(c.World.X, Is.EqualTo(10).Within(0.01));
            Assert.That(b.World.Y, Is.EqualTo(100).Within(0.01), "aligning left moved it up and down as well");
        });
    }

    [Test]
    public void AligningToTheMiddleUsesTheMiddleOfTheWholeSelection()
    {
        var (canvas, scene) = Stage();
        var a = Box(0, 0, 100, 40);
        var b = Box(200, 100, 40, 40);
        scene.Add(a);
        scene.Add(b);
        canvas.SelectMany(new ICanvasItem[] { a, b }, false);

        canvas.Align(CanvasAlignment.HorizontalCenter);

        var middle = 120.0;

        Assert.That(a.World.X + a.World.Width / 2, Is.EqualTo(middle).Within(0.01));
        Assert.That(b.World.X + b.World.Width / 2, Is.EqualTo(middle).Within(0.01));
    }

    // A ROW lined up on its left edges is a COLUMN, not a pile. Every item would land at the same place, and a heap of
    // nodes says less than the row did - so the line opens out down the page instead, in the order it had across it.
    [Test]
    public void AligningARowLeftMakesAColumnAndNotAPile()
    {
        var (canvas, scene) = Stage();
        var a = Box(0, 50);
        var b = Box(100, 50);
        var c = Box(200, 50);
        scene.Add(a);
        scene.Add(b);
        scene.Add(c);
        canvas.SelectMany(new ICanvasItem[] { a, b, c }, false);

        canvas.Align(CanvasAlignment.Left);

        Assert.Multiple(() =>
        {
            Assert.That(a.World.X, Is.EqualTo(0).Within(0.01));
            Assert.That(b.World.X, Is.EqualTo(0).Within(0.01));
            Assert.That(c.World.X, Is.EqualTo(0).Within(0.01));

            Assert.That(b.World.Y, Is.GreaterThanOrEqualTo(a.World.Bottom), "b is standing on top of a");
            Assert.That(c.World.Y, Is.GreaterThanOrEqualTo(b.World.Bottom), "c is standing on top of b");

            // The order across the row is the order down the column: what was leftmost is topmost.
            Assert.That(a.World.Y, Is.LessThan(b.World.Y));
            Assert.That(b.World.Y, Is.LessThan(c.World.Y));
        });
    }

    // ...and a COLUMN lined up on its top edges is a row, by the same rule.
    [Test]
    public void AligningAColumnTopMakesARowAndNotAPile()
    {
        var (canvas, scene) = Stage();
        var a = Box(50, 0);
        var b = Box(50, 100);
        scene.Add(a);
        scene.Add(b);
        canvas.SelectMany(new ICanvasItem[] { a, b }, false);

        canvas.Align(CanvasAlignment.Top);

        Assert.Multiple(() =>
        {
            Assert.That(a.World.Y, Is.EqualTo(0).Within(0.01));
            Assert.That(b.World.Y, Is.EqualTo(0).Within(0.01));
            Assert.That(b.World.X, Is.GreaterThanOrEqualTo(a.World.Right), "the two are on top of each other");
        });
    }

    // What was ALREADY clear of its neighbour does not move: a layout somebody has arranged is not rearranged by being
    // lined up, only the overlaps are opened.
    [Test]
    public void AligningLeavesWhatIsAlreadyApartWhereItIs()
    {
        var (canvas, scene) = Stage();
        var a = Box(10, 0);
        var b = Box(80, 300);
        scene.Add(a);
        scene.Add(b);
        canvas.SelectMany(new ICanvasItem[] { a, b }, false);

        canvas.Align(CanvasAlignment.Left);

        Assert.Multiple(() =>
        {
            Assert.That(a.World.Y, Is.EqualTo(0).Within(0.01));
            Assert.That(b.World.Y, Is.EqualTo(300).Within(0.01), "it was nowhere near the other one and was moved anyway");
        });
    }

    // One thing is nothing to line up.
    [Test]
    public void AligningOneThingDoesNothing()
    {
        var (canvas, scene) = Stage();
        var a = Box(10, 20);
        scene.Add(a);
        canvas.Select(a, false);

        Assert.That(canvas.Align(CanvasAlignment.Left), Is.False);
        Assert.That(a.World.X, Is.EqualTo(10).Within(0.01));
    }

    // SPREADING puts equal GAPS between things - which is what the eye reads as even spacing when they are different
    // sizes - and leaves the two on the outside where the person put them.
    [Test]
    public void SpreadingPutsEqualGapsBetweenThem()
    {
        var (canvas, scene) = Stage();
        var a = Box(0, 0, 100, 40);
        var b = Box(120, 0, 20, 40);
        var c = Box(400, 0, 60, 40);
        scene.Add(a);
        scene.Add(b);
        scene.Add(c);
        canvas.SelectMany(new ICanvasItem[] { a, b, c }, false);

        Assert.That(canvas.Spread(CanvasSpread.Horizontal), Is.True);

        var first = b.World.X - (a.World.X + a.World.Width);
        var second = c.World.X - (b.World.X + b.World.Width);

        Assert.Multiple(() =>
        {
            Assert.That(a.World.X, Is.EqualTo(0).Within(0.01), "the outermost one moved");
            Assert.That(c.World.X, Is.EqualTo(400).Within(0.01), "the outermost one moved");
            Assert.That(first, Is.EqualTo(second).Within(0.01), "the gaps are not equal");
        });
    }

    // Two things are already evenly spread - there is nothing between them to move.
    [Test]
    public void SpreadingTwoThingsDoesNothing()
    {
        var (canvas, scene) = Stage();
        var a = Box(0, 0);
        var b = Box(400, 0);
        scene.Add(a);
        scene.Add(b);
        canvas.SelectMany(new ICanvasItem[] { a, b }, false);

        Assert.That(canvas.Spread(CanvasSpread.Horizontal), Is.False);
    }
}
