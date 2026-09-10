using System;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>What a grid costs to measure AGAIN. A template is full of grids and a table's cells are measured on every
/// pass, so a grid that re-describes its tracks with fresh objects each time is a garbage source proportional to the
/// window - which is the shape of stutter, not of a slow function.</summary>
[TestFixture]
public class GridAllocationTests
{
    private static Grid Built()
    {
        var grid = new Grid
        {
            RowDefinitions = { new RowDefinition { Height = GridLength.Auto },
                               new RowDefinition { Height = new GridLength(1, GridUnitType.Star) } },
            ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(20) },
                                  new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) } }
        };

        for (var i = 0; i < 4; i++)
        {
            var child = new Border { Width = 10, Height = 10 };
            Grid.SetRow(child, i % 2);
            Grid.SetColumn(child, i / 2);
            grid.Children.Add(child);
        }

        return grid;
    }

    // The tracks, the cells and the groups are DESCRIBED again on every measure - but the objects that hold them are
    // the same ones, so measuring an unchanged grid a hundred times allocates a handful of bytes rather than a hundred
    // sets of segments, cells and lists.
    [Test]
    public void MeasuringAnUnchangedGridAgain_AllocatesAlmostNothing()
    {
        var grid = Built();
        var slot = new Size(200, 100);

        // Warm: the first pass is where the objects are made, and the layout system's own one-time work happens.
        for (var i = 0; i < 5; i++) grid.Measure(slot, force: true);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++) grid.Measure(slot, force: true);
        var perPass = (GC.GetAllocatedBytesForCurrentThread() - before) / 100.0;

        TestContext.WriteLine($"{perPass:F0} bytes per re-measure");
        Assert.That(perPass, Is.LessThan(200), "a grid that describes the same shape again must not build it again");
    }
}
