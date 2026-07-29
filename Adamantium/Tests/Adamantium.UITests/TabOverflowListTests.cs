using System.Linq;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core.Templates;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// The overflow flyout - the ▾ list of every tab, shown when the strip is too narrow to hold them - must show what the
/// tabs SAY, never the tabs themselves. An authored TabItem is a live control, and a list given it as an item hosts it
/// as a row's content: that takes the tab out of the strip, the strip gives it up, and the whole strip empties the
/// moment the flyout is opened.
/// <para>Found on the docking control, which is a TabControl whose panes are authored TabItems - squeeze a pane group
/// until the ▾ appears, open it, and every tab disappears.</para>
/// </summary>
[TestFixture]
public class TabOverflowListTests
{
    private static TabControl WithTabs(params string[] headers)
    {
        var tc = new TabControl();
        foreach (var header in headers) tc.Items.Add(new TabItem { Header = header, Content = header + " body" });
        // Realize the containers, as a laid-out strip has: closing a tab is asked of the generator ("which item is this
        // container?"), so a strip that was never realized cannot answer and closes nothing.
        for (var i = 0; i < tc.Items.Count; i++) tc.ItemContainerGenerator.Realize(i);
        return tc;
    }

    private static TabOverflowItem[] RowsOf(TabControl tc) =>
        tc.BuildOverflowRows().Cast<TabOverflowItem>().ToArray();

    [Test]
    public void TheFlyoutRows_AreNeverTheLiveTabs()
    {
        var tc = WithTabs("Scene", "Game", "Inspector");

        var rows = tc.BuildOverflowRows();

        Assert.Multiple(() =>
        {
            Assert.That(rows, Has.Count.EqualTo(3));
            foreach (var row in rows)
                Assert.That(row, Is.Not.InstanceOf<TabItem>(), "a row must not be the tab itself - hosting it steals it from the strip");
        });
    }

    /// <summary>What a row shows: an authored tab is named by its header.</summary>
    [Test]
    public void AnAuthoredTab_IsListedByItsHeader()
    {
        var tc = WithTabs("Scene", "Game");

        Assert.That(RowsOf(tc).Select(r => r.Header), Is.EqualTo(new object[] { "Scene", "Game" }));
    }

    /// <summary>A data-bound tab's item is plain data, not a control, so it goes in as it is - and carries the strip's own
    /// ItemTemplate, so the flyout draws it exactly as the strip does.</summary>
    [Test]
    public void ADataBoundTab_IsListedAsItsOwnItem_WithTheStripsTemplate()
    {
        var template = new DataTemplate(() => new TemplateResult { RootComponent = new Border() });
        var tc = new TabControl { ItemsSource = new[] { "x", "y" }, ItemTemplate = template };

        var rows = RowsOf(tc);

        Assert.Multiple(() =>
        {
            Assert.That(rows.Select(r => r.Header), Is.EqualTo(new object[] { "x", "y" }));
            Assert.That(rows[0].HeaderTemplate, Is.SameAs(template));
        });
    }

    /// <summary>The icon travels to the row as DATA plus the template that draws it, which is the whole reason it is data:
    /// the strip and the flyout each build their own visual, so one icon can be in both at once.</summary>
    [Test]
    public void ARowCarriesTheTabsIcon_AndTheTemplateThatDrawsIt()
    {
        var iconTemplate = new DataTemplate(() => new TemplateResult { RootComponent = new Border() });
        var tc = new TabControl { IconTemplate = iconTemplate };
        tc.Items.Add(new TabItem { Header = "Scene", Icon = "scene-glyph" });

        var row = RowsOf(tc)[0];

        Assert.Multiple(() =>
        {
            Assert.That(row.Icon, Is.EqualTo("scene-glyph"));
            Assert.That(row.IconTemplate, Is.SameAs(iconTemplate), "one template for the strip serves the flyout too");
        });
    }

    /// <summary>A tab's own icon template wins over the strip's, exactly as a tab may override any other inherited look.</summary>
    [Test]
    public void ATabsOwnIconTemplate_WinsOverTheStrips()
    {
        var stripTemplate = new DataTemplate(() => new TemplateResult { RootComponent = new Border() });
        var ownTemplate = new DataTemplate(() => new TemplateResult { RootComponent = new Border() });
        var tc = new TabControl { IconTemplate = stripTemplate };
        tc.Items.Add(new TabItem { Header = "Scene", Icon = "x", IconTemplate = ownTemplate });

        Assert.That(RowsOf(tc)[0].IconTemplate, Is.SameAs(ownTemplate));
    }

    /// <summary>The row's close button goes through the tab's own close path, so everything that governs closing a tab -
    /// including a veto - governs it here too.</summary>
    [Test]
    public void ClosingFromARow_RemovesThatTab()
    {
        var tc = WithTabs("Scene", "Game", "Inspector");
        tc.ShowCloseButton = true;
        foreach (TabItem tab in tc.Items) tab.ShowCloseButton = true;   // the effective value the owner would push on attach

        RowsOf(tc)[1].Close.Execute();

        Assert.That(tc.Items.Cast<TabItem>().Select(t => t.Header).ToArray(), Is.EqualTo(new object[] { "Scene", "Inspector" }));
    }

    [Test]
    public void ClosingFromARow_IsVetoable()
    {
        var tc = WithTabs("Scene", "Game");
        foreach (TabItem tab in tc.Items) tab.ShowCloseButton = true;
        tc.TabCloseRequested += (_, e) => e.Cancel = true;

        RowsOf(tc)[1].Close.Execute();

        Assert.That(tc.Items, Has.Count.EqualTo(2), "a vetoed close leaves the tab where it is");
    }

    /// <summary>A tab that may not be closed offers no button - the row answers with the tab's own effective state.</summary>
    [Test]
    public void ARowOffersNoCloseButton_WhenItsTabIsNotClosable()
    {
        var tc = WithTabs("Scene");

        Assert.That(RowsOf(tc)[0].CanClose, Is.False);
    }

    /// <summary>Picking row N means picking tab N: the rows are a projection, so the mapping back is by position.</summary>
    [Test]
    public void PickingARow_SelectsTheTabAtThatPosition()
    {
        var tc = WithTabs("Scene", "Game", "Inspector");

        tc.SelectOverflowRow(2);

        Assert.Multiple(() =>
        {
            Assert.That(tc.SelectedIndex, Is.EqualTo(2));
            Assert.That(((TabItem)tc.SelectedItem).Header, Is.EqualTo("Inspector"));
        });
    }

    /// <summary>A row index that no longer has a tab (the list was built, then a tab closed) selects nothing rather than
    /// throwing.</summary>
    [Test]
    public void PickingARowThatIsGone_ChangesNothing()
    {
        var tc = WithTabs("Scene", "Game");
        tc.SelectedIndex = 0;

        tc.SelectOverflowRow(5);

        Assert.That(tc.SelectedIndex, Is.EqualTo(0));
    }
}
