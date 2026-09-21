using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>Where the letters sit in a line of text. A line is the FONT's line - an ascent, a descent, and whatever
/// leading the font asks for - and the letters sit on a baseline an ascent below the top, with the leading split evenly.
/// <para>Measured because it cannot be seen from inside a control: a drop-down whose words rode low read as a box a
/// line too tall, and no alignment setting anywhere could correct it - alignment places the BOX, and the box was
/// already where it should be.</para></summary>
public class TextLineTests
{
    private static void Settle(Adamantium.UI.Controls.Window window)
    {
        for (var i = 0; i < 6; i++) Adamantium.UI.Extensions.WindowExtension.UpdateTree(window);
    }

    private static TextBlock Line(string words, double size = 14)
    {
        var block = new TextBlock { Text = words, FontSize = size };
        block.Measure(new Size(400, 400), force: true);

        return block;
    }

    // A LINE IS A FONT METRIC, not the ink of what is in it: the same box for "Dots" and for a word with tails, or a
    // column of labels changes height with what is typed into it.
    [Test]
    public void ALineIsTheSameHeightWhateverIsInIt()
    {
        Assert.That(Line("gyp").DesiredSize.Height, Is.EqualTo(Line("Dots").DesiredSize.Height),
            "the box changed height with the letters in it");
    }

    // ...and it grows with the size, rather than being a constant somebody guessed.
    [Test]
    public void ALineGrowsWithTheSize()
    {
        Assert.That(Line("Dots", 28).DesiredSize.Height, Is.GreaterThan(Line("Dots", 14).DesiredSize.Height));
    }

