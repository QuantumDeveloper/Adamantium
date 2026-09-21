using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Templates;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>The strip has no arrows - it pans on the wheel - so the fade at its edge is the only thing saying there are
/// tabs past it. The state has to reach the CONTROL: a trigger watches the templated control, and the fade must be a
/// sibling over the scroller (inside it, it would pan away with the tabs it is dissolving).</summary>
[TestFixture]
public class TabStripEdgeFadeTests
{
    private static TabControl Strip(int tabs, double width)
    {
        var control = new TabControl();
        for (var i = 0; i < tabs; i++)
        {
            control.Items.Add(new TabItem { Header = $"tab {i}", Width = 120, MinWidth = 120, MaxWidth = 120 });
        }

        // The theme's own strip panel. Without it the default (a vertical stack) would leave a horizontal strip nothing
        // to overflow.
        control.ItemsPanel = new ItemsPanelTemplate(() =>
            new TemplateResult { RootComponent = new TabPanel { Orientation = Orientation.Horizontal } });

        control.Template = new ControlTemplate(() =>
        {
            var scroller = new TabStripScroller { Orientation = Orientation.Horizontal };
            var presenter = new ItemsPresenter();
            scroller.Child = presenter;

            var result = new TemplateResult { RootComponent = scroller };
            result.RegisterName("PART_TabStrip", scroller);
            result.RegisterName("PART_ItemsPresenter", presenter);
            return result;
        });

        ((IMeasurableComponent)control).Measure(new Size(width, 40));
        ((IMeasurableComponent)control).Arrange(new Rect(0, 0, width, 40));
        return control;
    }

    [Test]
    public void AStripThatFitsFadesNeitherEdge()
    {
        var control = Strip(2, 400);

        Assert.Multiple(() =>
        {
            Assert.That(control.CanScrollTabsBack, Is.False);
            Assert.That(control.CanScrollTabsForward, Is.False);
        });
    }

    [Test]
    public void AnOverflowingStripFadesTheFarEdge()
    {
        var control = Strip(6, 300);

        Assert.Multiple(() =>
        {
            Assert.That(control.CanScrollTabsForward, Is.True, "the control never learned its strip overflows");
            Assert.That(control.CanScrollTabsBack, Is.False, "nothing has gone past the near edge yet");
        });
    }
}
