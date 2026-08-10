using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>What a presenter reports has to follow what its content actually became. The fast path that skips re-walking
/// an unchanged subtree may only fire while the subtree really is unchanged.</summary>
[TestFixture]
public class ContentPresenterGrowthTests
{
    // The content grew on its own - re-measured by the layout pass before its presenter's turn came, so it is VALID again
    // and a different size. A presenter that reads "valid" as "the size I last saw" reports the old one, and everything
    // laid out beside it stays where it was. That is the caption: a quick-access bar gains a button and the window title
    // never steps aside.
    [Test]
    public void ContentThatGrewWhileValid_IsNotReportedAtItsOldSize()
    {
        var content = new Border { Width = 50, Height = 20 };
        var presenter = new ContentPresenter { Content = content };

        presenter.Measure(new Size(double.PositiveInfinity, 38));
        Assert.That(presenter.DesiredSize.Width, Is.EqualTo(50), "precondition");

        // Grown and re-measured on its own, exactly as the layout manager does before reaching the presenter.
        content.Width = 80;
        content.Measure(new Size(double.PositiveInfinity, 38));
        Assert.That(content.IsMeasureValid, Is.True, "precondition: the content is valid again, at its NEW size");

        // ...and now the presenter's turn, at the SAME constraint it had before.
        presenter.InvalidateMeasure();
        presenter.Measure(new Size(double.PositiveInfinity, 38));

        Assert.That(presenter.DesiredSize.Width, Is.EqualTo(80));
    }

    private sealed class CountingBorder : Border
    {
        public int Measures;

        protected override Size MeasureOverride(Size availableSize)
        {
            Measures++;
            return base.MeasureOverride(availableSize);
        }
    }

    // The optimisation itself must survive: an untouched subtree is not re-walked. Without this the fix would be "measure
    // everything, always", which is the cost the fast path exists to avoid.
    [Test]
    public void ContentThatDidNotChange_IsStillNotReWalked()
    {
        var content = new CountingBorder { Width = 50, Height = 20 };
        var presenter = new ContentPresenter { Content = content };

        presenter.Measure(new Size(double.PositiveInfinity, 38));
        var walks = content.Measures;

        presenter.InvalidateMeasure();
        presenter.Measure(new Size(double.PositiveInfinity, 38));

        Assert.That(content.Measures, Is.EqualTo(walks),
            "nothing changed underneath, so the subtree must not be measured again");
    }
}
