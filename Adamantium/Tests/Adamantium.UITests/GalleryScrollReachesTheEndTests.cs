using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Panels;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// A gallery is given a fixed height by the band, and that height is not always a whole number of cells: a viewport a
/// little shorter than two of them shows one whole row and part of another. Scrolling has to be able to bring the LAST
/// row fully into view - clamped to the rows ASKED FOR, it could not, and the bottom row stayed half cut with nowhere
/// further to go.
/// </summary>
[TestFixture]
public class GalleryScrollReachesTheEndTests
{
    // Three rows of cells, in a viewport that holds less than two of them.
    private static (RibbonGalleryPanel panel, double cell) Hosted(double viewportHeight)
    {
        var panel = new RibbonGalleryPanel { Columns = 2, Rows = 2 };
        for (var i = 0; i < 6; i++) panel.Children.Add(new ContentPresenter { Width = 20, Height = 30 });

        panel.Measure(new Size(200, viewportHeight));
        panel.Arrange(new Rect(0, 0, 200, viewportHeight));

        return (panel, 30);
    }

    [Test]
    public void ItCountsTheRowsThatFIT_NotTheRowsAskedFor()
    {
        var (panel, _) = Hosted(50);   // one whole 30-tall row and two thirds of the next

        Assert.That(panel.VisibleRows, Is.EqualTo(1),
            "counted as two, the clamp stops one row early and the last row can never be brought into view");
    }

    [Test]
    public void AViewportThatHoldsThemAllCountsThemAll()
    {
        var (panel, _) = Hosted(90);

        Assert.That(panel.VisibleRows, Is.EqualTo(3));
    }

    /// <summary>And the offset never runs past the end: at the last row the content sits flush with the bottom rather
    /// than scrolling into empty space below it.</summary>
    [Test]
    public void ScrollingStopsWithTheLastRowFlushWithTheBottom()
    {
        var (panel, cell) = Hosted(50);

        panel.FirstRow = 99;   // far past the end
        panel.Arrange(new Rect(0, 0, 200, 50));

        var lastCell = (IMeasurableComponent)panel.Children[panel.Children.Count - 1];

        Assert.That(lastCell.Bounds.Y + lastCell.Bounds.Height, Is.EqualTo(50).Within(0.5),
            "the last row should end exactly at the bottom of the viewport");
    }
}
