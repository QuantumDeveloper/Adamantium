using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UITests.Rendering;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>The MAP of the plane. A plane with no edges has no scrollbars, and a scrollbar is what normally says both
/// how much there is and where in it you are - on a big graph those are the two questions asked most often.</summary>
public class CanvasMiniMapTests
{
    private static (CanvasMiniMap Map, InfiniteCanvas Canvas, CanvasScene Scene) Stage()
    {
        var canvas = new InfiniteCanvas();
        canvas.Measure(new Size(800, 600), force: true);
        canvas.Arrange(new Rect(0, 0, 800, 600));

        var scene = new CanvasScene();
        canvas.Scene = scene;

        var map = new CanvasMiniMap
        {
            Canvas = canvas,
            ItemBrush = Brushes.White,
            ViewBrush = Brushes.Red
        };

        map.Measure(new Size(200, 150), force: true);
        map.Arrange(new Rect(0, 0, 200, 150));

        return (map, canvas, scene);
    }

    private static ShapeItem Box(double x, double y, double w = 100, double h = 60) =>
        new(CanvasShape.Rectangle, new Rect(x, y, w, h), Brushes.White, 2);

    private static RecordingDrawingSession Drawn(CanvasMiniMap map)
    {
        var session = new RecordingDrawingSession();
        map.Draw(session);

        return session;
    }

    // ONE BOX PER THING and a box round what is being looked at. A small picture of the drawing would be a second
    // renderer to keep in step with the first; boxes say WHERE things are, which is all a map is for.
    [Test]
    public void TheMapDrawsEverythingOnThePlaneAndTheViewport()
    {
        var (map, _, scene) = Stage();
        scene.Add(Box(0, 0));
        scene.Add(Box(4000, 3000));

        var drawn = Drawn(map);

        Assert.That(drawn.Rectangles, Has.Count.EqualTo(3), "two things and the viewport make three rectangles");
    }

    // ...and it hears about a change the CAMERA did not make. A node put down while the camera stood still moved
    // nothing the map was listening to, so the map went on showing the graph as it had been.
    [Test]
    public void SomethingPutOnThePlaneTellsWhoeverDrawsAPictureOfIt()
    {
        var (_, canvas, scene) = Stage();

        var told = 0;
        canvas.PlaneChanged += (_, _) => told++;

        scene.Add(Box(0, 0));

        Assert.That(told, Is.GreaterThan(0), "the plane changed and nothing standing beside it was told");
    }

    // Everything drawn INSIDE the map: a map that spilled over its own edge would be telling you about somewhere you
    // cannot see on it.
    [Test]
    public void NothingIsDrawnOutsideTheMap()
    {
        var (map, _, scene) = Stage();
        scene.Add(Box(-9000, -7000));
        scene.Add(Box(9000, 7000));

        foreach (var (_, box, _) in Drawn(map).Rectangles)
        {
            Assert.That(box.X, Is.GreaterThanOrEqualTo(-0.5));
            Assert.That(box.Y, Is.GreaterThanOrEqualTo(-0.5));
            Assert.That(box.X + box.Width, Is.LessThanOrEqualTo(200.5));
            Assert.That(box.Y + box.Height, Is.LessThanOrEqualTo(150.5));
        }
    }

    // ...and it holds the CAMERA as well as the drawing, so a camera that has wandered off the work is still somewhere
    // on the map - which is exactly when a map is looked at.
    [Test]
    public void TheMapHoldsTheCameraEvenWhenItHasWanderedOff()
    {
        var (map, canvas, scene) = Stage();
        scene.Add(Box(0, 0));

        canvas.CenterOn(new Vector2(50000, 40000));

        var drawn = Drawn(map);

        Assert.That(drawn.Rectangles, Has.Count.EqualTo(2));
        foreach (var (_, box, _) in drawn.Rectangles)
        {
            Assert.That(box.X + box.Width, Is.LessThanOrEqualTo(200.5), "the map spilled over its own edge");
        }
    }

    // PRESSING THE MAP takes the camera there. A map you can only look at answers half the question it raises.
    [Test]
    public void PressingTheMapTakesTheCameraThere()
    {
        var (map, canvas, scene) = Stage();
        scene.Add(Box(0, 0));
        scene.Add(Box(4000, 3000, 100, 60));

        Drawn(map);

        // The far corner of the map is the far corner of the plane.
        map.Look(new Vector2(199, 149));
        var far = canvas.VisibleWorld;

        map.Look(new Vector2(1, 1));
        var near = canvas.VisibleWorld;

        Assert.Multiple(() =>
        {
            Assert.That(far.X, Is.GreaterThan(near.X), "both ends of the map take the camera to the same place");
            Assert.That(far.Y, Is.GreaterThan(near.Y));
            Assert.That(far.Contains(new Vector2(4050, 3030)), Is.True,
                "the far corner of the map is not the far corner of the plane");
        });
    }

    // A map of an empty plane draws nothing rather than dividing by a size of zero.
    [Test]
    public void AMapOfAnEmptyPlaneDrawsTheViewportAndNothingElse()
    {
        var (map, _, _) = Stage();

        Assert.That(Drawn(map).Rectangles, Has.Count.EqualTo(1));
    }
}
