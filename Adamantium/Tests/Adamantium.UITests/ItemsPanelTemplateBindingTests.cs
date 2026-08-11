using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Templates;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>The items host is a templated part like any other, so it may state its settings as {TemplateBinding}
/// against the control whose items it lays out. Without this, a gallery could not tell its grid how many columns the
/// size it is currently drawn at allows, and every such setting would have to be pushed by hand.</summary>
[TestFixture]
public class ItemsPanelTemplateBindingTests
{
    private static ItemsPanelTemplate ColumnsBoundTo(string path) =>
        new(() =>
        {
            var result = new TemplateResult();
            var panel = new RibbonGalleryPanel();
            result.RootComponent = panel;
            result.AddTemplateBinding(panel, nameof(RibbonGalleryPanel.Columns), new TemplateBinding { Path = path });
            return result;
        });

    [Test]
    public void ThePanelReadsItsSettingFromTheControl()
    {
        var gallery = new RibbonGallery { Columns = 7 };

        var panel = (RibbonGalleryPanel)ColumnsBoundTo(nameof(RibbonGallery.EffectiveColumns)).Build(gallery).RootComponent;

        Assert.That(panel.Columns, Is.EqualTo(7));
    }

    [Test]
    public void AndKeepsFollowingIt()
    {
        var gallery = new RibbonGallery { Columns = 7, CompactColumns = 3 };
        var panel = (RibbonGalleryPanel)ColumnsBoundTo(nameof(RibbonGallery.EffectiveColumns)).Build(gallery).RootComponent;

        Ribbon.SetSize(gallery, RibbonSize.Medium);

        Assert.That(panel.Columns, Is.EqualTo(3), "the band narrowed the gallery, and the grid followed");
    }

    [Test]
    public void APanelWithNoOwnerIsStillBuilt()
    {
        var panel = ColumnsBoundTo(nameof(RibbonGallery.EffectiveColumns)).Build(null).RootComponent;

        Assert.That(panel, Is.InstanceOf<RibbonGalleryPanel>());
    }
}
