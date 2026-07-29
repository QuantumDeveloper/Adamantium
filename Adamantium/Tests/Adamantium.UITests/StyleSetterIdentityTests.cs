using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// A style setter whose value is an OBJECT - a panel template, a control template - must hand out the same object every
/// time it is applied. Anything else and the property "changes" on every re-application, and whoever listens for that
/// change tears down and rebuilds what the value controls.
/// <para>Found through docking: the items panel of a tab strip was being rebuilt over and over, each time leaving the
/// live panel orphaned while the tabs moved to a fresh one. Measured in the running app, ItemsPanel arrived as a
/// DIFFERENT instance on every application (#58549640, then #63772203, then #53681453).</para>
/// </summary>
[TestFixture]
public class StyleSetterIdentityTests
{
    [Test]
    public void ApplyingOneSetterTwice_GivesTheSameInstance()
    {
        var template = new ItemsPanelTemplate();
        var setter = new Setter(nameof(ItemsControl.ItemsPanel), template);
        var control = new ItemsControl();

        setter.Apply(control, null, null);
        var first = control.ItemsPanel;

        setter.Apply(control, null, null);
        var second = control.ItemsPanel;

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.SameAs(template), "the setter must apply the object it holds, not a copy of it");
            Assert.That(second, Is.SameAs(first), "and the same one again on the next application");
        });
    }

    /// <summary>The consequence, stated as a test: re-applying an unchanged value must not look like a change. This is
    /// what decides whether an items panel, a control template or anything else keyed off such a property survives a
    /// style being applied a second time.</summary>
    [Test]
    public void ReapplyingAnUnchangedValue_IsNotAChange()
    {
        var template = new ItemsPanelTemplate();
        var setter = new Setter(nameof(ItemsControl.ItemsPanel), template);
        var control = new ItemsControl();

        var changes = 0;
        control.PropertyChanged += (_, e) =>
        {
            if (e.Property == ItemsControl.ItemsPanelProperty) changes++;
        };

        setter.Apply(control, null, null);
        setter.Apply(control, null, null);
        setter.Apply(control, null, null);

        Assert.That(changes, Is.EqualTo(1), "only the first application changes anything");
    }
}
