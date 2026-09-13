using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;
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

        var centre = canvas.WorldToScreen(new Vector2(1100, 1050));
        Assert.Multiple(() =>
        {
            Assert.That(centre.X, Is.EqualTo(200).Within(1e-6));
            Assert.That(centre.Y, Is.EqualTo(150).Within(1e-6));
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
}
