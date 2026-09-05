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
/// The macOS tab strip is a SEGMENTED CONTROL: a groove with one raised capsule on it. A re-drawing this thorough is
/// exactly where a part gets dropped - the control looks its parts up BY NAME and quietly does nothing when one is
/// missing - so every placement is built here and asked for the whole contract.
/// </summary>
[TestFixture]
public class MacOsTabStripTests
{
    private FakeApp _app;
    private ThemeManager _themes;

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
        _themes = new ThemeManager(new AdamantiumDependencyContainer());
        _app.ThemeManager = _themes;
        ((FakeContext)_app.UIContext).ThemeEngine = _themes;

        var theme = new MacOs();
        _themes.AddTheme(theme.Name, theme);
        _themes.SetTheme(theme);
    }

    private static TabControl Built(TabStripPlacement placement)
    {
        var control = new TabControl { TabStripPlacement = placement };
        control.Items.Add(new TabItem { Header = "One" });
        control.Items.Add(new TabItem { Header = "Two" });
        control.SelectedIndex = 0;

        control.ApplyCurrentTheme();
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(control);
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        control.Measure(new Size(600, 400));
        control.Arrange(new Rect(0, 0, 600, 400));
        return control;
    }

    [TestCase(TabStripPlacement.Top)]
    [TestCase(TabStripPlacement.Bottom)]
    [TestCase(TabStripPlacement.Left)]
    [TestCase(TabStripPlacement.Right)]
    public void EveryPlacementKeepsTheStripsContract(TabStripPlacement placement)
    {
        var control = Built(placement);

        Assert.Multiple(() =>
        {
            Assert.That(control.GetTemplateChild("PART_TabStrip"), Is.Not.Null, "the strip itself");
            Assert.That(control.GetTemplateChild("PART_SelectionIndicator"), Is.Not.Null,
                "the bar is silenced, NOT removed - the control drives it by name");
            Assert.That(control.GetTemplateChild("PART_TabOverflow"), Is.Not.Null, "the overflow chevron");
            Assert.That(control.GetTemplateChild("PART_TabOverflowPopup"), Is.Not.Null, "and its flyout");
            Assert.That(control.GetTemplateChild("PART_SelectedContentHost"), Is.Not.Null, "the content host");
            Assert.That(control.GetTemplateChild("StripTrack"), Is.Not.Null, "the groove the segments sit in");
        });
    }

    // The pinned row is the Top strip's alone, and it is the half a rewrite loses: two lists, each with a bar of its
    // own, and a pinned tab that must be impossible to scroll out of sight.
    [Test]
    public void TheTopStripKeepsBothLists()
    {
        var control = Built(TabStripPlacement.Top);

        Assert.Multiple(() =>
        {
            Assert.That(control.GetTemplateChild("PART_Tabs"), Is.Not.Null, "the ordinary tabs' list");
            Assert.That(control.GetTemplateChild("PART_PinnedTabs"), Is.Not.Null, "the pinned tabs' list");
            Assert.That(control.GetTemplateChild("PART_PinnedSelectionIndicator"), Is.Not.Null,
                "the pinned row's own bar - a bar cannot point at a tab in another row");
        });
    }

    // The selection is the CAPSULE, so the sliding bar has to draw nothing at all: left visible it would be a Fluent
    // accent line under a Mac segment.
    [Test]
    public void TheSlidingBarDrawsNothing()
    {
        var control = Built(TabStripPlacement.Top);

        TestContext.WriteLine($"indicator thickness={control.SelectionIndicatorThickness}, brush={control.SelectionIndicatorBrush}");
        Assert.That(control.SelectionIndicatorThickness, Is.EqualTo(0).Within(0.001));
    }

    // A tool panel is where this theme parts company with Fluent hardest: the caption stops being an accent block. The
    // parts are what the group WIRES, so a caption that lost one is a button that silently does nothing.
    [Test]
    public void AToolPanelKeepsItsCaptionAndItsButtons()
    {
        var group = new Adamantium.UI.Controls.Docking.PaneGroup
        {
            Kind = Adamantium.UI.Controls.Docking.PaneKind.Tool
        };
        group.ApplyCurrentTheme();
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(group);
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        group.Measure(new Size(300, 400));
        group.Arrange(new Rect(0, 0, 300, 400));

        Assert.Multiple(() =>
        {
            Assert.That(group.GetTemplateChild("PART_Header"), Is.Not.Null, "the caption");
            Assert.That(group.GetTemplateChild("PART_AutoHideButton"), Is.Not.Null, "put away");
            Assert.That(group.GetTemplateChild("PART_CloseButton"), Is.Not.Null, "close");
            Assert.That(group.GetTemplateChild("PART_TabStrip"), Is.Not.Null, "the strip along the bottom");
            Assert.That(group.GetTemplateChild("PART_SelectedContentHost"), Is.Not.Null, "the docked body");
            Assert.That(group.GetTemplateChild("PART_RevealFlyout"), Is.Not.Null, "the flyout a revealed panel uses");
            Assert.That(group.GetTemplateChild("PART_FlyoutHeader"), Is.Not.Null, "...which has a caption of its own");
            Assert.That(group.GetTemplateChild("PART_FlyoutContentHost"), Is.Not.Null, "...and a body");
        });
    }

    // The colour picker is driven entirely BY PART NAME from code - the square's hue, the alpha gradient, the preview
    // fill, all four grips' positions. Every one of them lost is a piece of the picker that stops moving.
    [Test]
    public void TheColourPickerKeepsEveryPartTheControlDrives()
    {
        var picker = new ColorPicker();
        picker.ApplyCurrentTheme();
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(picker);
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        picker.Measure(new Size(700, 400));
        picker.Arrange(new Rect(0, 0, 700, 400));

        Assert.Multiple(() =>
        {
            Assert.That(picker.GetTemplateChild("PART_SVArea"), Is.Not.Null);
            Assert.That(picker.GetTemplateChild("PART_SVThumb"), Is.Not.Null);
            Assert.That(picker.GetTemplateChild("PART_HueArea"), Is.Not.Null);
            Assert.That(picker.GetTemplateChild("PART_HueThumb"), Is.Not.Null);
            Assert.That(picker.GetTemplateChild("PART_AlphaArea"), Is.Not.Null);
            Assert.That(picker.GetTemplateChild("PART_AlphaGradient"), Is.Not.Null);
            Assert.That(picker.GetTemplateChild("PART_AlphaThumb"), Is.Not.Null);
            Assert.That(picker.GetTemplateChild("PART_Preview"), Is.Not.Null);
        });
    }

    // The rails' grips are opted into by CLASS. A class that matches nothing is the quietest failure there is: the grip
    // is still built, still positioned, and drawn as a bare unstyled box.
    [Test]
    public void TheRailGripsActuallyPickUpTheirClass()
    {
        var picker = new ColorPicker();
        picker.ApplyCurrentTheme();
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(picker);
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        picker.Measure(new Size(700, 400));
        picker.Arrange(new Rect(0, 0, 700, 400));

        var hue = picker.GetTemplateChild("PART_HueThumb") as Adamantium.UI.Controls.Decorators.Border;
        TestContext.WriteLine($"hue grip: height={hue?.Height}, background={hue?.Background}");

        Assert.That(hue, Is.Not.Null);
        Assert.That(hue.Background, Is.Not.Null, "the class style is what gives the grip its white body");
    }

    // The whole point of the re-drawing: the selected tab wears a surface and the others do not.
    [Test]
    public void TheSelectedTabIsTheOnlyOneWearingASurface()
    {
        var control = Built(TabStripPlacement.Top);

        var selected = (TabItem)control.Items[0];
        var other = (TabItem)control.Items[1];

        var selectedBorder = selected.GetTemplateChild("TabBorder") as Adamantium.UI.Controls.Decorators.Border;
        var otherBorder = other.GetTemplateChild("TabBorder") as Adamantium.UI.Controls.Decorators.Border;
        Assert.That(selectedBorder, Is.Not.Null);
        Assert.That(otherBorder, Is.Not.Null);

        var atRest = selectedBorder.Background;
        selected.IsSelected = true;

        TestContext.WriteLine($"selected={selectedBorder.Background}, other={otherBorder.Background}");
        Assert.Multiple(() =>
        {
            Assert.That(selectedBorder.Background, Is.Not.SameAs(atRest), "the selected segment takes a capsule");
            Assert.That(selectedBorder.Background, Is.SameAs(selected.BackgroundSelected),
                "and the capsule is the state brush the style declares");
            Assert.That(otherBorder.Background, Is.SameAs(atRest), "the unselected one lies flat on the groove");
        });
    }
}
