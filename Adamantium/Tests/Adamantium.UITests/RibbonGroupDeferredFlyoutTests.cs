using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core.Templates;
using Adamantium.UI.Extensions;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>A collapsed group shows its commands in a flyout, and that flyout's card lives in the popup's ChildTemplate -
/// built on FIRST OPEN, not with every group's template. A band carries a couple of dozen groups and hardly any are ever
/// collapsed, let alone opened, so the card is pure cost until then. What the deferral puts at risk is the part that makes
/// the flyout work at all: the group MOVES its content into PART_PopupHost, and that host is not in the template's
/// namescope any more - it arrives with the content. Miss that and a collapsed group opens onto an empty card.</summary>
[TestFixture]
public class RibbonGroupDeferredFlyoutTests
{
    // The theme's shape, cut down to what this is about: the commands sit in PART_Content (the thing the group MOVES),
    // the inline host holds it while the group is open, and the flyout's card is deferred behind ChildTemplate.
    private static ControlTemplate DeferredFlyoutTemplate() => new(() =>
    {
        var popup = new Popup
        {
            ChildTemplate = new ControlTemplate(() =>
            {
                var host = new Border();
                var inner = new TemplateResult { RootComponent = new Border { Child = host } };
                inner.RegisterName("PART_PopupHost", host);
                return inner;
            })
        };

        var presenter = new ItemsPresenter();
        var inlineHost = new Border { Child = presenter };
        var collapsedButton = new ToggleButton();

        var grid = new Grid();
        grid.Children.Add(inlineHost);
        grid.Children.Add(collapsedButton);
        grid.Children.Add(popup);

        var result = new TemplateResult { RootComponent = grid };
        result.RegisterName("PART_InlineHost", inlineHost);
        result.RegisterName("PART_Content", presenter);
        result.RegisterName("PART_ItemsPresenter", presenter);
        result.RegisterName("PART_CollapsedButton", collapsedButton);
        result.RegisterName("PART_Popup", popup);
        return result;
    });

    private static (RibbonGroup group, Window window) Hosted()
    {
        var group = new RibbonGroup
        {
            Width = 160,
            Height = 90,
            ItemsPanel = new ItemsPanelTemplate(() => new TemplateResult { RootComponent = new RibbonGroupPanel() }),
            Template = DeferredFlyoutTemplate()
        };
        group.Items.Add(new Button { Content = "One" });
        group.Items.Add(new Button { Content = "Two" });

        var window = new Window { Width = 400, Height = 200, Content = group };
        for (var i = 0; i < 5; i++) WindowExtension.UpdateTree(window);

        return (group, window);
    }

    // Collapsing is not settable - a group collapses by taking its last variant, which is always the collapsed one.
    private static void Collapse(RibbonGroup group, Window window)
    {
        group.ApplyVariant(group.Variants.Count - 1);
        for (var i = 0; i < 3; i++) WindowExtension.UpdateTree(window);
    }

    private static Popup PopupOf(RibbonGroup group) => group.GetTemplateChild("PART_Popup") as Popup;

    [Test]
    public void AGroupThatWasNeverOpenedBuildsNoCard()
    {
        var (group, _) = Hosted();

        Assert.That(PopupOf(group).Child, Is.Null,
            "a band of groups must not each pay for a flyout nobody has asked for");
    }

    [Test]
    public void CollapsingAloneStillBuildsNoCard()
    {
        var (group, window) = Hosted();

        Collapse(group, window);

        Assert.That(group.IsCollapsed, Is.True, "the last variant must be the collapsed one, or this proves nothing");
        Assert.That(PopupOf(group).Child, Is.Null, "collapsed is not open - the card waits for the click");
    }

    // The one the deferral could break: the host the group moves its content into arrives WITH the content, so the group
    // has to take it then. Without that the flyout opens onto an empty card and the group's commands are simply gone.
    [Test]
    public void OpeningTheFlyoutPutsTheGroupsContentInTheCard()
    {
        var (group, window) = Hosted();
        var content = group.GetTemplateChild("PART_Content");

        Collapse(group, window);
        PopupOf(group).IsOpen = true;
        for (var i = 0; i < 3; i++) WindowExtension.UpdateTree(window);

        var host = PopupOf(group).FindContentChild("PART_PopupHost") as Border;
        Assert.That(host, Is.Not.Null, "the deferred card has to carry the host the group hands its content to");
        Assert.That(host.Child, Is.SameAs(content), "the group's commands must end up in the flyout, not stay behind");
    }

    // ---- the gallery's drop-down, deferred the same way ----
    // Its CELLS were already built only while the drop-down is down; now the card holding them is too. The panel the
    // cells go into therefore arrives with the content, and the gallery has to fill it then - otherwise the drop-down
    // opens onto an empty card, which is the same failure in a different control.

    private static ControlTemplate DeferredGalleryTemplate() => new(() =>
    {
        var popup = new Popup
        {
            ChildTemplate = new ControlTemplate(() =>
            {
                var panel = new RibbonGalleryPanel();
                var inner = new TemplateResult { RootComponent = new Border { Child = panel } };
                inner.RegisterName("PART_DropDownPanel", panel);
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

    private static (RibbonGallery gallery, Window window) HostedGallery()
    {
        var gallery = new RibbonGallery { Columns = 3, Rows = 2, Template = DeferredGalleryTemplate() };
        for (var i = 0; i < 6; i++) gallery.Items.Add($"item {i}");

        var window = new Window { Width = 400, Height = 200, Content = gallery };
        for (var i = 0; i < 5; i++) WindowExtension.UpdateTree(window);

        return (gallery, window);
    }

    [Test]
    public void AGalleryThatWasNeverDroppedDownBuildsNoCard()
    {
        var (gallery, _) = HostedGallery();

        Assert.That((gallery.GetTemplateChild("PART_Popup") as Popup).Child, Is.Null);
    }

    [Test]
    public void DroppingDownFillsTheDeferredPanelWithCells()
    {
        var (gallery, window) = HostedGallery();

        gallery.IsDropDownOpen = true;
        for (var i = 0; i < 3; i++) WindowExtension.UpdateTree(window);

        var panel = (gallery.GetTemplateChild("PART_Popup") as Popup).FindContentChild("PART_DropDownPanel") as RibbonGalleryPanel;
        Assert.That(panel, Is.Not.Null, "the deferred card has to carry the panel the cells go into");
        Assert.That(panel.Children.Count, Is.EqualTo(6), "every item must get a cell once the drop-down is down");
    }
}
