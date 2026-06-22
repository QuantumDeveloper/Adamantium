using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// Pins the WPF arrange rule: an element with a non-Stretch alignment and an Auto (NaN) size shrinks to its content's
/// DesiredSize instead of filling the slot its parent handed it. A Stretch element still fills. (The previous code
/// clamped the arrange size to finalRect - a no-op - so a Left/Top-aligned Auto element wrongly stretched full-size.)
/// </summary>
[TestFixture]
public class LayoutAlignmentTests
{
    // Reports a fixed desired (content) size; uses the base ArrangeOverride which returns the arrange size as-is, so
    // the alignment clamp in ArrangeCore is what determines the final size.
    private sealed class FixedContent : MeasurableUIComponent
    {
        protected override Size MeasureOverride(Size availableSize) => new Size(100, 50);
    }

    [Test]
    public void AutoSize_NonStretch_ShrinksToContent()
    {
        var c = new FixedContent
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };

        c.Measure(new Size(1280, 720));
        Assert.That(c.DesiredSize, Is.EqualTo(new Size(100, 50)));

        c.Arrange(new Rect(0, 0, 1280, 720));

        Assert.Multiple(() =>
        {
            Assert.That(c.Bounds.Width, Is.EqualTo(100), "non-Stretch Auto width should shrink to content, not fill the parent");
            Assert.That(c.Bounds.Height, Is.EqualTo(50), "non-Stretch Auto height should shrink to content, not fill the parent");
        });
    }

    [Test]
    public void AutoSize_Stretch_FillsParent()
    {
        var c = new FixedContent
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        c.Measure(new Size(1280, 720));
        c.Arrange(new Rect(0, 0, 1280, 720));

        Assert.Multiple(() =>
        {
            Assert.That(c.Bounds.Width, Is.EqualTo(1280), "Stretch should still fill the parent width");
            Assert.That(c.Bounds.Height, Is.EqualTo(720), "Stretch should still fill the parent height");
        });
    }
}
