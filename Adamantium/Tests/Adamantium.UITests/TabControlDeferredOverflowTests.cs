using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core.Templates;
using Adamantium.UI.Extensions;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>The tab strip's overflow flyout lists every tab, and its card lives in the popup's ChildTemplate - built on
/// FIRST OPEN, not with every TabControl's template. A list is a scroll host, two scrollbars and a container per row, and
/// most strips never overflow at all. What the deferral puts at risk is the list itself: it is no longer in the template's
/// namescope, it arrives with the content - miss that and the flyout opens EMPTY, or picking a row selects nothing.</summary>
[TestFixture]
public class TabControlDeferredOverflowTests
{
    // The theme's shape, cut down to what this is about: the ▾ toggle, and the flyout's card deferred behind ChildTemplate.
    private static ControlTemplate DeferredOverflowTemplate() => new(() =>
    {
        var popup = new Popup
        {
            ChildTemplate = new ControlTemplate(() =>
            {
                var list = new ListBox();
                var inner = new TemplateResult { RootComponent = new Border { Child = list } };
                inner.RegisterName("PART_TabOverflowList", list);
                return inner;
            })
        };

        var overflow = new ToggleButton();
        var host = new TabPanel();

        var grid = new Grid();
        grid.Children.Add(host);
        grid.Children.Add(overflow);
        grid.Children.Add(popup);

        var result = new TemplateResult { RootComponent = grid };
        result.RegisterName("PART_TabsHost", host);
        result.RegisterName("PART_TabOverflow", overflow);
        result.RegisterName("PART_TabOverflowPopup", popup);
        return result;
    });

    private static (TabControl tabs, Window window) Hosted()
    {
        var tabs = new TabControl { Template = DeferredOverflowTemplate() };
        for (var i = 0; i < 4; i++) tabs.Items.Add(new TabItem { Header = $"Tab {i}", Content = $"Body {i}" });

        var window = new Window { Width = 400, Height = 200, Content = tabs };
        for (var i = 0; i < 5; i++) WindowExtension.UpdateTree(window);

        return (tabs, window);
    }

    private static Popup PopupOf(TabControl tabs) => tabs.GetTemplateChild("PART_TabOverflowPopup") as Popup;

    private static ListBox OpenOverflow(TabControl tabs, Window window)
    {
        (tabs.GetTemplateChild("PART_TabOverflow") as ToggleButton).IsChecked = true;
        for (var i = 0; i < 3; i++) WindowExtension.UpdateTree(window);
        return PopupOf(tabs).FindContentChild("PART_TabOverflowList") as ListBox;
    }

    [Test]
    public void AStripThatNeverOverflowedBuildsNoList()
    {
        var (tabs, _) = Hosted();

        Assert.That(PopupOf(tabs).Child, Is.Null,
            "every TabControl must not pay for a list of tabs nobody has asked to see");
    }

    // The one the deferral could break: the list arrives WITH the card, so the rows have to be put in after the open, not
    // before it. Fill first and the flyout comes up empty.
    [Test]
    public void OpeningTheFlyoutFillsTheDeferredListWithARowPerTab()
    {
        var (tabs, window) = Hosted();

        var list = OpenOverflow(tabs, window);

        Assert.That(list, Is.Not.Null, "the deferred card has to carry the list the rows go into");
        Assert.That(list.Items.Count, Is.EqualTo(4), "every tab must get a row once the flyout is up");
        Assert.That(list.SelectedIndex, Is.EqualTo(tabs.SelectedIndex), "the current tab must come up highlighted");
    }

    // The list's SelectionChanged is subscribed where the list is FOUND, so the deferral moved that too: without it the
    // flyout lists the tabs and picking one does nothing at all.
    [Test]
    public void PickingARowInTheDeferredListSelectsThatTab()
    {
        var (tabs, window) = Hosted();
        var list = OpenOverflow(tabs, window);

        list.SelectedIndex = 2;
        for (var i = 0; i < 3; i++) WindowExtension.UpdateTree(window);

        Assert.That(tabs.SelectedIndex, Is.EqualTo(2), "a row picked in the flyout must select its tab");
    }

    // Re-opening rebuilds the rows against a card that already exists - the second open must not double-subscribe or come
    // up stale.
    [Test]
    public void ASecondOpenListsTheTabsAsTheyAreNow()
    {
        var (tabs, window) = Hosted();
        var list = OpenOverflow(tabs, window);
        (tabs.GetTemplateChild("PART_TabOverflow") as ToggleButton).IsChecked = false;

        tabs.Items.Add(new TabItem { Header = "Tab 4" });
        for (var i = 0; i < 3; i++) WindowExtension.UpdateTree(window);
        var second = OpenOverflow(tabs, window);

        Assert.That(second, Is.SameAs(list), "the card is built once - a re-open must reuse it");
        Assert.That(second.Items.Count, Is.EqualTo(5), "the rows are rebuilt per open, so a new tab must be listed");
    }

    /// <summary>The flyout says which tab is current, and it has to say it EVERY time it is opened - not only the first.
    /// <para>The rows are rebuilt per open, so the container carrying the selection is a new object each time, while the
    /// index is a number: re-assigning the number it already holds changes nothing and notifies nobody, and the new row
    /// never learns it is the selected one.</para></summary>
    [Test]
    public void EveryOpen_MarksTheCurrentTabAsSelected_NotOnlyTheFirst()
    {
        var (tabs, window) = Hosted();
        tabs.SelectedIndex = 2;
        for (var i = 0; i < 3; i++) WindowExtension.UpdateTree(window);

        var first = OpenOverflow(tabs, window);
        Assume.That(first.SelectedIndex, Is.EqualTo(2), "precondition: the first open marks it - this half always worked");

        (tabs.GetTemplateChild("PART_TabOverflow") as ToggleButton).IsChecked = false;
        for (var i = 0; i < 3; i++) WindowExtension.UpdateTree(window);

        var second = OpenOverflow(tabs, window);
        Assert.That(second.SelectedIndex, Is.EqualTo(2),
            "re-opened on the same tab, the flyout still has to show which one that is");
    }

    /// <summary>A flyout dismissed from OUTSIDE (a click elsewhere closes the popup on its own) has to be re-openable by
    /// the very next press of the arrow. The arrow's checked state and the popup's open state are two facts kept in step
    /// by three handlers, and if the dismissal leaves them disagreeing the next press only puts them back in step -
    /// which from outside looks like a button that sometimes works and sometimes does nothing.</summary>
    [Test]
    public void AFlyoutDismissedFromOutside_OpensAgainOnTheNextPress()
    {
        var (tabs, window) = Hosted();
        var toggle = tabs.GetTemplateChild("PART_TabOverflow") as ToggleButton;

        OpenOverflow(tabs, window);
        Assume.That(PopupOf(tabs).IsOpen, Is.True, "precondition: it opened at all");

        PopupOf(tabs).IsOpen = false;                       // dismissed by something other than the arrow
        for (var i = 0; i < 3; i++) WindowExtension.UpdateTree(window);
        Assert.That(toggle.IsChecked, Is.False, "the arrow must come back UP, or its next press only un-presses it");

        toggle.IsChecked = true;                            // the next press
        for (var i = 0; i < 3; i++) WindowExtension.UpdateTree(window);
        Assert.That(PopupOf(tabs).IsOpen, Is.True, "and that press has to open the flyout again");
    }
}
