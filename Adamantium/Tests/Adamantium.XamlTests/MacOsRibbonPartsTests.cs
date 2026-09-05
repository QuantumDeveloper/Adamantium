using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Themes.MacOsTheme;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// The ribbon is the largest set in the theme and almost all of it is PARTS: the adaptive size ladder, a group's
/// collapse into a button and back, the gallery's rows, the backstage's deferred build, the quick-access overflow -
/// every one of them is found by name and none of them says anything when it is missing. Re-dressing 1500 lines is
/// exactly where one gets dropped, so the contract is asked for here rather than looked for on screen.
/// </summary>
[TestFixture]
public class MacOsRibbonPartsTests
{
    private FakeApp _app;

    [OneTimeSetUp]
    public void EnsureAppContext()
    {
        _app = new FakeApp(new AdamantiumDependencyContainer()) { ResourceManager = new ResourceManager() };
        UIAppContext.Initialize(_app, null);
    }

    [SetUp]
    public void Fresh()
    {
        _app.ResourceManager = new ResourceManager();
        typeof(UIAppContext).GetProperty(nameof(UIAppContext.Current)).SetValue(null, _app);
        var themes = new ThemeManager(new AdamantiumDependencyContainer());
        _app.ThemeManager = themes;
        ((FakeContext)_app.UIContext).ThemeEngine = themes;

        var theme = new MacOs();
        themes.AddTheme(theme.Name, theme);
        themes.SetTheme(theme);
    }

    private static T Built<T>(T control, double width, double height) where T : MeasurableUIComponent
    {
        control.ApplyCurrentTheme();
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(control);
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        control.Measure(new Size(width, height));
        control.Arrange(new Rect(0, 0, width, height));
        return control;
    }

    [Test]
    public void TheRibbonKeepsItsStripBandAndFlyout()
    {
        var ribbon = Built(new Ribbon(), 900, 160);

        Assert.Multiple(() =>
        {
            Assert.That(ribbon.GetTemplateChild("PART_ApplicationMenuHost"), Is.Not.Null, "the File tab's host");
            Assert.That(ribbon.GetTemplateChild("PART_ItemsPresenter"), Is.Not.Null, "the strip");
            Assert.That(ribbon.GetTemplateChild("PART_MinimizeButton"), Is.Not.Null, "the collapse chevron");
            Assert.That(ribbon.GetTemplateChild("PART_BandHost"), Is.Not.Null, "the band");
            Assert.That(ribbon.GetTemplateChild("PART_SelectedContentHost"), Is.Not.Null, "the open tab's host");
            Assert.That(ribbon.GetTemplateChild("PART_Flyout"), Is.Not.Null, "the minimized band");
            Assert.That(ribbon.GetTemplateChild("PART_FlyoutHost"), Is.Not.Null, "...and the thing that slides in it");
        });
    }

    [Test]
    public void ATabKeepsItsRowAndItsTwoScrollArrows()
    {
        var tab = Built(new RibbonTab(), 600, 106);

        Assert.Multiple(() =>
        {
            Assert.That(tab.GetTemplateChild("PART_ItemsPresenter"), Is.Not.Null, "the groups");
            Assert.That(tab.GetTemplateChild("PART_ScrollBack"), Is.Not.Null);
            Assert.That(tab.GetTemplateChild("PART_ScrollForward"), Is.Not.Null);
        });
    }

    // Both hosts AND the collapsed button: the group MOVES its content between them, which is why the template states
    // one block and two places to put it rather than two templates.
    [Test]
    public void AGroupKeepsBothHostsAndItsCollapsedButton()
    {
        var group = Built(new RibbonGroup(), 300, 106);

        Assert.Multiple(() =>
        {
            Assert.That(group.GetTemplateChild("PART_InlineHost"), Is.Not.Null);
            Assert.That(group.GetTemplateChild("PART_Content"), Is.Not.Null, "the block that travels");
            Assert.That(group.GetTemplateChild("PART_ItemsPresenter"), Is.Not.Null);
            Assert.That(group.GetTemplateChild("PART_CollapsedButton"), Is.Not.Null);
            Assert.That(group.GetTemplateChild("PART_Popup"), Is.Not.Null, "where a collapsed group opens");
        });
    }

    [Test]
    public void TheGalleryKeepsItsViewportArrowsAndDropDown()
    {
        var gallery = Built(new RibbonGallery(), 300, 90);

        Assert.Multiple(() =>
        {
            Assert.That(gallery.GetTemplateChild("PART_ItemsPresenter"), Is.Not.Null);
            Assert.That(gallery.GetTemplateChild("PART_ScrollUp"), Is.Not.Null);
            Assert.That(gallery.GetTemplateChild("PART_ScrollDown"), Is.Not.Null);
            Assert.That(gallery.GetTemplateChild("PART_More"), Is.Not.Null, "the whole set, one press away");
            Assert.That(gallery.GetTemplateChild("PART_Popup"), Is.Not.Null);
        });
    }

    [Test]
    public void ACommandKeepsItsIconAndLabel()
    {
        var button = Built(new RibbonButton { Content = "Paste" }, 90, 70);

        Assert.Multiple(() =>
        {
            Assert.That(button.GetTemplateChild("PART_Icon"), Is.Not.Null);
            Assert.That(button.GetTemplateChild("PART_ContentPresenter"), Is.Not.Null);
        });
    }

    // The split button's whole point is that its two halves light separately, and the control asks PART_DropDownArea
    // where the pointer is - a glyph in a corner would answer nothing.
    [Test]
    public void TheSplitButtonKeepsItsDropDownArea()
    {
        var split = Built(new RibbonSplitButton { Content = "Paste" }, 90, 70);

        Assert.That(split.GetTemplateChild("PART_DropDownArea"), Is.Not.Null);
    }

    [Test]
    public void TheApplicationMenuKeepsItsButtonAndBackstage()
    {
        var menu = Built(new RibbonApplicationMenu { Header = "File" }, 120, 40);

        Assert.Multiple(() =>
        {
            Assert.That(menu.GetTemplateChild("PART_Button"), Is.Not.Null);
            Assert.That(menu.GetTemplateChild("PART_Popup"), Is.Not.Null);
        });
    }

    [Test]
    public void TheQuickAccessBarKeepsItsOverflow()
    {
        var bar = Built(new RibbonQuickAccess(), 320, 32);

        Assert.Multiple(() =>
        {
            Assert.That(bar.GetTemplateChild("PART_ItemsPresenter"), Is.Not.Null);
            Assert.That(bar.GetTemplateChild("PART_OverflowButton"), Is.Not.Null);
            Assert.That(bar.GetTemplateChild("PART_OverflowMenu"), Is.Not.Null);
        });
    }
}
