using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.Resources.Triggers;
using Adamantium.UI.Core.Templates;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// Minimizing the ribbon must give the band's ROW back, not just empty it. The theme states that as a trigger on the
/// ribbon that collapses the named band part, and the band carries an explicit Height inside an Auto row - so "the row
/// goes away" rests on a collapsed element with a stated height contributing nothing to its parent.
/// <para>Written after the band stayed full height while its content moved into the flyout: an empty strip of nothing
/// where the groups had been.</para>
/// </summary>
[TestFixture]
public class RibbonBandCollapseTests
{
    private const double BandHeight = 100;
    private const double StripHeight = 30;

    private static Border _band;

    // The ribbon's template reduced to what this question needs: a strip, and the band in its own Auto row with the
    // height the theme gives it.
    private static ControlTemplate BandTemplate() => new(() =>
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0, GridUnitType.Auto) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0, GridUnitType.Auto) });

        var strip = new Border { Height = StripHeight };
        _band = new Border { Height = BandHeight };
        Grid.SetRow(strip, 0);
        Grid.SetRow(_band, 1);
        grid.Children.Add(strip);
        grid.Children.Add(_band);

        var result = new TemplateResult { RootComponent = grid };
        result.RegisterName("Band", _band);
        return result;
    });

    private static Border Band(out Ribbon host)
    {
        host = new Ribbon();

        var style = new Style();
        style.Selector.Types.Add(typeof(Ribbon));
        var trigger = new PropertyTrigger { Property = "IsMinimized", Value = true };
        trigger.Add(new Setter { TargetName = "Band", Property = "Visibility", Value = Visibility.Collapsed });
        style.Triggers.Add(trigger);
        style.Attach(host);

        host.Template = BandTemplate();
        host.Measure(new Size(800, 600));
        host.Arrange(new Rect(0, 0, 800, 600));
        return _band;
    }

    [Test]
    public void WithTheBandShowing_TheControlIsAsTallAsBoth()
    {
        var band = Band(out var host);

        Assert.That(band.Visibility, Is.EqualTo(Visibility.Visible));
        Assert.That(host.DesiredSize.Height, Is.EqualTo(StripHeight + BandHeight).Within(0.01));
    }

    [Test]
    public void CollapsingTheBand_GivesItsRowBack()
    {
        var band = Band(out var host);

        host.IsMinimized = true;

        Assert.That(band.Visibility, Is.EqualTo(Visibility.Collapsed), "the trigger has to reach the named part at all");
        Assert.That(((Grid)band.VisualParent).IsMeasureValid, Is.False,
            "the row that held the band has to be re-measured - nothing else will notice the band stopped asking for height");

        host.Measure(new Size(800, 600));
        Assert.That(host.DesiredSize.Height, Is.EqualTo(StripHeight).Within(0.01),
            "a collapsed band takes no room, whatever Height it states - the row must go, not just empty out");
    }

    [Test]
    public void BringingTheBandBack_RestoresTheRow()
    {
        var band = Band(out var host);
        host.IsMinimized = true;
        host.Measure(new Size(800, 600));

        host.IsMinimized = false;

        Assert.That(band.Visibility, Is.EqualTo(Visibility.Visible), "the trigger must undo itself");
        host.Measure(new Size(800, 600));
        Assert.That(host.DesiredSize.Height, Is.EqualTo(StripHeight + BandHeight).Within(0.01));
    }
}
