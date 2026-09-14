using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Rendering;
using Adamantium.UI.Rendering.Payloads;
using NUnit.Framework;

namespace Adamantium.UITests;

// The camera IS the canvas: there is no scrollable extent here and nothing to run into, so everything the control does
// and everything a tool on it will ever do goes through world <-> screen. These are about that arithmetic.
public class InfiniteCanvasTests
{
    private static InfiniteCanvas Sized(double width = 400, double height = 300)
    {
        var canvas = new InfiniteCanvas();
        canvas.Measure(new Size(width, height), force: true);
        canvas.Arrange(new Rect(0, 0, width, height));

        return canvas;
    }

    // The world's origin starts in the MIDDLE. Zero offset would put it in the top-left corner, and a plane whose
    // origin sits off in a corner reads as one that has been scrolled away from.
    [Test]
    public void TheOriginStartsInTheMiddle()
    {
        var canvas = Sized();

        Assert.That(canvas.WorldToScreen(Vector2.Zero), Is.EqualTo(new Vector2(200, 150)));
    }

    [Test]
    public void ScreenAndWorldAreEachOthersInverse()
    {
        var canvas = Sized();
        canvas.Scale = 2.5;

        var world = new Vector2(137.5, -42.25);
        var back = canvas.ScreenToWorld(canvas.WorldToScreen(world));

        Assert.Multiple(() =>
        {
            Assert.That(back.X, Is.EqualTo(world.X).Within(1e-9));
            Assert.That(back.Y, Is.EqualTo(world.Y).Within(1e-9));
        });
    }

    // Panning has nothing to run into - that is what "unbounded" costs and buys. A million pixels out is a legal place
    // to be, and what is under the cursor there is still exact.
    [Test]
    public void PanningNeverRunsIntoAnything()
    {
        var canvas = Sized();
        var start = canvas.Offset;

        canvas.PanBy(new Vector2(1_000_000, -2_500_000));

        Assert.Multiple(() =>
        {
            Assert.That(canvas.Offset, Is.EqualTo(start + new Vector2(1_000_000, -2_500_000)));
            Assert.That(canvas.ScreenToWorld(canvas.WorldToScreen(new Vector2(3, 4))).X, Is.EqualTo(3).Within(1e-6));
        });
    }

    // Zooming toward a point means the world under that point does not move. Anything else and the thing being looked
    // at slides out from under the cursor as it is zoomed into.
    [Test]
    public void ZoomingAtAPointKeepsTheWorldUnderItStill()
    {
        var canvas = Sized();
        var at = new Vector2(320, 90);
        var before = canvas.ScreenToWorld(at);

        canvas.SetScaleAt(at, 7.5);
        var after = canvas.ScreenToWorld(at);

        Assert.Multiple(() =>
        {
            Assert.That(canvas.Scale, Is.EqualTo(7.5));
            Assert.That(after.X, Is.EqualTo(before.X).Within(1e-9));
            Assert.That(after.Y, Is.EqualTo(before.Y).Within(1e-9));
        });
    }

    // The limits are practical, not technical - but while they are set, they hold.
    [Test]
    public void TheScaleStaysBetweenItsLimits()
    {
        var canvas = Sized();
        canvas.MinScale = 0.5;
        canvas.MaxScale = 4;

        canvas.Scale = 100;
        Assert.That(canvas.Scale, Is.EqualTo(4));

        canvas.Scale = 0.001;
        Assert.That(canvas.Scale, Is.EqualTo(0.5));
    }

    // A tolerance - what counts as a hit, how close a snap pulls - is a SCREEN distance. At 20x nobody can hit a thin
    // line given in world units, and at a tenth everything is a hit.
    [Test]
    public void AScreenDistanceIsWorthLessWorldTheFurtherItIsZoomedIn()
    {
        var canvas = Sized();

        canvas.Scale = 1;
        Assert.That(canvas.ScreenToWorldLength(8), Is.EqualTo(8));

        canvas.Scale = 20;
        Assert.That(canvas.ScreenToWorldLength(8), Is.EqualTo(0.4).Within(1e-9));
    }

