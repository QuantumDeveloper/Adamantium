using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// A star track has to be written on EVERY arrange, including to zero. It used to be assigned only while there was room
/// left over to share, so a star squeezed out entirely kept the width it had at the previous, larger arrange - and the
/// tracks after it stayed pushed along by that much.
/// <para>Found on a docking panel folded against a side: the group went from 70 wide to 32, its template grid is
/// Auto,*,Auto with the folded tab strip in the last column, and the strip was laid out at x=38 - the share the star
/// still held from the 70-wide pass. The strip drew outside its own panel, hard against the window's edge, while its
/// bounds, its hit-testing and every render snapshot agreed with each other and were all "correct".</para>
/// </summary>
[TestFixture]
public class GridStarShrinkTests
{
    private static (Grid grid, Border tail) Build()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var tail = new Border { Width = 32, Height = 40 };
        Grid.SetColumn(tail, 2);
        grid.Children.Add(tail);
        return (grid, tail);
    }

    [Test]
    public void AStarSqueezedToNothing_StopsPushingTheTracksAfterIt()
    {
        var (grid, tail) = Build();

        // Measured UNBOUNDED, arranged into a slot - which is how a pane of Auto length is laid out (PaneHost asks the
        // child how much it needs with no limit, then arranges it into the answer). A star has no share of an unbounded
        // width, so nothing is resolved at measure and the arrange is the only pass that sizes it.
        var unbounded = new Size(double.PositiveInfinity, 40);

        grid.Measure(unbounded);
        grid.Arrange(new Rect(0, 0, 70, 40));
        Assert.That(tail.Bounds.X, Is.EqualTo(38).Within(0.5), "the star owns the spare 38 while there is spare");

        // Narrow: the Auto column takes everything, so the star's share is nothing at all.
        grid.Measure(unbounded);
        grid.Arrange(new Rect(0, 0, 32, 40));

        Assert.That(tail.Bounds.X, Is.EqualTo(0).Within(0.5),
            "with no room left the star is zero - keeping its old share puts the column past the grid's own edge");
    }
}
