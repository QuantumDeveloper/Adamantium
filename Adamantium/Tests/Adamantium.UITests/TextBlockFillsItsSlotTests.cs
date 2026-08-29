using Adamantium.Graphics.Fonts;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using Adamantium.UI.Extensions;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// A TextBlock takes the WHOLE SLOT it is arranged into, as WPF's does - it does not shrink back to its ink.
/// <para>Returning the text's own size instead quietly disabled both text alignments: a block arranged at its ink sits
/// against the top-left of the slot, so "centre the text" centred it inside a box that was itself against the edge.
/// What that looked like was every label in every list row sitting a few pixels high - reported by eye, and impossible
/// to correct from any theme, because alignment can only place text WITHIN the box the block was given.</para>
/// </summary>
[TestFixture]
public class TextBlockFillsItsSlotTests
{
    private static (TextBlock text, Border host) Hosted(double width, double height)
    {
        var text = new TextBlock { Text = "Saturn", VerticalTextAlignment = VerticalTextAlignment.Center };
        var host = new Border { Width = width, Height = height, Child = text };

        var window = new Window { Width = 400, Height = 300, Content = host };
        for (var i = 0; i < 5; i++) WindowExtension.UpdateTree(window);

        return (text, host);
    }

    /// <summary>The slot, not the ink. Everything else here follows from this one answer.</summary>
    [Test]
    public void ItTakesTheSlotItWasGiven()
    {
        var (text, _) = Hosted(200, 40);

        Assert.Multiple(() =>
        {
            Assert.That(text.Bounds.Width, Is.EqualTo(200).Within(0.5),
                "shrunk back to the ink, so horizontal text alignment has nothing to work across");
            Assert.That(text.Bounds.Height, Is.EqualTo(40).Within(0.5),
                "shrunk back to the ink, so the text is pinned to the top of the row");
        });
    }

    /// <summary>MEASURE still answers with the ink - that is what a container asks when deciding how much room to give.
    /// Confusing the two would make every label claim whatever it was last arranged into.</summary>
    [Test]
    public void ButItStillMeasuresAsItsInk()
    {
        var text = new TextBlock { Text = "Saturn" };
        text.Measure(new Size(500, 500));

        Assert.Multiple(() =>
        {
            Assert.That(text.DesiredSize.Width, Is.LessThan(200), "a label must not ask for the room it was offered");
            Assert.That(text.DesiredSize.Height, Is.LessThan(60));
        });
    }

    /// <summary>And a block given exactly its own size is unchanged - the fix is about the surplus, not about growing
    /// anything.</summary>
    [Test]
    public void ATightSlotIsLeftAlone()
    {
        var text = new TextBlock { Text = "Saturn" };
        text.Measure(new Size(500, 500));
        var ink = text.DesiredSize;

        var (hosted, _) = Hosted(ink.Width, ink.Height);

        Assert.Multiple(() =>
        {
            Assert.That(hosted.Bounds.Width, Is.EqualTo(ink.Width).Within(0.5));
            Assert.That(hosted.Bounds.Height, Is.EqualTo(ink.Height).Within(0.5));
        });
    }
}