    [Test]
    public void TheVisibleWorldIsWhatTheViewportShows()
    {
        var canvas = Sized(400, 300);
        canvas.SetScaleAt(new Vector2(200, 150), 2);

        var seen = canvas.VisibleWorld;
        Assert.Multiple(() =>
        {
            Assert.That(seen.Width, Is.EqualTo(200).Within(1e-9), "twice the scale, half the world across");
            Assert.That(seen.Height, Is.EqualTo(150).Within(1e-9));
            Assert.That(seen.X, Is.EqualTo(-100).Within(1e-9), "the origin is still in the middle");
        });
    }

    // Fitting is what replaces "scroll to the content" on a plane that has no scrollbars to do it with.
    [Test]
    public void FittingPutsThePieceOfWorldInTheMiddle()
    {
        var canvas = Sized(400, 300);
        var wanted = new Rect(1000, 1000, 200, 100);

        canvas.ScaleToFit(wanted, padding: 0);

        var center = canvas.WorldToScreen(new Vector2(1100, 1050));
        Assert.Multiple(() =>
        {
            Assert.That(center.X, Is.EqualTo(200).Within(1e-6));
            Assert.That(center.Y, Is.EqualTo(150).Within(1e-6));
            Assert.That(canvas.Scale, Is.EqualTo(2).Within(1e-9), "the tighter of the two axes decides");
        });
    }

    // Bringing something into view MOVES the camera and leaves the scale alone: something already the right size does
    // not need resizing to be looked at.
    [Test]
    public void BringingIntoViewMovesAndDoesNotZoom()
    {
        var canvas = Sized(400, 300);
        var wanted = new Rect(500, 0, 50, 50);

        Assert.That(canvas.WorldToScreen(new Vector2(500, 0)).X, Is.GreaterThan(400), "off to the right to begin with");

        canvas.BringIntoView(wanted);

        Assert.Multiple(() =>
        {
            Assert.That(canvas.Scale, Is.EqualTo(1), "untouched");
            Assert.That(canvas.WorldToScreen(new Vector2(550, 50)).X, Is.EqualTo(400).Within(1e-6), "just inside");
        });
    }

    [Test]
    public void HomeIsTheOriginInTheMiddleAtOneToOne()
    {
        var canvas = Sized(400, 300);
        canvas.SetScaleAt(new Vector2(10, 10), 12);
        canvas.PanBy(new Vector2(5000, -7000));

        canvas.ResetCamera();

        Assert.Multiple(() =>
        {
            Assert.That(canvas.Scale, Is.EqualTo(1));
            Assert.That(canvas.WorldToScreen(Vector2.Zero), Is.EqualTo(new Vector2(200, 150)));
        });
    }

    // The grid is a ruler: its step is coarsened by whole powers so that what is read off it stays round - 10, 100,
    // 1000 - and never so dense that the marks run together.
    [Test]
    public void TheGridStepCoarsensSoTheMarksStayApart()
    {
        var canvas = Sized();
        canvas.GridSpacing = 20;
        canvas.GridCoarsening = 10;
        canvas.MinGridPitch = 12;

        canvas.Scale = 1;
        Assert.That(canvas.EffectiveGridSpacing, Is.EqualTo(20), "20 px apart already reads");

        canvas.Scale = 0.02;
        Assert.Multiple(() =>
        {
            Assert.That(canvas.EffectiveGridSpacing, Is.EqualTo(2000), "coarsened by whole powers, not to a free step");
            Assert.That(canvas.EffectiveGridSpacing * canvas.Scale, Is.GreaterThanOrEqualTo(12));
        });
    }

    // A stroke keeps its points as offsets from its OWN origin. That is what lets them be floats: a stroke is a tight
    // cluster a few hundred units across, and the same numbers written absolutely lose precision the further the
    // drawing goes from zero. Moving one is then a field, not a walk.
    [Test]
    public void AStrokeKeepsItsPointsRelativeToItself()
    {
        var stroke = new StrokeItem(new Vector2(1_000_000, -500_000), Brushes.White, 2);
        stroke.Add(new Vector2(1_000_010, -500_000));
        stroke.Add(new Vector2(1_000_020, -499_990));

        Assert.Multiple(() =>
        {
            Assert.That(stroke.Points[0].At, Is.EqualTo(new Vector2F(10, 0)));
            Assert.That(stroke.Points[1].At, Is.EqualTo(new Vector2F(20, 10)));
            Assert.That(stroke.Points[0].Pressure, Is.EqualTo(1f), "no stylus yet, so full pressure until one says otherwise");
        });

        // Moving it is ONE field: the points do not know where in the world they are.
        stroke.Origin = new Vector2(0, 0);
        Assert.That(stroke.Points[1].At, Is.EqualTo(new Vector2F(20, 10)));
        // The leftmost point is ten along, and the ink reaches half its width beyond it.
        Assert.That(stroke.Bounds.X, Is.EqualTo(9).Within(1e-9), "and the bounds follow the origin");
    }