    // A LINE IS AS TALL AS IT IS, whether or not it was given a width to fit into. Measured against a slot it measured
    // one height; measured with room to spare, another - and a box laid out from the first holds glyphs sized by the
    // second, which is text sitting low in its own field.
    [Test]
    public void AWidthToFitIntoDoesNotChangeTheHeightOfALine()
    {
        var block = new TextBlock
        {
            Text = "Dots",
            FontSize = 14,
            TextTrimming = Adamantium.Graphics.Fonts.TextTrimming.CharEllipses
        };

        block.Measure(new Size(60, double.PositiveInfinity), force: true);
        var fitted = block.DesiredSize.Height;

        block.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity), force: true);
        var free = block.DesiredSize.Height;

        Assert.That(fitted, Is.EqualTo(free), $"fitted into a slot it is {fitted} tall, with room it is {free}");
    }

    // A SLOT THAT GROWS gives what is in it room to grow. Measured once into a slot too short for it, a label kept that
    // height for ever after - it is re-measured against the constraint it was FIRST given, and nothing re-offers the
    // bigger one - so a field that started narrow held a squashed line of text with its glyphs spilling out of it.
    [Test]
    public void ALabelMeasuredIntoAShortSlotGrowsWhenTheSlotDoes()
    {
        var block = new TextBlock { Text = "Dots", FontSize = 14 };
        var around = new Adamantium.UI.Controls.Decorators.Border { Height = 13, Child = block };
        var window = new Adamantium.UI.Controls.Window { Width = 400, Height = 200, Content = around };

        Settle(window);
        var squashed = block.DesiredSize.Height;

        around.Height = 40;
        Settle(window);

        Assert.That(block.DesiredSize.Height, Is.GreaterThan(squashed),
            $"the label stayed {squashed} tall after its slot grew");
    }

    // A STAR ROW IS WHAT IS LEFT, and what is in it is measured against that - not against something smaller. A
    // drop-down's own template is a star row over a hairline rule: measured short, the line of text inside it came out
    // 13 pixels tall in a box the control then arranged to 24, and the glyphs spilled out of it.
    [Test]
    public void AStarRowMeasuresItsChildAgainstWhatIsLeft()
    {
        var block = new TextBlock { Text = "Dots", FontSize = 14 };
        var rule = new Adamantium.UI.Controls.Decorators.Border { Height = 1 };

        var grid = new Adamantium.UI.Controls.Panels.Grid();
        grid.RowDefinitions.Add(new Adamantium.UI.Controls.Panels.RowDefinition { Height = Adamantium.UI.Controls.Panels.GridLength.Star });
        grid.RowDefinitions.Add(new Adamantium.UI.Controls.Panels.RowDefinition { Height = Adamantium.UI.Controls.Panels.GridLength.Auto });
        grid.Children.Add(block);
        grid.Children.Add(rule);
        Adamantium.UI.Controls.Panels.Grid.SetRow(rule, 1);

        grid.Measure(new Size(200, 24), force: true);

        Assert.That(block.DesiredSize.Height, Is.EqualTo(19).Within(1),
            $"the line measured {block.DesiredSize.Height} in a row that had 23 to give");
    }

    // A LABEL A PRESENTER MADE takes the size that reaches it LATER too. The words in a drop-down's closed box are one
    // of those, and they kept the box they were first measured with: measured at 13 pixels while a fresh measure of the
    // very same block said 16, so the glyphs were drawn at one size into a box laid out for another.
    [Test]
    public void AGeneratedLabelIsMeasuredAgainWhenTheSizeAroundItChanges()
    {
        var presenter = new ContentPresenter { Content = "Dots" };
        var around = new Adamantium.UI.Controls.Decorators.Border { FontSize = 10, Child = presenter };
        var window = new Adamantium.UI.Controls.Window { Width = 400, Height = 200, Content = around };

        Settle(window);

        var words = Words(presenter);

        Assert.That(words, Is.Not.Null, "the presenter made no label, so this proves nothing");

        var small = words.DesiredSize.Height;

        around.FontSize = 20;
        Settle(window);

        Assert.That(words.DesiredSize.Height, Is.GreaterThan(small),
            "the label kept the box it was first measured with");
    }

    private static TextBlock Words(IUIComponent from)
    {
        if (from is TextBlock words) return words;

        foreach (var child in from.VisualChildren)
        {
            if (child is IUIComponent inside && Words(inside) is { } found) return found;
        }

        return null;
    }

    // ASKING FOR AN ELLIPSIS must not change how TALL a line is. Trimming is about what happens at the right-hand end;
    // a line that measured shorter for it has its glyphs drawn into a box that no longer fits them, and they read as
    // sitting low in their own field.
    [Test]
    public void TrimmingDoesNotChangeTheHeightOfALine()
    {
        var plain = new TextBlock { Text = "Dots", FontSize = 14 };
        var trimmed = new TextBlock { Text = "Dots", FontSize = 14, TextTrimming = Adamantium.Graphics.Fonts.TextTrimming.CharEllipses };

        var around = new Adamantium.UI.Controls.Panels.StackPanel { Width = 120 };
        around.Children.Add(plain);
        around.Children.Add(trimmed);

        var window = new Adamantium.UI.Controls.Window { Width = 400, Height = 200, Content = around };
        Settle(window);

        Assert.That(trimmed.DesiredSize.Height, Is.EqualTo(plain.DesiredSize.Height),
            "a line asked to trim measured a different height");
    }

    // ...INCLUDING a size that arrives from an ancestor, which is how most labels get theirs.
    [Test]
    public void ALineTakesTheSizeItInherits()
    {
        var block = new TextBlock { Text = "Dots" };
        var around = new Adamantium.UI.Controls.Decorators.Border { FontSize = 12, Child = block };
        var window = new Adamantium.UI.Controls.Window { Width = 400, Height = 200, Content = around };

        // Through the ORDINARY passes and not a forced measure: a forced one re-measures whatever the state of the
        // dirty flags, which is exactly the thing being asked about.
        Settle(window);
        var small = block.DesiredSize.Height;

        around.FontSize = 28;
        Settle(window);

        Assert.That(block.DesiredSize.Height, Is.GreaterThan(small),
            "the label kept the box it was measured with when its size came from around it");
    }
}
