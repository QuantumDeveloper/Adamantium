using Adamantium.Mathematics;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>WHAT A PIECE OF TEXT COVERS ON THE PLANE. Its size is in WORLD units - typed at 1000x, a line is a
/// thousandth of the size it looks - and the box round it is what the selection frame and every hit-test are drawn
/// from. A box that does not shrink with the letters is a frame the size of the sky round a word.</summary>
[TestFixture]
public class CanvasTextBoxTests
{
    private static TextItem Said(double fontSize) =>
        new(new Vector2(0, 0), "Hello", Brushes.Black, fontSize);

    // ...AND THE FRAME CAN MAKE IT SMALLER. Text typed at 1000x is a hundredth of a world unit tall, and a floor
    // stated in world units stopped it a third of the way down - at that zoom the floor IS the text.
    [TestCase(16.0)]
    [TestCase(0.0144)]
    public void TheFrameCanMakeTextSmaller(double fontSize)
    {
        var text = Said(fontSize);
        var was = text.Bounds;

        text.Resize(new Rect(was.X, was.Y, was.Width / 4, was.Height / 4));

        Assert.That(text.FontSize, Is.EqualTo(fontSize / 4).Within(fontSize / 4 * 0.05),
            $"at {fontSize} it would go no smaller than {text.FontSize}");
    }

    // THE BOX FOLLOWS THE LETTERS, at any size. Ten times smaller text covers ten times less plane - anything else is
    // a frame that has nothing to do with what it is round.
    [TestCase(16.0, 1.6)]
    [TestCase(16.0, 0.16)]
    [TestCase(16.0, 0.016)]
    public void TheBoxShrinksWithTheLetters(double big, double small)
    {
        var whole = Said(big).Bounds;

        Assert.That(whole.Width, Is.GreaterThan(0), "the measuring layout said nothing at all");

        var little = Said(small).Bounds;
        var times = big / small;

        Assert.Multiple(() =>
        {
            Assert.That(little.Width, Is.EqualTo(whole.Width / times).Within(whole.Width / times * 0.05),
                $"at {small} the box is {little.Width / (whole.Width / times):0.#} times too wide");
            Assert.That(little.Height, Is.EqualTo(whole.Height / times).Within(whole.Height / times * 0.05),
                $"at {small} the box is {little.Height / (whole.Height / times):0.#} times too tall");
        });
    }
}
