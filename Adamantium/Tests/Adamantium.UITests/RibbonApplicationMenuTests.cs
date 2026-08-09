using Adamantium.Mathematics;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Templates;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>The "File" backstage. The rule that carries it: a row with a PAGE chooses that page and the backstage
/// stays; a row without one is a plain command that runs and closes it.</summary>
[TestFixture]
public class RibbonApplicationMenuTests
{
    private static RibbonApplicationMenuItem Row(string label, object page = null) =>
        new() { Content = label, PageContent = page };

    private static void Click(RibbonApplicationMenuItem row) =>
        ((IObservableComponent)row).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, row)
        { RoutedEvent = ButtonBase.ClickEvent });

    private static void PointAt(RibbonApplicationMenuItem row) =>
        ((IObservableComponent)row).RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, InputModifiers.None, 0)
        { RoutedEvent = Mouse.MouseEnterEvent });

    private static void PointAway(RibbonApplicationMenuItem row) =>
        ((IObservableComponent)row).RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, InputModifiers.None, 0)
        { RoutedEvent = Mouse.MouseLeaveEvent });

    // The rail, reduced to the one part the containers come out of - a selection only reaches a row that exists.
    private static ControlTemplate RailTemplate() => new(() =>
    {
        var presenter = new ItemsPresenter();
        var result = new TemplateResult { RootComponent = presenter };
        result.RegisterName("PART_ItemsPresenter", presenter);
        return result;
    });

    private static RibbonApplicationMenu WithRows(params RibbonApplicationMenuItem[] rows)
    {
        var menu = new RibbonApplicationMenu { Header = "File", Template = RailTemplate() };
        foreach (var row in rows) menu.Items.Add(row);

        menu.Measure(new Size(200, 400));
        menu.Arrange(new Rect(0, 0, 200, 400));
        return menu;
    }

    // A logical child, or the window's DataContext never reaches the rows and every {Binding} on them is dead.
    [Test]
    public void TheMenuInTheSlot_IsALogicalChildOfTheRibbon()
    {
        var menu = WithRows();
        var ribbon = new Ribbon { ApplicationMenu = menu };

        Assert.That(ribbon.LogicalChildren, Does.Contain(menu));
    }

    [Test]
    public void ReplacingTheMenu_DropsThePreviousOne()
    {
        var menu = WithRows();
        var ribbon = new Ribbon { ApplicationMenu = menu };
        var replacement = WithRows();

        ribbon.ApplicationMenu = replacement;

        Assert.Multiple(() =>
        {
            Assert.That(ribbon.LogicalChildren, Does.Not.Contain(menu));
            Assert.That(ribbon.LogicalChildren, Does.Contain(replacement));
        });
    }

    // Opening onto a blank half is not a state anyone asked for.
    [Test]
    public void Opening_ShowsTheFirstRowThatHasAPage()
    {
        var command = Row("Save");
        var withPage = Row("Open", "the recent files");
        var menu = WithRows(command, withPage);

        menu.IsOpen = true;

        Assert.Multiple(() =>
        {
            Assert.That(menu.SelectedItem, Is.SameAs(withPage), "a plain command is not a page and cannot be shown");
            Assert.That(menu.SelectedPage, Is.EqualTo("the recent files"));
        });
    }

    // Nothing to show is not an error: a menu of plain commands opens on no page rather than refusing to open.
    [Test]
    public void AMenuOfPlainCommands_StillOpens()
    {
        var menu = WithRows(Row("Save"), Row("Exit"));

        menu.IsOpen = true;

        Assert.Multiple(() =>
        {
            Assert.That(menu.IsOpen, Is.True);
            Assert.That(menu.SelectedPage, Is.Null);
        });
    }

    // The page follows the selection, and only rows that have one can hold it.
    [Test]
    public void ChoosingAnotherRow_SwapsThePage()
    {
        var first = Row("New", "the templates");
        var second = Row("Open", "the recent files");
        var menu = WithRows(first, second);
        menu.IsOpen = true;

        menu.SelectedItem = second;

        Assert.That(menu.SelectedPage, Is.EqualTo("the recent files"));
    }

    // The rail marks whose page is showing - that lit row is the only thing saying which of them you are looking at.
    [Test]
    public void TheChosenRow_IsTheOnlyOneLit()
    {
        var first = Row("New", "the templates");
        var second = Row("Open", "the recent files");
        var menu = WithRows(first, second);

        menu.IsOpen = true;
        menu.SelectedItem = second;

        Assert.That(new[] { first.IsSelected, second.IsSelected }, Is.EqualTo(new[] { false, true }));
    }

    // Closing does NOT forget which page was open: reopening lands where it was left, as Office does.
    [Test]
    public void ReopeningKeepsThePageItWasLeftOn()
    {
        var first = Row("New", "the templates");
        var second = Row("Open", "the recent files");
        var menu = WithRows(first, second);
        menu.IsOpen = true;
        menu.SelectedItem = second;

        menu.IsOpen = false;
        menu.IsOpen = true;

        Assert.That(menu.SelectedPage, Is.EqualTo("the recent files"));
    }

    // An authored row is its own container: the rail must not wrap it in a second one.
    [Test]
    public void AnAuthoredRow_IsItsOwnContainer()
    {
        var row = Row("Save");
        var menu = WithRows(row);

        Assert.That(menu.IsItemItsOwnContainer(row), Is.True);
    }

    // ...and because it is, the generator hands it back UNTOUCHED - PrepareContainer never runs for it. Anything the
    // menu needs from its rows has to be wired from the collection instead, or every row written in markup is inert.
    [Test]
    public void AnAuthoredRow_IsStillWiredToTheMenu()
    {
        var row = Row("Save");
        var menu = WithRows(row);
        menu.IsOpen = true;

        Click(row);

        Assert.That(menu.IsOpen, Is.False, "a command row closes the menu behind it");
    }

    // ...but a row that HAS a page is a way in, not an answer. Closing on it would put away the very thing the press
    // asked to see.
    [Test]
    public void PressingARowWithAPage_LeavesTheMenuOpen()
    {
        var withPage = Row("Export", "the export formats");
        var menu = WithRows(withPage);
        menu.IsBackstage = false;
        menu.IsOpen = true;

        Click(withPage);

        Assert.Multiple(() =>
        {
            Assert.That(menu.IsOpen, Is.True);
            Assert.That(menu.SelectedPage, Is.EqualTo("the export formats"), "and the press pins what it asked for");
        });
    }

    // The dropped panel follows the POINTER: the pane previews the row under it. That is what makes it a menu rather
    // than a small backstage.
    [Test]
    public void InMenuShape_ThePanePreviewsTheRowUnderThePointer()
    {
        var withPage = Row("Export", "the export formats");
        var menu = WithRows(Row("Save"), withPage);
        menu.IsBackstage = false;
        menu.AuxiliaryPaneContent = "the recent files";
        menu.IsOpen = true;

        Assert.That(menu.SelectedPage, Is.EqualTo("the recent files"), "precondition: its own page, nothing pointed at");

        PointAt(withPage);

        Assert.That(menu.SelectedPage, Is.EqualTo("the export formats"));
    }

    // The way to a page's buttons leads OFF the rail, so leaving the row must not take the page with it - that is a
    // preview nobody can ever reach. Only another row replaces it.
    [Test]
    public void LeavingARow_KeepsThePageItOpened()
    {
        var first = Row("Import", "the import formats");
        var second = Row("Export", "the export formats");
        var menu = WithRows(first, second);
        menu.IsBackstage = false;
        menu.AuxiliaryPaneContent = "the recent files";
        menu.IsOpen = true;
        PointAt(first);

        PointAway(first);   // ...on the way to the buttons on its page

        Assert.That(menu.SelectedPage, Is.EqualTo("the import formats"));

        PointAt(second);

        Assert.That(menu.SelectedPage, Is.EqualTo("the export formats"), "another row is what replaces it");
    }

    // The backstage does NOT follow the pointer - it stands on the row that was chosen, or moving the mouse across the
    // rail would throw away the page someone navigated to.
    [Test]
    public void InBackstageShape_ThePointerChangesNothing()
    {
        var chosen = Row("New", "the templates");
        var other = Row("Export", "the export formats");
        var menu = WithRows(chosen, other);
        menu.IsOpen = true;

        PointAt(other);

        Assert.That(menu.SelectedPage, Is.EqualTo("the templates"));
    }
}
