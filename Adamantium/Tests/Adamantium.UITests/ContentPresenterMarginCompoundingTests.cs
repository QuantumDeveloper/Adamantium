using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>What <c>MeasureOverride</c> is allowed to return: the INNER size. <c>MeasureCore</c> adds the element's own
/// margin on top of it, so any fast path that hands back <c>DesiredSize</c> - which already carries that margin - adds
/// it a second time, and a third, once per re-measure.</summary>
[TestFixture]
public class ContentPresenterMarginCompoundingTests
{
    private static ContentPresenter Presenter(Thickness margin) =>
        new() { Content = "Clipboard", FontSize = 11, Margin = margin };

    // The size an element reports may not depend on how many times it has been asked.
    [Test]
    public void RemeasuringAPresenterWithAMargin_DoesNotGrowIt()
    {
        var presenter = Presenter(new Thickness(0, 4, 0, 0));

        presenter.Measure(Size.Infinity);
        var first = presenter.DesiredSize;

        for (var i = 0; i < 5; i++)
        {
            presenter.InvalidateMeasure();
            presenter.Measure(Size.Infinity);
        }

        Assert.That(presenter.DesiredSize, Is.EqualTo(first),
            $"grew by {presenter.DesiredSize.Height - first.Height} over five re-measures");
    }

    // The skip is only reached on a re-measure at the SAME constraint - which is exactly the case a re-opened tab hits.
    [Test]
    public void TheMarginIsCountedOnce_NotOncePerMeasure()
    {
        var bare = Presenter(new Thickness(0));
        var withMargin = Presenter(new Thickness(0, 4, 0, 0));

        foreach (var p in new[] { bare, withMargin })
        {
            for (var i = 0; i < 4; i++)
            {
                p.InvalidateMeasure();
                p.Measure(Size.Infinity);
            }
        }

        Assert.That(withMargin.DesiredSize.Height - bare.DesiredSize.Height, Is.EqualTo(4),
            "one margin's worth taller than the same content without it, however often either was measured");
    }
}
