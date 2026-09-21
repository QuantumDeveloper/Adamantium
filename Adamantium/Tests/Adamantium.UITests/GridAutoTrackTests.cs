using System;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>What an <c>Auto</c> track is: exactly as big as what is in it. Not bigger when the grid has room to spare,
/// not smaller when it has not - spare space belongs to <c>*</c> tracks, and a shortfall overflows.
/// <para>Arrange used to re-decide this: it split any spare equally between the Auto tracks, and divided any shortfall
/// between them by ratio. So a two-column Auto grid holding a 16-wide icon and a 24-wide label arranged them into
/// 17.25 and 22.75 - the icon drifting right inside its inflated cell, the label clipped by what the icon took.</para></summary>
[TestFixture]
public class GridAutoTrackTests
{
    private static ColumnDefinition Auto() => new() { Width = GridLength.Auto };

    private static ColumnDefinition Star() => new() { Width = new GridLength(1, GridUnitType.Star) };

    private static Border Cell(double width, int column)
    {
        var child = new Border { Width = width, Height = 10 };
        Grid.SetColumn(child, column);
        return child;
    }

    private static Grid TwoAutoColumns(double first, double second)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(Auto());
        grid.ColumnDefinitions.Add(Auto());
        grid.Children.Add(Cell(first, 0));
        grid.Children.Add(Cell(second, 1));
        return grid;
    }

    // Room to spare goes NOWHERE: two Auto tracks holding 16 and 24 stay 16 and 24 in a grid arranged at 200.
    [Test]
    public void AutoTracks_DoNotGrowIntoSpareRoom()
    {
        var grid = TwoAutoColumns(16, 24);
        grid.Measure(Size.Infinity);
        grid.Arrange(new Rect(0, 0, 200, 10));

        Assert.Multiple(() =>
        {
            Assert.That(grid.Children[0].Bounds, Is.EqualTo(new Rect(0, 0, 16, 10)));
            Assert.That(grid.Children[1].Bounds.X, Is.EqualTo(16), "the second track starts where the first ends");
            Assert.That(grid.Children[1].Bounds.Width, Is.EqualTo(24));
        });
    }

    // ...and the same at the exact fit, which is the case a template hits when its grid is sized to its own content.
    [Test]
    public void AutoTracks_AreThemselvesAtAnExactFit()
    {
        var grid = TwoAutoColumns(16, 24);
        grid.Measure(Size.Infinity);
        grid.Arrange(new Rect(0, 0, 40, 10));

        Assert.Multiple(() =>
        {
            Assert.That(grid.Children[0].Bounds.Width, Is.EqualTo(16));
            Assert.That(grid.Children[1].Bounds.X, Is.EqualTo(16));
            Assert.That(grid.Children[1].Bounds.Width, Is.EqualTo(24));
        });
    }

    // A MARGIN belongs to the child, not to the track beside it. The label of a ribbon command is offset from its icon
    // this way, and the offset used to be paid for out of the icon's own column.
    [Test]
    public void AMarginOnACellChild_DoesNotTakeRoomFromTheOtherTrack()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(Auto());
        grid.ColumnDefinitions.Add(Auto());

        var icon = Cell(16, 0);
        var label = Cell(24, 1);
        label.Margin = new Thickness(6, 0, 0, 0);
        grid.Children.Add(icon);
        grid.Children.Add(label);

        grid.Measure(Size.Infinity);
        grid.Arrange(new Rect(0, 0, grid.DesiredSize.Width, 10));

        Assert.Multiple(() =>
        {
            Assert.That(icon.Bounds, Is.EqualTo(new Rect(0, 0, 16, 10)), "the icon owns its track whole");
            Assert.That(label.Bounds.X, Is.EqualTo(22), "16 of track plus its own 6 of margin");
            Assert.That(label.Bounds.Width, Is.EqualTo(24), "and it is not clipped for it");
        });
    }

    // Spare room is what a STAR track is for; an Auto track beside it still keeps to its content.
    [Test]
    public void SpareRoomGoesToTheStarTrack_NotToTheAutoOne()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(Auto());
        grid.ColumnDefinitions.Add(Star());
        grid.Children.Add(Cell(16, 0));

        // No width of its own, so it STRETCHES - which is what makes its bounds report the track it is in.
        var filler = new Border { Height = 10 };
        Grid.SetColumn(filler, 1);
        grid.Children.Add(filler);

        grid.Measure(Size.Infinity);
        grid.Arrange(new Rect(0, 0, 200, 10));

        Assert.Multiple(() =>
        {
            Assert.That(grid.Children[0].Bounds.Width, Is.EqualTo(16), "the Auto track is still its content");
            Assert.That(filler.Bounds.X, Is.EqualTo(16));
            Assert.That(filler.Bounds.Width, Is.EqualTo(184), "and the star takes everything left");
        });
    }

    // Too little room: the tracks keep their sizes and the content overflows. Scaling them down instead is what squeezed
    // a label to two thirds of itself while the icon beside it grew.
    [Test]
    public void AutoTracks_KeepTheirSize_WhenTheGridIsTooNarrow()
    {
        var grid = TwoAutoColumns(16, 24);
        grid.Measure(Size.Infinity);
        grid.Arrange(new Rect(0, 0, 30, 10));

        Assert.Multiple(() =>
        {
            Assert.That(grid.Children[0].Bounds.Width, Is.EqualTo(16));
            Assert.That(grid.Children[1].Bounds.X, Is.EqualTo(16));
            Assert.That(grid.Children[1].Bounds.Width, Is.EqualTo(24));
        });
    }

    // A child whose height depends on the width it is handed - what wrapping text is. The engine's grid measures cells
    // that sit in a star track TWICE: once before the star sizes exist, once after. Only the star half of that first,
    // provisional measurement is meant to be thrown away.
    private sealed class WrappingProbe : Border
    {
        private const double Ink = 1200;
        private const double LineHeight = 20;

        protected override Size MeasureOverride(Size availableSize)
        {
            var width = double.IsInfinity(availableSize.Width) || availableSize.Width <= 0 ? 1 : availableSize.Width;
            return new Size(Math.Min(Ink, width), Math.Ceiling(Ink / width) * LineHeight);
        }
    }

    // An Auto ROW holding a child that spans STAR columns. The provisional pass hands that child the star width it does
    // not know yet - zero - and a wrapping child answers that with a column of one word per line. That answer was going
    // straight into the row's Auto size, and since a track size only ever grows, the honest second measurement could
    // never take it back: a two-line paragraph kept half the page, and everything under it was pushed off the bottom.
    [Test]
    public void AnAutoRow_IsSizedByTheHONESTMeasurementOfAStarSpanningChild()
    {
        var grid = new Grid
        {
            RowDefinitions = { new RowDefinition { Height = GridLength.Auto },
                               new RowDefinition { Height = new GridLength(1, GridUnitType.Star) } },
            ColumnDefinitions = { Star(), Star() }
        };

        var paragraph = new WrappingProbe();
        Grid.SetColumnSpan(paragraph, 2);
        var body = new Border();
        Grid.SetRow(body, 1);
        Grid.SetColumnSpan(body, 2);
        grid.Children.Add(paragraph);
        grid.Children.Add(body);

        grid.Measure(new Size(600, 400));
        grid.Arrange(new Rect(0, 0, 600, 400));

        Assert.Multiple(() =>
        {
            Assert.That(paragraph.Bounds.Height, Is.EqualTo(40), "600 wide is two lines of it, and the row is two lines");
            Assert.That(body.Bounds.Y, Is.EqualTo(40), "so the star row starts right under it");
            Assert.That(body.Bounds.Height, Is.EqualTo(360), "and keeps the rest of the grid");
        });
    }

    // Rows are the same rule turned ninety degrees - the split button's three stacked rows are Auto for this reason.
    [Test]
    public void AutoRows_FollowTheSameRule()
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var top = new Border { Width = 10, Height = 32 };
        var bottom = new Border { Width = 10, Height = 12 };
        Grid.SetRow(bottom, 1);
        grid.Children.Add(top);
        grid.Children.Add(bottom);

        grid.Measure(Size.Infinity);
        grid.Arrange(new Rect(0, 0, 10, 120));

        Assert.Multiple(() =>
        {
            Assert.That(top.Bounds.Height, Is.EqualTo(32));
            Assert.That(bottom.Bounds.Y, Is.EqualTo(32));
            Assert.That(bottom.Bounds.Height, Is.EqualTo(12));
        });
    }
}
