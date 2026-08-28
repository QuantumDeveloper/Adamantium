using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// A pane folded against a SIDE turns its tab labels ninety degrees, and the narrow column they end up in has to be as
/// wide as the turned text is tall. On the stand the labels came out clipped along their own height, which can only
/// mean something in that chain reported a size for text lying flat. These measure the chain one link at a time so the
/// answer is a number rather than a guess: the transform itself, then the strip that has to make room for it.
/// </summary>
[TestFixture]
public class RotatedTabLabelTests
{
    private static readonly Size Unbounded = new(double.PositiveInfinity, double.PositiveInfinity);

    private static TextBlock Label()
    {
        var text = new TextBlock { Text = "Inspector", FontSize = 12 };
        text.Measure(Unbounded);
        return text;
    }

    /// <summary>The link everything else rests on: a turned element must report the BOUNDING BOX of the turned content,
    /// so its width becomes the flat text's height and its height the flat text's width.</summary>
    [Test]
    public void ALayoutTransform_SwapsTheReportedSize()
    {
        var flat = Label().DesiredSize;

        var turned = Label();
        turned.LayoutTransform = new Transform { RotationAngle = 90 };
        turned.Measure(Unbounded);

        Assert.Multiple(() =>
        {
            Assert.That(turned.DesiredSize.Width, Is.EqualTo(flat.Height).Within(0.5),
                "the turned label is as WIDE as the flat one was tall");
            Assert.That(turned.DesiredSize.Height, Is.EqualTo(flat.Width).Within(0.5),
                "...and as TALL as it was wide");
        });
    }

    /// <summary>And the strip that holds them has to pass that width on. The scroller CLIPS to its own bounds, so a
    /// cross size measured one pixel short is text with its ascenders cut off - which is exactly what a folded pane
    /// showed.</summary>
    [Test]
    public void AVerticalStrip_IsAsWideAsItsTurnedLabels()
    {
        var probe = Label();
        probe.LayoutTransform = new Transform { RotationAngle = 90 };
        probe.Measure(Unbounded);
        var turnedWidth = probe.DesiredSize.Width;

        var panel = new TabPanel { Orientation = Orientation.Vertical };
        for (var i = 0; i < 3; i++)
        {
            var label = Label();
            label.LayoutTransform = new Transform { RotationAngle = 90 };
            panel.Children.Add(label);
        }

        var scroller = new TabStripScroller { Orientation = Orientation.Vertical, Child = panel };
        scroller.Measure(Unbounded);

        Assert.That(scroller.DesiredSize.Width, Is.GreaterThanOrEqualTo(turnedWidth - 0.5),
            "the column must be at least as wide as a turned label, or the scroller's clip cuts it");
    }

    /// <summary>The link in the middle: the tab template puts its label in the STARRED track of a four-column grid, so
    /// that a lone stretched document tab keeps its close button against the right edge. A folded strip measures with
    /// no width limit at all, and a star track has no share of an unbounded width to take - so this asks whether the
    /// track still gives its child the room the child asked for.</summary>
    [Test]
    public void AStarredTrack_StillGivesItsChildRoom_WhenTheWidthIsUnbounded()
    {
        var label = Label();
        label.LayoutTransform = new Transform { RotationAngle = 90 };
        label.Measure(Unbounded);
        var turnedWidth = label.DesiredSize.Width;

        // The tab template's inner grid, in miniature: icon, label, pin, close.
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var content = Label();
        content.LayoutTransform = new Transform { RotationAngle = 90 };
        Grid.SetColumn(content, 1);
        grid.Children.Add(content);

        grid.Measure(Unbounded);

        Assert.That(grid.DesiredSize.Width, Is.GreaterThanOrEqualTo(turnedWidth - 0.5),
            "a star track measured with no bound must still report its content's width");
    }

    /// <summary>The pass that actually decides the folded column's width. PaneHost measures an Auto pane TWICE: once
    /// unbounded, to ask how much it needs, and once at exactly the answer. So the second measurement has no slack at
    /// all - if anything in the strip asks for a pixel more the second time round, the scroller's clip takes it out of
    /// the label, and the turned text loses its ascenders down one side.</summary>
    [Test]
    public void MeasuringAgainAtTheAnswer_DoesNotCostTheStripAnyWidth()
    {
        var panel = new TabPanel { Orientation = Orientation.Vertical };
        for (var i = 0; i < 3; i++)
        {
            var label = Label();
            label.LayoutTransform = new Transform { RotationAngle = 90 };
            panel.Children.Add(label);
        }
        var scroller = new TabStripScroller { Orientation = Orientation.Vertical, Child = panel };

        // Pass one: how much do you need?
        scroller.Measure(Unbounded);
        var asked = scroller.DesiredSize.Width;

        // Pass two: here is exactly that, and nothing more.
        scroller.Measure(new Size(asked, 400));

        Assert.That(scroller.DesiredSize.Width, Is.EqualTo(asked).Within(0.5),
            "the strip must still fit in the width it asked for");
    }

    /// <summary>The last link, and the one the stand points at: measured off the screenshots, a folded strip comes out
    /// about as wide as the turned TEXT, with none of the tab's padding in it - and a tab that is 12 either side should
    /// be 24 wider than its label. This is the tab template's actual shape: a padded Border around the grid that holds
    /// the turned header.</summary>
    [Test]
    public void ATabsPadding_CountsTowardsItsTurnedWidth()
    {
        var probe = Label();
        probe.LayoutTransform = new Transform { RotationAngle = 90 };
        probe.Measure(Unbounded);
        var turnedWidth = probe.DesiredSize.Width;

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var content = Label();
        content.LayoutTransform = new Transform { RotationAngle = 90 };
        Grid.SetColumn(content, 1);
        grid.Children.Add(content);

        var tab = new Border { Padding = new Thickness(12, 6, 12, 6), Child = grid };
        tab.Measure(Unbounded);

        Assert.That(tab.DesiredSize.Width, Is.EqualTo(turnedWidth + 24).Within(0.5),
            "the padding either side of a turned label is part of how wide the tab is");
    }
}
