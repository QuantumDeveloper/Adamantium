using System.Linq;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core.Templates;
using Adamantium.UI.Extensions;
using NUnit.Framework;

namespace Adamantium.UITests;

// The list card lives in the popup's ChildTemplate, so it is built on FIRST OPEN and not in every drop-down's template -
// a closed list otherwise costs a card, a scroll host and an items host that most lists never show. That puts
// PART_ItemsPresenter out of OnApplyTemplate's reach, so the control has to connect the items host when the content
// arrives instead; an unconnected host has no owner and would leave the list permanently empty.
[TestFixture]
public class DropDownDeferredListTests
{
    private static ControlTemplate DeferredListTemplate() => new(() =>
    {
        var popup = new Popup
        {
            ChildTemplate = new ControlTemplate(() =>
            {
                var presenter = new ItemsPresenter();
                var inner = new TemplateResult { RootComponent = new Border { Child = presenter } };
                inner.RegisterName("PART_ItemsPresenter", presenter);
                return inner;
            })
        };

        var grid = new Grid();
        grid.Children.Add(new Border());
        grid.Children.Add(popup);

        var result = new TemplateResult { RootComponent = grid };
        result.RegisterName("PART_Popup", popup);
        return result;
    });

    private static (DropDown drop, Window window) Hosted()
    {
        var drop = new DropDown { Width = 120, Height = 26, Template = DeferredListTemplate() };
        drop.Items.Add(new DropDownItem { Content = "One" });
        drop.Items.Add(new DropDownItem { Content = "Two" });

        var window = new Window { Width = 300, Height = 200, Content = drop };
        for (var i = 0; i < 5; i++) WindowExtension.UpdateTree(window);

        return (drop, window);
    }

    private static Popup PopupOf(DropDown drop) => drop.GetTemplateChild("PART_Popup") as Popup;

    [Test]
    public void AClosedListBuildsNothing()
    {
        var (drop, _) = Hosted();

        Assert.That(PopupOf(drop).Child, Is.Null,
            "a closed drop-down must not pay to build the list it is not showing");
    }

    [Test]
    public void OpeningBuildsTheList()
    {
        var (drop, _) = Hosted();

        drop.IsDropDownOpen = true;

        Assert.That(PopupOf(drop).Child, Is.Not.Null);
    }

    // The part that the deferral puts at risk: an items host that arrives after OnApplyTemplate has to be connected to
    // its control, or it stays ownerless and never grows an items panel - a list that opens onto nothing.
    [Test]
    public void OpeningConnectsTheItemsHostToTheControl()
    {
        var (drop, _) = Hosted();

        drop.IsDropDownOpen = true;

        var presenter = PopupOf(drop).FindContentChild("PART_ItemsPresenter") as ItemsPresenter;
        Assert.That(presenter, Is.Not.Null, "the deferred content has to carry the items host");
        Assert.That(presenter.VisualChildren.FirstOrDefault(), Is.InstanceOf<Panel>(),
            "an items host only builds its panel once it has an owner - no panel means it was never connected");
    }
}
