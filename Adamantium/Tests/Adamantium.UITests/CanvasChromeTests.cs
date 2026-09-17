using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

// The chrome layer is where a canvas's panels are PLACED. A pane says which edge it wants and whether it reserves the
// room it takes; everything else - what happens when two want the same edge, what a docked one costs the canvas, where
// one dropped in the middle ends up - is arithmetic here, and these are about that arithmetic.
public class CanvasChromeTests
{
    private const double Room = 400;

    private static CanvasPane Pane(CanvasPanePlacement placement, double width = 80, double height = 40,
        bool docked = false)
    {
        return new CanvasPane
        {
            Placement = placement,
            Width = width,
            Height = height,
            IsDocked = docked
        };
    }

    private static CanvasChromeLayer Laid(params CanvasPane[] panes)
    {
        var layer = new CanvasChromeLayer { PaneInset = new Thickness(10), PaneGap = 8 };
        var collection = new CanvasPanes();

        foreach (var pane in panes) collection.Add(pane);

        layer.Sync(collection);
        layer.Measure(new Size(Room, Room), force: true);
        layer.Arrange(new Rect(0, 0, Room, Room));

        return layer;
    }

    [Test]
    public void APaneSitsAgainstTheEdgeItAsksFor()
    {
        var pane = Pane(CanvasPanePlacement.TopLeft);
        Laid(pane);

        Assert.That(pane.Bounds.X, Is.EqualTo(10));
        Assert.That(pane.Bounds.Y, Is.EqualTo(10));
    }

    // Measured from the FAR edge, not from the near one: a right-hand panel whose left edge was placed would drift off
    // the canvas the moment it got wider than the arithmetic expected.
    [Test]
    public void ARightHandPaneIsMeasuredFromTheRightEdge()
    {
        var pane = Pane(CanvasPanePlacement.TopRight, width: 120);
        Laid(pane);

        Assert.That(pane.Bounds.X, Is.EqualTo(Room - 10 - 120));
    }

    [Test]
    public void ABottomCenterPaneIsCenteredAcrossTheViewport()
    {
        var pane = Pane(CanvasPanePlacement.BottomCenter, width: 100, height: 30);
        Laid(pane);

        Assert.That(pane.Bounds.X, Is.EqualTo((Room - 100) / 2));
        Assert.That(pane.Bounds.Y, Is.EqualTo(Room - 10 - 30));
    }

    // Two panels asking for one corner is not a mistake and must not look like one: they stack, in the order they were
    // given, with the gap between them.
    [Test]
    public void TwoPanesInOneSlotStackInsteadOfCoveringEachOther()
    {
        var first = Pane(CanvasPanePlacement.TopLeft, height: 40);
        var second = Pane(CanvasPanePlacement.TopLeft, height: 25);
        Laid(first, second);

        Assert.That(first.Bounds.Y, Is.EqualTo(10));
        Assert.That(second.Bounds.Y, Is.EqualTo(10 + 40 + 8));
    }

    // A pane stacked upwards from the bottom, so the one written first is the one nearest the edge - which is what
    // reading the markup top to bottom leads anyone to expect.
    [Test]
    public void PanesAtTheBottomStackUpwards()
    {
        var first = Pane(CanvasPanePlacement.BottomLeft, height: 40);
        var second = Pane(CanvasPanePlacement.BottomLeft, height: 25);
        Laid(first, second);

        Assert.That(first.Bounds.Y, Is.EqualTo(Room - 10 - 40));
        Assert.That(second.Bounds.Y, Is.EqualTo(Room - 10 - 40 - 8 - 25));
    }

    [Test]
    public void AFreePaneSitsWhereItWasPut()
    {
        var pane = Pane(CanvasPanePlacement.Free);
        pane.Offset = new Vector2(123, 77);
        Laid(pane);

        Assert.That(pane.Bounds.X, Is.EqualTo(123));
        Assert.That(pane.Bounds.Y, Is.EqualTo(77));
    }

    // A pane that cannot be reached is a pane that is gone, and an edgeless plane has no corner to find it in.
    [Test]
    public void APaneIsNeverPlacedOffTheEdge()
    {
        var pane = Pane(CanvasPanePlacement.Free, width: 80, height: 40);
        pane.Offset = new Vector2(5000, 5000);
        Laid(pane);

        Assert.That(pane.Bounds.X, Is.EqualTo(Room - 80));
        Assert.That(pane.Bounds.Y, Is.EqualTo(Room - 40));
    }

