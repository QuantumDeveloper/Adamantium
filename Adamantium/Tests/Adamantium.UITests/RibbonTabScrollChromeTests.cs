using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Templates;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>The other half of the band scroll: the panel knows it has run out of room, but the ARROWS belong to the
/// tab - the panel is built by the ItemsPanelTemplate and sits inside the tab's items presenter, where a template
/// cannot reach it. If the tab never finds its row, the panel scrolls perfectly and nothing is ever shown.</summary>
[TestFixture]
public class RibbonTabScrollChromeTests
{
    private static RibbonTab Tab(int groups, double width)
    {
        var tab = new RibbonTab();
        for (var i = 0; i < groups; i++)
        {
            tab.Items.Add(new RibbonGroup { Header = "G", Width = 100, MinWidth = 100, MaxWidth = 100 });
        }

        tab.ItemsPanel = new ItemsPanelTemplate(() => new TemplateResult { RootComponent = new RibbonGroupsPanel() });
        tab.Template = new ControlTemplate(() =>
        {
            var presenter = new ItemsPresenter();
            var result = new TemplateResult { RootComponent = presenter };
            result.RegisterName("PART_ItemsPresenter", presenter);
            return result;
        });

        ((IMeasurableComponent)tab).Measure(new Size(width, 100));
        ((IMeasurableComponent)tab).Arrange(new Rect(0, 0, width, 100));
        return tab;
    }

    [Test]
    public void ATabWithRoomShowsNoArrows()
    {
        var tab = Tab(2, 400);

        Assert.Multiple(() =>
        {
            Assert.That(tab.CanScrollBack, Is.False);
            Assert.That(tab.CanScrollForward, Is.False);
        });
    }

    [Test]
    public void ATabOutOfRoomOffersTheWayForward()
    {
        var tab = Tab(6, 300);

        Assert.That(tab.CanScrollForward, Is.True, "the tab never learned its row had run out of room");
    }
}