    [Test]
    public void AStrokesBoundsHoldItsInk()
    {
        var stroke = new StrokeItem(new Vector2(100, 100), Brushes.White, 4);
        stroke.Add(new Vector2(100, 100));
        stroke.Add(new Vector2(140, 130));

        var bounds = stroke.Bounds;
        Assert.Multiple(() =>
        {
            Assert.That(bounds.X, Is.EqualTo(98).Within(1e-9), "half the thickness beyond the first point");
            Assert.That(bounds.Y, Is.EqualTo(98).Within(1e-9));
            Assert.That(bounds.Width, Is.EqualTo(44).Within(1e-9));
            Assert.That(bounds.Height, Is.EqualTo(34).Within(1e-9));
        });
    }

    // What counts as a hit is a SCREEN distance turned into a world one, so the same gap under the cursor is a hit at
    // any zoom. Here the tolerance is given directly, which is what the canvas hands over.
    [Test]
    public void AStrokeIsHitAlongItsWholeLength()
    {
        var stroke = new StrokeItem(new Vector2(0, 0), Brushes.White, 4);
        stroke.Add(new Vector2(0, 0));
        stroke.Add(new Vector2(100, 0));

        Assert.Multiple(() =>
        {
            Assert.That(stroke.HitTest(new Vector2(50, 1), 0), Is.True, "inside the ink");
            Assert.That(stroke.HitTest(new Vector2(50, 3), 0), Is.False, "beyond it");
            Assert.That(stroke.HitTest(new Vector2(50, 3), 2), Is.True, "but within the tolerance");
            Assert.That(stroke.HitTest(new Vector2(140, 0), 2), Is.False, "past the end is past the stroke");
        });
    }

    // A single tap is a stroke too - one point, no segment. Dropping it would lose every dot a user made.
    [Test]
    public void ASingleTapIsAStroke()
    {
        var stroke = new StrokeItem(new Vector2(10, 10), Brushes.White, 6);
        stroke.Add(new Vector2(10, 10));

        Assert.Multiple(() =>
        {
            Assert.That(stroke.HitTest(new Vector2(11, 11), 0), Is.True);
            Assert.That(stroke.Bounds.Width, Is.EqualTo(6).Within(1e-9));
        });
    }

    // The scene answers ONE question - what falls inside the piece of world the camera can see - so what a frame costs
    // follows the viewport and not the drawing.
    [Test]
    public void TheSceneAnswersWithWhatIsVisible()
    {
        var scene = new CanvasScene();

        var near = new StrokeItem(new Vector2(0, 0), Brushes.White, 1);
        near.Add(new Vector2(10, 10));

        var far = new StrokeItem(new Vector2(100_000, 100_000), Brushes.White, 1);
        far.Add(new Vector2(100_010, 100_010));

        scene.Add(near);
        scene.Add(far);

        var seen = new List<ICanvasItem>(scene.ItemsIn(new Rect(-50, -50, 200, 200)));
        Assert.That(seen, Is.EqualTo(new[] { near }));
    }

    [Test]
    public void TheSceneSaysWhenItChanges()
    {
        var scene = new CanvasScene();
        var told = 0;
        scene.Changed += (_, _) => told++;

        var stroke = new StrokeItem(Vector2.Zero, Brushes.White, 1);
        scene.Add(stroke);
        Assert.That(told, Is.EqualTo(1));

        scene.Remove(stroke);
        Assert.That(told, Is.EqualTo(2));

        scene.Remove(stroke);
        Assert.That(told, Is.EqualTo(2), "removing what is not there changed nothing");
    }

    // Paint order is the list's order, so what is ON TOP is what answers.
    [Test]
    public void TheSceneHitsTheTopmost()
    {
        var scene = new CanvasScene();

        var under = new StrokeItem(Vector2.Zero, Brushes.White, 4);
        under.Add(new Vector2(0, 0));
        under.Add(new Vector2(100, 0));

        var over = new StrokeItem(Vector2.Zero, Brushes.Red, 4);
        over.Add(new Vector2(0, 0));
        over.Add(new Vector2(100, 0));

        scene.Add(under);
        scene.Add(over);

        Assert.That(scene.HitTest(new Vector2(50, 0), 0), Is.SameAs(over));
    }

