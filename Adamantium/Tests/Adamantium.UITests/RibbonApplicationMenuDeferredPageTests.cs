using System.Linq;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Templates;
using Adamantium.UI.Extensions;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>The application menu's page - backstage or dropped card - lives in the popup's ChildTemplate, built on FIRST
/// OPEN. It is the heaviest thing the theme declares: a window-sized page, a rail, and a row per command, all of it paid
/// for by an application whose File page is never asked for. Two things the deferral could break, and both are guarded
/// here: the parts (back button, items host) are no longer in the owner's namescope, and the page's own bindings can no
/// longer be {TemplateBinding} - they reach the menu as a LOGICAL ancestor, across the popup boundary.</summary>
[TestFixture]
public class RibbonApplicationMenuDeferredPageTests
{
    // The theme's shape, cut down: the File button, and the page deferred behind ChildTemplate. The rail's width is bound
    // the way the theme binds it, so what this proves about {Ancestor} is what the theme relies on.
    private static ControlTemplate DeferredPageTemplate() => new(() =>
    {
        var popup = new Popup
        {
            ChildTemplate = new ControlTemplate(() =>
            {
                var back = new Button();
                var presenter = new ItemsPresenter();
                var rail = new Border { Child = presenter };
                new Ancestor { AncestorType = typeof(RibbonApplicationMenu), Path = "RailWidth", Logical = true }
                    .Apply(rail, nameof(Border.Width));

                var grid = new Grid();
                grid.Children.Add(back);
                grid.Children.Add(rail);

                var inner = new TemplateResult { RootComponent = grid };
                inner.RegisterName("PART_BackButton", back);
                inner.RegisterName("PART_ItemsPresenter", presenter);
                inner.RegisterName("Rail", rail);
                return inner;
            })
        };

        var button = new ToggleButton();
        var root = new Grid();
        root.Children.Add(button);
        root.Children.Add(popup);

        var result = new TemplateResult { RootComponent = root };
        result.RegisterName("PART_Button", button);
        result.RegisterName("PART_Popup", popup);
        return result;
    });

    private static (RibbonApplicationMenu menu, Window window) Hosted()
    {
        var menu = new RibbonApplicationMenu { RailWidth = 220, Template = DeferredPageTemplate() };
        menu.Items.Add(new RibbonApplicationMenuItem { Content = "New", PageContent = "new page" });
        menu.Items.Add(new RibbonApplicationMenuItem { Content = "Open", PageContent = "open page" });
        menu.Items.Add(new RibbonApplicationMenuItem { Content = "Save" });

        var window = new Window { Width = 800, Height = 600, Content = menu };
        for (var i = 0; i < 5; i++) WindowExtension.UpdateTree(window);

        return (menu, window);
    }

    private static Popup PopupOf(RibbonApplicationMenu menu) => menu.GetTemplateChild("PART_Popup") as Popup;

    private static void Open(RibbonApplicationMenu menu, Window window)
    {
        menu.IsOpen = true;
        for (var i = 0; i < 5; i++) WindowExtension.UpdateTree(window);
    }

    [Test]
    public void AnApplicationThatNeverOpenedItsFilePageBuildsNone()
    {
        var (menu, _) = Hosted();

        Assert.That(PopupOf(menu).Child, Is.Null,
            "the backstage is the heaviest page in the theme - nothing may build it until it is asked for");
    }

    // The items host arrives with the page, so the rows exist only after the open. Miss that and the backstage opens with
    // an empty rail.
    [Test]
    public void OpeningBuildsTheRowsInTheDeferredItemsHost()
    {
        var (menu, window) = Hosted();

        Open(menu, window);

        var presenter = PopupOf(menu).FindContentChild("PART_ItemsPresenter") as ItemsPresenter;
        Assert.That(presenter, Is.Not.Null, "the deferred page has to carry the items host");
        Assert.That(presenter.VisualChildren.FirstOrDefault(), Is.InstanceOf<Panel>(),
            "an items host only builds its panel once it has an owner - no panel means it was never connected");
    }

    // The page cannot use {TemplateBinding} any more (a ChildTemplate is built against the POPUP), so the theme reaches
    // the menu as a LOGICAL ancestor across the popup boundary. If that walk does not arrive, the rail loses its width
    // and the page comes up misshapen - silently, since a binding that resolves to nothing says nothing.
    [Test]
    public void ThePagesAncestorBindingReachesTheMenuAcrossThePopup()
    {
        var (menu, window) = Hosted();

        Open(menu, window);

        var rail = PopupOf(menu).FindContentChild("Rail") as Border;
        Assert.That(rail.Width, Is.EqualTo(220), "the rail's width must come from the menu it belongs to");

        menu.RailWidth = 260;
        for (var i = 0; i < 3; i++) WindowExtension.UpdateTree(window);
        Assert.That(rail.Width, Is.EqualTo(260), "and stay live - the binding is not a one-off copy");
    }

    // The back button is the way OUT of a backstage (there is no outside to press), and it too arrives with the page.
    [Test]
    public void TheDeferredBackButtonClosesTheMenu()
    {
        var (menu, window) = Hosted();
        Open(menu, window);

        var back = PopupOf(menu).FindContentChild("PART_BackButton") as Button;
        Assert.That(back, Is.Not.Null, "the deferred page has to carry the way out of it");

        back.PerformClick();
        for (var i = 0; i < 3; i++) WindowExtension.UpdateTree(window);

        Assert.That(menu.IsOpen, Is.False, "the back button must be wired where it is found");
    }
}
