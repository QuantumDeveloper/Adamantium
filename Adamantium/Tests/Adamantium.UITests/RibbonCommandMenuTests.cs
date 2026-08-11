using System;
using System.Linq;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Templates;
using Adamantium.UI.Extensions;
using NUnit.Framework;

namespace Adamantium.UITests;

// A command in a group is given the right-click menu that offers the quick-access bar. WHAT is in that menu comes from a
// TEMPLATE the theme states - a menu points at ONE command, so they cannot share an object, and a setter would hand them
// all the same one.
[TestFixture]
public class RibbonCommandMenuTests
{
    private static DataTemplate MenuTemplate()
    {
        return new DataTemplate(() =>
        {
            var menu = new ContextMenu();
            menu.Items.Add(new RibbonQuickAccessMenuItem());
            return new TemplateResult { RootComponent = menu };
        });
    }

    private static DataTemplate TwoRowMenuTemplate()
    {
        return new DataTemplate(() =>
        {
            var menu = new ContextMenu();
            menu.Items.Add(new RibbonQuickAccessMenuItem());
            menu.Items.Add(new Adamantium.UI.Controls.Primitives.MenuItem { Header = "Something of the shell own" });
            return new TemplateResult { RootComponent = menu };
        });
    }

    private static RibbonGroup Grouped(DataTemplate template, IUIComponent command, Action<Window> statedAbove = null)
        => Grouped(template, statedAbove, command);

    private static RibbonGroup Grouped(DataTemplate template, params IUIComponent[] commands)
        => Grouped(template, null, commands);

    private static RibbonGroup Grouped(DataTemplate template, Action<Window> statedAbove, params IUIComponent[] commands)
    {
        var group = new RibbonGroup { Header = "Group" };
        foreach (var command in commands) group.Items.Add(command);

        if (template != null) Ribbon.SetCommandContextMenuTemplate(group, template);

        // No theme in a test, so the group has no template - and with no PART_ItemsPresenter nothing is ever generated.
        group.Template = new ControlTemplate(() =>
        {
            var presenter = new ItemsPresenter();
            var result = new TemplateResult { RootComponent = presenter };
            result.RegisterName("PART_ItemsPresenter", presenter);
            return result;
        });

        var window = new Window { Width = 400, Height = 200 };
        statedAbove?.Invoke(window);
        window.Content = group;
        for (var i = 0; i < 5; i++) WindowExtension.UpdateTree(window);

        return group;
    }

    private static RibbonButton Command(string label) => new() { Content = label };

    [Test]
    public void EveryCommandGetsItsOwnMenu()
    {
        var first = Command("Save");
        var second = Command("Open");
        Grouped(MenuTemplate(), first, second);

        Assert.That(first.ContextMenu, Is.Not.Null);
        Assert.That(second.ContextMenu, Is.Not.Null);
        Assert.That(first.ContextMenu, Is.Not.SameAs(second.ContextMenu),
            "a menu points at ONE command - sharing one object would leave every command but the last unreachable");
        Assert.That(first.ContextMenu.Items.OfType<RibbonQuickAccessMenuItem>().Count(), Is.EqualTo(1));
    }

    [Test]
    public void ACommandThatWroteItsOwnMenuKeepsIt()
    {
        var authored = new ContextMenu();
        var command = Command("Paste");
        command.ContextMenu = authored;

        Grouped(MenuTemplate(), command);

        Assert.That(command.ContextMenu, Is.SameAs(authored), "a row nobody placed is worse than one they did");
    }

    [Test]
    public void ACommandThatRefusesTheBarIsGivenNothing()
    {
        var command = Command("Grid size");
        Ribbon.SetCanAddToQuickAccess(command, false);

        Grouped(MenuTemplate(), command);

        Assert.That(command.ContextMenu, Is.Null);
    }

    // The property INHERITS, which is what lets it be stated once high up - on the ribbon - instead of on every group by
    // hand. Stated on an ancestor, a group finds it without being told again.
    [Test]
    public void StatedOnAnAncestorItReachesTheGroup()
    {
        var command = Command("Save");
        var group = Grouped(null, command, host => Ribbon.SetCommandContextMenuTemplate(host, MenuTemplate()));

        Assert.That(Ribbon.GetCommandContextMenuTemplate(group), Is.Not.Null);
        Assert.That(command.ContextMenu, Is.Not.Null, "and the group builds its commands' menus from it");
    }

    // ...and a group that states its own overrides just itself, leaving the rest of the band on the ancestor's.
    [Test]
    public void AGroupsOwnTemplateWins()
    {
        var command = Command("Save");
        var group = Grouped(TwoRowMenuTemplate(), command, host => Ribbon.SetCommandContextMenuTemplate(host, MenuTemplate()));

        Assert.That(Ribbon.GetCommandContextMenuTemplate(group), Is.Not.Null);
        Assert.That(command.ContextMenu.Items.Count, Is.EqualTo(2), "the group's own template, not the ancestor's");
    }

    // A command that neither runs anything nor was named would be unrecognisable: put in the bar again on every asking,
    // and never taken back out because the request names nothing to match. So it is given an identity of its own.
    [Test]
    public void ACommandNamedByNothingIsGivenAnIdentity()
    {
        var first = Command("Snap to vertices");
        var second = Command("Snap to edges");
        Grouped(MenuTemplate(), first, second);

        Assert.That(Ribbon.GetQuickAccessKey(first), Is.Not.Null);
        Assert.That(Ribbon.GetQuickAccessKey(first), Is.Not.EqualTo(Ribbon.GetQuickAccessKey(second)),
            "two commands told apart by nothing else must not answer with the same identity");
    }

    [Test]
    public void AnAuthorsOwnKeyIsKept()
    {
        var command = Command("Grid");
        Ribbon.SetQuickAccessKey(command, "ShowGrid");

        Grouped(MenuTemplate(), command);

        Assert.That(Ribbon.GetQuickAccessKey(command), Is.EqualTo("ShowGrid"));
    }

    // No template stated - no menu invented in code. The words and the rows belong to the theme, so with no theme there
    // is nothing to give.
    [Test]
    public void WithNoTemplateNoMenuIsMade()
    {
        var command = Command("Save");

        Grouped(null, command);

        Assert.That(command.ContextMenu, Is.Null);
    }
}