    [Test]
    public void AFloatingPaneTakesNoRoomFromTheCanvas()
    {
        var layer = Laid(Pane(CanvasPanePlacement.Left, width: 60));

        Assert.That(layer.Inset(), Is.EqualTo(new Thickness(0)));
    }

    [Test]
    public void ADockedPaneTakesItsWidthFromTheSideItIsOn()
    {
        var layer = Laid(Pane(CanvasPanePlacement.Left, width: 60, docked: true));

        Assert.That(layer.Inset(), Is.EqualTo(new Thickness(60, 0, 0, 0)));
    }

    [Test]
    public void ADockedBarAtTheBottomTakesHeightAndNotWidth()
    {
        var layer = Laid(Pane(CanvasPanePlacement.BottomCenter, width: 100, height: 30, docked: true));

        Assert.That(layer.Inset(), Is.EqualTo(new Thickness(0, 0, 0, 30)));
    }

    // Two docked to one edge sit one above the other, so together they take the width of the WIDER - not of both, which
    // is what summing them would claim and what would leave a strip of the plane nobody could reach.
    [Test]
    public void TwoPanesDockedToOneEdgeTakeTheWiderAndNotTheSum()
    {
        var layer = Laid(
            Pane(CanvasPanePlacement.TopLeft, width: 60, docked: true),
            Pane(CanvasPanePlacement.BottomLeft, width: 90, docked: true));

        Assert.That(layer.Inset(), Is.EqualTo(new Thickness(90, 0, 0, 0)));
    }

    // A pane that FOLLOWS the selection is shown by there being one. With no canvas behind the layer there is no
    // selection either, so it stays out of the way rather than parking itself in a corner.
    [Test]
    public void APaneThatFollowsTheSelectionIsHiddenWhileNothingIsSelected()
    {
        var pane = Pane(CanvasPanePlacement.Selection);
        Laid(pane);

        Assert.That(pane.Visibility, Is.EqualTo(Visibility.Collapsed));
    }

    // Widening is a drag along X read against the side the pane's inner edge is on: a panel against the RIGHT edge gets
    // wider when its edge is pulled LEFT, and one against the left edge the other way round.
    [Test]
    public void APaneAgainstTheRightEdgeWidensWhenItsEdgeIsPulledLeft()
    {
        var pane = Pane(CanvasPanePlacement.TopRight, width: 200);
        pane.MinResizeWidth = 100;

        Assert.That(pane.Widened(200, -60, Room), Is.EqualTo(260));
        Assert.That(pane.Widened(200, 60, Room), Is.EqualTo(140));
    }

    [Test]
    public void APaneAgainstTheLeftEdgeWidensTheOtherWay()
    {
        var pane = Pane(CanvasPanePlacement.TopLeft, width: 200);
        pane.MinResizeWidth = 100;

        Assert.That(pane.Widened(200, 60, Room), Is.EqualTo(260));
        Assert.That(pane.Widened(200, -60, Room), Is.EqualTo(140));
    }

    // A panel pulled to nothing is a panel with no edge left to pull back out by.
    [Test]
    public void ItCannotBePulledNarrowerThanItsFloor()
    {
        var pane = Pane(CanvasPanePlacement.TopRight, width: 200);
        pane.MinResizeWidth = 160;

        Assert.That(pane.Widened(200, 400, Room), Is.EqualTo(160));
    }

    // ...nor wider than the canvas, which would leave nothing behind it to look at.
    [Test]
    public void ItCannotBePulledWiderThanTheViewport()
    {
        var pane = Pane(CanvasPanePlacement.TopRight, width: 200);

        Assert.That(pane.Widened(200, -5000, Room), Is.EqualTo(Room));
    }

    // A viewport narrower than the floor: the floor wins, because Clamp throws when the ceiling is below it - and a
    // window dragged very narrow is not a reason for the canvas to fall over.
    [Test]
    public void AViewportNarrowerThanTheFloorDoesNotBreakTheArithmetic()
    {
        var pane = Pane(CanvasPanePlacement.TopRight, width: 200);
        pane.MinResizeWidth = 160;

        Assert.That(pane.Widened(200, -5000, room: 40), Is.EqualTo(160));
    }
}
