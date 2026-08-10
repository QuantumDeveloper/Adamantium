using System.Linq;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Templates;
using Adamantium.UI.Extensions;
using NUnit.Framework;

namespace Adamantium.UITests;

// A command that is not a button hands the bar its own compact form; everything else is drawn by the bar's default.
[TestFixture]
public class RibbonQuickAccessTemplateTests
{
    private class Item : IQuickAccessItem
    {
        public DataTemplate QuickAccessTemplate { get; set; }
    }

    private static RibbonQuickAccess Bar(params object[] items)
    {
        var bar = new RibbonQuickAccess
        {
            // Outside a TitleBar the bar shows itself only in this slot; collapsed, it would never measure and its
            // template - and with it every container - would never be built.
            Placement = RibbonQuickAccessPlacement.BelowRibbon,
            ItemsSource = items,
            ItemTemplateSelector = new RibbonQuickAccessTemplateSelector
            {
                Default = new DataTemplate(() => new TemplateResult { RootComponent = new Button { Width = 28, Height = 26 } })
            },
            // No theme in a test, so the bar has no template - and with no PART_ItemsPresenter nothing is ever generated.
            Template = new ControlTemplate(() =>
            {
                var presenter = new ItemsPresenter();
                var result = new TemplateResult { RootComponent = presenter };
                result.RegisterName("PART_ItemsPresenter", presenter);
                return result;
            })
        };

        var window = new Window { Width = 400, Height = 120, Content = new Border { Child = bar } };
        for (var i = 0; i < 5; i++) WindowExtension.UpdateTree(window);

        return bar;
    }

    private static T Built<T>(IUIComponent container) where T : class
    {
        if (container is T match) return match;

        return container.VisualChildren.Select(Built<T>).FirstOrDefault(x => x != null);
    }

    [Test]
    public void AnItemWithNoFormOfItsOwnGetsTheDefault()
    {
        var bar = Bar(new Item());
        var container = bar.ItemContainerGenerator.ContainerFromIndex(0);

        Assert.That(Built<Button>(container), Is.Not.Null);
    }

    [Test]
    public void AnItemThatBroughtItsOwnFormIsDrawnByIt()
    {
        var item = new Item
        {
            QuickAccessTemplate = new DataTemplate(() => new TemplateResult { RootComponent = new Slider { Width = 80 } })
        };

        var bar = Bar(item);
        var container = bar.ItemContainerGenerator.ContainerFromIndex(0);

        Assert.That(Built<Slider>(container), Is.Not.Null, "the command's own compact form must win over the default");
        Assert.That(Built<Button>(container), Is.Null, "and the default must not be built as well");
    }
}
