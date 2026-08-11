using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Templates;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>Crossing between the STRIP and the BAND with the arrows. The generic outward walk cannot answer it: the two
/// live in separate subtrees of the ribbon's template, and the panel between them answers only by the order its own
/// children stand in - so each side has to say where the other is (docs/TECH_DEBT.md).</summary>
[TestFixture]
public class RibbonStripToBandNavigationTests
{
    // OriginalSource stated: the header acts only on a key that was pressed ON IT, so that a command inside the band
    // keeps its own arrows. A raised event carries none unless it is given one.
    private static KeyEventArgs Press(Key key, object on) =>
        new(KeyboardDevice.CurrentDevice, key, InputModifiers.None, 0)
        {
            RoutedEvent = Keyboard.KeyDownEvent,
            OriginalSource = on
        };

    // A ribbon whose strip is REALLY under it: a header reaches its ribbon by walking up, so a container built beside
    // the tree stands for nothing and the key would be quietly ignored.
    private static Ribbon Strip(params RibbonTab[] tabs)
    {
        var ribbon = new Ribbon();
        foreach (var tab in tabs) ribbon.Items.Add(tab);

        ribbon.Template = new ControlTemplate(() =>
        {
            var presenter = new ItemsPresenter();
            var result = new TemplateResult { RootComponent = presenter };
            result.RegisterName("PART_ItemsPresenter", presenter);
            return result;
        });

        ((IMeasurableComponent)ribbon).Measure(new Size(800, 200));
        ((IMeasurableComponent)ribbon).Arrange(new Rect(0, 0, 800, 200));
        return ribbon;
    }

    // DOWN off a tab header opens that tab and steps into it, exactly as Enter does - that is where its commands are.
    [Test]
    public void DownOffAHeaderEntersItsTab()
    {
        var second = new RibbonTab { Header = "View" };
        var ribbon = Strip(new RibbonTab { Header = "Home" }, second);
        var header = (RibbonTabHeader)ribbon.ItemContainerGenerator.ContainerFromIndex(1);

        header.RaiseEvent(Press(Key.DownArrow, header));

        Assert.Multiple(() =>
        {
            Assert.That(ribbon.SelectedItem, Is.SameAs(second), "the tab it stepped into is the tab it opened");
            Assert.That(ribbon.SelectedHeader, Is.SameAs(header));
        });
    }

    // ...and Down must not be swallowed on the way: an unhandled arrow would fall through to the generic walk and land
    // on whatever happens to stand next in the template.
    [Test]
    public void DownIsHandledByTheHeader()
    {
        var ribbon = Strip(new RibbonTab { Header = "Home" });
        var header = (RibbonTabHeader)ribbon.ItemContainerGenerator.ContainerFromIndex(0);

        var args = Press(Key.DownArrow, header);
        header.RaiseEvent(args);

        Assert.That(args.Handled, Is.True);
    }

    // What UP out of the band comes back to. The row of groups asks the ribbon for it by walking up - the same way a
    // header finds its ribbon - so what this pins down is the answer, not the walk.
    [Test]
    public void TheRibbonNamesTheOpenTabsHeader()
    {
        var ribbon = new Ribbon();
        var first = new RibbonTab { Header = "Home" };
        var second = new RibbonTab { Header = "View" };
        ribbon.Items.Add(first);
        ribbon.Items.Add(second);

        var header = (RibbonTabHeader)ribbon.ItemContainerGenerator.Realize(1);
        ribbon.SelectedItem = second;

        Assert.That(ribbon.SelectedHeader, Is.SameAs(header));
    }

    // Out of a band that belongs to no ribbon, UP answers nothing rather than throwing - a panel used on its own (a
    // test, a designer surface) must not depend on being inside one.
    [Test]
    public void UpAnswersNothingWithoutARibbon()
    {
        var row = new RibbonGroupsPanel();
        var group = new RibbonGroup { Header = "Clipboard" };
        row.Children.Add(group);

        Assert.That(row.Navigate(group, FocusNavigationDirection.Up), Is.Null);
    }

    // DOWN out of the band answers nothing: below the groups is the document, and an arrow must not throw the focus
    // out of the ribbon.
    [Test]
    public void DownOutOfTheBandGoesNowhere()
    {
        var row = new RibbonGroupsPanel();
        var group = new RibbonGroup { Header = "Clipboard" };
        row.Children.Add(group);

        Assert.That(row.Navigate(group, FocusNavigationDirection.Down), Is.Null);
    }

    // Left/Right still walk the groups themselves - the row's own answer, untouched by the crossing.
    [Test]
    public void TheArrowsStillWalkTheGroups()
    {
        var row = new RibbonGroupsPanel();
        var a = new RibbonGroup { Header = "A", Width = 100 };
        var b = new RibbonGroup { Header = "B", Width = 100 };
        row.Children.Add(a);
        row.Children.Add(b);

        ((IMeasurableComponent)row).Measure(new Size(double.PositiveInfinity, 100));
        ((IMeasurableComponent)row).Arrange(new Rect(0, 0, 400, 100));

        Assert.Multiple(() =>
        {
            Assert.That(row.Navigate(a, FocusNavigationDirection.Right), Is.SameAs(b));
            Assert.That(row.Navigate(b, FocusNavigationDirection.Left), Is.SameAs(a));
        });
    }
}
