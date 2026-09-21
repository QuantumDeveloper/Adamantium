using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Core.Templates;
using Adamantium.UI.Extensions;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>A context menu's card lives in the popup's ChildTemplate, built on FIRST OPEN. This is the deferral that pays
/// across the whole application rather than inside one control: ANY element may carry a context menu, and a right-click
/// menu is by nature something most elements never show - the ribbon alone hands one to every command. What it puts at
/// risk is everything the menu needs from that card: the items host it hangs the rows on, the click root that closes the
/// menu after a row runs, and the scroller it caps to the window. None of them are in the menu's own namescope any
/// more.</summary>
[TestFixture]
public class ContextMenuDeferredCardTests
{
    // The theme's shape, cut down to the parts the menu actually takes.
    private static ControlTemplate DeferredCardTemplate() => new(() =>
    {
        var popup = new Popup
        {
            ChildTemplate = new ControlTemplate(() =>
            {
                var presenter = new ItemsPresenter();
                var scroll = new MenuScrollViewer { Content = presenter };
                var inner = new TemplateResult { RootComponent = new Border { Child = scroll } };
                inner.RegisterName("PART_MenuScroll", scroll);
                inner.RegisterName("PART_ItemsPresenter", presenter);
                return inner;
            })
        };

        var grid = new Grid();
        grid.Children.Add(popup);

        var result = new TemplateResult { RootComponent = grid };
        result.RegisterName("PART_Popup", popup);
        return result;
    });

    private static (ContextMenu menu, Window window) Hosted()
    {
        var menu = new ContextMenu { Template = DeferredCardTemplate() };
        menu.Items.Add(new MenuItem { Header = "Add to quick access" });
        menu.Items.Add(new MenuItem { Header = "Remove" });

        var window = new Window { Width = 400, Height = 300, Content = menu };
        for (var i = 0; i < 5; i++) WindowExtension.UpdateTree(window);

        return (menu, window);
    }

    private static Popup PopupOf(ContextMenu menu) => menu.GetTemplateChild("PART_Popup") as Popup;

    private static ItemsPresenter Open(ContextMenu menu, Window window)
    {
        menu.IsOpen = true;
        for (var i = 0; i < 3; i++) WindowExtension.UpdateTree(window);
        return PopupOf(menu).FindContentChild("PART_ItemsPresenter") as ItemsPresenter;
    }

    // Nothing at all: not the card, and not the portal that would hold it either. A menu is shown nowhere where it
    // stands, so an element that carries one it is never right-clicked on pays for the menu OBJECT and no more.
    [Test]
    public void AMenuNobodyRightClickedBuildsNothing()
    {
        var (menu, _) = Hosted();

        Assert.That(PopupOf(menu), Is.Null,
            "an unopened menu must not build even its own template - every element may carry one");
    }

    [Test]
    public void OpeningBuildsTheMenu()
    {
        var (menu, window) = Hosted();

        Open(menu, window);

        Assert.That(PopupOf(menu), Is.Not.Null, "the template has to be built on the open that needs it");
        Assert.That(PopupOf(menu).Child, Is.Not.Null, "and the card with it");
    }

    // The FIRST open is the one that builds the portal, and it must still land at the cursor: a right-click states the
    // placement on the menu and only then opens it, so the popup built in between has to be given what the menu already
    // holds. Get that order wrong and the first right-click on any element drops its menu in the default spot.
    [Test]
    public void TheFirstOpenStillLandsAtTheCursor()
    {
        var (menu, window) = Hosted();
        var target = new Button { Width = 80, Height = 24 };

        menu.Open(target, new Vector2(37, 11));
        for (var i = 0; i < 3; i++) WindowExtension.UpdateTree(window);

        var popup = PopupOf(menu);
        Assert.That(popup.Placement, Is.EqualTo(PlacementMode.Relative));
        Assert.That(popup.HorizontalOffset, Is.EqualTo(37));
        Assert.That(popup.VerticalOffset, Is.EqualTo(11));
        Assert.That(popup.PlacementTarget, Is.SameAs(target));
    }

    // The items host arrives with the card, so it has to be connected then - an unconnected host grows no panel and the
    // menu opens onto nothing.
    [Test]
    public void OpeningConnectsTheDeferredItemsHost()
    {
        var (menu, window) = Hosted();

        var presenter = Open(menu, window);

        Assert.That(presenter, Is.Not.Null, "the deferred card has to carry the items host");
        Assert.That(presenter.VisualChildren.FirstOrDefault(), Is.InstanceOf<Panel>(),
            "an items host only builds its panel once it has an owner - no panel means it was never connected");
    }

    // A row's Click bubbles to the items presenter, and THAT is where the menu listens in order to close itself. The
    // handler is added where the presenter is found, so the deferral moved it: miss it and every menu stays up after the
    // command it ran.
    [Test]
    public void PickingARowStillClosesTheDeferredMenu()
    {
        var (menu, window) = Hosted();
        var presenter = Open(menu, window);

        ((IInputComponent)presenter).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        Assert.That(menu.IsOpen, Is.False, "a picked row must put the menu away");
    }
}