    // What the shader is handed. The grid is ONE rectangle whose pixels decide for themselves whether they are on a
    // mark, so everything the pass needs rides in this record - and the two things it must NOT have to do are compute
    // the step a second time and divide.
    [Test]
    public void TheGridBakesWhatTheShaderNeeds()
    {
        var brush = new CanvasGridBrush
        {
            Marks = CanvasGridMarks.Lines,
            Offset = new Vector2(120, 40),
            Scale = 2.5,
            Spacing = 200,
            Coarsening = 10,
            MinPitch = 12,
            MarkSize = 1,
            Background = new Color(32, 32, 32, 255),
            Color = new Color(255, 255, 255, 51),
            AxisColor = new Color(255, 255, 255, 204)
        };
        var payload = new RectanglePayload(brush, new Rect(0, 0, 400, 300), CornerRadius.Empty, null);

        Assert.That(CanvasGridCollector.WantsBatch(payload), Is.True);
        Assert.That(CanvasGridCollector.BakeItem(payload, Matrix4x4F.Identity, 1, 3, -1, out var item), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(item.Bounds, Is.EqualTo(new Vector4F(0, 0, 400, 300)));
            Assert.That(item.Camera.X, Is.EqualTo(120));
            Assert.That(item.Camera.Z, Is.EqualTo(2.5f), "screen pixels per world unit");
            Assert.That(item.Step.X, Is.EqualTo(200), "the step ALREADY coarsened - the shader works none of it out");
            Assert.That(item.Step.Z, Is.EqualTo(1f / 12).Within(1e-6),
                "the pitch as a RECIPROCAL: a division in the pixel stage stopped the driver creating the pass");
            Assert.That(item.Params.X, Is.EqualTo(3), "transform slot");
            Assert.That(item.Params.Y, Is.EqualTo(2), "lines");
            Assert.That(item.Background.W, Is.EqualTo(1).Within(1e-6));
            Assert.That(item.GridColor.W, Is.EqualTo(51 / 255f).Within(1e-6), "straight alpha, opacity folded in");
        });
    }

    // A pen would be chrome, and chrome belongs to whatever frames the canvas - drawn as itself, not by the grid pass.
    [Test]
    public void AGridWithAPenIsNotThisPass()
    {
        var payload = new RectanglePayload(new CanvasGridBrush(), new Rect(0, 0, 10, 10), CornerRadius.Empty,
            new Pen(Brushes.Red, 1));

        Assert.That(CanvasGridCollector.WantsBatch(payload), Is.False);
    }

    [Test]
    public void AnOrdinaryFillIsNotThisPass()
    {
        var payload = new RectanglePayload(Brushes.Red, new Rect(0, 0, 10, 10), CornerRadius.Empty, null);

        Assert.That(CanvasGridCollector.WantsBatch(payload), Is.False);
    }

    [Test]
    public void TheGridStepRefinesAgainWhenZoomedIn()
    {
        var canvas = Sized();
        canvas.GridSpacing = 20;
        canvas.GridCoarsening = 10;
        canvas.MinGridPitch = 12;

        canvas.Scale = 50;
        Assert.Multiple(() =>
        {
            Assert.That(canvas.EffectiveGridSpacing, Is.EqualTo(2).Within(1e-9));
            Assert.That(canvas.EffectiveGridSpacing * canvas.Scale, Is.GreaterThanOrEqualTo(12));
            Assert.That(canvas.EffectiveGridSpacing * canvas.Scale, Is.LessThan(120));
        });
    }

    private static StrokeItem Straight(double from, double to, double y = 0, double thickness = 1)
    {
        var stroke = new StrokeItem(new Vector2(from, y), Brushes.White, thickness);
        stroke.Add(new Vector2(from, y));
        stroke.Add(new Vector2(to, y));

        return stroke;
    }

    // How wide the hole looks: Bounds is the PAINTED extent - round ends included - so the gap between the two pieces
    // is the distance between their boxes and nothing more.
    private static double PaintedGap(StrokeItem before, StrokeItem after) =>
        after.Bounds.X - (before.Bounds.X + before.Bounds.Width);

    // The eraser rubs a HOLE and the line becomes two - which is the whole difference between erasing by point and
    // deleting what was touched, and the reason this returns pieces rather than a yes or no.
    [Test]
    public void ErasingTheMiddleOfAStrokeLeavesTwoPieces()
    {
        var stroke = Straight(0, 100);
        var pieces = new List<StrokeItem>();

        Assert.That(stroke.Erase(new Vector2(50, 0), 10, pieces), Is.True);
        Assert.That(pieces, Has.Count.EqualTo(2));

        Assert.Multiple(() =>
        {
            // Cut at the CROSSINGS: the hole is exactly as wide as the eraser, not as wide as the gap between whatever
            // points the thinning left behind.
            Assert.That(pieces[0].Bounds.X, Is.EqualTo(-0.5).Within(1e-6));
            Assert.That(pieces[0].Bounds.X + pieces[0].Bounds.Width, Is.EqualTo(40).Within(1e-6));
            Assert.That(pieces[1].Bounds.X, Is.EqualTo(60).Within(1e-6));
            Assert.That(pieces[1].Bounds.X + pieces[1].Bounds.Width, Is.EqualTo(100.5).Within(1e-6));
        });
    }

    // The hole is the ERASER's, whatever the ink's width - and that is the whole of what was wrong: the cut was made in
    // the centerline at the bare radius, and the round ends left on the two pieces reached back half a thickness each
    // and closed it again. An eraser thinner than the line closed it completely and appeared to do nothing.
    [TestCase(1.0)]
    [TestCase(8.0)]
    [TestCase(40.0)]
    public void TheHoleIsAsWideAsTheEraserHoweverFatTheLine(double thickness)
    {
        var stroke = Straight(-200, 200, thickness: thickness);
        var pieces = new List<StrokeItem>();

        Assert.That(stroke.Erase(new Vector2(0, 0), 4, pieces), Is.True);
        Assert.That(pieces, Has.Count.EqualTo(2));
        Assert.That(PaintedGap(pieces[0], pieces[1]), Is.EqualTo(8).Within(1e-6));
    }

    [Test]
    public void ErasingAnEndLeavesOnePiece()
    {
        var stroke = Straight(0, 100);
        var pieces = new List<StrokeItem>();

        Assert.That(stroke.Erase(new Vector2(0, 0), 20, pieces), Is.True);
        Assert.That(pieces, Has.Count.EqualTo(1));
        Assert.That(pieces[0].Bounds.X, Is.EqualTo(20).Within(1e-6));
    }

    // Nothing left is not the same as nothing happened: the caller has to take the stroke out of the scene either way,
    // and it can only tell the two apart by the answer.
    [Test]
    public void AnEraserOverTheWholeStrokeLeavesNothingAndStillSaysItCut()
    {
        var stroke = Straight(0, 20);
        var pieces = new List<StrokeItem>();

        Assert.That(stroke.Erase(new Vector2(10, 0), 40, pieces), Is.True);
        Assert.That(pieces, Is.Empty);
    }

    // Order here is PAINT order. A stroke rubbed through must stay where it was in it - taken out and re-added, both
    // halves rose above everything put on the canvas after the original was drawn.
    [Test]
    public void APieceOfAnErasedStrokeKeepsItsPlaceInPaintOrder()
    {
        var scene = new CanvasScene();
        var under = Straight(0, 100);
        var over = Straight(0, 100, 40);

        scene.Add(under);
        scene.Add(over);

        var pieces = new List<StrokeItem>();
        Assert.That(under.Erase(new Vector2(50, 0), 10, pieces), Is.True);
        Assert.That(scene.Replace(under, pieces), Is.True);

        Assert.That(scene.Items, Has.Count.EqualTo(3));
        Assert.Multiple(() =>
        {
            Assert.That(scene.Items[0], Is.SameAs(pieces[0]));
            Assert.That(scene.Items[1], Is.SameAs(pieces[1]));
            Assert.That(scene.Items[2], Is.SameAs(over), "what was painted after the stroke stays after both halves");
        });
    }

    [Test]
    public void AnEraserThatMissesChangesNothing()
    {
        var stroke = Straight(0, 100);
        var pieces = new List<StrokeItem>();

        Assert.That(stroke.Erase(new Vector2(50, 80), 10, pieces), Is.False);
        Assert.That(pieces, Is.Empty);
        Assert.That(stroke.Points, Has.Count.EqualTo(2));
    }
}
