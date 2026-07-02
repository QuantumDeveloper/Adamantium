using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.Resources.Triggers;
using Adamantium.UI.Core.Templates;
using Adamantium.UI.Core.TypeParsers;
using NUnit.Framework;

namespace Adamantium.UITests;

// TabControl single-selection: the first tab is auto-selected, the selected tab's body surfaces as SelectedContent, and
// selection is reflected onto the realized TabItem containers' IsSelected. Containers are realized directly via the
// generator (no GPU/layout needed) - the initial on-attach highlight of own-container tabs is left to a visual run.
public class TabControlTests
{
    private static TabControl WithTabs(params (string header, string body)[] tabs)
    {
        var tc = new TabControl();
        foreach (var (h, b) in tabs)
            tc.Items.Add(new TabItem { Header = h, Content = b });
        return tc;
    }

    [Test]
    public void FirstTab_IsAutoSelected()
    {
        var tc = WithTabs(("A", "a"), ("B", "b"));
        Assert.Multiple(() =>
        {
            Assert.That(tc.SelectedIndex, Is.EqualTo(0));
            Assert.That(((TabItem)tc.SelectedItem).Header, Is.EqualTo("A"));
            Assert.That(tc.SelectedContent, Is.EqualTo("a"));
        });
    }

    [Test]
    public void SelectedIndex_SurfacesThatTabsBody_AsSelectedContent()
    {
        var tc = WithTabs(("A", "a"), ("B", "b"), ("C", "c"));
        tc.SelectedIndex = 2;
        Assert.That(tc.SelectedContent, Is.EqualTo("c"));
    }

    [Test]
    public void Selection_ReflectsOntoRealizedTabItems()
    {
        var tc = WithTabs(("A", "a"), ("B", "b"), ("C", "c"));
        var g = tc.ItemContainerGenerator;
        var t0 = (TabItem)g.Realize(0);
        var t1 = (TabItem)g.Realize(1);
        var t2 = (TabItem)g.Realize(2);

        tc.SelectedIndex = 1;

        Assert.Multiple(() =>
        {
            Assert.That(t0.IsSelected, Is.False);
            Assert.That(t1.IsSelected, Is.True);
            Assert.That(t2.IsSelected, Is.False);
        });
    }

    [Test]
    public void SelectTab_SelectsThatContainer()
    {
        var tc = WithTabs(("A", "a"), ("B", "b"), ("C", "c"));
        var t2 = (TabItem)tc.ItemContainerGenerator.Realize(2);

        tc.SelectTab(t2);   // simulates a click on the tab header

        Assert.Multiple(() =>
        {
            Assert.That(tc.SelectedIndex, Is.EqualTo(2));
            Assert.That(tc.SelectedContent, Is.EqualTo("c"));
            Assert.That(t2.IsSelected, Is.True);
        });
    }

    [Test]
    public void DataItems_AreWrappedInTabItems_HeaderAndBodyAreTheItem()
    {
        var tc = new TabControl { ItemsSource = new[] { "x", "y" } };
        var t0 = (TabItem)tc.ItemContainerGenerator.Realize(0);

        Assert.Multiple(() =>
        {
            Assert.That(tc.SelectedIndex, Is.EqualTo(0));
            Assert.That(t0.Header, Is.EqualTo("x"));
            Assert.That(tc.SelectedContent, Is.EqualTo("x"));
        });
    }

    // TabStripPlacement is declarative: each placement selects its OWN control template via a PropertyTrigger on the enum
    // (in the theme, a Left/Right placement also flips the header panel vertical). This proves the exact mechanism the
    // theme relies on - a base template from a Style setter, a trigger keyed on TabStripPlacement=Left that swaps in a
    // different one at Trigger priority (which outranks the Style base), and a clean revert on exit - without the full
    // theme loaded. Enum trigger values parse from their string form ("Left" -> the enum). Guards the framework fix: a
    // Template-swapping trigger must not recurse through the template-change trigger reevaluation.
    [Test]
    public void TabStripPlacement_SelectsTemplateViaTrigger()
    {
        var tc = new TabControl();
        tc.Items.Add(new TabItem { Header = "A", Content = "a" });

        var style = new Style();
        style.Selector.Types.Add(typeof(TabControl));
        style.Setters.Add(new Setter { Property = "Template", Value = PartTemplate("PART_Top") });   // base (Top)
        var left = new PropertyTrigger { Property = "TabStripPlacement", Value = "Left" };
        left.Add(new Setter { Property = "Template", Value = PartTemplate("PART_Left") });
        style.Triggers.Add(left);
        style.Attach(tc);

        Assert.That(tc.GetTemplateChild("PART_Top"), Is.Not.Null, "base template active at the default Top placement");

        tc.TabStripPlacement = TabStripPlacement.Left;
        Assert.Multiple(() =>
        {
            Assert.That(tc.TabStripPlacement, Is.EqualTo(TabStripPlacement.Left));
            Assert.That(tc.GetTemplateChild("PART_Left"), Is.Not.Null, "Left placement swapped in its own template");
            Assert.That(tc.GetTemplateChild("PART_Top"), Is.Null, "the base template is gone, not stacked under the new one");
        });

        tc.TabStripPlacement = TabStripPlacement.Top;
        Assert.Multiple(() =>
        {
            Assert.That(tc.GetTemplateChild("PART_Top"), Is.Not.Null, "reverts to the base template once the trigger no longer holds");
            Assert.That(tc.GetTemplateChild("PART_Left"), Is.Null, "the placement template is fully torn down on exit");
        });
    }

    // The mechanism the THEME actually uses: a property-condition SELECTOR ("TabControl[TabStripPlacement=Left]") is its
    // own small style; it attaches to every TabControl (structurally) and applies its template only WHILE the placement
    // matches, switching as the property changes. Exercises the selector parser + StyleSelector.Conditions + the
    // conditioned-setter activation in Style.Attach end to end.
    [Test]
    public void TabStripPlacement_SelectsTemplateViaConditionalSelector()
    {
        var tc = new TabControl();
        tc.Items.Add(new TabItem { Header = "A", Content = "a" });

        var baseStyle = new Style();
        baseStyle.Selector.Types.Add(typeof(TabControl));
        baseStyle.Setters.Add(new Setter { Property = "Template", Value = PartTemplate("PART_Top") });
        baseStyle.Attach(tc);

        var leftStyle = new Style { Selector = new SelectorParser().Parse("TabControl[TabStripPlacement=Left]") };
        leftStyle.Setters.Add(new Setter { Property = "Template", Value = PartTemplate("PART_Left") });
        leftStyle.Attach(tc);   // attaches structurally even though the condition is unmet right now

        Assert.That(tc.GetTemplateChild("PART_Top"), Is.Not.Null, "base template while the placement condition is unmet");

        tc.TabStripPlacement = TabStripPlacement.Left;
        Assert.Multiple(() =>
        {
            Assert.That(tc.GetTemplateChild("PART_Left"), Is.Not.Null, "the matching placement style swapped its template in");
            Assert.That(tc.GetTemplateChild("PART_Top"), Is.Null, "base template is overridden, not stacked");
        });

        tc.TabStripPlacement = TabStripPlacement.Top;
        Assert.That(tc.GetTemplateChild("PART_Top"), Is.Not.Null, "reverts to the base template once the condition no longer holds");
    }

    private static ControlTemplate PartTemplate(string partName) => new(() =>
    {
        var root = new Border();
        var result = new TemplateResult { RootComponent = root };
        result.RegisterName(partName, root);
        return result;
    });
}
